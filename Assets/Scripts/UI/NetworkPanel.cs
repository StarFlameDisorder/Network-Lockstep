using System;
using System.Text;
using Google.Protobuf;
using TMPro;
using UnityEngine;
using Network;
using SyncMessage;

namespace UI
{
    public class NetworkPanel:MonoBehaviour
    {
        //public ButtonClick closeButton;
        public ButtonClick applyButton;
        public TMP_InputField inputIP;
        public TMP_InputField inputPort;
        public ButtonClick tcpMessageButton;
        public ButtonClick udpMessageButton;
        public TMP_Text tcpMessage;
        public TMP_Text udpMessage;
        private int tcpTimes = 0;
        private int udpTimes = 0;

        void Awake()
        {
            // closeButton.OnClickEvent += () =>
            // {
            //     this.enabled = false;
            // };
            applyButton.OnClickEvent += () =>
            {
                string ip = inputIP.text;
                int port = int.Parse(inputPort.text);
                NetworkManager.Instance.StartLink(ip, port);
            };
            tcpMessageButton.OnClickEvent += () =>
            {
                TcpSendTest();
            };
            udpMessageButton.OnClickEvent += () =>
            {
                UdpSendTest();
            };
        }

        private void TcpSendTest()
        {
            tcpMessage.text = tcpTimes.ToString();
            
            ClientMessage message = new ClientMessage
            {
                ClientId = NetworkManager.Instance.GetClientId(),
                CommonMessage = "Tcp-消息"+tcpTimes
            };
            NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
            
            tcpTimes++;
        }

        private void UdpSendTest()
        {
            udpMessage.text = udpTimes.ToString();
            
            ClientMessage message = new ClientMessage
            {
                ClientId = NetworkManager.Instance.GetClientId(),
                CommonMessage = "Udp-消息"+udpTimes
            };
            NetworkManager.Instance.TcpSendMessage(message.ToByteArray());
            
            udpTimes++;
        }
    }
}