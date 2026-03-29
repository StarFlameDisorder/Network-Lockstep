using System;
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

        private void Awake()
        {
            Instance = this;
            _applyName.OnClickEvent += ApplyName;
        }

        private void ApplyName()
        {
            UInt64 clientId = NetworkManager.Instance.GetClientId();
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                LobbySync = new LobbySyncRequest
                {
                    PlayerLogin = new PlayerLoginRequest
                    {
                        Name = _nameInputField.text
                    }
                }
            };
            NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
        }
        
    }
}