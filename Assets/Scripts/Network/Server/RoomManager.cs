using System;
using System.Collections.Generic;
using System.Linq;
using GameMessage;
using Google.Protobuf;
using LobbyMessage;
using SyncMessage;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// 玩家会话数据（供调试面板读取）
    /// </summary>
    public class PlayerSession
    {
        public uint Id;                              // 玩家 ID（服务端内部分配）
        public uint ClientId;                        // 客户端网络 ID
        public string Name;                           // 玩家名称
        public long ActiveTime;                       // 上次活跃时间（毫秒时间戳）
        public ulong LastFrameId;                     // 最新收到的客户端发送序号
        public bool Online;                           // 是否在线
        public float SecondsSinceHeartbeat => (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ActiveTime) / 1000f;

        public Queue<PlayerSync> InputQueue = new();           // 待广播的帧输入队列（每帧消费一个）
        public int InputQueueCount => InputQueue.Count;
        public Dictionary<ulong, PlayerSync> Frames = new();  // 历史帧缓存（服务端帧号→帧数据）
        public Queue<ulong> CurrentFrameIds = new();           // 当前已发送帧序号队列
        public ulong PreSnapshotId;                             // 上次快照中的帧 ID（服务端帧号）
    }

    /// <summary>
    /// 房间管理器：房间生命周期 + 玩家会话 + 帧广播 + 心跳检测 + 快照
    /// 移植自 C++/Qt RoomManager
    /// </summary>
    public class RoomManager
    {
        #region 属性

        // 玩家管理
        private readonly Dictionary<uint, uint> _playerByClient = new();  // clientId → playerId
        private readonly Dictionary<uint, PlayerSession> _players = new(); // playerId → PlayerSession
        private uint _nextPlayerId = 1;

        // 游戏状态
        private GameSnapshot _gameSnapshot;
        private bool _isRunning;
        private ulong _serverFrameId;                     // 服务端全局帧号（统一分配）
        private ulong _sendIndex;

        // 帧广播定时器
        private System.Timers.Timer _broadcastTimer;
        private int _gameFrameRate = 30;
        private readonly float _heartbeatTimeoutMs = 4000f;

        // 事件：向外发送消息
        public event Action<uint, byte[]> OnSendTcp;
        public event Action<uint, byte[]> OnSendUdp;
        public event Action<uint> OnRemoveClient;

        /// <summary>房间是否运行中</summary>
        public bool IsRunning => _isRunning;
        /// <summary>服务端全局帧号</summary>
        public ulong ServerFrameId => _serverFrameId;
        /// <summary>当前玩家列表快照（只读副本）</summary>
        public IReadOnlyList<PlayerSession> Players => _players.Values.ToList().AsReadOnly();
        /// <summary>游戏帧率</summary>
        public int GameFrameRate => _gameFrameRate;
        /// <summary>玩家数量</summary>
        public int PlayerCount => _players.Count;
        /// <summary>房主名称（playerId=1 的玩家）</summary>
        public string OwnerName => _players.TryGetValue(1, out var p) ? p.Name : "";

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化房间管理器
        /// </summary>
        /// <param name="gameFrameRate">游戏帧率</param>
        /// <param name="heartbeatTimeoutSec">心跳超时时间（秒）</param>
        public void Initialize(int gameFrameRate = 30, float heartbeatTimeoutSec = 4f)
        {
            _gameFrameRate = gameFrameRate;
        }

        public void Start()
        {
            _broadcastTimer = new System.Timers.Timer(1000.0 / _gameFrameRate);
            _broadcastTimer.Elapsed += (_, __) => BroadcastGameSync();
            _broadcastTimer.AutoReset = true;
        }

        public void Stop()
        {
            _isRunning = false;
            _broadcastTimer?.Stop();
            _broadcastTimer?.Dispose();
            _broadcastTimer = null;
        }

        #endregion

        #region 大厅操作

        /// <summary>
        /// 处理大厅同步消息（TCP）：加入/离开/开始/结束房间
        /// </summary>
        public void HandleLobbySync(uint clientId, LobbySyncRequest message)
        {
            switch (message.ContentCase)
            {
                case LobbySyncRequest.ContentOneofCase.JoinRoom:
                    JoinRoom(message.JoinRoom.Name, clientId);
                    break;
                case LobbySyncRequest.ContentOneofCase.LeaveRoom:
                    LeaveRoom(message.LeaveRoom.Name, clientId);
                    break;
                case LobbySyncRequest.ContentOneofCase.StartRoom:
                    StartRoom(message.StartRoom.Name);
                    break;
                case LobbySyncRequest.ContentOneofCase.EndRoom:
                    EndRoom();
                    break;
                default:
                    Debug.LogError($"[Server][RoomManager] 未知大厅消息类型: {message.ContentCase}");
                    break;
            }
        }

        private void JoinRoom(string name, uint clientId)
        {
            uint playerId = GetPlayerIdByName(name);
            bool isReconnect = playerId != 0;

            if (!isReconnect)
            {
                playerId = _nextPlayerId++;
            }

            var player = new PlayerSession
            {
                Id = playerId,
                ClientId = clientId,
                Name = name,
                ActiveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Online = true
            };

            _players[playerId] = player;
            _playerByClient[clientId] = playerId;

            Debug.Log($"[Server][RoomManager] 玩家加入: {name} clientId={clientId} playerId={playerId} 总数={_players.Count}");

            // 广播玩家列表给所有人
            var joinMessage = new ServerMessage
            {
                LobbySync = new LobbySyncResponse
                {
                    JoinRoom = new PlayerJoinRoomResponse
                    {
                        Owner = _players.ContainsKey(1) ? _players[1].Name : "",
                    }
                }
            };
            foreach (var p in _players.Values)
                joinMessage.LobbySync.JoinRoom.Players.Add(p.Name);

            BroadcastTcp(joinMessage);

            // 断线重连：补发快照和历史帧
            if (isReconnect && _isRunning)
            {
                SendReconnectData(clientId, player);
            }
        }

        private void LeaveRoom(string name, uint clientId)
        {
            if (!_playerByClient.TryGetValue(clientId, out uint playerId))
            {
                Debug.LogError($"[Server][RoomManager] LeaveRoom 找不到 clientId={clientId}");
                return;
            }

            _players.Remove(playerId);
            _playerByClient.Remove(clientId);
            Debug.Log($"[Server][RoomManager] 玩家离开: {name} clientId={clientId}");

            if (_players.Count == 0)
                EndRoom();
        }

        private void StartRoom(string name)
        {
            _isRunning = true;
            _broadcastTimer.Start();

            var msg = new ServerMessage
            {
                LobbySync = new LobbySyncResponse
                {
                    StartRoom = new PlayerStartRoomResponse
                    {
                        Name = _players.ContainsKey(1) ? _players[1].Name : name
                    }
                }
            };

            BroadcastTcp(msg);
            Debug.Log($"[Server][RoomManager] 房间开始: {name}");
        }

        private void EndRoom()
        {
            _isRunning = false;
            _broadcastTimer?.Stop();
            Debug.Log("[Server][RoomManager] 房间结束");
        }

        #endregion

        #region 游戏同步

        public void ReceiveGameSync(uint clientId, GameSyncMessage message)
        {
            if (!_playerByClient.TryGetValue(clientId, out uint playerId)) return;
            var player = _players[playerId];

            if (message.Players.Count > 0)
            {
                var sync = message.Players[0]; // 每个客户端只发送自己的操作
                player.LastFrameId = sync.FrameId; // 记录客户端发送序号（调试用）

                // 入队等待下一帧广播统一分配服务端帧号
                player.InputQueue.Enqueue(sync);
            }
        }

        public void ReceiveSnapshot(uint clientId, GameSnapshotMessage message)
        {
            if (message.ContentCase != GameSnapshotMessage.ContentOneofCase.Snapshot)
            {
                Debug.LogError($"[Server][RoomManager] 快照错误类型 clientId={clientId}");
                return;
            }

            var snapshot = message.Snapshot;
            _gameSnapshot = snapshot;

            Debug.Log($"[Server][RoomManager] 收到快照 clientId={clientId} 玩家数={snapshot.PlayerSSs.Count} 帧={snapshot.FrameId}");

            foreach (var ss in snapshot.PlayerSSs)
            {
                uint playerId = GetPlayerIdByName(ss.Name);
                if (playerId == 0) continue;

                var player = _players[playerId];
                player.PreSnapshotId = ss.FrameId;

                // 清理已确认的历史帧
                while (player.CurrentFrameIds.TryPeek(out ulong frameId))
                {
                    if (!player.Frames.ContainsKey(frameId) || frameId > ss.FrameId) break;
                    player.Frames.Remove(player.CurrentFrameIds.Dequeue());
                }
            }
        }

        public void ReceiveHeartBeat(uint clientId, HeartBeat message)
        {
            if (!_playerByClient.TryGetValue(clientId, out uint playerId)) return;
            var player = _players[playerId];
            player.ActiveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            player.Online = true;
        }

        #endregion

        #region 断线处理

        public void HandleClientDisconnection(uint clientId)
        {
            if (!_playerByClient.TryGetValue(clientId, out uint playerId)) return;
            var player = _players[playerId];

            if (player.Online)
            {
                player.Online = false;
                Debug.Log($"[Server][RoomManager] {player.Name} TCP 断开，标记离线");
                OnRemoveClient?.Invoke(clientId);
            }
        }

        #endregion

        #region 帧广播

        private void BroadcastGameSync()
        {
            // 心跳超时检测
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var player in _players.Values)
            {
                if (player.Online && now - player.ActiveTime > _heartbeatTimeoutMs)
                {
                    player.Online = false;
                    Debug.Log($"[Server][RoomManager] {player.Name} 心跳超时，标记离线");
                    OnRemoveClient?.Invoke(player.ClientId);
                }
            }

            // 严格帧同步：等待所有在线玩家至少有一个输入才广播
            var onlinePlayers = _players.Values.Where(p => p.Online).ToList();
            if (onlinePlayers.Count == 0) return;

            bool allReady = onlinePlayers.All(p => p.InputQueue.Count > 0);
            if (!allReady)
            {
                // 未全部就绪，等待下一轮
                return;
            }

            _serverFrameId++;

            string log = $"{_sendIndex}(帧{_serverFrameId}):";
            _sendIndex++;

            var syncMessage = new ServerMessage();
            var gameSync = new GameSyncMessage
            {
                FrameId = _serverFrameId // 消息级帧号，客户端用于同步
            };
            syncMessage.GameSyncMessage = gameSync;

            foreach (var player in onlinePlayers)
            {
                var sync = player.InputQueue.Dequeue();
                sync.FrameId = _serverFrameId; // 服务端统一分配帧号

                log += $" {sync.Name}:{sync.InputMove.X},{sync.InputMove.Y},{sync.InputMove.Z}";
                gameSync.Players.Add(sync);

                // 缓存历史帧（服务端帧号为Key，用于断线重连补发）
                player.Frames[_serverFrameId] = sync;
                player.CurrentFrameIds.Enqueue(_serverFrameId);
            }

            // Debug.Log($"[Server][RoomManager] {log}");

            byte[] data = syncMessage.ToByteArray();
            foreach (var player in onlinePlayers)
            {
                OnSendUdp?.Invoke(player.ClientId, data);
            }
        }

        #endregion

        #region 辅助方法

        private void SendReconnectData(uint clientId, PlayerSession player)
        {
            // 1. 发送快照
            var snapMsg = new ServerMessage
            {
                GameSnapshotMessage = new GameSnapshotMessage
                {
                    Snapshot = new GameSnapshot
                    {
                        FrameId = _gameSnapshot.FrameId,
                    }
                }
            };

            foreach (var ss in _gameSnapshot.PlayerSSs)
            {
                var ps = new PlayerSnapshotSync
                {
                    Name = ss.Name,
                    FrameId = ss.FrameId,
                    Pos = ss.Pos,
                    Velocity = ss.Velocity,
                    LastFrameId = 0
                };

                uint pid = GetPlayerIdByName(ss.Name);
                if (pid != 0 && _players.TryGetValue(pid, out var p))
                    ps.LastFrameId = p.LastFrameId;

                snapMsg.GameSnapshotMessage.Snapshot.PlayerSSs.Add(ps);
            }

            OnSendUdp?.Invoke(clientId, snapMsg.ToByteArray());

            // 2. 补发历史帧（分包）
            const int MAX_FRAMES_PER_PACKET = 10;
            var framesMsg = new ServerMessage
            {
                GameSnapshotMessage = new GameSnapshotMessage
                {
                    Frames = new GameFrame()
                }
            };
            framesMsg.GameSnapshotMessage.Frames.FrameId = _gameSnapshot.FrameId;

            int frameCount = 0;
            foreach (var p in _players.Values)
            {
                for (ulong i = p.PreSnapshotId + 1; p.Frames.ContainsKey(i); i++)
                {
                    framesMsg.GameSnapshotMessage.Frames.Players.Add(p.Frames[i]);
                    frameCount++;

                    if (frameCount >= MAX_FRAMES_PER_PACKET)
                    {
                        OnSendUdp?.Invoke(clientId, framesMsg.ToByteArray());
                        framesMsg.GameSnapshotMessage.Frames.Players.Clear();
                        frameCount = 0;
                    }
                }
            }

            if (frameCount > 0)
                OnSendUdp?.Invoke(clientId, framesMsg.ToByteArray());
        }

        private uint GetPlayerIdByName(string name)
        {
            foreach (var (id, p) in _players)
            {
                if (p.Name == name) return id;
            }
            return 0;
        }

        private void BroadcastTcp(ServerMessage message)
        {
            byte[] data = message.ToByteArray();
            foreach (var player in _players.Values)
            {
                OnSendTcp?.Invoke(player.ClientId, data);
            }
        }

        #endregion
    }
}
