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
        private Coroutine _syncCoroutine;
        private bool _isRunning = true;
        
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
            _syncCoroutine = StartCoroutine(SyncInputCoroutine());
        }
        
        private IEnumerator SyncInputCoroutine()
        {
            while (true)
            {
                if (_isRunning)
                {
                    SyncInput();
                }
                yield return new WaitForSeconds(0.1f); // 固定间隔时间
            }
        }


        public void OnMoveInput(InputAction.CallbackContext ctx)
        {
            Vector2 value ;
            //value = new Vector2(0, 1f);
            value = ctx.ReadValue<Vector2>();
            _input = value;
            //Debug.Log(value);
            GameSync.Instance.PlayerAction(value);
        }
        
        public void SyncInput()
        {
            UInt64 clientId = NetworkManager.Instance.GetClientId();
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                GameSyncMessage = new GameSyncMessage
                {
                    Players = 
                    {
                        new PlayerSync
                        {
                            PlayerId = clientId,
                            InputMove = new Vector3D
                            {
                                X = _input.x,
                                Y=0f,
                                Z=_input.y
                            }
                        }
                    }
                }
            };
            
            //NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
            NetworkManager.Instance.UdpSendMessage(message.ToByteArray());
        }
        
    }
}