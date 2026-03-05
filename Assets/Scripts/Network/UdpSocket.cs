using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Network
{
    public class UdpSocket
    {
        private IPEndPoint _ipEndPoint;
        private Socket _socketUdp;
        
        public void StartLink(string ip, int port)
        {
            Debug.Log("初始化UDP服务器"+ip+":"+(port+1));
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port+1);
            _socketUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socketUdp.Connect(_ipEndPoint);
            _socketUdp.Send(Encoding.UTF8.GetBytes("UDP:这里是客户端，请求连接"));
        }
        
        public bool IsConnected()
        {
            return _socketUdp!=null&&_socketUdp.Connected;
        }
    }
}