using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using ConnectMessage;
using GameMessage;
using Google.Protobuf;
using LobbyMessage;
using SyncMessage;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// 客户端连接信息（供调试面板读取）：TCP 连接 + KCP 端点 + clientId
    /// </summary>
    public class ClientState
    {
        public uint ClientId;
        public TcpClient TcpSocket;
        /// <summary>TCP 端点字符串（调试用）</summary>
        public string TcpEndpoint
        {
            get
            {
                try { return TcpSocket?.Client?.RemoteEndPoint?.ToString() ?? "-"; }
                catch (ObjectDisposedException) { return "-"; }
            }
        }
        /// <summary>TCP 是否已绑定</summary>
        public bool HasTcp => TcpSocket != null;
        /// <summary>KCP 是否已绑定（由 Dispatcher 维护）</summary>
        public bool HasKcp { get; internal set; }
        /// <summary>KCP 端点字符串（由 Dispatcher 维护）</summary>
        public string KcpEndpoint { get; internal set; } = "-";
    }

    /// <summary>
    /// 服务端网络消息分发器：消息路由 + 客户端身份管理
    /// 移植自 C++/Qt NetworkDispatcher
    /// </summary>
    public class ServerNetworkDispatcher : IDisposable
    {
        #region 属性

        private readonly TcpServer _tcpServer;
        private readonly KcpServer _kcpServer;
        private readonly int _tcpPort;
        private readonly int _kcpPort;

        // 客户端管理
        private readonly Dictionary<uint, ClientState> _clientsById = new();
        private readonly Dictionary<TcpClient, uint> _tcpToClientId = new();
        private readonly object _lock = new();
        private uint _nextClientId = 1;

        // 网络事件队列：TcpServer/KcpServer 的回调来自后台线程，先入队，由主线程 DrainEvents 统一派发，
        // 保证 _clientsById 与 RoomManager 事件全部在主线程处理，避免线程竞态。
        private readonly object _eventLock = new();
        private readonly Queue<Action> _eventQueue = new();

        /// <summary>TCP 端口</summary>
        public int TcpPort => _tcpPort;
        /// <summary>KCP 端口</summary>
        public int KcpPort => _kcpPort;
        /// <summary>当前客户端列表快照（只读副本）</summary>
        public IReadOnlyList<ClientState> Clients
        {
            get { lock (_lock) return _clientsById.Values.ToList().AsReadOnly(); }
        }
        /// <summary>在线客户端数量</summary>
        public int ClientCount { get { lock (_lock) return _clientsById.Count; } }

        // 事件：向 RoomManager 转发消息
        public event Action<uint, LobbySyncRequest> OnTcpLobby;
        public event Action<uint, GameSyncMessage> OnKcpGameSync;
        public event Action<uint, GameSnapshotMessage> OnKcpGameSnapshot;
        public event Action<uint, HeartBeat> OnKcpHeartBeat;
        public event Action<uint> OnClientDisconnectRequest;

        #endregion

        #region 生命周期

        public ServerNetworkDispatcher(int tcpPort = 1975, int kcpPort = 1975)
        {
            _tcpPort = tcpPort;
            _kcpPort = kcpPort;

            _tcpServer = new TcpServer(tcpPort);
            _kcpServer = new KcpServer(kcpPort);

            // 网络回调来自后台线程：入队，主线程 DrainEvents 统一处理
            _tcpServer.OnClientConnected += tcp => EnqueueEvent(() => HandleTcpConnected(tcp));
            _tcpServer.OnMessageReceived += (tcp, data) => EnqueueEvent(() => HandleTcpMessage(tcp, data));
            _tcpServer.OnClientDisconnected += tcp => EnqueueEvent(() => HandleTcpDisconnected(tcp));
            _kcpServer.OnMessageReceived += (conv, data) => EnqueueEvent(() => HandleKcpMessage(conv, data));
        }

        /// <summary>
        /// 入队一个网络事件（后台线程调用，仅入队，不处理）
        /// </summary>
        private void EnqueueEvent(Action action)
        {
            lock (_eventLock)
            {
                _eventQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// 主线程逐帧派发网络事件（由 GameServer.Update 驱动）
        /// </summary>
        public void DrainEvents()
        {
            while (true)
            {
                Action action;
                lock (_eventLock)
                {
                    if (_eventQueue.Count == 0) return;
                    action = _eventQueue.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Server][ServerNetworkDispatcher] 网络事件处理异常：{ex}");
                }
            }
        }

        public void Start()
        {
            _tcpServer.Start();
            _kcpServer.Start();
            Debug.Log("[Server][ServerNetworkDispatcher] 网络分发器已启动");
        }

        // 保活包间隔与累加器（覆盖所有已连接客户端，含未入房者）
        private const float KEEPALIVE_INTERVAL = 1f;
        private float _keepAliveAccum;

        /// <summary>
        /// 主线程逐帧驱动（由 GameServer.Update 调用）：周期广播保活包，
        /// 客户端据此做"收包超时"断线检测（大厅/游戏阶段都生效）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _keepAliveAccum += deltaTime;
            if (_keepAliveAccum < KEEPALIVE_INTERVAL) return;
            _keepAliveAccum = 0;

            var msg = new ServerMessage { KeepAlive = true };
            byte[] data = msg.ToByteArray();

            lock (_lock)
            {
                foreach (var client in _clientsById.Values)
                {
                    _tcpServer.Send(client.TcpSocket, data);
                    if (client.HasKcp)
                        _kcpServer.Send(client.ClientId, data);
                }
            }
        }

        public void Stop()
        {
            _tcpServer.Stop();
            _kcpServer.Stop();
            Debug.Log("[Server][ServerNetworkDispatcher] 网络分发器已停止");
        }

        public void Dispose()
        {
            Stop();
        }

        #endregion

        #region 消息处理

        #region TCP
        private void HandleTcpConnected(TcpClient tcp)
        {
            uint clientId;
            lock (_lock)
            {
                clientId = _nextClientId++;
                var client = new ClientState
                {
                    ClientId = clientId,
                    TcpSocket = tcp,
                };
                _clientsById[clientId] = client;
                _tcpToClientId[tcp] = clientId;
            }

            // 发送握手，分配 clientId
            var message = new ServerMessage
            {
                ConnectMessage = new ServerConnectMessage
                {
                    HandShakeMessage = new HandShakeResponse
                    {
                        Content = "Tcp-这里是服务器，建立连接",
                        ClientId = clientId
                    }
                }
            };
            _tcpServer.Send(tcp, message.ToByteArray());
            Debug.Log($"[Server][Dispatcher] 分配 clientId={clientId} -> {_tcpServer.GetClientInfo(tcp)}");
        }
        
        private void HandleTcpDisconnected(TcpClient tcp)
        {
            lock (_lock)
            {
                if (_tcpToClientId.TryGetValue(tcp, out uint clientId))
                {
                    _tcpToClientId.Remove(tcp);
                    if (_clientsById.TryGetValue(clientId, out var c))
                        c.TcpSocket = null;
                    Debug.Log($"[Server][Dispatcher] TCP 断开 clientId={clientId}");
                    OnClientDisconnectRequest?.Invoke(clientId);
                }
            }
        }

        private void HandleTcpMessage(TcpClient tcp, byte[] data)
        {
            try
            {
                ClientMessage msg = ClientMessage.Parser.ParseFrom(data);
                if (!TryGetClientId(tcp, out uint clientId, msg.ClientId)) return;

                BindTcp(clientId, tcp);

                switch (msg.ContentCase)
                {
                    case ClientMessage.ContentOneofCase.CommonMessage:
                        Debug.Log($"[Server][Dispatcher] TCP-{clientId}: {msg.CommonMessage}");
                        break;
                    case ClientMessage.ContentOneofCase.LobbySync:
                        OnTcpLobby?.Invoke(clientId, msg.LobbySync);
                        break;
                    default:
                        Debug.LogWarning($"[Server][Dispatcher] TCP 未知消息类型 clientId={clientId}: {msg.ContentCase}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server][Dispatcher] TCP 解析消息失败：{ex.Message}");
            }
        }
        
        #endregion

        #region KCP
        
        /// <summary>
        /// KCP 消息处理：conv 即为 clientId，无需 IPEndPoint 映射
        /// </summary>
        private void HandleKcpMessage(uint conv, byte[] data)
        {
            try
            {
                ClientMessage msg = ClientMessage.Parser.ParseFrom(data);
                uint clientId = conv; // KCP conv 即为 clientId
                
                // 验证 clientId 是否存在
                lock (_lock)
                {
                    if (!_clientsById.ContainsKey(clientId))
                    {
                        Debug.LogWarning($"[Server][Dispatcher] KCP 收到未知 clientId={clientId} 的消息");
                        return;
                    }
                    
                    // 首次收到该客户端的 KCP 消息时标记为已绑定
                    if (!_clientsById[clientId].HasKcp)
                    {
                        _clientsById[clientId].HasKcp = true;
                        _clientsById[clientId].KcpEndpoint = _kcpServer.GetClientInfo(clientId);
                    }
                }

                switch (msg.ContentCase)
                {
                    case ClientMessage.ContentOneofCase.CommonMessage:
                        Debug.Log($"[Server][Dispatcher] KCP-{clientId}: {msg.CommonMessage}");
                        break;
                    case ClientMessage.ContentOneofCase.GameSyncMessage:
                        OnKcpGameSync?.Invoke(clientId, msg.GameSyncMessage);
                        break;
                    case ClientMessage.ContentOneofCase.HeartBeat:
                        OnKcpHeartBeat?.Invoke(clientId, msg.HeartBeat);
                        break;
                    case ClientMessage.ContentOneofCase.GameSnapshotMessage:
                        OnKcpGameSnapshot?.Invoke(clientId, msg.GameSnapshotMessage);
                        break;
                    default:
                        Debug.LogWarning($"[Server][Dispatcher] KCP 未知消息类型 clientId={clientId}: {msg.ContentCase}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server][Dispatcher] KCP 解析消息失败：{ex.Message}");
            }
        }
        
        #endregion

        #endregion

        #region 发送方法

        public void SendTcp(uint clientId, byte[] data)
        {
            TcpClient tcp;
            lock (_lock)
            {
                if (!_clientsById.TryGetValue(clientId, out var c))
                {
                    Debug.LogError($"[Server][Dispatcher] 未找到 clientId={clientId} 发送 TCP 失败");
                    return;
                }
                tcp = c.TcpSocket;
            }
            _tcpServer.Send(tcp, data);
        }

        public void SendKcp(uint clientId, byte[] data)
        {
            _kcpServer.Send(clientId, data);
        }

        #endregion

        #region 客户端管理

        private bool TryGetClientId(TcpClient tcp, out uint clientId, uint msgClientId)
        {
            lock (_lock)
            {
                // 已绑定的直接返回
                if (_tcpToClientId.TryGetValue(tcp, out clientId))
                    return true;

                // 未绑定的检查 _clientsById 中是否有此 id
                if (_clientsById.TryGetValue(msgClientId, out var c) && c.TcpSocket == null)
                {
                    clientId = msgClientId;
                    return true;
                }
            }

            Debug.LogError($"[Server][Dispatcher] 未知 TCP 客户端，无法获取 clientId");
            clientId = 0;
            return false;
        }

        private void BindTcp(uint clientId, TcpClient tcp)
        {
            lock (_lock)
            {
                if (_clientsById.TryGetValue(clientId, out var c) && c.TcpSocket == null)
                {
                    c.TcpSocket = tcp;
                    _tcpToClientId[tcp] = clientId;
                }
            }
        }

        public void DeleteClient(uint clientId)
        {
            lock (_lock)
            {
                if (!_clientsById.TryGetValue(clientId, out var c)) return;

                try
                {
                    if (c.TcpSocket != null)
                    {
                        _tcpToClientId.Remove(c.TcpSocket);
                        _tcpServer.DisconnectClient(c.TcpSocket);
                    }
                    _kcpServer.RemoveClient(clientId);
                }
                catch (Exception ex)
                {
                    // 清理异常不能阻止客户端移除，否则死客户端会永久占用
                    Debug.LogError($"[Server][Dispatcher] DeleteClient 清理异常 clientId={clientId}: {ex}");
                }

                c.HasKcp = false;
                _clientsById.Remove(clientId);
                Debug.Log($"[Server][Dispatcher] 移除 clientId={clientId}");
            }
        }

        #endregion
    }
}
