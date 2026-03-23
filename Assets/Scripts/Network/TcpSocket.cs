using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Google.Protobuf;
using UI;
using UnityEngine;
using UnityEngine.Events;
using SyncMessage;
using ConnectMessage;

namespace Network
{
    public class TcpSocket
    {
        private IPEndPoint _ipEndPoint;
        [SerializeField] private string _ip="127.0.0.1";
        [SerializeField]private int _port=1975;
        private UInt64  _clientId=0; 
        
        private List<byte> _tcpMessageBuffer=new List<byte>();
        private Socket _socketTcp;

        public TcpSocket()
        {
        }
        
        public void StartLink(string ip, int port)
        {
            try
            {
                if(IsConnected())CloseLink();
                Debug.Log("初始化TCP客户端"+ip+":"+port);
                _socketTcp=new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                _socketTcp.Connect(_ipEndPoint);//这里是客户端，使用connect  服务器处应使用bind
                // ClientMessage message = new ClientMessage
                // {
                //     ClientId = 0,
                //     CommonMessage = "TCP-这是客户端，建立连接"
                // };
                // Send(message.ToByteArray());
            }
            catch (SocketException e)
            {
                Debug.LogError(e);
                throw;
            }
            
            ReceiveAsync((message) =>
            {
                Debug.Log("Tcp:收到消息");
                NetworkManager.Instance.HandleMessage(message);
                // Debug.Log(Encoding.UTF8.GetString(message));
                // MessagePanel.Instance?.AddMessage(Encoding.UTF8.GetString(message));
            });
        }

        public void CloseLink()
        {
            _socketTcp.Shutdown(SocketShutdown.Both);
            _socketTcp.Close();
            _socketTcp = null;
        }
        
        
        public void Send(byte[] buf)
        {
            if(_socketTcp.Connected)
            {
                int length = buf.Length;
                //长度为4字节
                byte[] headBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
                byte[] sendBuf = new byte[buf.Length + headBytes.Length];
                Buffer.BlockCopy(headBytes, 0, sendBuf, 0, headBytes.Length); //快速复制数据
                Buffer.BlockCopy(buf, 0, sendBuf, headBytes.Length, buf.Length);
                _socketTcp.Send(sendBuf);
                Debug.Log($"发送-长度:{length}原始有效字节(十六进制): {BitConverter.ToString(buf, 0, length)}");//有效载荷长度
            }
        }
        
        private async void ReceiveAsync(UnityAction<byte[]> callback)
        {
            try
            {
                while (_socketTcp.Connected)
                {
                    byte[] buf = new byte[2048];
                    int originalLength = await _socketTcp.ReceiveAsync(buf, SocketFlags.None);
                    Debug.Log($"接收-Socket长度:{originalLength}");
                    _tcpMessageBuffer.AddRange(buf.Take(originalLength));
                    while (_tcpMessageBuffer.Count >= 4)
                    {
                        byte[] headBytes = new byte[4];
                        _tcpMessageBuffer.CopyTo(0, headBytes, 0, 4);
                        int tmpLen = BitConverter.ToInt32(headBytes, 0);
                        int length = IPAddress.NetworkToHostOrder(tmpLen);
                        if (_tcpMessageBuffer.Count >= length + 4)
                        {
                            byte[] message = new byte[length];
                            _tcpMessageBuffer.CopyTo(4, message, 0, length);
                            Debug.Log(
                                $"接收-长度:{length}原始有效字节(十六进制): {BitConverter.ToString(message, 0, length)}"); //有效载荷长度
                            _tcpMessageBuffer.RemoveRange(0, length + 4);
                            callback?.Invoke(message);
                        }
                        else break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("消息接收错误"+e);
            }
        }

        private void OnDestroy()
        {
            _socketTcp?.Close();
        }

        public bool IsConnected()
        {
            return _socketTcp!=null && _socketTcp.Connected;
        }
        
        public void BindClientId(UInt64 clientId)
        {
            _clientId=clientId;
            Debug.Log("Tcp:服务器分配id:"+clientId);
            ClientMessage message = new ClientMessage
            {
                ClientId = clientId,
                CommonMessage = "TCP-这是客户端，建立连接"
            };
            Send(message.ToByteArray());
        }
    }
}