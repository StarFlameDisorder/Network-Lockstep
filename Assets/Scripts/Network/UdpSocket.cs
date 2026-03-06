using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UI;
using UnityEngine;
using UnityEngine.Events;

namespace Network
{
    public class UdpSocket
    {
        private IPEndPoint _ipEndPoint;
        private Socket _socketUdp;
        
        public void StartLink(string ip, int port)
        {
            try
            {
                if (IsConnected()) CloseLink();
                Debug.Log("初始化UDP客户端" + ip + ":" + (port + 1));
                _ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port + 1);
                _socketUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socketUdp.Connect(_ipEndPoint);
                Send(Encoding.UTF8.GetBytes("UDP-这里是客户端，请求连接"));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
            
            ReceiveAsync(message =>
            {
                Debug.Log(Encoding.UTF8.GetString(message));
                MessagePanel.Instance?.AddMessage(Encoding.UTF8.GetString(message));
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
            if (IsConnected()) _socketUdp.Send(buf);
        }

        private async void ReceiveAsync(UnityAction<byte[]> callback)
        {
            while (IsConnected())
            {
                byte[] buf = new byte[600];
                int originalLength=await _socketUdp.ReceiveAsync(buf, SocketFlags.None);
                Debug.Log($"接收-Socket长度:{originalLength}原始有效字节(十六进制): {BitConverter.ToString(buf, 0, originalLength)}");
                callback?.Invoke(buf);
            }
        }
        
        
        
        public bool IsConnected()
        {
            return _socketUdp!=null&&_socketUdp.Connected;
        }
    }
}