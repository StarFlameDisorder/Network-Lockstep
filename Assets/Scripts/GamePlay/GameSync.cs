using System;
using System.Collections.Generic;
using GameMessage;
using Network;
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
        private List<Rigidbody> _rigidbodys;
        
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
            // foreach (var player in players)
            // {
            //     _rigidbodys.Add(player.GetComponent<Rigidbody>());
            // }
        }

        private void Start()
        {
            NetworkManager.Instance.RegisterHandler<GameSyncMessage>(Signals.GameSync,ReceiveMessage);
        }

        public void PlayerAction(Vector2 mov)
        {
            _velocity1 = new Vector3(mov.x,0,mov.y);
        }

        private void Update()
        {
            _rigidbody1.transform.Translate(_speed*Time.deltaTime*_velocity1);
            _rigidbody2.transform.Translate(_speed*Time.deltaTime*_velocity2);
        }

        void ReceiveMessage(GameSyncMessage message)
        {
            var player = message.Players[0];
            var v = player.InputMove;
            _velocity2 = new Vector3(v.X, v.Y, v.Z);
        }
        
        GameStatus _status=GameStatus.Notstarted;
        
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
        
        public Action GameStartEvent;
        public Action GamePauseEvent;
        public Action GameContinueEvent;
    }
}