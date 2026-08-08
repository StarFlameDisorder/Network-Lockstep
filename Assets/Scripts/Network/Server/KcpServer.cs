using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Core;
using Network.Base;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// Kcp 服务器：监听端口，管理客户端连接
    /// 库为KumoKyaku/kcp
    /// </summary>
    public class KcpServer:IDisposable
    {
        #region 协议规定
        
        /*
         * conv等于外界设置的clientId，必须外界分配clientId再使用
         * 
         * | KCP: conv 4字节  其他 | 传输数据 |
         */

        #endregion
        
        #region 属性

        private UdpClient _udpClient;
        private readonly int _port;
        private bool _isRunning=false;
        private Dictionary<uint, KcpSession> _convToKcp;

        /// <summary>
        /// 待发缓存：conv 会话尚未创建（客户端刚重连、首个 KCP 包未到达）时暂存的消息，
        /// 会话创建后立即补发。解决"重连补发被静默丢弃"问题。
        /// </summary>
        private readonly Dictionary<uint, List<byte[]>> _pendingSends = new();

        /// <summary>
        /// 线程安全锁：ReceiveAsync 在后台线程读写字典，Send/RemoveClient 在主线程调用
        /// </summary>
        private readonly object _lock = new();
        
        public event Action<uint,byte[]> OnMessageReceived;

        public KcpServer(int port = 1975)
        {
            _port = port;
        }

        #endregion
        
        #region 生命周期

        public void Start()
        {
            lock (_lock)
            {
                _convToKcp = new Dictionary<uint, KcpSession>();
                _pendingSends.Clear();
            }
            _udpClient = new UdpClient(_port);
            _isRunning = true;
            ReceiveAsync();
        }

        public void Stop()
        {
            _isRunning = false;
            List<KcpSession> sessions;
            lock (_lock)
            {
                sessions = _convToKcp != null
                    ? new List<KcpSession>(_convToKcp.Values)
                    : new List<KcpSession>();
                _pendingSends.Clear();
            }
            foreach (var session in sessions)
            {
                session.Dispose();
            }
            lock (_lock)
            {
                _convToKcp?.Clear();
                _convToKcp = null;
            }
            _udpClient.Close();
            _udpClient = null;
        }
        
        public void Dispose()
        {
            Stop();   
        }

        #endregion

        #region 消息收发

        #region UDP对接KCP
    
        //KCP消息通过UDP发送
        private void Send(IPEndPoint endPoint,byte[] datagram)
        {
            _udpClient.Send(datagram,datagram.Length,endPoint);
        }
        
        //UDP接收的消息交给KCP
        private async void ReceiveAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var res = await _udpClient.ReceiveAsync();
                    uint conv = BinaryPrimitives.ReadUInt32LittleEndian(res.Buffer);
                    KcpSession session;
                    lock (_lock)
                    {
                        if (_convToKcp != null && _convToKcp.TryGetValue(conv, out session))
                        {
                            // 已有会话，直接输入
                        }
                        else
                        {
                            // 首次收到该 conv 的包：创建会话并补发待发缓存
                            session = new KcpSession(conv, res.RemoteEndPoint);
                            session.OnUdpReceive += Send;
                            session.OnMessageReceived += Receive;
                            if (_convToKcp != null)
                            {
                                _convToKcp[conv] = session;

                                if (_pendingSends.TryGetValue(conv, out var pending))
                                {
                                    foreach (var data in pending)
                                    {
                                        session.Send(data);
                                    }
                                    _pendingSends.Remove(conv);
                                }
                            }
                        }
                    }
                    session.Input(res.Buffer);
                }
                catch (Exception e)
                {
                    // 单个客户端断开等瞬时异常不应终止接收循环，否则后续新 conv 无法注册
                    if (_isRunning)
                        Debug.LogError("[Server][KcpServer] 消息接收错误" + e);
                }
            }
        }

        #endregion

        #region KCP对接上层

        //上层消息传入KCP
        public void Send(uint conv,byte[] datagram)
        {
            lock (_lock)
            {
                if (_convToKcp != null && _convToKcp.TryGetValue(conv, out var session))
                {
                    session.Send(datagram);
                    return;
                }

                // 会话尚未创建（客户端刚重连/中途加入，首个KCP包未到达）→ 缓存待发，会话创建后补发
                if (!_pendingSends.TryGetValue(conv, out var list))
                {
                    list = new List<byte[]>();
                    _pendingSends[conv] = list;
                    Debug.LogWarning($"[Server][KcpServer] conv={conv} 会话未创建，消息进入待发缓存");
                }

                // 缓存上限保护：只保留最新，防止"只连TCP不发KCP"的客户端导致内存无限增长
                const int MAX_PENDING_SENDS = GameConstants.MAX_PENDING_SENDS;
                if (list.Count >= MAX_PENDING_SENDS) list.RemoveAt(0);
                list.Add(datagram);
            }
        }
        
        //KCP消息传给上层
        private void Receive(uint conv, byte[] datagram)
        {
            OnMessageReceived?.Invoke(conv,datagram);
        }

        #endregion

        #endregion
        
        
        #region 客户端管理

        public string GetClientInfo(uint conv)
        {
            lock (_lock)
            {
                if (_convToKcp == null || !_convToKcp.TryGetValue(conv, out var value))
                {
                    return "unknown";
                }

                IPEndPoint endPoint = value.endPoint;
                return $"{endPoint.Address}:{endPoint.Port}";
            }
        }
        
        /// <summary>获取客户端的 IPEndPoint</summary>
        public IPEndPoint GetEndpoint(uint conv)
        {
            lock (_lock)
            {
                if (_convToKcp != null && _convToKcp.TryGetValue(conv, out var session))
                    return session.endPoint;
                return null;
            }
        }
        
        /// <summary>检查是否存在指定 conv 的客户端</summary>
        public bool HasClient(uint conv)
        {
            lock (_lock)
            {
                return _convToKcp != null && _convToKcp.ContainsKey(conv);
            }
        }

        /// <summary>移除并断开指定客户端</summary>
        public void RemoveClient(uint conv)
        {
            lock (_lock)
            {
                if (_convToKcp != null && _convToKcp.TryGetValue(conv, out var session))
                {
                    session.Dispose();
                    _convToKcp.Remove(conv);
                }
                _pendingSends.Remove(conv);
            }
        }
        
        #endregion
    }
}
