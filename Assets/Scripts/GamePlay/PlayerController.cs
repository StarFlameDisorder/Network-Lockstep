using System;
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
        
        public void OnMoveInput(InputAction.CallbackContext ctx)
        {
            Vector2 value ;
            value = ctx.ReadValue<Vector2>();
            GameSync.Instance.PlayerAction(value);
        }
        
    }
}