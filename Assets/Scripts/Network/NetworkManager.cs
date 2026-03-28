using System;
using System.Text;
using ConnectMessage;
using GamePlay;
using Google.Protobuf;
using TMPro;
using UI;
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

        public void RegisterHandler<T>(Signals signal, Action<T> handler)where T:IMessage//,new()
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
            NetworkManager.Instance.RegisterHandler(Signals.ConnectHandShake, (HandShakeResponse msg) =>
            {
                SetClientId(msg.ClientId);
            });
        }
        
        public void StartLink(string ip, int port)
        {
            _tcpSocket.StartLink(ip, port);
            _udpSocket.StartLink(ip, port);
        }

        public void StopLink()
        {
            if(TcpIsConnected())_tcpSocket.CloseLink();
            if(UdpIsConnected())_udpSocket.CloseLink();
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
            _messageDispatcher.HandleMessage(data);
        }

        public void SetClientId(UInt64 clientId)
        {
            Debug.Log($"SetClientId {clientId}");
            StatusPanel.Instance.UpdateClientIdStatus(clientId);
            this.clientId = clientId;
            _tcpSocket.BindClientId(clientId);
            _udpSocket.bindClientId(clientId);
            if(GameSync.Instance.GetStatus()==GameStatus.Notstarted)GameSync.Instance.StartGame();
        }

        public UInt64 GetClientId()
        {
            return clientId;
        }

        private void OnDestroy()
        {
            _tcpSocket.Destroy();
            _udpSocket.Destroy();
        }
    }
}
