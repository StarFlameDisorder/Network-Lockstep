using System;
using System.Collections;
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
        private Vector2 _input=new();
        private UInt64 _frameId = 0;
        private uint playerId = 0;
        
        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            
        }

        private void Start()
        {
            GameSync.Instance.GameStartEvent+=GameStartHandle;
        }

        private void GameStartHandle()
        {
            GameSync.Instance.RegisterTimerEvent(SyncInput);
        }
        

        public void OnMoveInput(InputAction.CallbackContext ctx)
        {
            Vector2 value ;
            value = ctx.ReadValue<Vector2>();
            _input = value;
            //Debug.Log(value);
            //GameSync.Instance.PlayerAction(value);
        }
        
        public void SyncInput()
        {
            UInt64 clientId = NetworkManager.Instance.GetClientId();
            GameSyncMessage gameSyncMessage = new GameSyncMessage
            {
                Id = _frameId,
                Players =
                {
                    new PlayerSync
                    {
                        PlayerId = clientId,
                        InputMove = new Vector3D
                        {
                            X = _input.x,
                            Y = 0f,
                            Z = _input.y
                        }
                    }
                }
            };
            
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                GameSyncMessage = gameSyncMessage
            };
            
            //NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
            NetworkManager.Instance.UdpSendMessage(message.ToByteArray());
            GameSync.Instance.PlayerAction(gameSyncMessage);
        }
        
    }
}