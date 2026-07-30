using System;
using System.Collections.Generic;
using Framework;
using GameMessage;
using LobbyMessage;
using Network;
using UnityEngine;
using UnityMath;
using Google.Protobuf;
using Network.Client;
using SyncMessage;

namespace GamePlay
{
    public enum GameStatus
    {
        Notstarted,
        Started,
        Pause
    }
    
    /// <summary>
    /// 帧同步主控子系统。
    /// 通过 SubSystemBase.Update 累加器驱动逻辑帧，替代协程 TimerHandle。
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
        /// 计算世界哈希：对所有玩家的位置原始值做XOR
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
            return hash.ToString("X8");
        }

        #endregion
        
        // Update 累加器（替代协程 TimerHandle）
        private float _gameTickAccum;
        private float _heartBeatAccum;
        private const float GAME_TICK_INTERVAL = 1f / 30f;
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
            _gameClient.RegisterHandler<GameSnapshotMessage>(Signals.GameSnapShot, ReceiveSnapshotMessage);//断线重连 收到快照
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
                UpdateGame();
            }
            
            _heartBeatAccum += deltaTime;
            if (_heartBeatAccum >= HEARTBEAT_INTERVAL)
            {
                _heartBeatAccum -= HEARTBEAT_INTERVAL;
                HeartBeat();
            }
        }
        
        public override void Destroy()
        {
            EndGame();
            Instance = null;
            _players.Clear();
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
        
        private void JoinRoom(PlayerJoinRoomResponse response)
        {
            Debug.Log($"[Client][GameSync] 收到PlayerJoinRoomResponse 房主{response.Owner}");
            
            foreach (var playerName in response.Players)
            {
                _pendingPlayerNames.Add(playerName);
            }
            _ownerName = response.Owner;
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
                
                // 绑定表现层
                var view = o.GetComponent<PlayerView>();
                if (view == null) view = o.AddComponent<PlayerView>();
                view.Bind(entity);
            }
        }

        private void LeaveRoom(PlayerLeaveRoomResponse response)
        {
            Debug.Log("[Client][GameSync] 收到PlayerLeaveRoomResponse");
            _players.Remove(response.Name);
        }

        private void StartRoom(PlayerStartRoomResponse response)
        {
            Debug.Log("[Client][GameSync] 收到PlayerStartRoomResponse");
            
            // 游戏开始：统一创建所有玩家实体
            CreateAllPlayerEntities();
            StartGame();
            
            if (_name == _ownerName)
            {
                SyncSnapshot(0, _gameClient.GetClientId());
            }
        }
        #endregion
        
        #region 玩家输入操作处理及心跳
        private UInt64 _sendSeq = 1;                   // 本地发送序号（仅用于跟踪，非帧权威）
        private UInt64 _latestServerFrameId;           // 服务端广播的最新帧号
        private Vector2 _pendingInput;
        
        
        /// <summary>
        /// 缓存当前帧的输入操作（由 PlayerController 等外部调用）
        /// </summary>
        public void EnqueueInput(Vector2 mov)
        {
            _pendingInput = mov;
        }

        /// <summary>
        /// 每帧调用：发送本帧输入到服务器（FrameId由服务端统一分配）。
        /// 不在本地预写入——严格 Lockstep 下等服务端广播后才执行。
        /// </summary>
        public void SyncPlayerAction()
        {
            uint clientId = _gameClient.GetClientId();
            
            GameSyncMessage gameSyncMessage = new GameSyncMessage
            {
                FrameId = _sendSeq // 本地发送序号，服务端会覆盖为统一帧号
            };

            FixedPointVector3 dir = FixedPointVector3.FromFloat(_pendingInput.x, 0, _pendingInput.y);
            var sync = new PlayerSync
            {
                FrameId = _sendSeq,
                Name = _name,
                InputMove = new Vector3D
                {
                    X=dir.GetRawX(),
                    Y=dir.GetRawY(),
                    Z=dir.GetRawZ()
                }
            };
            gameSyncMessage.Players.Add(sync);
            
            // 发送到服务器（不本地预写，等待服务端广播统一帧号后再执行）
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                GameSyncMessage = gameSyncMessage
            };
            _gameClient.UdpSendMessage(message.ToByteArray());
            
            // 房主定时发送快照（使用服务端帧号判断时机）
            if (_name == _ownerName && _latestServerFrameId % (UInt64)(_gameFrameRate * _snapshotSpacing) == 0
                && _latestServerFrameId > 0)
            {
                SyncSnapshot(_latestServerFrameId, clientId);
            }
            
            _sendSeq++;
        }

        private void SyncSnapshot(UInt64 frameId, uint clientId)
        {
            GameSnapshot snapshot = new GameSnapshot();
            foreach (var pair in _players)
            {
                snapshot.PlayerSSs.Add(pair.Value.GetSnapshotSync());
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
            _gameClient.UdpSendMessage(snapMessage.ToByteArray());
        }
        
        void HeartBeat()
        {
            uint clientId = _gameClient.GetClientId();
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                HeartBeat = new HeartBeat
                {
                    Name = _name
                }
            };
            _gameClient.UdpSendMessage(message.ToByteArray());
        }
        
        #endregion

        #region 游戏状态更新
        
        private void UpdateGame()
        {
            if (_status != GameStatus.Started) return;
            
            // 1. 发送本帧本地输入到服务器，并将输入写入本地玩家缓冲区
            SyncPlayerAction();
            
            // 2. 推动所有玩家从缓冲区逐帧消费（远程输入由 ReceiveMessage 预先写入）
            var frame = new GameFrame(_latestServerFrameId, _players);
            frame.PushFrames();
        }
        #endregion
        
        #region 游戏包接收
        
        void ReceiveMessage(GameSyncMessage message)
        {
            foreach (var playerSync in message.Players)
            {
                // 所有玩家（含本地）统一由服务端帧号写入缓冲区
                if (_players.ContainsKey(playerSync.Name))
                {
                    _players[playerSync.Name].AddSyncMessage(playerSync);
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
                GameSnapshot snapshot = message.Snapshot;
                
                // 断线重连：清空本地所有内容，从快照全量重建
                foreach (var playerEntity in _players)
                {
                    playerEntity.Value.Destroy();
                }
                _players.Clear();
                
                foreach (var playerSS in snapshot.PlayerSSs)
                {
                    AddPlayer(playerSS.Name);
                    
                    if (playerSS.Name == _name)
                    {
                        _latestServerFrameId = playerSS.LastFrameId;
                        _sendSeq = playerSS.LastFrameId + 1;
                        Debug.Log($"[Client][GameSync] {playerSS.Name} 快照恢复 帧={playerSS.LastFrameId}");
                    }
                    _players[playerSS.Name].SetSnapshotSync(playerSS);
                }
                Debug.Log("[Client][GameSync] 断线重连-开始游戏");
                StartGame();
            }
            else if (message.ContentCase == GameSnapshotMessage.ContentOneofCase.Frames)
            {
                GameMessage.GameFrame frames = message.Frames;
                foreach (var player in frames.Players)
                {
                    if (!_players.ContainsKey(player.Name)) AddPlayer(player.Name);
                    _players[player.Name].AddSyncMessage(player);
                }
            }
            else
            {
                Debug.LogError("[Client][GameSync] HandleMessage:未知类型" + message.ContentCase + BitConverter.ToString(message.ToByteArray()));
            }
        }
        #endregion
        
        #region 游戏状态及触发器
        
        public event Action GameStartEvent;
        public event Action GamePauseEvent;
        public event Action GameContinueEvent;
        
        GameStatus _status = GameStatus.Notstarted;

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
            _status = GameStatus.Notstarted;
        }
        
        #endregion
        
    }
}
