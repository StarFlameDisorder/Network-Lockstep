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
        public Dictionary<ulong, PlayerSync> Frames = new();  // 历史帧缓存（服务端帧号→帧数据，重连补帧用）
        public Queue<ulong> CurrentFrameIds = new();           // 当前已发送帧序号队列
        public ulong PreSnapshotId;                             // 上次快照中的帧 ID（服务端帧号）
        public bool Joining;                                    // 重连/中途加入恢复中：不参与 Lockstep 帧等待，收到首个输入后置 false
    }

    /// <summary>
    /// 房间管理器：房间生命周期 + 玩家会话 + 帧广播 + 心跳检测 + 快照
    /// 移植自 C++/Qt RoomManager
    /// 
    /// 线程模型（2026-08-02 重构）：
    /// - 所有方法均由主线程调用（GameServer.Update → Tick，网络事件经 Dispatcher 主线程派发）
    /// - 不再使用 System.Timers.Timer 后台线程，消除字典并发竞态
    /// - 断线重连复用原 PlayerSession（保留 Frames 历史帧缓存），不再重建清空
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

        // 主线程驱动累加器（替代 System.Timers.Timer）
        private float _broadcastAccum;
        private float _heartbeatAccum;
        private int _gameFrameRate = 30;
        private float _heartbeatTimeoutMs = 4000f;
        private const float HEARTBEAT_CHECK_INTERVAL = 1f;   // 心跳超时检测节流
        private const int MAX_CACHED_FRAMES = 600;           // 每玩家历史帧缓存上限（防止内存无限增长）

        // 事件：向外发送消息
        public event Action<uint, byte[]> OnSendTcp;
        public event Action<uint, byte[]> OnSendKcp;
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
            _heartbeatTimeoutMs = heartbeatTimeoutSec * 1000f;
        }

        public void Start()
        {
            _broadcastAccum = 0;
            _heartbeatAccum = 0;
        }

        public void Stop()
        {
            _isRunning = false;
            _broadcastAccum = 0;
            _heartbeatAccum = 0;
        }

        /// <summary>
        /// 主线程逐帧驱动（由 GameServer.Update 调用）：心跳检测 + 帧广播
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 心跳超时检测（节流 1s，大厅阶段也生效）
            _heartbeatAccum += deltaTime;
            if (_heartbeatAccum >= HEARTBEAT_CHECK_INTERVAL)
            {
                _heartbeatAccum = 0;
                CheckHeartbeatTimeout();
            }

            // 帧广播（按游戏帧率）
            _broadcastAccum += deltaTime;
            if (_broadcastAccum >= 1f / _gameFrameRate)
            {
                _broadcastAccum -= 1f / _gameFrameRate;
                BroadcastGameSync();
            }
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
            bool isMidGameJoin = !isReconnect && _isRunning;

            if (!isReconnect)
            {
                playerId = _nextPlayerId++;
            }

            PlayerSession player;
            if (isReconnect)
            {
                // 断线重连：复用原会话，保留 Frames/PreSnapshotId 历史帧缓存，仅更新网络身份
                player = _players[playerId];
                player.ClientId = clientId;
                player.Online = true;
                player.InputQueue.Clear(); // 清空断线前的过期输入
            }
            else
            {
                player = new PlayerSession
                {
                    Id = playerId,
                    ClientId = clientId,
                    Name = name
                };
                _players[playerId] = player;
            }
            player.ActiveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 游戏运行中加入（重连/中途加入）：恢复期间不参与 Lockstep 帧等待，避免卡住全房间
            player.Joining = _isRunning;

            _playerByClient[clientId] = playerId;

            Debug.Log($"[Server][RoomManager] 玩家加入: {name} clientId={clientId} playerId={playerId} 总数={_players.Count} 重连={isReconnect} 中途加入={isMidGameJoin}");

            // 广播玩家列表给所有人（含游戏是否已开始，供中途加入客户端判断）
            var joinMessage = new ServerMessage
            {
                LobbySync = new LobbySyncResponse
                {
                    JoinRoom = new PlayerJoinRoomResponse
                    {
                        Owner = _players.ContainsKey(1) ? _players[1].Name : "",
                        GameStarted = _isRunning
                    }
                }
            };
            foreach (var p in _players.Values)
                joinMessage.LobbySync.JoinRoom.Players.Add(p.Name);

            BroadcastTcp(joinMessage);

            // 游戏运行中：断线重连 或 中途加入 → 补发快照和历史帧
            if (_isRunning)
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

            // 广播离开消息，其他客户端据此刷新大厅显示
            var msg = new ServerMessage
            {
                LobbySync = new LobbySyncResponse
                {
                    LeaveRoom = new PlayerLeaveRoomResponse { Name = name }
                }
            };
            BroadcastTcp(msg);

            if (_players.Count == 0)
                EndRoom();
        }

        private void StartRoom(string name)
        {
            _isRunning = true;
            _serverFrameId = 0;
            _sendIndex = 0;

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
            _gameSnapshot = null;
            _serverFrameId = 0;
            _sendIndex = 0;

            foreach (var p in _players.Values)
            {
                p.InputQueue.Clear();
            }

            // 广播房间结束，客户端据此重置游戏状态
            var msg = new ServerMessage
            {
                LobbySync = new LobbySyncResponse
                {
                    EndRoom = new PlayerEndRoomResponse { Name = "" }
                }
            };
            BroadcastTcp(msg);
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

                // 首个输入到达：加入中状态结束，正式参与帧同步
                player.Joining = false;

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

            // 防御：快照帧号超前于当前服务端帧 = 客户端失步残留（如房间重启后旧客户端上报），
            // 接受会毒化快照缓存，导致后续重连补发为空、客户端永远等不到目标帧
            if (snapshot.FrameId > _serverFrameId)
            {
                Debug.LogWarning($"[Server][RoomManager] 拒绝异常快照 帧={snapshot.FrameId} 当前服务端帧={_serverFrameId} clientId={clientId}");
                return;
            }

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

        /// <summary>各玩家最近一次哈希上报（Name → 帧号+哈希），Desync 比对用</summary>
        private readonly Dictionary<string, (ulong frameId, uint hash)> _playerHashes = new();

        /// <summary>
        /// 接收客户端哈希上报并跨客户端比对（Desync 检测闭环）：
        /// 同一帧号（±1 帧进度容差）下不同玩家的世界哈希不一致 → 判定分歧，广播 DesyncNotice。
        /// 帧同步最怕静默分歧（各端算出不同结果却无人发现），此比对 + 分歧帧号记录是底线保障。
        /// </summary>
        public void ReceiveHashReport(uint clientId, HashReportMessage message)
        {
            if (!_playerByClient.TryGetValue(clientId, out uint playerId)) return;
            var player = _players[playerId];
            if (player.Name != message.Name) return; // 简单身份校验

            _playerHashes[player.Name] = (message.FrameId, message.WorldHash);

            // 以刚收到的上报为基准，与其他玩家同帧（±1）的上报比对
            foreach (var (name, other) in _playerHashes)
            {
                if (name == player.Name) continue;
                ulong delta = other.frameId > message.FrameId ? other.frameId - message.FrameId : message.FrameId - other.frameId;
                if (delta > 1) continue; // 进度差太大（上报未对齐），跳过本次比对

                if (other.hash != message.WorldHash)
                {
                    string detail = $"帧={message.FrameId} {player.Name}={message.WorldHash:X8} vs {name}={other.hash:X8}";
                    Debug.LogError($"[Server][RoomManager] Desync 检测到分歧！{detail}");
                    BroadcastDesyncNotice(message.FrameId, detail);
                }
            }
        }

        /// <summary>广播 Desync 分歧通知给所有在线客户端（客户端据此日志/UI 标记分歧）</summary>
        private void BroadcastDesyncNotice(ulong frameId, string detail)
        {
            var msg = new ServerMessage
            {
                DesyncNotice = new DesyncNoticeMessage
                {
                    FrameId = frameId,
                    Detail = detail
                }
            };
            byte[] data = msg.ToByteArray();
            foreach (var p in _players.Values)
            {
                if (p.Online) OnSendKcp?.Invoke(p.ClientId, data);
            }
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
            }
            _playerByClient.Remove(clientId);
            OnRemoveClient?.Invoke(clientId);
        }

        #endregion

        #region 心跳检测

        private void CheckHeartbeatTimeout()
        {
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
        }

        #endregion

        #region 帧广播

        private void BroadcastGameSync()
        {
            // 严格帧同步：等待所有"参与中"的在线玩家至少有一个输入才广播
            // （重连/中途加入恢复中的玩家 Joining=true，不参与等待，避免恢复期卡住全房间）
            var onlinePlayers = _players.Values.Where(p => p.Online).ToList();
            var activePlayers = onlinePlayers.Where(p => !p.Joining).ToList();
            if (activePlayers.Count == 0) return;

            bool allReady = activePlayers.All(p => p.InputQueue.Count > 0);
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

            foreach (var player in activePlayers)
            {
                var sync = player.InputQueue.Dequeue();
                sync.FrameId = _serverFrameId; // 服务端统一分配帧号

                log += $" {sync.Name}:{sync.Commands.Count}cmd";
                gameSync.Players.Add(sync);

                // 缓存历史帧（服务端帧号为Key，用于断线重连补发；重连复用会话，缓存不清空）
                player.Frames[_serverFrameId] = sync;
                player.CurrentFrameIds.Enqueue(_serverFrameId);

                // 帧缓存上限保护，超出淘汰最旧帧
                while (player.CurrentFrameIds.Count > MAX_CACHED_FRAMES)
                {
                    player.Frames.Remove(player.CurrentFrameIds.Dequeue());
                }
            }

            // Debug.Log($"[Server][RoomManager] {log}");

            byte[] data = syncMessage.ToByteArray();
            foreach (var player in activePlayers)
            {
                OnSendKcp?.Invoke(player.ClientId, data);
            }
        }

        #endregion

        #region 辅助方法

        private void SendReconnectData(uint clientId, PlayerSession player)
        {
            if (_gameSnapshot == null)
            {
                Debug.LogWarning($"[Server][RoomManager] 重连/中途加入时无快照可发 clientId={clientId}");
                return;
            }

            // 1. 权威快照广播给所有在线客户端（快照漂移根治 2026-08-03）：
            //    重连玩家 + 在线玩家全部从同一快照点重置，消除"不同时间点恢复导致位置漂移"。
            //    （LastFrameId 取快照中该玩家的执行帧，客户端据此恢复 _sendSeq）
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
                    // 每个玩家以自己的执行帧为恢复点（LastFrameId=ss.FrameId），
                    // 补发起点取所有玩家恢复点的最小值（见下方），保证覆盖每个玩家的恢复范围
                    LastFrameId = ss.FrameId
                };

                snapMsg.GameSnapshotMessage.Snapshot.PlayerSSs.Add(ps);
            }

            // 物品快照一并广播（协作搬运 demo：重连/中途加入后客户端需按物品快照重建世界）
            foreach (var ss in _gameSnapshot.ItemSSs)
            {
                snapMsg.GameSnapshotMessage.Snapshot.ItemSSs.Add(ss);
            }

            BroadcastKcp(snapMsg.ToByteArray());

            // 补发起点：所有玩家恢复点的最小值 + 1（必须从玩家实际断点开始，
            // 否则像"快照帧=539 但某玩家执行帧=302"时，从540起会把 303..539 全部跳过造成帧缺口）
            ulong resumeFrom = _gameSnapshot.FrameId + 1;
            foreach (var ss in _gameSnapshot.PlayerSSs)
            {
                if (ss.FrameId + 1 < resumeFrom) resumeFrom = ss.FrameId + 1;
            }

            // 防御：补发起点超出帧缓存窗口时补发会不完整，属异常场景，打日志便于定位
            if (resumeFrom < _serverFrameId - MAX_CACHED_FRAMES)
            {
                Debug.LogWarning($"[Server][RoomManager] 快照帧={_gameSnapshot.FrameId} 落后当前帧={_serverFrameId} 超过缓存窗口，补发可能不完整");
            }

            // 2. 补发历史帧 [resumeFrom, 当前帧]（按服务端帧号整帧补发，分包广播给所有客户端；
            //    离线/停滞玩家的缺失帧会自然跳过，客户端侧用"跳帧容错"冻结跳过）
            const int MAX_FRAMES_PER_PACKET = 10;
            var framesMsg = new ServerMessage
            {
                GameSnapshotMessage = new GameSnapshotMessage
                {
                    Frames = new GameFrame()
                }
            };
            framesMsg.GameSnapshotMessage.Frames.FrameId = _serverFrameId;

            // 各玩家自己的补发起点（快照执行帧+1）：只补发其恢复点之后的帧，
            // 避免恢复点之前的旧帧灌入客户端缓冲（造成"缓冲炸了"的假象）
            var resumeByPlayer = new Dictionary<string, ulong>();
            foreach (var ss in _gameSnapshot.PlayerSSs)
                resumeByPlayer[ss.Name] = ss.FrameId + 1;

            int frameCount = 0;
            for (ulong frameId = resumeFrom; frameId <= _serverFrameId; frameId++)
            {
                foreach (var p in _players.Values)
                {
                    if (!p.Frames.TryGetValue(frameId, out var sync)) continue;

                    // 该帧早于该玩家的恢复点则跳过（快照中无此玩家的中途加入者默认从快照帧+1 起）
                    if (!resumeByPlayer.TryGetValue(sync.Name, out ulong playerResume))
                        playerResume = _gameSnapshot.FrameId + 1;
                    if (frameId < playerResume) continue;

                    framesMsg.GameSnapshotMessage.Frames.Players.Add(sync);
                    frameCount++;
                }

                if (frameCount >= MAX_FRAMES_PER_PACKET)
                {
                    BroadcastKcp(framesMsg.ToByteArray());
                    framesMsg.GameSnapshotMessage.Frames.Players.Clear();
                    frameCount = 0;
                }
            }

            if (frameCount > 0)
                BroadcastKcp(framesMsg.ToByteArray());

            Debug.Log($"[Server][RoomManager] 补发重连数据（广播全客户端） clientId={clientId} 快照帧={_gameSnapshot.FrameId} 补帧至={_serverFrameId}");
        }

        /// <summary>向所有在线客户端广播（KCP）</summary>
        private void BroadcastKcp(byte[] data)
        {
            foreach (var p in _players.Values)
            {
                if (p.Online)
                    OnSendKcp?.Invoke(p.ClientId, data);
            }
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
