using System;
using UnityEngine;

namespace GamePlay
{
    public class GameSync:MonoBehaviour
    {
     
        public static GameSync Instance;
        [SerializeField]private GameObject player1;
        private Rigidbody _rigidbody;
        private Vector3 _velocity;
        private float _speed=10f;
        private void Awake()
        {
            Instance = this;
            _rigidbody = player1.GetComponent<Rigidbody>();
        }

        public void PlayerAction(Vector2 mov)
        {
            _velocity = new Vector3(mov.x,0,mov.y);
        }

        private void Update()
        {
            _rigidbody.transform.Translate(_speed*Time.deltaTime*_velocity);
        }
    }
}