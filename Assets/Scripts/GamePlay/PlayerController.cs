using System;
using Framework;
using FrameSync;
using Network;
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

        private void Update()
        {
            if (_gameSync == null) return;

            // E 键上下文交互（协作搬运 demo：拾取/放下）——事件型命令，按下瞬间发送
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                _gameSync.EnqueueCommand(new InputCommand { Type = CommandType.Interact });
            }
        }
        
        public void OnMoveInput(InputAction.CallbackContext ctx)
        {
            Vector2 value = ctx.ReadValue<Vector2>();
            // 输入语义化：键盘方向 → MoveDirection 语义命令（每帧由 GameSync 统一打包发送）
            _gameSync.EnqueueCommand(new InputCommand
            {
                Type = CommandType.MoveDirection,
                MoveDirection = FixedPointVector3.FromFloat(value.x, 0, value.y)
            });
        }
        
    }
}
