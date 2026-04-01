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
        private Int64 _index=0;
        
        public void StartLink(string ip, int port)
        {
            try
            {
                if (IsConnected()) CloseLink();
                Debug.Log("初始化UDP客户端" + ip + ":" + (port + 1));
                _ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port + 1);
                _socketUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socketUdp.Connect(_ipEndPoint);
            }
            catch (SocketException e)
            {
                Debug.LogError(e);
                throw;
            }
            
            ReceiveAsync(message =>
            {
                // Debug.Log("Udp:收到消息");
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
                byte[] headerBuf = Encoding.ASCII.GetBytes("SEQ");
                int length = buf.Length;
                byte[] indexBytes=BitConverter.GetBytes(IPAddress.HostToNetworkOrder(_index));
                byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
                if(length>548)Debug.LogWarning("UDP包过长，可能出现分包！");
                
                byte[] sendBuf = new byte[buf.Length +indexBytes.Length +lengthBytes.Length+headerBuf.Length];
                
                int offset=0;
                Buffer.BlockCopy(headerBuf, 0, sendBuf, 0, headerBuf.Length);//类型
                
                offset+=headerBuf.Length;
                Buffer.BlockCopy(indexBytes, 0, sendBuf, offset, indexBytes.Length);//序号

                offset += indexBytes.Length;
                Buffer.BlockCopy(lengthBytes,0,sendBuf,offset,lengthBytes.Length);//长度
                
                offset+= lengthBytes.Length;
                Buffer.BlockCopy(buf, 0, sendBuf, offset, buf.Length);//数据
                
                
                Debug.Log($"发送-序号{_index}-长度:{length}原始有效字节(十六进制): {BitConverter.ToString(buf, 0, length)}");//有效载荷长度
                _socketUdp.Send(sendBuf);
                _index++;
            }
        }

        private async void ReceiveAsync(UnityAction<byte[]> callback)
        {
            while (IsConnected())
            {
                byte[] buf = new byte[600];
                int originalLength=await _socketUdp.ReceiveAsync(buf, SocketFlags.None);
                
                string t=Encoding.UTF8.GetString(buf, 0, 3);
                if (t == "SEQ")
                {

                    Int64 index= IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buf, 3)); 
                    int length=IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 11)); 
                    Debug.Log($"接收-长度{length}序号{index}");
                    byte[] actualData = new byte[length];
                    Array.Copy(buf, 15, actualData, 0, length);
                    Debug.Log($"接收-Socket长度:{originalLength}原始有效字节(十六进制): {BitConverter.ToString(actualData, 0, length)}");
                    callback?.Invoke(actualData);
                }
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
        
        public void Destroy()
        {
            _socketUdp?.Close();
        }
    }
}