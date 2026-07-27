using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Network.Base;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// Kcp 服务器：监听端口，管理客户端连接
    /// 库为KumoKyaku/kcp
    /// </summary>
    public class KcpServer:IDisposable
    {
        #region 协议规定
        
        /*
         * conv等于外界设置的clientId，必须外界分配clientId再使用
         * 
         * | KCP: conv 4字节  其他 | 传输数据 |
         */

        #endregion
        
        #region 属性

        private UdpClient _udpClient;
        private readonly int _port;
        private bool _isRunning=false;
        private Dictionary<uint, KcpSession> _convToKcp;
        
        public event Action<uint,byte[]> OnMessageReceived;

        public KcpServer(int port = 1975)
        {
            _port = port;
        }

        #endregion
        
        #region 生命周期

        public void Start()
        {
            _convToKcp = new Dictionary<uint, KcpSession>();
            _udpClient = new UdpClient(_port);
            _isRunning = true;
            ReceiveAsync();
        }

        public void Stop()
        {
            _isRunning = false;
            foreach (var conv in _convToKcp)
            {
                conv.Value.Dispose();
            }
            _convToKcp.Clear();
            _convToKcp = null;
            _udpClient.Close();
            _udpClient = null;
        }
        
        public void Dispose()
        {
            Stop();   
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
                    uint conv = BinaryPrimitives.ReadUInt32LittleEndian(res.Buffer);
                    if (!_convToKcp.ContainsKey(conv))
                    {
                        _convToKcp[conv] = new KcpSession(conv, res.RemoteEndPoint);
                        _convToKcp[conv].OnUdpReceive += Send;
                        _convToKcp[conv].OnMessageReceived += Receive;
                    }
                    _convToKcp[conv].Input(res.Buffer);
                }
            }
            catch (Exception e)
            {
                if(_isRunning)Debug.LogError("[Server][KcpServer] 消息接收错误"+e);
            }
        }

        #endregion

        #region KCP对接上层

        //上层消息传入KCP
        public void Send(uint conv,byte[] datagram)
        {
            if (!_convToKcp.TryGetValue(conv, out var value))
            {
                Debug.LogError($"[Server][KcpServer]发送消息错误，未知conv:{conv}");
                return;
            }
            value.Send(datagram);
        }
        
        //KCP消息传给上层
        private void Receive(uint conv, byte[] datagram)
        {
            OnMessageReceived?.Invoke(conv,datagram);
        }

        #endregion

        #endregion
        
        
        #region 客户端管理

        public string GetClientInfo(uint conv)
        {
            if (!_convToKcp.TryGetValue(conv, out var value))
            {
                return "unknown";
            }

            IPEndPoint endPoint = value.endPoint;
            return $"{endPoint.Address}:{endPoint.Port}";
        }
        
        #endregion
    }
}