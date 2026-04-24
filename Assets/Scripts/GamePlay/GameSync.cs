using System;
using System.Collections.Generic;
using GameMessage;
using LobbyMessage;
using Network;
using UI;
using UnityEngine;
using UnityMath;
using Google.Protobuf;
using SyncMessage;
using Unity.VisualScripting;

namespace GamePlay//TODO: UDP重传风暴
{
    public enum GameStatus
    {
        Notstarted,
        Started,
        Pause
    }
    
    public class GameSync:MonoBehaviour
    {
        public static GameSync Instance;

        public const int BufferSize = 3;
        public const int MaxCatchupTime = 5;

        private static int _gameFrameRate = 30;
        private static FixedPoint _gameFrameSpacing = FixedPoint.FromFloat(1f / _gameFrameRate);
        private static int _snapshotSpacing = 10;
        
        [SerializeField]private PlayerController _controller;
        private UInt64 _playerId;
        
        private FixedPoint _speed= FixedPoint.FromFloat(10f);
        
        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.SetResolution(1920, 1080, false);
            Application.runInBackground = true;//TODO:不知道能否保证后台运行正常
        }

        private void Start()
        {
            NetworkManager.Instance.RegisterHandler<GameSyncMessage>(Signals.GameSync,ReceiveMessage);//服务器消息接收
            NetworkManager.Instance.RegisterHandler<PlayerJoinRoomResponse>(Signals.LobbyJoinRoom,JoinRoom);//加入房间消息
            NetworkManager.Instance.RegisterHandler<PlayerLeaveRoomResponse>(Signals.LobbyLeaveRoom,LeaveRoom);//离开房间
            NetworkManager.Instance.RegisterHandler<PlayerStartRoomResponse>(Signals.LobbyStartRoom,StartRoom);//开始游戏
            NetworkManager.Instance.RegisterHandler<GameSnapshotMessage>(Signals.GameSnapShot,ReceiveSnapshotMessage);//断线重连 收到快照
            
            RegisterTimerEvent(UpdateGame);
            _heartBeatHandle.OnTimeTriggerEvent += HeartBeat;
        }
        
        #region 房间操作
        
        private string _name="";
        private string _otherName = "";
        private string _ownerName = "";

        public void SetName(string playerName)
        {
            _name = playerName;
            AddPlayer(playerName);
        }

        [SerializeField] private GameObject _playerPrefab;
        
        private Dictionary<string,Player> _players = new();
        
        private void JoinRoom(PlayerJoinRoomResponse response)
        {
            Debug.Log($"收到PlayerJoinRoomResponse 房主{response.Owner}");
            
            foreach (var otherPlayer in response.Players)
            {
                if (otherPlayer != _name && !_players.ContainsKey(otherPlayer))
                {
                    AddPlayer(otherPlayer);
                }
            }
            _ownerName=response.Owner;
        }

        private void AddPlayer(string playerName)
        {
            if(!_players.ContainsKey(playerName))
            {
                Debug.Log("添加玩家");
                GameObject o = Instantiate(_playerPrefab);
                _players.Add(playerName, new Player(playerName,o,_gameFrameSpacing,_speed));
                
            }
        }

        private void LeaveRoom(PlayerLeaveRoomResponse response)
        {
            Debug.Log("收到PlayerLeaveRoomResponse");

            _players.Remove(response.Name);
        }

        private void StartRoom(PlayerStartRoomResponse response)
        {
            Debug.Log("收到PlayerStartRoomResponse");
            
            StartGame();
            
            if (_name==_ownerName)
            {
                SyncSnapshot(_frameId-1,NetworkManager.Instance.GetClientId());
            }
        }
        #endregion
        
        #region 玩家输入操作处理及心跳
        private UInt64 _frameId = 1;
        private Vector2 _input;
        
        public void PlayerAction(Vector2 mov)
        {
            _input= mov;
        }

        public void SyncPlayerAction()
        {
            FixedPointVector3 _inputV3 = FixedPointVector3.FromFloat(_input.x,0,_input.y);
            UInt64 clientId = NetworkManager.Instance.GetClientId();
            GameSyncMessage gameSyncMessage = new GameSyncMessage
            {
                FrameId = _frameId,
                Players =
                {
                    new PlayerSync
                    {
                        FrameId = _frameId,
                        Name = _name,
                        InputMove = new Vector3D
                        {
                            X = _inputV3.GetRawX(),
                            Y = _inputV3.GetRawY(),
                            Z = _inputV3.GetRawZ()
                        }
                    }
                }
            };
            
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                GameSyncMessage = gameSyncMessage
            };
            
            NetworkManager.Instance.UdpSendMessage(message.ToByteArray());
            PlayerAction(gameSyncMessage);

            if (_name==_ownerName&&_frameId % (UInt64)(_gameFrameRate * _snapshotSpacing) == 0)
            {
                SyncSnapshot(_frameId-1,clientId);
            }
            
            _frameId++;
        }

        private void SyncSnapshot(UInt64 frameId,UInt64 clientId)
        {
            GameSnapshot snapshot = new GameSnapshot();
            foreach (var pair in _players)
            {
                snapshot.PlayerSSs.Add(pair.Value.GetSnapshotSync());
            }

            snapshot.FrameId = frameId;//TODO:确认是否frameId正确

            ClientMessage snapMessage = new ClientMessage
            {
                ClientId = clientId,
                GameSnapshotMessage = new GameSnapshotMessage
                {
                   
                    Snapshot = snapshot
                }
            };
            NetworkManager.Instance.UdpSendMessage(snapMessage.ToByteArray());
        }
        
        
        private TimerHandle _heartBeatHandle = new TimerHandle(1);
        void HeartBeat()
        {
            UInt64 clientId = NetworkManager.Instance.GetClientId();
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                HeartBeat = new HeartBeat
                {
                    Name = _name
                }
                
            };
            NetworkManager.Instance.UdpSendMessage(message.ToByteArray());
        }
        
        #endregion

        #region 游戏状态更新
        
        private int _fixedUpdateCounter = 0;
        
        private void FixedUpdate()
        {
            
        }
        private void UpdateGame()
        {
            if (_status != GameStatus.Started)return;
            
            SyncPlayerAction();
            
            //RunNextFrame();
            foreach (var pair in _players)
            {
                var player = pair.Value;
                player.UpdateFrame();
                
                //操作显示
                if (pair.Key == _name)
                {
                    StatusPanel.Instance.UpdateLocalStatus(player.GetFrameCount());
                    StatusPanel.Instance.UpdateLocalPos(player.GetPosition().ToVector3());
                }
                else
                {
                    if (_otherName == "") _otherName = pair.Key;//测试用，查找另一个玩家
                    StatusPanel.Instance.UpdateExternalStatus(player.GetFrameCount());
                    StatusPanel.Instance.UpdateExternalPos(player.GetPosition().ToVector3());
                }
            }
            /*
            if(_name!="" && _status == GameStatus.Started)try//日志输出
            {
                string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                string dirPath = System.IO.Path.Combine(desktopPath, "logs");
                if (!System.IO.Directory.Exists(dirPath))
                    System.IO.Directory.CreateDirectory(dirPath);
            
                // 用玩家名区分不同客户端的日志
                string filePath = System.IO.Path.Combine(dirPath, $"fixedupdate_log_{_name}.txt");
            
                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(filePath, true))
                {
                    // 写入帧标记（时间戳 + FixedUpdate调用序号）
                    writer.WriteLine($"===== FixedUpdate #{_fixedUpdateCounter} at {System.DateTime.Now:HH:mm:ss.fff} =====");
            
                    foreach (var kvp in _inputMove)
                    {
                        string playerName = kvp.Key;
                        Vector3 vel = kvp.Value.ToVector3();
                        Vector3 pos = _playerPos.ContainsKey(playerName) ? _playerPos[playerName].ToVector3() : Vector3.zero;
                        writer.WriteLine($"{playerName}: vel=({vel.x:F3}, {vel.y:F3}, {vel.z:F3}), pos=({pos.x:F3}, {pos.y:F3}, {pos.z:F3})");
                    }
                    writer.WriteLine(); // 空行分隔
                }
                _fixedUpdateCounter++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"记录FixedUpdate状态失败: {e.Message}");
            }*/
        }
        #endregion
        
        #region 游戏包接收
        public void PlayerAction(GameSyncMessage message)
        {
            var player = message.Players[0];
            _players[player.Name].AddSyncMessage(player);
        }
        
        void ReceiveMessage(GameSyncMessage message)
        {
            foreach (var player in message.Players)
            {
                if(player.Name!=_name&&_players.ContainsKey(player.Name))
                {
                    _players[player.Name].AddSyncMessage(player);
                }
            }
        }

        void ReceiveSnapshotMessage(GameSnapshotMessage message)
        {
            if (message.ContentCase == GameSnapshotMessage.ContentOneofCase.Snapshot)
            {
                GameSnapshot snapshot = message.Snapshot;
                // _frameId = snapshot.LastFrameId+1;
                foreach (var playerSS in snapshot.PlayerSSs)
                {
                    if (!_players.ContainsKey(playerSS.Name))AddPlayer(playerSS.Name);
                    
                    if (playerSS.Name == _name)
                    {
                        _frameId = playerSS.LastFrameId + 1;
                        Debug.Log($"{playerSS.Name}输入设置帧{_frameId}");
                    }
                    _players[playerSS.Name].SetSnapshotSync(playerSS);
                }
                //TODO:物体位置同步
                Debug.Log("断线重连-开始游戏");
                StartGame();
            }else if (message.ContentCase == GameSnapshotMessage.ContentOneofCase.Frames)
            {
                GameFrame frames = message.Frames;
                foreach (var player in frames.Players)
                {
                    if (!_players.ContainsKey(player.Name))AddPlayer(player.Name);
                    
                    _players[player.Name].AddSyncMessage(player);
                    
                }
            }
            else
            {
                Debug.LogError("HandleMessage:未知类型"+message.ContentCase+BitConverter.ToString(message.ToByteArray()));
            }
        }
        #endregion
        
        #region 游戏状态及触发器
        
        public event Action GameStartEvent;
        public event Action GamePauseEvent;
        public event Action GameContinueEvent;

        private TimerHandle _timerHandle = new TimerHandle(_gameFrameRate);
        
        GameStatus _status=GameStatus.Notstarted;

        public void RegisterTimerEvent(Action e)
        {
            _timerHandle.OnTimeTriggerEvent += e;
        }
        
        public GameStatus GetStatus()
        {
            return _status;
        }

        public void StartGame()
        {
            _status = GameStatus.Started;
            GameStartEvent?.Invoke();
            _timerHandle.StartTimer();
            _heartBeatHandle.StartTimer();
        }
        
        public void PauseGame()
        {
            _status = GameStatus.Pause;
            GamePauseEvent?.Invoke();
            _timerHandle.StopTimer();
        }

        public void ContinueGame()
        {
            _status = GameStatus.Started;
            GameContinueEvent?.Invoke();
            _timerHandle.StartTimer();
        }

        public void EndGame()
        {
            _status = GameStatus.Notstarted;
            _timerHandle.Destroy();
            _heartBeatHandle.Destroy();
        }
        
        #endregion
        
    }
}