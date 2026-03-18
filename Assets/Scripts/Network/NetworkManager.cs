using System;
using System.Text;
using ConnectMessage;
using Google.Protobuf;
using TMPro;
using UnityEngine;

namespace Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;
        private TcpSocket _tcpSocket=new TcpSocket();
        private UdpSocket _udpSocket=new UdpSocket();
        private int _index = 0;
        private UInt64 clientId = 0;
        private string _ip;
        private int _port;
        private MessageDispatcher _messageDispatcher=new();

        public void RegisterHandler<T>(Signals signal, Action<T> handler)where T:IMessage,new()
        {
            _messageDispatcher.RegisterHandler(signal,handler);
        }

        public void UnregisterHandler(Signals signal)
        {
            _messageDispatcher.UnregisterHandler(signal);
        }
        
        
        private void Awake()
        {
            Instance = this;
            RegisterHandler<HandShakeResponse>(Signals.ConnectHandShake,test);
        }

        void test(HandShakeResponse response)
        {
            Debug.Log("触发处理函数");
        }

        public void StartLink(string ip, int port)
        {
            _tcpSocket.StartLink(ip, port);
            _udpSocket.StartLink(ip, port);
        }

        public void StopLink()
        {
            if(TcpIsConnected())_tcpSocket.CloseLink();
        }
        
        public bool TcpIsConnected()
        {
            return _tcpSocket != null && _tcpSocket.IsConnected();
        }

        public void TcpSendMessage(byte[] data)
        {
            if(TcpIsConnected())_tcpSocket.Send(data);
            //else Debug.Log("TcpSendMessage:未建立连接");
        }
        
        public bool TcpSendMessageBool(byte[] data)
        {
            if(TcpIsConnected())
            {
                _tcpSocket.Send(data);
                return true;
            }
            else
            {
                //Debug.Log("TcpSendMessage:未建立连接");
                return false;
            }
        }

        public bool UdpIsConnected()
        {
            return _udpSocket != null && _udpSocket.IsConnected();
        }

        public void UdpSendMessage(byte[] data)
        {
            if(UdpIsConnected())_udpSocket.Send(data);
        }

        public void HandleMessage(byte[] data)
        {
            MessageDispatcher.HandleMessage(data);
        }

        public void SetClientId(UInt64 clientId)
        {
            this.clientId = clientId;
            _tcpSocket.bindClientId(clientId);
            _udpSocket.bindClientId(clientId);
        }
    }
}
