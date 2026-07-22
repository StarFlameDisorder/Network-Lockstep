using System;
using Framework;
using GameMessage;
using LobbyMessage;
using SyncMessage;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// 游戏服务器子系统：组合网络分发器与房间管理器，作为 SubSystemBase 嵌入 Unity 生命周期
    /// 移植自 C++/Qt GameServer
    /// </summary>
    public class GameServer : SubSystemBase
    {
        public override SubSystemPriority Priority => SubSystemPriority.GameServer;

        private ServerConfig _config;
        private ServerNetworkDispatcher _dispatcher;
        private RoomManager _roomManager;
        private bool _isRunning;

        #region 配置属性

        public int TcpPort => _config?.TcpPort ?? 1975;
        public int UdpPort => _config?.UdpPort ?? 1975;
        public int GameFrameRate => _config?.GameFrameRate ?? 30;

        #endregion

        public override void Init()
        {
            // 尝试加载配置
            _config = Resources.Load<ServerConfig>("ServerConfig");
            if (_config == null)
            {
                Debug.LogWarning("[Server][GameServer] 未找到 ServerConfig.asset，使用默认配置");
                _config = ScriptableObject.CreateInstance<ServerConfig>();
            }

            Debug.Log($"[Server][GameServer] 初始化 TCP={_config.TcpPort} UDP={_config.UdpPort} 帧率={_config.GameFrameRate}");

            // 创建网络分发器
            _dispatcher = new ServerNetworkDispatcher(_config.TcpPort, _config.UdpPort);

            // 创建房间管理器
            _roomManager = new RoomManager();
            _roomManager.Initialize(_config.GameFrameRate, _config.HeartbeatTimeoutSec);

            // 连接事件管线：NetworkDispatcher → RoomManager
            _dispatcher.OnTcpLobby += HandleTcpLobby;
            _dispatcher.OnUdpGameSync += HandleUdpGameSync;
            _dispatcher.OnUdpGameSnapshot += HandleUdpGameSnapshot;
            _dispatcher.OnUdpHeartBeat += HandleUdpHeartBeat;
            _dispatcher.OnClientDisconnectRequest += HandleClientDisconnect;

            // RoomManager → NetworkDispatcher（发送消息）
            _roomManager.OnSendTcp += SendTcpToClient;
            _roomManager.OnSendUdp += SendUdpToClient;
            _roomManager.OnRemoveClient += RemoveClient;

            // 自动启动
            if (_config.RunServerAutomatically)
            {
                StartServer();
            }
        }

        #region 启动/停止

        public void StartServer()
        {
            if (_isRunning) return;

            _dispatcher.Start();
            _roomManager.Start();
            _isRunning = true;
            Debug.Log("[Server][GameServer] 服务器已启动");
        }

        public void StopServer()
        {
            if (!_isRunning) return;

            _roomManager.Stop();
            _dispatcher.Stop();
            _isRunning = false;
            Debug.Log("[Server][GameServer] 服务器已停止");
        }

        #endregion

        #region 事件转发

        private void HandleTcpLobby(ulong clientId, LobbySyncRequest message)
        {
            _roomManager.HandleLobbySync(clientId, message);
        }

        private void HandleUdpGameSync(ulong clientId, GameSyncMessage message)
        {
            _roomManager.ReceiveGameSync(clientId, message);
        }

        private void HandleUdpGameSnapshot(ulong clientId, GameSnapshotMessage message)
        {
            _roomManager.ReceiveSnapshot(clientId, message);
        }

        private void HandleUdpHeartBeat(ulong clientId, HeartBeat message)
        {
            _roomManager.ReceiveHeartBeat(clientId, message);
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            _roomManager.HandleClientDisconnection(clientId);
        }

        private void SendTcpToClient(ulong clientId, byte[] data)
        {
            _dispatcher.SendTcp(clientId, data);
        }

        private void SendUdpToClient(ulong clientId, byte[] data)
        {
            _dispatcher.SendUdp(clientId, data);
        }

        private void RemoveClient(ulong clientId)
        {
            _dispatcher.DeleteClient(clientId);
        }

        #endregion

        #region 生命周期

        public override void Update(float deltaTime)
        {
            // 服务端网络操作在独立线程（TcpListener/UdpClient 异步回调），
            // Unity 主线程 Update 仅用于处理需要通过 Unity API 的逻辑
        }

        public override void Destroy()
        {
            StopServer();
            _dispatcher?.Dispose();
            _dispatcher = null;
            _roomManager = null;
            _config = null;
        }

        #endregion
    }
}
