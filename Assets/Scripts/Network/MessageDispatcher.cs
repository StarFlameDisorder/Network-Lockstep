using ConnectMessage;
using SyncMessage;
using UnityEngine;

namespace Network
{
    public class MessageDispatcher
    {
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
        }
        
    }
}