using System;
using GamePlay;
using Google.Protobuf;
using LobbyMessage;
using Network;
using SyncMessage;
using TMPro;
using UnityEngine;

namespace UI
{
    public class LobbyPanel:MonoBehaviour
    {
        public static LobbyPanel Instance;
        [SerializeField] TMP_InputField _nameInputField;
        [SerializeField] private ButtonClick _applyName;
        
        [SerializeField] private ButtonClick _startGame;

        private void Awake()
        {
            Instance = this;
            _applyName.OnClickEvent += ApplyName;
            _startGame.OnClickEvent += StartGame;
        }

        private void ApplyName()
        {
            UInt64 clientId = NetworkManager.Instance.GetClientId();
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
            NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
            GameSync.Instance.SetName(_nameInputField.text);
        }

        private void StartGame()
        {
            UInt64 clientId = NetworkManager.Instance.GetClientId();
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
            NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
        }
    }
}