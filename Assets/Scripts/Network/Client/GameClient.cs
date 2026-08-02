using System;
using System.Collections.Generic;
using ConnectMessage;
using Framework;
using Google.Protobuf;
using SyncMessage;
using UnityEngine;

namespace Network.Client
{
    /// <summary>
    /// 客户端网络子系统：组合 TCP/KCP 传输通道 + 消息分发
    /// 
    /// 重构要点（2026-08-02）：
    /// - 消息统一入队，主线程 Update 消费派发：消除"后台线程回调里调 Unity API"的跨线程问题
    /// - KCP 事件订阅只注册一次：修复重连时重复订阅导致消息被处理多次的泄漏
    /// - 连接状态一致性（TCP/KCP 双通道任一失效即整体断线）：修复"TCP 断了 UDP 还显示连着"
    /// - 握手后立即发送 KCP 注册包：确保服务端提前创建 conv 会话
    /// </summary>
    public class GameClient : SubSystemBase
    {
        public override SubSystemPriority Priority => SubSystemPriority.GameClient;
        
        private TcpSocket _tcpSocket = new TcpSocket();
        private KcpClient _kcpClient = new KcpClient();
        private uint _clientId = 0;
        private string _ip;
        private int _port;
        private int _kcpPort;
        private MessageDispatcher _messageDispatcher = new();

        #region 连接状态（D7 双通道一致性）

        public enum ConnectionState
        {
            Disconnected,   // 未连接/已断线
            Connected,      // TCP+KCP 双通道均正常
        }

        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>当前连接状态（TCP/KCP 双通道统一判定）</summary>
        public ConnectionState State => _state;

        /// <summary>从已连接变为断线时触发（UI 监听，提示手动重连）</summary>
        public event Action OnConnectionLost;

        /// <summary>收包超时（毫秒）：超过此时长未收到服务端任何消息即判定该通道断线（D5）</summary>
        private const long RECEIVE_TIMEOUT_MS = 3000;

        private long _lastTcpReceiveTime;   // 上次收到 TCP 服务端消息的时间戳（毫秒）
        private long _lastKcpReceiveTime;   // 上次收到 KCP 服务端消息的时间戳（毫秒）

        #endregion

        #region 主线程消息队列

        private readonly Queue<byte[]> _messageQueue = new();
        private readonly object _queueLock = new();

        #endregion

        public void RegisterHandler<T>(Signals signal, Action<T> handler) where T : IMessage//,new()
        {
            _messageDispatcher.RegisterHandler(signal, handler);
        }

        public void UnregisterHandler(Signals signal)
        {
            _messageDispatcher.UnregisterHandler(signal);
        }

        #region 生命周期

        public override void Init()
        {
            // KCP 消息回调只注册一次（放在 Init，避免 SetClientId 每次握手重复订阅）
            _kcpClient.OnMessageReceived += (conv, data) => HandleMessage(data, false);
            // TCP 断开立即标记，加速断线检测
            _tcpSocket.OnDisconnected += () => { _lastTcpReceiveTime = 0; };

            RegisterHandler(Signals.ConnectHandShake, (HandShakeResponse msg) =>
            {
                SetClientId(msg.ClientId);
            });
        }

        public override void Update(float deltaTime)
        {
            // 1. 主线程消费消息队列（消息处理器可安全调用 Unity API）
            DrainMessages();

            // 2. 连接状态检测（收包超时 + 双通道状态）
            UpdateConnectionState();
        }

        public override void Destroy()
        {
            _tcpSocket.Dispose();
            _kcpClient.Dispose();
        }

        #endregion

        #region 消息收发

        /// <summary>
        /// 主线程逐条派发网络消息
        /// </summary>
        private void DrainMessages()
        {
            while (true)
            {
                byte[] data;
                lock (_queueLock)
                {
                    if (_messageQueue.Count == 0) return;
                    data = _messageQueue.Dequeue();
                }

                try
                {
                    _messageDispatcher.HandleMessage(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Client][GameClient] 消息处理异常" + ex);
                }
            }
        }

        /// <summary>
        /// 网络线程入口：更新对应通道收包时间并入队（不直接处理）
        /// </summary>
        public void HandleMessage(byte[] data, bool fromTcp)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (fromTcp) _lastTcpReceiveTime = now;
            else _lastKcpReceiveTime = now;

            lock (_queueLock)
            {
                _messageQueue.Enqueue(data);
            }
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 建立连接（TCP + KCP）。重复调用会先关闭旧连接，避免资源泄漏。
        /// </summary>
        /// <param name="ip">服务器 IP</param>
        /// <param name="tcpPort">TCP 端口</param>
        /// <param name="kcpPort">KCP 端口（0 表示与 TCP 相同）</param>
        public void StartLink(string ip, int tcpPort, int kcpPort = 0)
        {
            StopLink(); // 确保旧连接完全关闭（重连时防泄漏）
            _ip = ip;
            _port = tcpPort;
            _kcpPort = kcpPort > 0 ? kcpPort : tcpPort;
            _clientId = 0;

            // 清空旧会话残留的未处理消息，避免断线前积压的帧/快照污染重连后的恢复
            lock (_queueLock)
            {
                _messageQueue.Clear();
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _lastTcpReceiveTime = now;
            _lastKcpReceiveTime = now;

            _tcpSocket.StartLink(ip, tcpPort);
            // KCP 需要 clientId（conv），在 SetClientId 中延迟启动
        }

        public void StopLink()
        {
            if (TcpIsConnected()) _tcpSocket.CloseLink();
            if (KcpIsConnected()) _kcpClient.StopLink();
            _clientId = 0;
            _lastTcpReceiveTime = 0;
            _lastKcpReceiveTime = 0;
        }

        public bool TcpIsConnected()
        {
            return _tcpSocket != null && _tcpSocket.IsConnected();
        }

        public bool TcpSendMessage(byte[] data)
        {
            if (TcpIsConnected()) _tcpSocket.Send(data);
            return TcpIsConnected();
            //else Debug.Log("TcpSendMessage:未建立连接");
        }

        public bool KcpIsConnected()
        {
            return _kcpClient != null && _kcpClient.IsConnected;
        }

        public void KcpSendMessage(byte[] data)
        {
            if (KcpIsConnected()) _kcpClient.Send(data);
        }

        public void SetClientId(uint clientId)
        {
            Debug.Log($"[Client][GameClient] SetClientId {clientId}");
            _clientId = clientId;
            _tcpSocket.BindClientId(clientId);

            // KCP 延迟启动：拿到 clientId 作为 conv 后才建立 KCP 连接
            _kcpClient.StartLink(_ip, _kcpPort, clientId);

            // 立即发送 KCP 注册包：确保服务端提前创建该 conv 会话，
            // 否则服务端向新 conv 补发快照/保活会被"会话未创建"丢弃
            var reg = new ClientMessage
            {
                ClientId = clientId,
                CommonMessage = "kcp-register"
            };
            KcpSendMessage(reg.ToByteArray());
        }

        public uint GetClientId()
        {
            return _clientId;
        }

        #endregion

        #region 断线检测

        /// <summary>
        /// 双通道状态一致性检测：任一通道断开/超时即整体置为断线
        /// </summary>
        private void UpdateConnectionState()
        {
            if (_clientId == 0)
            {
                SetState(ConnectionState.Disconnected); // 尚未完成握手
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bool tcpConn = TcpIsConnected();
            bool kcpConn = KcpIsConnected();
            bool tcpAlive = tcpConn && _lastTcpReceiveTime != 0 && (now - _lastTcpReceiveTime) <= RECEIVE_TIMEOUT_MS;
            bool kcpAlive = kcpConn && _lastKcpReceiveTime != 0 && (now - _lastKcpReceiveTime) <= RECEIVE_TIMEOUT_MS;

            bool connected = tcpConn && kcpConn && tcpAlive && kcpAlive;
            SetState(connected ? ConnectionState.Connected : ConnectionState.Disconnected);
        }

        private void SetState(ConnectionState newState)
        {
            if (_state == newState) return;

            ConnectionState old = _state;
            _state = newState;
            Debug.Log($"[Client][GameClient] 连接状态: {old} -> {newState}");

            if (newState == ConnectionState.Disconnected && old == ConnectionState.Connected)
            {
                Debug.Log("[Client][GameClient] 检测到断线（TCP/KCP 通道失效或收包超时），等待手动重连");
                OnConnectionLost?.Invoke();
            }
        }

        #endregion
    }
}
