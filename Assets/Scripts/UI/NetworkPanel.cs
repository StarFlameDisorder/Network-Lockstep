using TMPro;
using UnityEngine;
using Network;

namespace UI
{
    public class NetworkPanel:MonoBehaviour
    {
        public ButtonClick closeButton;
        public ButtonClick applyButton;
        public TMP_InputField inputIP;
        public TMP_InputField inputPort;

        void Awake()
        {
            closeButton.OnClickEvent += () =>
            {
                this.enabled = false;
            };
            applyButton.OnClickEvent += () =>
            {
                string ip = inputIP.text;
                int port = int.Parse(inputPort.text);
                NetworkManager.Instance.StartLink(ip, port);
            };
        }
    }
}