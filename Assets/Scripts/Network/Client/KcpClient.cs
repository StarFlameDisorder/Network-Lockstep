using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Network.Base;
using UnityEngine;

namespace Network.Client
{
    public class KcpClient:IDisposable
    {
        #region 属性

        private UdpClient _udpClient;
        private IPEndPoint _ipEndPoint;
        private uint _conv;
        private bool _isRunning=false;
        private KcpSession _kcpSession;
        
        /// <summary>KCP 是否正在运行</summary>
        public bool IsConnected => _isRunning;
        
        public event Action<uint,byte[]> OnMessageReceived;

        #endregion
        
        #region 生命周期

        public void StartLink(string ip,int port,uint conv)
        {
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _conv = conv;

            _udpClient = new UdpClient();

            _kcpSession = new KcpSession(conv, _ipEndPoint);
            
            _kcpSession.OnUdpReceive += Send;
            _kcpSession.OnMessageReceived += Receive;
            
            _isRunning = true;
            ReceiveAsync();
        }

        public void StopLink()
        {
            _isRunning = false;
            
            _kcpSession.Dispose();
            _kcpSession = null;
            
            _udpClient.Close();
            _udpClient = null;
        }
        
        public void Dispose()
        {
            StopLink();   
        }

        #endregion
        
        #region 消息收发

        #region UDP对接KCP
    
        //KCP消息通过UDP发送
        private void Send(IPEndPoint endPoint,byte[] datagram)
        {
            _udpClient.Send(datagram,datagram.Length,endPoint);
        }
        
        //UDP接收的消息交给KCP
        private async void ReceiveAsync()
        {
            try
            {
                while (_isRunning)
                {
                    var res=await _udpClient.ReceiveAsync();

                    // StopLink 后 _kcpSession 可能已置空，旧接收循环需退出
                    if (_kcpSession == null) break;
                    _kcpSession.Input(res.Buffer);
                }
            }
            catch (Exception e)
            {
                if(_isRunning)Debug.LogError("[Client][KcpClient] 消息接收错误"+e);
            }
        }

        #endregion

        #region KCP对接上层

        //上层消息传入KCP
        public void Send(byte[] datagram)
        {
            _kcpSession.Send(datagram);
        }
        
        //KCP消息传给上层
        private void Receive(uint conv, byte[] datagram)
        {
            OnMessageReceived?.Invoke(conv,datagram);
        }

        #endregion

        #endregion
    }
}