using System;
using ConnectMessage;
using Framework;
using Google.Protobuf;
using UnityEngine;

namespace Network.Client
{
    public class GameClient : SubSystemBase
    {
        public override SubSystemPriority Priority => SubSystemPriority.GameClient;
        
        // public static GameClient Instance;
        private TcpSocket _tcpSocket=new TcpSocket();
        private UdpSocket _udpSocket=new UdpSocket();
        private int _index = 0;
        private UInt64 _clientId = 0;
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

        #region 生命周期

        public override void Init()
        {
            RegisterHandler(Signals.ConnectHandShake, (HandShakeResponse msg) =>
            {
                SetClientId(msg.ClientId);
            });
        }

        public override void Destroy()
        {
            _tcpSocket.Dispose();
            _udpSocket.Dispose();
        }

        #endregion
        
        public void StartLink(string ip, int port)
        {
            _ip = ip;
            _port = port;
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

        public bool TcpSendMessage(byte[] data)
        {
            if(TcpIsConnected())_tcpSocket.Send(data);
            return TcpIsConnected();
            //else Debug.Log("TcpSendMessage:未建立连接");
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
            Debug.Log($"[Client][NetworkManager] SetClientId {clientId}");
            // if (StatusPanel.Instance != null)
            //     StatusPanel.Instance.UpdateClientIdStatus(clientId);
            _clientId = clientId;
            _tcpSocket.BindClientId(clientId);
            _udpSocket.BindClientId(clientId);
        }

        public UInt64 GetClientId()
        {
            return _clientId;
        }
        
    }
}
