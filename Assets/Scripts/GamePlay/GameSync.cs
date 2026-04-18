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
        private static int _gameFrameRate = 30;
        private static FixedPoint _gameFrameSpace = FixedPoint.FromFloat(1f / _gameFrameRate);
        
        public static GameSync Instance;
        
        [SerializeField]private PlayerController _controller;
        private UInt64 _playerId;
        
        private FixedPoint _speed= FixedPoint.FromFloat(10f);
        private string _name="";
        
        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.SetResolution(1920, 1080, false);
        }

        private void Start()
        {
            NetworkManager.Instance.RegisterHandler<GameSyncMessage>(Signals.GameSync,ReceiveMessage);//服务器消息接收
            NetworkManager.Instance.RegisterHandler<PlayerJoinRoomResponse>(Signals.LobbyJoinRoom,JoinRoom);//加入房间消息
            NetworkManager.Instance.RegisterHandler<PlayerLeaveRoomResponse>(Signals.LobbyLeaveRoom,LeaveRoom);//离开房间
            NetworkManager.Instance.RegisterHandler<PlayerStartRoomResponse>(Signals.LobbyStartRoom,StartRoom);//开始游戏
            
            RegisterTimerEvent(UpdateGame);
            _heartBeatHandle.OnTimeTriggerEvent += HeartBeat;
        }

        public void SetName(string playerName)
        {
            _name = playerName;
            AddPlayer(playerName);
        }

        [SerializeField] private GameObject _playerPrefab;
        private Dictionary<string, GameObject> _players=new ();
        
        private Dictionary<string,FixedPointVector3> _inputMove=new ();
        private Dictionary<string, FixedPointVector3> _playerPos = new();
        
        private void JoinRoom(PlayerJoinRoomResponse response)
        {
            Debug.Log("收到PlayerJoinRoomResponse");
            
            foreach (var otherPlayer in response.Players)
            {
                if (otherPlayer != _name && !_players.ContainsKey(otherPlayer))
                {
                    AddPlayer(otherPlayer);
                }
            }
        }

        private void AddPlayer(string playerName)
        {
            if(!_players.ContainsKey(playerName))
            {
                Debug.Log("添加玩家");
                GameObject o = Instantiate(_playerPrefab);
                
                _players.Add(playerName, o);//玩家游戏对象
                _inputMove.Add(playerName, new FixedPointVector3());//玩家输入
                _playerSyncMessgae.Add(playerName,new Queue<PlayerSync>());//玩家消息
                _playerPos.Add(playerName, FixedPointVector3.FromVector3(o.transform.position));//玩家当前位置
            }
        }

        private void LeaveRoom(PlayerLeaveRoomResponse response)
        {
            Debug.Log("收到PlayerLeaveRoomResponse");

            _players.Remove(response.Name);
            _inputMove.Remove(response.Name);
            _playerSyncMessgae.Remove(response.Name);
            _playerPos.Remove(response.Name);
        }

        private void StartRoom(PlayerStartRoomResponse response)
        {
            Debug.Log("收到PlayerStartRoomResponse");
            
            StartGame();
        }
        
        #region 玩家操作处理
        private UInt64 _frameId = 0;
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
            _frameId++;
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
            
            foreach (var pair in _playerSyncMessgae)
            {
                var syncMes = pair.Value;
                if(syncMes.Count==0)continue;//接收操作为空
                if(!_players.ContainsKey(pair.Key))continue;//判断玩家是否存在
                
                //输入接收
                var player = syncMes.Dequeue();
                var v = player.InputMove;
                FixedPointVector3 realV = FixedPointVector3.FromRawValue(v.X,v.Y,v.Z);
                
                
                //输入逻辑处理
                GameObject o = _players[pair.Key];
                _playerPos[pair.Key] += (_speed * _gameFrameSpace * realV);
                o.transform.position = _playerPos[pair.Key].ToVector3();
                
                //操作显示
                if (player.Name == _name)
                {
                    StatusPanel.Instance.UpdateLocalStatus(syncMes.Count);
                    StatusPanel.Instance.UpdateLocalOffset(realV.ToVector3());
                }
                else
                {
                    if (_otherName == "") _otherName = player.Name;//测试用，查找另一个玩家
                    StatusPanel.Instance.UpdateExternalStatus(syncMes.Count);
                    if(_otherName!="")StatusPanel.Instance.UpdateExternalOffset(_inputMove[_otherName].ToVector3());
                }
            }
            

            // foreach (var p in _players)
            // {
            //     if (!_checkPlayers[p.Key])
            //     {
            //         _inputMove[p.Key] = FixedPointVector3.zero;
            //         Debug.LogWarning("空帧，归零");
            //     }
            //     _checkPlayers[p.Key] = false;
            // }
            

            // foreach (var pair in _players)
            // {
            //     GameObject o = pair.Value;
            //     _playerPos[pair.Key] += (_speed * _gameFrameSpace * _inputMove[pair.Key]);
            //     o.transform.position = _playerPos[pair.Key].ToVector3();
            // }
            
            if(_name!="")StatusPanel.Instance.UpdateLocalPos(_players[_name].transform.position);
            if (_otherName != "") StatusPanel.Instance.UpdateExternalPos(_players[_otherName].transform.position);
            
            // if(_name!="" && _status == GameStatus.Started)try//日志输出
            // {
            //     string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            //     string dirPath = System.IO.Path.Combine(desktopPath, "logs");
            //     if (!System.IO.Directory.Exists(dirPath))
            //         System.IO.Directory.CreateDirectory(dirPath);
            //
            //     // 用玩家名区分不同客户端的日志
            //     string filePath = System.IO.Path.Combine(dirPath, $"fixedupdate_log_{_name}.txt");
            //
            //     using (System.IO.StreamWriter writer = new System.IO.StreamWriter(filePath, true))
            //     {
            //         // 写入帧标记（时间戳 + FixedUpdate调用序号）
            //         writer.WriteLine($"===== FixedUpdate #{_fixedUpdateCounter} at {System.DateTime.Now:HH:mm:ss.fff} =====");
            //
            //         foreach (var kvp in _inputMove)
            //         {
            //             string playerName = kvp.Key;
            //             Vector3 vel = kvp.Value.ToVector3();
            //             Vector3 pos = _playerPos.ContainsKey(playerName) ? _playerPos[playerName].ToVector3() : Vector3.zero;
            //             writer.WriteLine($"{playerName}: vel=({vel.x:F3}, {vel.y:F3}, {vel.z:F3}), pos=({pos.x:F3}, {pos.y:F3}, {pos.z:F3})");
            //         }
            //         writer.WriteLine(); // 空行分隔
            //     }
            //     _fixedUpdateCounter++;
            // }
            // catch (System.Exception e)
            // {
            //     Debug.LogError($"记录FixedUpdate状态失败: {e.Message}");
            // }
        }
        
        private Dictionary<string, Queue<PlayerSync>> _playerSyncMessgae = new();
        
        public void PlayerAction(GameSyncMessage message)
        {
            var player = message.Players[0];
            _playerSyncMessgae[player.Name].Enqueue(player);
        }
        
        void ReceiveMessage(GameSyncMessage message)
        {
            foreach (var player in message.Players)
            {
                if(player.Name!=_name&&_players.ContainsKey(player.Name))_playerSyncMessgae[player.Name].Enqueue(player);
            }
        }

        private string _otherName = "";
        
        // private UInt64 _nextFrameId = 0;
        // private UInt64 _eNextFrameId = 0;
        
        //空帧判断
        // private Dictionary<string, bool> _checkPlayers=new();
        // void RunNextFrame()
        // {
        //     foreach (var syncMes in _playerSyncMessgae.Values)
        //     {
        //         if(syncMes.Count==0)continue;
        //         var player = syncMes.Dequeue();
        //         var v = player.InputMove;
        //         FixedPointVector3 realV = FixedPointVector3.FromRawValue(v.X,v.Y,v.Z);
        //         _inputMove[player.Name] = realV;
        //         
        //         _checkPlayers[player.Name] = true;
        //
        //         if (player.Name == _name)
        //         {
        //             StatusPanel.Instance.UpdateLocalStatus(syncMes.Count);
        //             StatusPanel.Instance.UpdateLocalOffset(realV.ToVector3());
        //         }
        //         else
        //         {
        //             if (_otherName == "") _otherName = player.Name;
        //             StatusPanel.Instance.UpdateExternalStatus(syncMes.Count);
        //             if(_otherName!="")StatusPanel.Instance.UpdateExternalOffset(_inputMove[_otherName].ToVector3());
        //         }
        //     }
        //     
        //
        //     foreach (var p in _players)
        //     {
        //         if (!_checkPlayers[p.Key])
        //         {
        //             _inputMove[p.Key] = FixedPointVector3.zero;
        //             Debug.LogWarning("空帧，归零");
        //         }
        //         _checkPlayers[p.Key] = false;
        //     }
        //     
        // }

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
        
        #region 游戏状态及触发器
        
        public event Action GameStartEvent;
        public event Action GamePauseEvent;
        public event Action GameContinueEvent;

        private TimerHandle _timerHandle = new TimerHandle(_gameFrameRate);
        private TimerHandle _heartBeatHandle = new TimerHandle(1);
        
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