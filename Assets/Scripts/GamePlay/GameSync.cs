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

namespace GamePlay
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
        
        [SerializeField]private PlayerController _controller;
        private UInt64 _playerId;
        
        private float _speed=10f;
        private string _name="";
        
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            NetworkManager.Instance.RegisterHandler<GameSyncMessage>(Signals.GameSync,ReceiveMessage);//服务器消息接收
            NetworkManager.Instance.RegisterHandler<PlayerJoinRoomResponse>(Signals.LobbyJoinRoom,JoinRoom);//加入房间消息
            NetworkManager.Instance.RegisterHandler<PlayerLeaveRoomResponse>(Signals.LobbyLeaveRoom,LeaveRoom);//离开房间
            NetworkManager.Instance.RegisterHandler<PlayerStartRoomResponse>(Signals.LobbyStartRoom,StartRoom);//开始游戏
            
            RegisterTimerEvent(SyncPlayerAction);//注册操作同步
            RegisterTimerEvent(RunNextFrame);//注册下帧处理函数
            _heartBeatHandle.OnTimeTriggerEvent += HeartBeat;
        }

        public void SetName(string playerName)
        {
            _name = playerName;
            AddPlayer(playerName);
        }

        [SerializeField] private GameObject _playerPrefab;
        private Dictionary<string, GameObject> _players=new ();
        private Dictionary<string, Rigidbody> _rigidbodies=new ();
        private Dictionary<string,Vector3> _velocities=new ();
        
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
                _players.Add(playerName, o);
                Rigidbody rb = o.GetComponent<Rigidbody>();
                _rigidbodies.Add(playerName, rb);
                _velocities.Add(playerName, new Vector3());
            }
        }

        private void LeaveRoom(PlayerLeaveRoomResponse response)
        {
            Debug.Log("收到PlayerLeaveRoomResponse");

            _players.Remove(response.Name);
            _rigidbodies.Remove(response.Name);
            _velocities.Remove(response.Name);
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
                            X = (int)(_input.x * 1000),
                            Y = 0,
                            Z = (int)(_input.y * 1000)
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
        
        private void FixedUpdate()
        {
            foreach (var pair in _rigidbodies)
            {
                Rigidbody rb = pair.Value;
                rb.transform.Translate(_speed*Time.deltaTime*_velocities[pair.Key]);
                rb.position = UnitizedPosition(rb.position);
            }
            if(_name!="")StatusPanel.Instance.UpdateLocalPos(_rigidbodies[_name].position);
            if(_otherName!="")StatusPanel.Instance.UpdateExternalPos(_rigidbodies[_otherName].position);
        }

        private Vector3 UnitizedPosition(Vector3 v3)
        {
            return new Vector3(
                Mathf.Round(v3.x * 1000f) / 1000f,
                Mathf.Round(v3.y * 1000f) / 1000f,
                Mathf.Round(v3.z * 1000f) / 1000f
            );
        }
        
        private Queue<GameSyncMessage> _localGameSyncMessages=new();
        private Queue<GameSyncMessage> _gameSyncMessages=new();
        
        public void PlayerAction(GameSyncMessage message)
        {
            _localGameSyncMessages.Enqueue(message);
        }
        
        void ReceiveMessage(GameSyncMessage message)
        {
            _gameSyncMessages.Enqueue(message);
            
        }

        private string _otherName = "";
        
        private UInt64 _nextFrameId = 0;
        private UInt64 _eNextFrameId = 0;
        void RunNextFrame()
        {
            
            if(_gameSyncMessages.Count>0)
            {
                GameSyncMessage message = _gameSyncMessages.Dequeue();//远程
                
                //Debug.Log("玩家数"+message.Players.Count);
                foreach (var player in message.Players)
                {
                    if(player.Name!=_name&&_players.ContainsKey(player.Name))
                    {
                        if (player.FrameId != _eNextFrameId)
                        {
                            Debug.Log("远程 帧序号不一致 本地"+_eNextFrameId+"收到"+player.FrameId);
                            MessagePanel.Instance.AddMessage("远程 帧序号不一致 本地"+_eNextFrameId+"收到"+player.FrameId);
                            _eNextFrameId = player.FrameId;
                        }

                        _eNextFrameId++;
                        var v = player.InputMove;
                        Vector3 realV = new Vector3(v.X / 1000f, v.Y / 1000f, v.Z / 1000f);
                        _velocities[player.Name] = realV;
                        if (_otherName == "") _otherName = player.Name;
                    }
                }
                
                StatusPanel.Instance.UpdateExternalStatus(_gameSyncMessages.Count);
                if(_otherName!="")StatusPanel.Instance.UpdateExternalOffset(_velocities[_otherName]);
            }
            if(_localGameSyncMessages.Count>0)
            {
                GameSyncMessage message = _localGameSyncMessages.Dequeue();//本地
        
                var player = message.Players[0];
                if (player.FrameId != _nextFrameId)
                {
                    Debug.Log("本地 帧序号不一致 本地"+_nextFrameId+"收到"+player.FrameId);
                    MessagePanel.Instance.AddMessage("本地 帧序号不一致 本地"+_nextFrameId+"收到"+player.FrameId);
                    _nextFrameId = player.FrameId;
                }
                _nextFrameId++;
                var v = player.InputMove;
                Vector3 realV=new Vector3(v.X/1000f, v.Y/1000f, v.Z/1000f);
                _velocities[player.Name] = realV;
                
                StatusPanel.Instance.UpdateLocalStatus(_localGameSyncMessages.Count);
                StatusPanel.Instance.UpdateLocalOffset(realV);
            }
                
            
            
        }

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

        private TimerHandle _timerHandle = new TimerHandle(15);
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