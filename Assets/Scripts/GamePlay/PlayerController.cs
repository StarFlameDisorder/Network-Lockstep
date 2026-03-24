using System;
using GameMessage;
using Google.Protobuf;
using Network;
using SyncMessage;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityMath;

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
            UInt64 clientId = NetworkManager.Instance.GetClientId();
            Vector2 value ;
            //value = new Vector2(0, 1f);
            value = ctx.ReadValue<Vector2>();
            
            Debug.Log(value);
            GameSync.Instance.PlayerAction(value);
            
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                GameSyncMessage = new GameSyncMessage
                {
                    Player = new PlayerSync
                    {
                        ClientId = clientId,
                        Move = new Vector3D
                        {
                            X = value.x,
                            Y=0f,
                            Z=value.y
                        }
                    }
                }
            };
            
            //NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
            NetworkManager.Instance.UdpSendMessage(message.ToByteArray());
        }
        
    }
}