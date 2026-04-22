using System;
using System.Collections.Generic;
using System.Text;
using ConnectMessage;
using Google.Protobuf;
using LobbyMessage;
using SyncMessage;
using UI;
using UnityEngine;

namespace Network
{
    
    public class MessageDispatcher
    {
        Dictionary<Signals,Action<IMessage>> _handlers=new ();

        public void RegisterHandler<T>(Signals signal, Action<T> handler)where T:IMessage//,new()
        {
            _handlers[signal] = msg => { handler((T)msg); };
            Debug.Log("MessageDispatcher:注册信号处理器"+signal);
        }

        public void UnregisterHandler(Signals signal)
        {
            _handlers.Remove(signal);
        }

        private void TriggerHandler(Signals signal, IMessage message)
        {
            if (_handlers.TryGetValue(signal, out var handler))
            {
                handler(message);//调用匿名函数
            }
            else
            {
                Debug.LogError("MessageDispatcher:无相关注册信号处理器:"+signal);
            }
        }
        
        public void HandleMessage(byte[] data)
        {
            //Debug.Log("HandleMessage");
            ServerMessage message = ServerMessage.Parser.ParseFrom(data);
            switch (message.ContentCase)
            {
                case ServerMessage.ContentOneofCase.ConnectMessage:
                    HandleConnectMessage(message.ConnectMessage);
                    break;
                case ServerMessage.ContentOneofCase.GameSyncMessage:
                    TriggerHandler(Signals.GameSync,message.GameSyncMessage);
                    break;
                case ServerMessage.ContentOneofCase.LobbySync:
                    HandleLobbyMessage(message.LobbySync);
                    break;
                case ServerMessage.ContentOneofCase.GameSnapshotMessage:
                    TriggerHandler(Signals.GameSnapShot,message.GameSnapshotMessage);
                    break;
                default:
                    Debug.LogError("HandleMessage:未知类型"+message.ContentCase+BitConverter.ToString(message.ToByteArray()));
                    break;
            }

        }

        private void HandleConnectMessage(ServerConnectMessage message)
        {
            switch (message.ContentCase)
            {
                case ServerConnectMessage.ContentOneofCase.HandShakeMessage:
                    Debug.Log("Tcp-" + message.HandShakeMessage.Content);
                    MessagePanel.Instance?.AddMessage(message.HandShakeMessage.Content);
                    TriggerHandler(Signals.ConnectHandShake,message.HandShakeMessage);
                    //NetworkManager.Instance.SetClientId(message.HandShakeMessage.ClientId);
                    break;
                default:
                    Debug.LogError("HandleConnectMessage:未知类型"+message.ContentCase+BitConverter.ToString(message.ToByteArray()));
                    break;
            }
            //Action<string> printMessage = Console.WriteLine;
        }

        private void HandleLobbyMessage(LobbySyncResponse message)
        {
            switch (message.ContentCase)
            {
                case LobbySyncResponse.ContentOneofCase.JoinRoom:
                    Debug.Log("HandleLobbyMessage-JoinRoom");
                    TriggerHandler(Signals.LobbyJoinRoom,message.JoinRoom);
                    break;
                case LobbySyncResponse.ContentOneofCase.LeaveRoom:
                    Debug.Log("HandleLobbyMessage-LeaveRoom");
                    break;
                case LobbySyncResponse.ContentOneofCase.StartRoom:
                    Debug.Log("HandleLobbyMessage-StartRoom");
                    TriggerHandler(Signals.LobbyStartRoom,message.StartRoom);
                    break;
                case LobbySyncResponse.ContentOneofCase.EndRoom:
                    Debug.Log("HandleLobbyMessage-EndRoom");
                    break;
                default:
                    Debug.LogError("HandleLobbyMessage:未知类型"+message.ContentCase+BitConverter.ToString(message.ToByteArray()));
                    break;
            }
        }
    }
}