using System;
using System.Collections.Generic;
using GameMessage;
using Network;
using UI;
using UnityEngine;

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
        [SerializeField]private GameObject player1;
        [SerializeField]private GameObject player2;
        [SerializeField]private List<GameObject> players;
        private List<Rigidbody> _rigidbodys=new();
        
        private Rigidbody _rigidbody1;
        private Rigidbody _rigidbody2;
        private Vector3 _velocity1;
        private Vector3 _velocity2;
        private float _speed=10f;
        private void Awake()
        {
            Instance = this;
            _rigidbody1 = player1.GetComponent<Rigidbody>();
            _rigidbody2 = player2.GetComponent<Rigidbody>();
            foreach (var player in players)
            {
                _rigidbodys.Add(player.GetComponent<Rigidbody>());
            }
        }

        private void Start()
        {
            NetworkManager.Instance.RegisterHandler<GameSyncMessage>(Signals.GameSync,ReceiveMessage);//服务器消息接收
            RegisterTimerEvent(RunNextFrame);//注册下帧处理函数
        }

        public void PlayerAction(Vector2 mov)
        {
            _velocity1 = new Vector3(mov.x,0,mov.y);
        }

        public void PlayerAction(GameSyncMessage message)
        {
            _localGameSyncMessages.Enqueue(message);
        }

        private void Update()
        {
            
            
            _rigidbody1.transform.Translate(_speed*Time.deltaTime*_velocity1);
            _rigidbody2.transform.Translate(_speed*Time.deltaTime*_velocity2);
        }
        
        private Queue<GameSyncMessage> _localGameSyncMessages=new();
        private Queue<GameSyncMessage> _gameSyncMessages=new();
        
        void ReceiveMessage(GameSyncMessage message)
        {
            _gameSyncMessages.Enqueue(message);
            
        }

        void RunNextFrame()
        {
            if(_gameSyncMessages.Count>0)
            {
                {
                    GameSyncMessage message = _gameSyncMessages.Dequeue();
            
                    var player = message.Players[0];
                    var v = player.InputMove;
                    _velocity2 = new Vector3(v.X, v.Y, v.Z);
                }
            }
            if(_localGameSyncMessages.Count>0)
            {
                {
                    GameSyncMessage message = _localGameSyncMessages.Dequeue();
            
                    var player = message.Players[0];
                    var v = player.InputMove;
                    _velocity1 = new Vector3(v.X, v.Y, v.Z);
                }
                
            }
            StatusControl.Instance.UpdateLocalStatus(_localGameSyncMessages.Count);
            StatusControl.Instance.UpdateExternalStatus(_gameSyncMessages.Count);
        }
        
        #region 游戏状态及触发器
        
        public Action GameStartEvent;
        public Action GamePauseEvent;
        public Action GameContinueEvent;

        private TimerHandle _timerHandle = new TimerHandle(30);
        
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
        }
        
        #endregion
    }
}