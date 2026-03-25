using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UI;
using UnityEngine;
using UnityEngine.Events;
using SyncMessage;
using ConnectMessage;
using Google.Protobuf;

namespace Network
{
    public class UdpSocket
    {
        private IPEndPoint _ipEndPoint;
        private Socket _socketUdp;
        private UInt64  _clientId=0; 
        
        public void StartLink(string ip, int port)
        {
            try
            {
                if (IsConnected()) CloseLink();
                Debug.Log("初始化UDP客户端" + ip + ":" + (port + 1));
                _ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port + 1);
                _socketUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socketUdp.Connect(_ipEndPoint);
                //Send(Encoding.UTF8.GetBytes("UDP-这里是客户端，请求连接"));
            }
            catch (SocketException e)
            {
                Debug.LogError(e);
                throw;
            }
            
            ReceiveAsync(message =>
            {
                //Debug.Log("Udp:收到消息");
                NetworkManager.Instance.HandleMessage(message);
                // Debug.Log(Encoding.UTF8.GetString(message));
                //MessagePanel.Instance?.AddMessage("Udp:收到消息");
            });
        }

        public void CloseLink()
        {
            _socketUdp.Shutdown(SocketShutdown.Both);
            _socketUdp.Close();
            _socketUdp = null;
        }

        public void Send(byte[] buf)
        {
            if (IsConnected())
            {
                int length = buf.Length;
                //Debug.Log($"发送-长度:{length}原始有效字节(十六进制): {BitConverter.ToString(buf, 0, length)}");//有效载荷长度
                _socketUdp.Send(buf);
            }
        }

        private async void ReceiveAsync(UnityAction<byte[]> callback)
        {
            while (IsConnected())
            {
                byte[] buf = new byte[600];
                int originalLength=await _socketUdp.ReceiveAsync(buf, SocketFlags.None);
                byte[] actualData = new byte[originalLength];
                Array.Copy(buf, 0, actualData, 0, originalLength);
                //Debug.Log($"接收-Socket长度:{originalLength}原始有效字节(十六进制): {BitConverter.ToString(actualData, 0, originalLength)}");
                callback?.Invoke(actualData);
            }
        }
         
        public bool IsConnected()
        {
            return _socketUdp!=null&&_socketUdp.Connected;
        }

        public void bindClientId(UInt64 clientId)
        {
            _clientId=clientId;
            Debug.Log("Udp:服务器分配id:"+clientId);
            ClientMessage message = new ClientMessage
            {
                ClientId = _clientId,
                CommonMessage = "UDP-这是客户端，绑定id"
            };
            Send(message.ToByteArray());
        }
    }
}