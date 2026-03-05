using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class MessagePanel:MonoBehaviour
    {
        public static MessagePanel Instance;
        [SerializeField] private GameObject subMessagePanel;
        [SerializeField] private TMP_Text _infoText;
        
        void Awake()
        {
            Instance = this;
            AddMessage("Test");
        }

        public void AddMessage(string msg)
        {
            GameObject go = Instantiate(subMessagePanel,transform);
            go.GetComponent<SubMessagePanel>().setText("["+DateTime.Now.ToString("HH:mm:ss")+"]"+msg);
        }

        public void SetMessageInfo(string msg)
        {
            _infoText.text = msg;
        }
    }
}