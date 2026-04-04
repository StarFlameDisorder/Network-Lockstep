using System;
using System.Collections;
using GamePlay;
using UnityEngine;

namespace Network
{
    
    public class TimerHandle
    {
        public event Action OnTimeTriggerEvent;
        private Coroutine _syncCoroutine;
        private bool _isRunning = true;
        private int _refreshRate;
        
        public TimerHandle(int refreshRate)
        {
            _refreshRate = refreshRate;
        }
        
        private IEnumerator TimerCoroutine()
        {
            while (true)
            {
                if (_isRunning)
                {
                    OnTimeTriggerEvent?.Invoke();
                }
                yield return new WaitForSeconds(1f/_refreshRate); // 固定间隔时间
            }
        }
        
        public void StartTimer()
        {
            if(_syncCoroutine==null)_syncCoroutine=GameSync.Instance.StartCoroutine(TimerCoroutine());
            _isRunning = true;
        }

        public void StopTimer()
        {
            _isRunning = false;
        }
        
        public void Destroy()
        {
            if(_syncCoroutine!=null)GameSync.Instance.StopCoroutine(_syncCoroutine);
            _syncCoroutine = null;
        }
    }
}