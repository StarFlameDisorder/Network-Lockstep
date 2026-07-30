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
    /// 客户端连接信息（供调试面板读取）：TCP 连接 + UDP 端点 + clientId
    /// </summary>
    public class ClientState
    {
        public uint ClientId;
        public TcpClient TcpSocket;
        public IPEndPoint UdpEndPoint;
        /// <summary>TCP 端点字符串（调试用）</summary>
        public string TcpEndpoint
        {
            get
            {
                try { return TcpSocket?.Client?.RemoteEndPoint?.ToString() ?? "-"; }
                catch (ObjectDisposedException) { return "-"; }
            }
        }
        /// <summary>UDP 端点字符串（调试用）</summary>
        public string UdpEndpoint => UdpEndPoint?.ToString() ?? "-";
        /// <summary>TCP 是否已绑定</summary>
        public bool HasTcp => TcpSocket != null;
        /// <summary>UDP 是否已绑定</summary>
        public bool HasUdp => UdpEndPoint != null;
    }

    /// <summary>
    /// 服务端网络消息分发器：消息路由 + 客户端身份管理
    /// 移植自 C++/Qt NetworkDispatcher
    /// </summary>
    public class ServerNetworkDispatcher : IDisposable
    {
        #region 属性

        private readonly TcpServer _tcpServer;
        private readonly UdpServer _udpServer;
        private readonly int _tcpPort;
        private readonly int _udpPort;

        // 客户端管理
        private readonly Dictionary<uint, ClientState> _clientsById = new();
        private readonly Dictionary<TcpClient, uint> _tcpToClientId = new();
        private readonly Dictionary<IPEndPoint, uint> _udpToClientId = new();
        private readonly object _lock = new();
        private uint _nextClientId = 1;

        /// <summary>TCP 端口</summary>
        public int TcpPort => _tcpPort;
        /// <summary>UDP 端口</summary>
        public int UdpPort => _udpPort;
        /// <summary>当前客户端列表快照（只读副本）</summary>
        public IReadOnlyList<ClientState> Clients
        {
            get { lock (_lock) return _clientsById.Values.ToList().AsReadOnly(); }
        }
        /// <summary>在线客户端数量</summary>
        public int ClientCount { get { lock (_lock) return _clientsById.Count; } }

        // 事件：向 RoomManager 转发消息
        public event Action<uint, LobbySyncRequest> OnTcpLobby;
        public event Action<uint, GameSyncMessage> OnUdpGameSync;
        public event Action<uint, GameSnapshotMessage> OnUdpGameSnapshot;
        public event Action<uint, HeartBeat> OnUdpHeartBeat;
        public event Action<uint> OnClientDisconnectRequest;

        #endregion

        #region 生命周期

        public ServerNetworkDispatcher(int tcpPort = 1975, int udpPort = 1975)
        {
            _tcpPort = tcpPort;
            _udpPort = udpPort;

            _tcpServer = new TcpServer(tcpPort);
            _udpServer = new UdpServer(udpPort);

            _tcpServer.OnClientConnected += HandleTcpConnected;
            _tcpServer.OnMessageReceived += HandleTcpMessage;
            _tcpServer.OnClientDisconnected += HandleTcpDisconnected;
            _udpServer.OnMessageReceived += HandleUdpMessage;
        }

        public void Start()
        {
            _tcpServer.Start();
            _udpServer.Start();
            Debug.Log("[Server][ServerNetworkDispatcher] 网络分发器已启动");
        }

        public void Stop()
        {
            _tcpServer.Stop();
            _udpServer.Stop();
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

        #region UDP
        
        private void HandleUdpMessage(IPEndPoint ep, byte[] data)
        {
            try
            {
                ClientMessage msg = ClientMessage.Parser.ParseFrom(data);
                if (!TryGetClientId(ep, out uint clientId, msg.ClientId)) return;

                BindUdp(clientId, ep);

                switch (msg.ContentCase)
                {
                    case ClientMessage.ContentOneofCase.CommonMessage:
                        Debug.Log($"[Server][Dispatcher] UDP-{clientId}: {msg.CommonMessage}");
                        break;
                    case ClientMessage.ContentOneofCase.GameSyncMessage:
                        OnUdpGameSync?.Invoke(clientId, msg.GameSyncMessage);
                        break;
                    case ClientMessage.ContentOneofCase.HeartBeat:
                        OnUdpHeartBeat?.Invoke(clientId, msg.HeartBeat);
                        break;
                    case ClientMessage.ContentOneofCase.GameSnapshotMessage:
                        OnUdpGameSnapshot?.Invoke(clientId, msg.GameSnapshotMessage);
                        break;
                    default:
                        Debug.LogWarning($"[Server][Dispatcher] UDP 未知消息类型 clientId={clientId}: {msg.ContentCase}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server][Dispatcher] UDP 解析消息失败：{ex.Message}");
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

        public void SendUdp(uint clientId, byte[] data)
        {
            IPEndPoint ep;
            lock (_lock)
            {
                if (!_clientsById.TryGetValue(clientId, out var c) || c.UdpEndPoint == null)
                {
                    Debug.LogError($"[Server][Dispatcher] 未找到 clientId={clientId} 发送 UDP 失败");
                    return;
                }
                ep = c.UdpEndPoint;
            }
            _udpServer.Send(ep, data);
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

        private bool TryGetClientId(IPEndPoint ep, out uint clientId, uint msgClientId)
        {
            lock (_lock)
            {
                if (_udpToClientId.TryGetValue(ep, out clientId))
                    return true;

                if (_clientsById.TryGetValue(msgClientId, out var c))
                {
                    clientId = msgClientId;
                    return true;
                }
            }

            Debug.LogError($"[Server][Dispatcher] 未知 UDP 客户端 {_udpServer.GetEndpointInfo(ep)}，无法获取 clientId");
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

        private void BindUdp(uint clientId, IPEndPoint ep)
        {
            lock (_lock)
            {
                if (_clientsById.TryGetValue(clientId, out var c) && c.UdpEndPoint == null)
                {
                    c.UdpEndPoint = ep;
                    _udpToClientId[ep] = clientId;
                }
            }
        }

        public void DeleteClient(uint clientId)
        {
            lock (_lock)
            {
                if (_clientsById.TryGetValue(clientId, out var c))
                {
                    if (c.TcpSocket != null)
                    {
                        _tcpToClientId.Remove(c.TcpSocket);
                        _tcpServer.DisconnectClient(c.TcpSocket);
                    }
                    if (c.UdpEndPoint != null)
                    {
                        _udpToClientId.Remove(c.UdpEndPoint);
                        _udpServer.CleanClient(c.UdpEndPoint);
                    }

                    _clientsById.Remove(clientId);
                    Debug.Log($"[Server][Dispatcher] 移除 clientId={clientId}");
                }
            }
        }

        #endregion
    }
}
