using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class StatusPanel:MonoBehaviour
    {
        public static StatusPanel Instance;
        [SerializeField]private TMP_Text _clientIdStatus;
        [SerializeField]private TMP_Text _playerIdStatus;
        [SerializeField]private TMP_Text _localStatus;
        [SerializeField]private TMP_Text _externalStatus;
        private int _localTime=0;
        private int _externalTime=0;
        
        private void Awake()
        {
            Instance = this;
        }

        public void UpdateClientIdStatus(UInt64 clientId)
        {
            _clientIdStatus.text = $"客户端ID: {clientId}";
        }

        public void UpdatePlayerIdStatus(UInt64 playerId)
        {
            _playerIdStatus.text=$"玩家ID: {playerId}";
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