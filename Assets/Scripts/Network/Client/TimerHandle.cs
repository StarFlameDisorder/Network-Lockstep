using System;
using System.Collections;
using UnityEngine;

namespace Network.Client
{
    
    public class TimerHandle
    {
        public event Action OnTimeTriggerEvent;
        private Coroutine _syncCoroutine;
        private bool _isRunning = true;
        private int _refreshRate;
        private MonoBehaviour _owner;
        
        /// <param name="owner">用于启动/停止协程的 MonoBehaviour，null 时回退到 GameCore.Instance</param>
        public TimerHandle(int refreshRate, MonoBehaviour owner = null)
        {
            _refreshRate = refreshRate;
            _owner = owner;
        }
        
        private MonoBehaviour GetOwner()
        {
            if (_owner != null) return _owner;
            return Core.GameCore.Instance;
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
            if(_syncCoroutine==null)_syncCoroutine=GetOwner().StartCoroutine(TimerCoroutine());
            _isRunning = true;
        }

        public void StopTimer()
        {
            _isRunning = false;
        }
        
        public void Destroy()
        {
            if(_syncCoroutine!=null)GetOwner().StopCoroutine(_syncCoroutine);
            _syncCoroutine = null;
        }
    }
}