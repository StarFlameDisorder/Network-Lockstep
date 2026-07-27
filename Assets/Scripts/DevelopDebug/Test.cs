using System;
using System.Text;
using Network.Client;
using Network.Server;
using UnityEngine;

namespace DevelopDebug
{
    public class Test : MonoBehaviour
    {
        private KcpServer _kcpServer;
        private KcpClient _kcpClient1;
        private KcpClient _kcpClient2;
        
        private void Start()
        {
            Debug.Log("Test Kcp");
            _kcpServer = new KcpServer(2221);
            _kcpServer.Start();
            _kcpServer.OnMessageReceived += ServerReceiveMessage;
            
            _kcpClient1 = new KcpClient();
            _kcpClient1.StartLink("127.0.0.1", 2221,1);
            _kcpClient1.OnMessageReceived += Client1ReceiveMessage;
            _kcpClient1.Send(Encoding.UTF8.GetBytes("1 Hello Kcp"));
            
            _kcpClient2 = new KcpClient();
            _kcpClient2.StartLink("127.0.0.1", 2221,2);
            _kcpClient2.OnMessageReceived += Client2ReceiveMessage;
            _kcpClient2.Send(Encoding.UTF8.GetBytes("2 Hello Kcp"));
            
        }

        void Client1ReceiveMessage(uint conv, byte[] data)
        {
            string s= Encoding.UTF8.GetString(data);
            Debug.Log($"[Client]conv:{conv} data:{s}");
            _kcpClient1.Send(Encoding.UTF8.GetBytes(s+"c1"));
        }

        void Client2ReceiveMessage(uint conv, byte[] data)
        {
            string s= Encoding.UTF8.GetString(data);
            Debug.Log($"[Client]conv:{conv} data:{s}");
            _kcpClient2.Send(Encoding.UTF8.GetBytes(s+"c2"));
        }

        void ServerReceiveMessage(uint conv, byte[] data)
        {
            string s= Encoding.UTF8.GetString(data);
            Debug.Log($"[Server]conv:{conv} data:{s}");
            _kcpServer.Send(conv,Encoding.UTF8.GetBytes(s+"s"));
        }

        private void OnDestroy()
        {
            _kcpServer.Dispose();
            _kcpClient1.Dispose();
            _kcpClient2.Dispose();
        }
    }
    
}

