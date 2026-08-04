using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
using GameMessage;
using GamePlay;
using LobbyMessage;
using Network;
using Network.Client;
using Network.Server;
using UnityEngine;
using UnityMath;
using Google.Protobuf;
using SyncMessage;

namespace FrameSync
{
    public enum GameStatus
    {
        NotStarted,
        Started,
        Pause
    }
    
    /// <summary>
    /// 帧同步主控子系统（框架层）。
    /// 通过 SubSystemBase.Update 累加器驱动逻辑帧，替代协程 TimerHandle。
    /// 负责：输入发送/接收、帧调度（喂帧给实体）、快照恢复、房间操作。
    /// 游戏逻辑实体（PlayerEntity）由本类创建并逐帧喂输入，实体本身不依赖框架。
    /// </summary>
    public class GameSync : SubSystemBase
    {
        public override SubSystemPriority Priority => SubSystemPriority.GamePlay;
        public static GameSync Instance;
        private GameClient _gameClient;

        public const int BufferSize = 3;
        public const int MaxCatchupTime = 5;

        private static int _gameFrameRate = 30;
        private static FixedPoint _gameFrameSpacing = FixedPoint.FromFloat(1f / _gameFrameRate);
        private static int _snapshotSpacing = 10;
        
        private FixedPoint _speed = FixedPoint.FromFloat(10f);
        
        /// <summary>玩家预制体（由 GameCore 注入）</summary>
        private GameObject _playerPrefab;

        #region 调试属性（供 UI 面板读取）

        /// <summary>服务端最新帧号</summary>
        public ulong LatestServerFrameId => _latestServerFrameId;
        /// <summary>本地发送序号</summary>
        public ulong SendSeq => _sendSeq;
        /// <summary>玩家列表（只读）</summary>
        public IReadOnlyDictionary<string, PlayerEntity> Players => _players;
        /// <summary>大厅阶段待加入的玩家名（游戏开始前显示用）</summary>
        public IReadOnlyCollection<string> PendingPlayerNames => _pendingPlayerNames;
        /// <summary>房主名</summary>
        public string OwnerName => _ownerName;
        /// <summary>世界哈希值（所有玩家位置XOR，用于一致性校验。静止时不变）</summary>
        public string WorldHash => ComputeWorldHash();

        /// <summary>
        /// 计算世界哈希：对所有玩家与物品的位置原始值做XOR
        /// （物品纳入哈希，Desync 检测才能覆盖搬运玩法）
        /// </summary>
        private string ComputeWorldHash()
        {
            if (_players.Count == 0) return "00000000";
            int hash = 0;
            foreach (var kv in _players)
            {
                var pos = kv.Value.Position;
                hash ^= pos.GetRawX();
                hash ^= pos.GetRawY();
                hash ^= pos.GetRawZ();
            }
            foreach (var item in _items)
            {
                hash ^= item.Position.GetRawX();
                hash ^= item.Position.GetRawY();
                hash ^= item.Position.GetRawZ();
            }
            return hash.ToString("X8");
        }

        #endregion
        
        // Update 累加器（替代协程 TimerHandle）
        private float _gameTickAccum;
        private float _heartBeatAccum;
        // 逻辑帧间隔与帧率保持单一来源（避免两处常量不一致）
        private static readonly float GAME_TICK_INTERVAL = 1f / _gameFrameRate;
        private const float HEARTBEAT_INTERVAL = 1f;
        
        public override void Init()
        {
            Instance = this;
        }
        
        /// <summary>
        /// 由 GameCore 在注册后注入依赖（替代 [SerializeField]）
        /// </summary>
        public void InjectPrefab(GameObject playerPrefab)
        {
            _playerPrefab = playerPrefab;
        }

        public void PostInit()
        {
            if (!Global.TryGet(out _gameClient))
            {
                Debug.LogError("[Client][GameSync]获取GameClient子系统错误");
                return;
            }
            
            _gameClient.RegisterHandler<GameSyncMessage>(Signals.GameSync, ReceiveMessage);//服务器消息接收
            _gameClient.RegisterHandler<PlayerJoinRoomResponse>(Signals.LobbyJoinRoom, JoinRoom);//加入房间消息
            _gameClient.RegisterHandler<PlayerLeaveRoomResponse>(Signals.LobbyLeaveRoom, LeaveRoom);//离开房间
            _gameClient.RegisterHandler<PlayerStartRoomResponse>(Signals.LobbyStartRoom, StartRoom);//开始游戏
            _gameClient.RegisterHandler<PlayerEndRoomResponse>(Signals.LobbyEndRoom, EndRoomHandler);//结束游戏
            _gameClient.RegisterHandler<GameSnapshotMessage>(Signals.GameSnapShot, ReceiveSnapshotMessage);//断线重连 收到快照
            _gameClient.RegisterHandler<DesyncNoticeMessage>(Signals.Desync, ReceiveDesyncNotice);//Desync 分歧通知
        }
        
        /// <summary>
        /// 累加器驱动：替代协程 TimerHandle，由 GameCore→SystemManager.Update 统一驱动
        /// </summary>
        public override void Update(float deltaTime)
        {
            _gameTickAccum += deltaTime;
            if (_gameTickAccum >= GAME_TICK_INTERVAL)
            {
                _gameTickAccum -= GAME_TICK_INTERVAL;
                TickGame();
            }
            
            _heartBeatAccum += deltaTime;
            if (_heartBeatAccum >= HEARTBEAT_INTERVAL)
            {
                _heartBeatAccum -= HEARTBEAT_INTERVAL;
                SendHeartBeat();
            }
        }
        
        public override void Destroy()
        {
            EndGame();
            Instance = null;
            _players.Clear();
            _frameBuffers.Clear();
            ClearItems();
        }
        
        #region 房间操作
        
        private string _name = "";
        private string _ownerName = "";
        private HashSet<string> _pendingPlayerNames = new();  // 大厅阶段收集的玩家名，游戏开始时统一创建实体

        public void SetName(string playerName)
        {
            _name = playerName;
            _pendingPlayerNames.Add(playerName);
        }
        
        private Dictionary<string, PlayerEntity> _players = new();
        /// <summary>每个玩家的帧缓冲（框架层：缓冲/帧号/缺口/追帧），与玩家实体一一对应</summary>
        private Dictionary<string, FrameBuffer> _frameBuffers = new();
        /// <summary>帧缓冲只读访问（调试面板用）</summary>
        public IReadOnlyDictionary<string, FrameBuffer> FrameBuffers => _frameBuffers;

        /// <summary>物品世界（协作搬运 demo）：所有客户端确定性模拟，不直接联网，天然一致</summary>
        private readonly List<ItemEntity> _items = new();
        /// <summary>物品只读访问（调试面板用）</summary>
        public IReadOnlyList<ItemEntity> Items => _items;
        /// <summary>火车（送达区）视觉对象（纯表现，不参与逻辑）</summary>
        private GameObject _trainVisual;

        /// <summary>已送达总次数（计分，用于 UI 显示；确定性，各端一致）</summary>
        public int DeliveredTotal
        {
            get
            {
                int total = 0;
                foreach (var item in _items) total += item.DeliverCount;
                return total;
            }
        }
        
        private void JoinRoom(PlayerJoinRoomResponse response)
        {
            Debug.Log($"[Client][GameSync] 收到PlayerJoinRoomResponse 房主{response.Owner} 游戏已开始={response.GameStarted}");
            
            foreach (var playerName in response.Players)
            {
                _pendingPlayerNames.Add(playerName);
                if (playerName != _name)
                {
                    // 大厅动态显示：其他玩家加入房间
                    DebugLogger.ClientLog("[TCP]", $"玩家 {playerName} 加入房间", "←");
                }
            }
            _ownerName = response.Owner;

            // 中途加入：游戏已开始，服务端会补发快照+历史帧，收到快照后自动进入游戏
            if (response.GameStarted)
            {
                Debug.Log("[Client][GameSync] 房间游戏已开始（中途加入），等待快照恢复");
            }
        }

        /// <summary>
        /// 从待创建列表中批量创建玩家实体（游戏开始时调用）
        /// </summary>
        private void CreateAllPlayerEntities()
        {
            foreach (var playerName in _pendingPlayerNames)
            {
                AddPlayer(playerName);
            }
            _pendingPlayerNames.Clear();
        }

        private void AddPlayer(string playerName)
        {
            if (!_players.ContainsKey(playerName))
            {
                Debug.Log("[Client][GameSync] 添加玩家");
                GameObject o = UnityEngine.Object.Instantiate(_playerPrefab);
                FixedPointVector3 startPos = FixedPointVector3.FromVector3(o.transform.position);
                var entity = new PlayerEntity(playerName, startPos, _gameFrameSpacing, _speed);
                _players.Add(playerName, entity);
                _frameBuffers.Add(playerName, new FrameBuffer());
                
                // 绑定表现层
                var view = o.GetComponent<PlayerView>();
                if (view == null) view = o.AddComponent<PlayerView>();
                view.Bind(entity);
            }
        }

        #region 物品世界（协作搬运 demo）

        /// <summary>创建全部物品 + 火车视觉（游戏开始时；出生点取自 CargoConfig 代码常量，各端一致）</summary>
        private void CreateAllItems()
        {
            ClearItems(); // 防御：避免重复创建
            for (int i = 0; i < CargoConfig.ItemSpawnPos.Length; i++)
            {
                AddItemEntity(new ItemEntity(i, CargoConfig.ItemSpawnPos[i]));
            }
            CreateTrainVisual();
            Debug.Log($"[Client][GameSync] 创建物品 {_items.Count} 个");
        }

        /// <summary>创建一个物品实体 + 表现层（立方体 + ItemView）</summary>
        private void AddItemEntity(ItemEntity item)
        {
            _items.Add(item);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Item_{item.Id}";
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            go.transform.position = item.Position.ToVector3();
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material.color = Color.yellow;

            var view = go.AddComponent<ItemView>();
            view.Bind(item);
        }

        /// <summary>创建火车（送达区）视觉对象（纯表现，逻辑判定用 CargoConfig.TrainPos）</summary>
        private void CreateTrainVisual()
        {
            if (_trainVisual != null) return;
            _trainVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _trainVisual.name = "Train";
            _trainVisual.transform.position = CargoConfig.TrainPos.ToVector3();
            _trainVisual.transform.localScale = new Vector3(3f, 2f, 1.5f);
            var renderer = _trainVisual.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material.color = Color.gray;
        }

        /// <summary>清空物品世界（含表现层与火车视觉；快照重建/房间结束时调用）</summary>
        private void ClearItems()
        {
            foreach (var item in _items) item.Destroy();
            _items.Clear();
            if (_trainVisual != null)
            {
                UnityEngine.Object.DestroyImmediate(_trainVisual);
                _trainVisual = null;
            }
        }

        /// <summary>从快照重建物品世界（断线重连/中途加入）：按快照全量恢复位置/持有者/送达次数</summary>
        private void RebuildItemsFromSnapshot(GameSnapshot snapshot)
        {
            ClearItems();
            foreach (var ss in snapshot.ItemSSs)
            {
                int id = (int)ss.ObjectId;
                // 防御：快照 id 越界时取模回退到出生点配置
                var spawn = CargoConfig.ItemSpawnPos[id % CargoConfig.ItemSpawnPos.Length];
                var item = new ItemEntity(id, spawn);
                item.SetSnapshotSync(ss);
                AddItemEntity(item);
            }
            CreateTrainVisual();

            // 恢复携带关系：持有者仍在本房间 → 重新挂回；已不在 → 释放为自由
            foreach (var item in _items)
            {
                if (!item.IsFree)
                {
                    if (_players.TryGetValue(item.Owner, out var owner))
                        owner.RestoreCarriedItem(item);
                    else
                        item.Release();
                }
            }
        }

        #endregion

        private void LeaveRoom(PlayerLeaveRoomResponse response)
        {
            Debug.Log("[Client][GameSync] 收到PlayerLeaveRoomResponse");
            if (_players.Remove(response.Name, out var entity))
            {
                entity.Destroy();
            }
            _frameBuffers.Remove(response.Name);
            _pendingPlayerNames.Remove(response.Name);

            // 大厅动态显示：其他玩家离开房间
            if (response.Name != _name)
                DebugLogger.ClientLog("[TCP]", $"玩家 {response.Name} 离开房间", "←");
        }

        /// <summary>
        /// 房间结束（服务端广播）：重置游戏状态，清理玩家实体与物品世界
        /// </summary>
        private void EndRoomHandler(PlayerEndRoomResponse response)
        {
            Debug.Log("[Client][GameSync] 收到PlayerEndRoomResponse，房间结束，重置状态");
            foreach (var entity in _players.Values)
            {
                entity.Destroy();
            }
            _players.Clear();
            _frameBuffers.Clear();
            _pendingPlayerNames.Clear();
            ClearItems();
            _latestServerFrameId = 0;
            _sendSeq = 1;
            EndGame();
        }

        private void StartRoom(PlayerStartRoomResponse response)
        {
            Debug.Log("[Client][GameSync] 收到PlayerStartRoomResponse");
            
            // 游戏开始：统一创建所有玩家实体 + 物品世界
            CreateAllPlayerEntities();
            CreateAllItems();
            StartGame();
            
            if (_name == _ownerName)
            {
                SyncSnapshot(0, _gameClient.GetClientId());
                _lastSnapshotFrameId = 0;
            }
        }
        #endregion
        
        #region 玩家输入操作处理及心跳
        
        private UInt64 _sendSeq = 1;                   // 本地发送序号（仅用于跟踪，非帧权威）
        private UInt64 _latestServerFrameId;           // 服务端广播的最新帧号（权威）
        private UInt64 _lastSnapshotFrameId;           // 上次上报快照的帧号（避免漏报/重复上报）
        private UInt64 _lastAppliedSnapshotFrameId;    // 最近一次应用（重建）的快照帧号（去重用）
        private readonly List<InputCommand> _pendingEvents = new(); // 事件型命令（MoveTo/Attack/...），发送后清空
        private InputCommand _lastMoveDirection;                   // 持续型命令：最后一次 MoveDirection（每帧重发）
        private bool _hasMoveDirection;
        
        
        /// <summary>
        /// 入队命令（由 PlayerController 等输入层调用，语义命令）。
        /// 持续型（MoveDirection）：缓存为最后一次方向，每帧重发——兼容 InputSystem 值不变不回调的情况
        ///（按住 WASD 方向不变只回调一次，若发后即清会导致玩家只动一帧；松开按键回调方向为 0 自然停止）；
        /// 事件型（MoveTo/Attack/...）：进本帧列表，发送后清空。
        /// </summary>
        public void EnqueueCommand(InputCommand command)
        {
            if (command.Type == CommandType.MoveDirection)
            {
                _lastMoveDirection = command;
                _hasMoveDirection = true;
            }
            else
            {
                _pendingEvents.Add(command);
            }
        }

        /// <summary>
        /// 发送本帧本地输入到服务器（帧号由服务端统一分配）。
        /// 不在本地预写入——严格 Lockstep 下等服务端广播后才执行。
        /// </summary>
        private void SendLocalInput()
        {
            // 断线/未连接时停止发送（服务端会丢弃，且避免 UI"发"序号持续增长造成"还在发数据"的假象）
            if (_gameClient.State != GameClient.ConnectionState.Connected) return;

            uint clientId = _gameClient.GetClientId();
            
            GameSyncMessage gameSyncMessage = new GameSyncMessage
            {
                FrameId = _sendSeq // 本地发送序号，服务端会覆盖为统一帧号
            };

            var sync = new PlayerSync
            {
                FrameId = _sendSeq,
                Name = _name
            };

            // 持续型：MoveDirection 每帧重发最后一次方向（InputSystem 值不变时不回调，必须缓存重发）
            if (_hasMoveDirection)
                sync.Commands.Add(ToProtoCommand(_lastMoveDirection));

            // 事件型：本帧一次性命令（MoveTo/Attack/...），发送后清空
            foreach (var cmd in _pendingEvents)
            {
                sync.Commands.Add(ToProtoCommand(cmd));
            }
            _pendingEvents.Clear();

            gameSyncMessage.Players.Add(sync);
            
            // 发送到服务器（不本地预写，等待服务端广播统一帧号后再执行）
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                GameSyncMessage = gameSyncMessage
            };
            _gameClient.KcpSendMessage(message.ToByteArray());
            
            _sendSeq++;
        }

        /// <summary>框架层 InputCommand → proto Command（发送端编码）</summary>
        private static Command ToProtoCommand(InputCommand cmd)
        {
            switch (cmd.Type)
            {
                case CommandType.MoveDirection:
                    return new Command
                    {
                        MoveDirection = new MoveDirectionCommand
                        {
                            Direction = ToVector3D(cmd.MoveDirection)
                        }
                    };
                case CommandType.MoveTo:
                    return new Command
                    {
                        MoveTo = new MoveToCommand
                        {
                            Target = ToVector3D(cmd.MoveToTarget)
                        }
                    };
                case CommandType.Interact:
                    return new Command
                    {
                        Interact = new InteractCommand()
                    };
                default:
                    return new Command();
            }
        }

        private static Vector3D ToVector3D(FixedPointVector3 v)
        {
            return new Vector3D
            {
                X = v.GetRawX(),
                Y = v.GetRawY(),
                Z = v.GetRawZ()
            };
        }

        /// <summary>
        /// 快照上报（D1：任意客户端定时上报，服务端缓存为权威快照，供断线重连/中途加入恢复）。
        /// 独立于输入发送，按"超过上次上报帧+间隔"触发而非取模，避免帧号批量到达跳变时漏报。
        /// </summary>
        private void TryReportSnapshot()
        {
            if (_gameClient.State != GameClient.ConnectionState.Connected) return;

            ulong snapshotSpacing = (UInt64)(_gameFrameRate * _snapshotSpacing);
            if (_latestServerFrameId > 0 && _latestServerFrameId >= _lastSnapshotFrameId + snapshotSpacing)
            {
                SyncSnapshot(_latestServerFrameId, _gameClient.GetClientId());
                _lastSnapshotFrameId = _latestServerFrameId;
            }
        }

        private void SyncSnapshot(UInt64 frameId, uint clientId)
        {
            GameSnapshot snapshot = new GameSnapshot();
            foreach (var pair in _players)
            {
                // 恢复点 = 每个玩家各自执行到的帧号（缓冲进度由框架层维护）
                snapshot.PlayerSSs.Add(pair.Value.GetSnapshotSync(_frameBuffers[pair.Key].LastExecutedFrameId));
            }

            // 物品状态进快照（协作搬运 demo：位置/持有者/送达次数，重连/中途加入恢复用）
            foreach (var item in _items)
            {
                snapshot.ItemSSs.Add(item.GetSnapshotSync());
            }

            snapshot.FrameId = frameId;

            ClientMessage snapMessage = new ClientMessage
            {
                ClientId = clientId,
                GameSnapshotMessage = new GameSnapshotMessage
                {
                    Snapshot = snapshot
                }
            };
            _gameClient.KcpSendMessage(snapMessage.ToByteArray());
        }
        
        private void SendHeartBeat()
        {
            // 断线/未连接时停止心跳
            if (_gameClient.State != GameClient.ConnectionState.Connected) return;

            uint clientId = _gameClient.GetClientId();
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                HeartBeat = new HeartBeat
                {
                    Name = _name
                }
            };
            _gameClient.KcpSendMessage(message.ToByteArray());
        }
        
        #endregion

        #region 游戏状态更新
        
        private void TickGame()
        {
            if (_status != GameStatus.Started) return;
            
            // 1. 发送本帧本地输入到服务器
            SendLocalInput();
            
            // 2. 快照上报（服务端缓存为权威快照，供断线重连/中途加入恢复）
            TryReportSnapshot();
            
            // 3. 哈希上报（Desync 检测：服务端跨客户端比对，不一致广播 DesyncNotice）
            TryReportHash();
            
            // 4. 框架调度：从缓冲取每帧输入喂给实体执行（缺口/离线 = 喂 null，实体冻结）
            ApplyFrames();
        }

        /// <summary>哈希上报间隔（逻辑帧数，30 = 约 1 秒）</summary>
        private const uint HASH_REPORT_INTERVAL = 30;
        private uint _hashReportCounter;

        /// <summary>
        /// 哈希上报（Desync 检测闭环客户端侧）：定期把世界哈希发给服务端，
        /// 服务端跨客户端比对，不一致时广播 DesyncNotice（见 ReceiveDesyncNotice）。
        /// </summary>
        private void TryReportHash()
        {
            if (_gameClient.State != GameClient.ConnectionState.Connected) return;
            if (_players.Count == 0) return;

            if (++_hashReportCounter < HASH_REPORT_INTERVAL) return;
            _hashReportCounter = 0;

            ClientMessage message = new ClientMessage
            {
                ClientId = _gameClient.GetClientId(),
                HashReport = new HashReportMessage
                {
                    Name = _name,
                    FrameId = _latestServerFrameId,
                    WorldHash = Convert.ToUInt32(ComputeWorldHash(), 16)
                }
            };
            _gameClient.KcpSendMessage(message.ToByteArray());
        }

        /// <summary>收到服务端 Desync 分歧通知：记录分歧帧号（帧同步一致性被破坏的信号）</summary>
        private void ReceiveDesyncNotice(DesyncNoticeMessage message)
        {
            Debug.LogError($"[Client][GameSync] Desync 分歧通知！帧={message.FrameId} 详情:{message.Detail}");
        }

        /// <summary>
        /// 框架调度核心：决定"喂哪一帧"——对每个玩家从缓冲取下一帧输入，喂给实体 Simulate。
        /// 实体不感知帧号/缓冲，只消费输入；缺口/离线时输入为 null，由实体自行冻结。
        /// 追帧：缓冲超阈值时每帧多消费几帧，快速追平服务端权威帧。
        /// 确定性：按玩家名（Ordinal）排序迭代——物品拾取冲突等"先到先得"裁决必须各端顺序一致。
        /// </summary>
        private void ApplyFrames()
        {
            // Ordinal 排序保证跨端一致（默认字符串比较受文化影响，不可用于确定性）
            foreach (var name in _players.Keys.OrderBy(n => n, StringComparer.Ordinal))
            {
                var entity = _players[name];
                var buffer = _frameBuffers[name];

                int catchupTarget = 1;
                if (buffer.Count > BufferSize)
                {
                    catchupTarget = Math.Min(IntSqrt(buffer.Count), MaxCatchupTime);
                    Debug.Log($"[Client][GameSync] {entity.Name}追帧{catchupTarget - 1}");
                }

                for (int i = 0; i < catchupTarget; i++)
                {
                    FrameInput input = buffer.TryPopNextFrame();
                    if (input == null)
                    {
                        // 缓冲非空但无可用帧 = 异常缺口（正常等待时缓冲为空不触发），
                        // 持续卡住时此日志会重复出现，用于定位
                        if (buffer.Count > 0)
                            Debug.LogWarning($"[Client][GameSync] {entity.Name} 帧缺口: 需帧{buffer.NextFrameId} 但缓冲最早为{buffer.MinFrameId} (缓冲{buffer.Count}帧)");
                        break;
                    }
                    entity.Simulate(input, _items);
                }
            }
        }

        /// <summary>整数开方（避免 Math.Sqrt(double) 在完全平方数上的浮点误差取小）</summary>
        private static int IntSqrt(int n)
        {
            if (n <= 1) return n;
            int x = n, y = (x + 1) / 2;
            while (y < x) { x = y; y = (x + n / x) / 2; }
            return x;
        }

        /// <summary>proto PlayerSync → 框架层 FrameInput（接收端解码：按命令列表映射）</summary>
        private static FrameInput ToFrameInput(PlayerSync sync)
        {
            var input = new FrameInput();
            foreach (var command in sync.Commands)
            {
                switch (command.ContentCase)
                {
                    case Command.ContentOneofCase.MoveDirection:
                        input.Commands.Add(new InputCommand
                        {
                            Type = CommandType.MoveDirection,
                            MoveDirection = FixedPointVector3.FromRawValue(
                                command.MoveDirection.Direction.X,
                                command.MoveDirection.Direction.Y,
                                command.MoveDirection.Direction.Z)
                        });
                        break;
                    case Command.ContentOneofCase.MoveTo:
                        input.Commands.Add(new InputCommand
                        {
                            Type = CommandType.MoveTo,
                            MoveToTarget = FixedPointVector3.FromRawValue(
                                command.MoveTo.Target.X,
                                command.MoveTo.Target.Y,
                                command.MoveTo.Target.Z)
                        });
                        break;
                    case Command.ContentOneofCase.Interact:
                        input.Commands.Add(new InputCommand { Type = CommandType.Interact });
                        break;
                }
            }
            return input;
        }
        #endregion
        
        #region 游戏包接收
        
        void ReceiveMessage(GameSyncMessage message)
        {
            foreach (var playerSync in message.Players)
            {
                // 所有玩家（含本地）统一由服务端帧号写入框架层缓冲
                if (_frameBuffers.TryGetValue(playerSync.Name, out var buffer))
                {
                    buffer.Push(playerSync.FrameId, ToFrameInput(playerSync));
                }
            }
            
            // 记录服务端最新帧号
            if (message.Players.Count > 0)
                _latestServerFrameId = message.FrameId;
        }

        void ReceiveSnapshotMessage(GameSnapshotMessage message)
        {
            if (message.ContentCase == GameSnapshotMessage.ContentOneofCase.Snapshot)
            {
                ApplySnapshot(message.Snapshot);
            }
            else if (message.ContentCase == GameSnapshotMessage.ContentOneofCase.Frames)
            {
                HandleReplayFrames(message.Frames);
            }
            else
            {
                Debug.LogError("[Client][GameSync] ReceiveSnapshotMessage:未知类型" + message.ContentCase + BitConverter.ToString(message.ToByteArray()));
            }
        }

        /// <summary>
        /// 应用权威快照：断线重连/中途加入时清空本地所有内容，从快照全量重建
        /// </summary>
        private void ApplySnapshot(GameSnapshot snapshot)
        {
            // 快照去重：重复加入/重连触发服务端重复补发同一份快照时，
            // 跳过后续重复快照，避免重复重建产生多个玩家对象
            if (_lastAppliedSnapshotFrameId >= snapshot.FrameId)
            {
                Debug.LogWarning($"[Client][GameSync] 跳过重复快照 帧={snapshot.FrameId} (已应用{_lastAppliedSnapshotFrameId})");
                return;
            }

            // 清空本地所有内容，从快照全量重建
            foreach (var playerEntity in _players)
            {
                playerEntity.Value.Destroy();
            }
            _players.Clear();
            _frameBuffers.Clear();
            _pendingPlayerNames.Clear();
            _sendSeq = 1;
            _lastSnapshotFrameId = snapshot.FrameId;
            _lastAppliedSnapshotFrameId = snapshot.FrameId;

            foreach (var playerSS in snapshot.PlayerSSs)
            {
                AddPlayer(playerSS.Name);

                if (playerSS.Name == _name)
                {
                    _latestServerFrameId = playerSS.LastFrameId;
                    _sendSeq = playerSS.LastFrameId + 1;
                    Debug.Log($"[Client][GameSync] {playerSS.Name} 快照恢复 帧={playerSS.LastFrameId}");
                }
                // 以快照帧号为执行进度起点，从下一帧继续（清空旧缓冲重建）
                var buffer = _frameBuffers[playerSS.Name];
                buffer.Clear();
                buffer.ResetNextFrameId(playerSS.LastFrameId);
                _players[playerSS.Name].SetSnapshotSync(playerSS);
            }

            // 中途加入：快照不含自己时，以快照帧号为起点创建自己实体（从出生点开始）
            if (!_players.ContainsKey(_name))
            {
                _latestServerFrameId = snapshot.FrameId;
                AddPlayer(_name);
                _frameBuffers[_name].ResetNextFrameId(snapshot.FrameId);
                var self = _players[_name];
                var selfSync = new PlayerSnapshotSync
                {
                    Name = _name,
                    FrameId = snapshot.FrameId,
                    LastFrameId = snapshot.FrameId,
                    Pos = new Vector3D
                    {
                        X = self.Position.GetRawX(),
                        Y = self.Position.GetRawY(),
                        Z = self.Position.GetRawZ()
                    }
                };
                self.SetSnapshotSync(selfSync);
                Debug.Log($"[Client][GameSync] 中途加入，以快照帧={snapshot.FrameId} 创建自己实体");
            }

            // 物品世界从快照全量重建（含携带关系恢复；放在玩家重建之后，确保持有者实体已存在）
            RebuildItemsFromSnapshot(snapshot);

            AlignSendSeqToServerFrame();

            Debug.Log("[Client][GameSync] 断线重连/中途加入-开始游戏");
            StartGame();
        }

        /// <summary>
        /// 处理补发历史帧：灌入各玩家缓冲区并推进权威帧号
        /// </summary>
        private void HandleReplayFrames(GameMessage.GameFrame frames)
        {
            foreach (var player in frames.Players)
            {
                if (!_players.ContainsKey(player.Name)) AddPlayer(player.Name);
                _frameBuffers[player.Name].Push(player.FrameId, ToFrameInput(player));
            }

            // 补发帧推进权威帧号，并同步发送序号（避免"发"远落后于"服"）
            if (frames.FrameId > _latestServerFrameId)
            {
                _latestServerFrameId = frames.FrameId;
                AlignSendSeqToServerFrame();
            }
        }

        /// <summary>
        /// 发送序号与权威帧号对齐：避免重连后"发"从 0/1 重新开始造成帧号错乱观感
        /// （服务端广播时会用统一帧号覆盖客户端序号，此处仅用于展示与发送跟踪）
        /// </summary>
        private void AlignSendSeqToServerFrame()
        {
            if (_sendSeq <= _latestServerFrameId)
                _sendSeq = _latestServerFrameId + 1;
        }
        #endregion
        
        #region 游戏状态及触发器
        
        public event Action GameStartEvent;
        public event Action GamePauseEvent;
        public event Action GameContinueEvent;
        
        GameStatus _status = GameStatus.NotStarted;

        public GameStatus GetStatus()
        {
            return _status;
        }

        public void StartGame()
        {
            _status = GameStatus.Started;
            GameStartEvent?.Invoke();
        }
        
        public void PauseGame()
        {
            _status = GameStatus.Pause;
            GamePauseEvent?.Invoke();
        }

        public void ContinueGame()
        {
            _status = GameStatus.Started;
            GameContinueEvent?.Invoke();
        }

        public void EndGame()
        {
            _status = GameStatus.NotStarted;
        }
        
        #endregion
        
    }
}
