using System;
using System.Text;
using TMPro;
using UnityEngine;
using Network;

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
            String s = "Tcp-消息"+tcpTimes;
            NetworkManager.Instance.TcpSendMessage(Encoding.UTF8.GetBytes(s));
            tcpTimes++;
        }

        private void UdpSendTest()
        {
            udpMessage.text = udpTimes.ToString();
            String s = "Udp-消息"+udpTimes;
            NetworkManager.Instance.UdpSendMessage(Encoding.UTF8.GetBytes(s));
            udpTimes++;
        }
    }
}