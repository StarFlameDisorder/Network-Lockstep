using System;
using Framework;
using GamePlay;
using Google.Protobuf;
using LobbyMessage;
using Network;
using Network.Client;
using SyncMessage;
using TMPro;
using UnityEngine;

namespace UI
{
    public class LobbyPanel:MonoBehaviour
    {
        public static LobbyPanel Instance;
        private GameClient _gameClient;
        [SerializeField] TMP_InputField _nameInputField;
        [SerializeField] private ButtonClick _applyName;
        
        [SerializeField] private ButtonClick _startGame;

        private void Awake()
        {
            Instance = this;
            _applyName.OnClickEvent += ApplyName;
            _startGame.OnClickEvent += StartGame;
        }

        private void Start()
        {
            if (!Global.TryGet(out _gameClient))
            {
                Debug.LogError("[Client][TcpSocket]获取GameClient子系统错误");
                return;
            }
        }

        private void ApplyName()
        {
            UInt64 clientId = _gameClient.GetClientId();
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                LobbySync = new LobbySyncRequest
                {
                    JoinRoom = new PlayerJoinRoomRequest
                    {
                        Name = _nameInputField.text
                    }
                    
                }
            };
            _gameClient.TcpSendMessage(message.ToByteArray());
            GameSync.Instance.SetName(_nameInputField.text);
        }

        private void StartGame()
        {
            UInt64 clientId = _gameClient.GetClientId();
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                LobbySync = new LobbySyncRequest
                {
                    StartRoom = new PlayerStartRoomRequest
                    {
                        Name = _nameInputField.text
                    }
                    
                }
            };
            _gameClient.TcpSendMessage(message.ToByteArray());
        }
    }
}