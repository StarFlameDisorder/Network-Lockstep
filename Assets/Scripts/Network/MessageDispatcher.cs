using System;
using System.Collections.Generic;
using ConnectMessage;
using Google.Protobuf;
using SyncMessage;
using UnityEngine;

namespace Network
{
    
    public class MessageDispatcher
    {
        Dictionary<Signals,Action<IMessage>> _handlers=new ();

        public void RegisterHandler<T>(Signals signal, Action<T> handler)where T:IMessage,new() 
        {
            _handlers[signal] = msg => { handler((T)msg); };
            Debug.Log("MessageDispatcher:注册信号处理器"+signal);
            
            // HandShakeResponse response = new HandShakeResponse();
            // TriggerHandler(Signals.ConnectHandShake, response);
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
                Debug.Log("MessageDispatcher:无相关注册信号处理器:"+signal);
            }
        }
        
        public static void HandleMessage(byte[] data)
        {
            
            
            ServerMessage message = ServerMessage.Parser.ParseFrom(data);
            switch (message.ContentCase)
            {
                case ServerMessage.ContentOneofCase.ConnectMessage:
                    HandleConnectMessage(message.ConnectMessage);
                    break;
                default:
                    Debug.Log("HandleMessage:未知类型"+message.ContentCase);
                    break;
            }

        }

        public static void HandleConnectMessage(ServerConnectMessage message)
        {
            switch (message.ContentCase)
            {
                case ServerConnectMessage.ContentOneofCase.HandShakeMessage:
                    Debug.Log("Tcp-"+message.HandShakeMessage.Content);
                    NetworkManager.Instance.SetClientId(message.HandShakeMessage.ClientId);
                    break;
                default:
                    Debug.Log("HandleConnectMessage:未知类型"+message.ContentCase);
                    break;
            }
            Action<string> printMessage = Console.WriteLine;
        }
        
    }
}