using System;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 弃用
    /// </summary>
    public class MessagePanel:MonoBehaviour
    {
        public static MessagePanel Instance;
        [SerializeField] private GameObject _subMessagePanel;
        [SerializeField] private TMP_Text _infoText;
        
        void Awake()
        {
            Instance = this;
            AddMessage("Test");
        }

        public void AddMessage(string msg)
        {
            GameObject go = Instantiate(_subMessagePanel,transform);
            go.GetComponent<SubMessagePanel>().setText("["+DateTime.Now.ToString("HH:mm:ss")+"]"+msg);
        }

        public void SetMessageInfo(string msg)
        {
            _infoText.text = msg;
        }
    }
}