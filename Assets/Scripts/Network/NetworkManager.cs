using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;
        private IPEndPoint _ipEndPoint;
        [SerializeField] private string _ip="127.0.0.1";
        [SerializeField]private int _port=1975;

        private void Awake()
        {
            Instance = this;
            
            Socket socketTcp=new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(_ip), _port);
            socketTcp.Connect(_ipEndPoint);//这里是客户端，使用connect  服务器处应使用bind
            
        }
        
    }
}
