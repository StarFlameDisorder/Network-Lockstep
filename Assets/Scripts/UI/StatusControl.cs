using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class StatusControl:MonoBehaviour
    {
        public static StatusControl Instance;
        [SerializeField]private TMP_Text _localStatus;
        [SerializeField]private TMP_Text _externalStatus;
        private int _localTime=0;
        private int _externalTime=0;
        
        private void Awake()
        {
            Instance = this;
        }

        public void UpdateLocalStatus(int remainingFrame)
        {
            _localTime++;
            _localStatus.text = $"本地剩余: {remainingFrame}\n判断{_localTime}";
        }

        public void UpdateExternalStatus(int remainingFrame)
        {
            _externalTime++;
            _externalStatus.text=$"外部剩余: {remainingFrame}\n判断{_externalTime}";
        }
    }
}