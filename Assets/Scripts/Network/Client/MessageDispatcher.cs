using System;
using System.Collections.Generic;
using ConnectMessage;
using Google.Protobuf;
using LobbyMessage;
using SyncMessage;
using UI;
using UnityEngine;

namespace Network.Client
{
    
    public class MessageDispatcher
    {
        Dictionary<Signals,Action<IMessage>> _handlers=new ();

        public void RegisterHandler<T>(Signals signal, Action<T> handler)where T:IMessage//,new()
        {
            _handlers[signal] = msg => { handler((T)msg); };
            Debug.Log("[Client][MessageDispatcher]注册信号处理器"+signal);
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
                Debug.LogError("[Client][MessageDispatcher]无相关注册信号处理器:"+signal);
            }
        }
        
        public void HandleMessage(byte[] data)
        {
            //Debug.Log([Client][MessageDispatcher] HandleMessage");
            ServerMessage message = ServerMessage.Parser.ParseFrom(data);

            // 服务端保活包（不在 oneof 中，ContentCase 为 None）：仅用于收包超时检测，不派发
            if (message.KeepAlive) return;

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
                case ServerMessage.ContentOneofCase.CommonMessage:
                    // 普通文本消息（KCP/TCP 注册包、服务端调试广播等）：仅记录，不派发
                    Debug.Log($"[Client][MessageDispatcher] CommonMessage: {message.CommonMessage}");
                    break;
                default:
                    Debug.LogError($"[Client][MessageDispatcher] HandleMessage:未知类型+{message.ContentCase+BitConverter.ToString(message.ToByteArray())}");
                    break;
            }

        }

        private void HandleConnectMessage(ServerConnectMessage message)
        {
            switch (message.ContentCase)
            {
                case ServerConnectMessage.ContentOneofCase.HandShakeMessage:
                    Debug.Log("[Client][MessageDispatcher] Tcp-" + message.HandShakeMessage.Content);
                    TriggerHandler(Signals.ConnectHandShake,message.HandShakeMessage);
                    //NetworkManager.Instance.SetClientId(message.HandShakeMessage.ClientId);
                    break;
                default:
                    Debug.LogError("[Client][MessageDispatcher] HandleConnectMessage:未知类型"+message.ContentCase+BitConverter.ToString(message.ToByteArray()));
                    break;
            }
            //Action<string> printMessage = Console.WriteLine;
        }

        private void HandleLobbyMessage(LobbySyncResponse message)
        {
            switch (message.ContentCase)
            {
                case LobbySyncResponse.ContentOneofCase.JoinRoom:
                    Debug.Log("[Client][MessageDispatcher] HandleLobbyMessage-JoinRoom");
                    TriggerHandler(Signals.LobbyJoinRoom,message.JoinRoom);
                    break;
                case LobbySyncResponse.ContentOneofCase.LeaveRoom:
                    Debug.Log("[Client][MessageDispatcher] HandleLobbyMessage-LeaveRoom");
                    TriggerHandler(Signals.LobbyLeaveRoom,message.LeaveRoom);
                    break;
                case LobbySyncResponse.ContentOneofCase.StartRoom:
                    Debug.Log("[Client][MessageDispatcher] HandleLobbyMessage-StartRoom");
                    TriggerHandler(Signals.LobbyStartRoom,message.StartRoom);
                    break;
                case LobbySyncResponse.ContentOneofCase.EndRoom:
                    Debug.Log("[Client][MessageDispatcher] HandleLobbyMessage-EndRoom");
                    TriggerHandler(Signals.LobbyEndRoom,message.EndRoom);
                    break;
                default:
                    Debug.LogError("[Client][MessageDispatcher] HandleLobbyMessage:未知类型"+message.ContentCase+BitConverter.ToString(message.ToByteArray()));
                    break;
            }
        }
    }
}