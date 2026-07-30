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
        private KcpClient _kcpClient=new KcpClient();
        private int _index = 0;
        private uint _clientId = 0;
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
            _kcpClient.Dispose();
        }

        #endregion
        
        public void StartLink(string ip, int port)
        {
            _ip = ip;
            _port = port;
            _tcpSocket.StartLink(ip, port);
            // KCP 需要 clientId（conv），在 SetClientId 中延迟启动
        }

        public void StopLink()
        {
            if(TcpIsConnected())_tcpSocket.CloseLink();
            if(KcpIsConnected())_kcpClient.StopLink();
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
        
        public bool KcpIsConnected()
        {
            return _kcpClient != null && _kcpClient.IsConnected;
        }

        public void KcpSendMessage(byte[] data)
        {
            if(KcpIsConnected())_kcpClient.Send(data);
        }

        public void HandleMessage(byte[] data)
        {
            _messageDispatcher.HandleMessage(data);
        }

        public void SetClientId(uint clientId)
        {
            Debug.Log($"[Client][NetworkManager] SetClientId {clientId}");
            // if (StatusPanel.Instance != null)
            //     StatusPanel.Instance.UpdateClientIdStatus(clientId);
            _clientId = clientId;
            _tcpSocket.BindClientId(clientId);
            
            // KCP 延迟启动：拿到 clientId 作为 conv 后才建立 KCP 连接
            _kcpClient.OnMessageReceived += (conv, data) => HandleMessage(data);
            _kcpClient.StartLink(_ip, _port, clientId);
        }

        public uint GetClientId()
        {
            return _clientId;
        }
        
    }
}
