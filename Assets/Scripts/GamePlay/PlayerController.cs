using System;
using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GamePlay
{
    public class PlayerController:MonoBehaviour
    {
        private PlayerInput _playerInput;
        
        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        GameSync _gameSync;
        
        private void Start()
        {
            if (!Global.TryGet(out _gameSync))
            {
                Debug.LogError("[Client][PlayerController]获取GameSync子系统错误");
                return;
            }
        }
        
        public void OnMoveInput(InputAction.CallbackContext ctx)
        {
            Vector2 value = ctx.ReadValue<Vector2>();
            _gameSync.EnqueueInput(value);
        }
        
    }
}
