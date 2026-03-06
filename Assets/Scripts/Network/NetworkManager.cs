using System;
using System.Text;
using UnityEngine;

namespace Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;
        private TcpSocket _tcpSocket=new TcpSocket();
        private UdpSocket _udpSocket=new UdpSocket();
        private int _index = 0;
        
        private void Awake()
        {
            Instance = this;
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
        
        private void Update()
        {
            if(_index<2){
                String s = "TCP-消息" + _index;
                if (TcpSendMessageBool(Encoding.UTF8.GetBytes(s))) _index++;
            }
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
    }
}
