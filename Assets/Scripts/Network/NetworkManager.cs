using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;
        private IPEndPoint _ipEndPoint;
        [SerializeField] private string _ip="127.0.0.1";
        [SerializeField]private int _port=1975;

        private Socket _socketTcp;
        private int _index = 0;
        private void Awake()
        {
            Instance = this;
            
            _socketTcp=new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(_ip), _port);
            _socketTcp.Connect(_ipEndPoint);//这里是客户端，使用connect  服务器处应使用bind
            String str = "这是客户端，请求连接";
            Send(Encoding.UTF8.GetBytes(str));
            byte[] buf=new byte[1024];
            _socketTcp.Receive(buf);
            Debug.Log(Encoding.UTF8.GetString(buf));
        }
        private void Update()
        {
            if(_index<100)
            {
                String s = "消息" + _index;
                Send(Encoding.UTF8.GetBytes(s));
                byte[] buf = new byte[1024];
                _socketTcp.Receive(buf);
                Debug.Log(Encoding.UTF8.GetString(buf));
                _index++;
            }
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
            }
        }

        public void Receive()
        {
            
        }

        private void OnDestroy()
        {
            _socketTcp.Close();
        }
    }
}
