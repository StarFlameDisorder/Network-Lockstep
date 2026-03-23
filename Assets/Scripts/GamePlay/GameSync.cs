using System;
using GameMessage;
using Network;
using UnityEngine;

namespace GamePlay
{
    public class GameSync:MonoBehaviour
    {
     
        public static GameSync Instance;
        [SerializeField]private GameObject player1;
        [SerializeField]private GameObject player2;
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
            var player = message.Player;
            var v = player.Move;
            _velocity2 = new Vector3(v.X, v.Y, v.Z);
        }
    }
}