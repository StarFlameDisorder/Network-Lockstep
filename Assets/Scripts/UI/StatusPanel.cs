using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class StatusPanel:MonoBehaviour
    {
        public static StatusPanel Instance;
        [SerializeField]private TMP_Text _clientIdStatus;
        [SerializeField]private TMP_Text _localOffset;
        [SerializeField]private TMP_Text _externalOffset;
        [SerializeField]private TMP_Text _localStatus;
        [SerializeField]private TMP_Text _externalStatus;
        private int _localTime=0;
        private int _externalTime=0;
        private Vector3 _localSum;
        private Vector3 _externalSum;
        
        private void Awake()
        {
            Instance = this;
        }

        public void UpdateClientIdStatus(UInt64 clientId)
        {
            _clientIdStatus.text = $"客户端ID: {clientId}";
        }

        public void UpdateLocalOffset(Vector3 mov)
        {
            _localSum += mov;
            _localOffset.text = $"本地:{_localSum}";
        }
        
        public void UpdateExternalOffset(Vector3 mov)
        {
            _externalSum+=mov;
            _externalOffset.text=$"外部:{_externalSum}";
            
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