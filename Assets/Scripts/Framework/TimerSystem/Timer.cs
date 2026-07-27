using UnityEngine;
using System;

namespace Framework
{
    /// <summary>
    /// 计时器
    /// autoReset时，如果设置的触发时间过短可能会多次触发
    /// </summary>
    public class Timer
    {
        private float _duration;
        private float _elapsedTime;
        private bool _isRunning;
        private bool _isPaused;
        private bool _autoReset;

        #region 事件
        public event Action OnComplete;
        public event Action<float> OnUpdate;
        #endregion
        
        /// <summary>
        /// 总时间
        /// </summary>
        public float Duration => _duration;
        
        /// <summary>
        /// 已经经过的时间
        /// </summary>
        public float ElapsedTime => _elapsedTime;
        
        /// <summary>
        /// 剩余时间
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, _duration - _elapsedTime);
        
        /// <summary>
        /// 时间进度
        /// </summary>
        public float Progress => Mathf.Clamp01(_elapsedTime / _duration);
        
        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;

        public void Init(float duration,bool autoReset, Action onComplete = null, Action<float> onUpdate = null)
        {
            _duration = duration;
            OnComplete = onComplete;
            OnUpdate = onUpdate;
            _elapsedTime = 0f;
            _isRunning = false;
            _isPaused = false;
            _autoReset = autoReset;
        }

        /// <summary>
        /// 开始计时，若之前已被暂停则将恢复之前的进度继续运行
        /// </summary>
        public void Start()
        {
            _isRunning = true;
            _isPaused = false;
        }

        /// <summary>
        /// 停止计时，并重置计时器进度
        /// </summary>
        public void Stop()
        {
            _elapsedTime = 0f;
            _isRunning = false;
            _isPaused = false;
        }

        /// <summary>
        /// 暂停计时器
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>
        /// 重置计时器，该操作不会影响计时器的状态，只会重置当前的时间进度
        /// </summary>
        public void Reset()
        {
            _elapsedTime = 0f;
        }

        public void Update(float deltaTime)
        {
            if (!_isRunning || _isPaused) return;

            _elapsedTime += deltaTime;
            OnUpdate?.Invoke(Progress);

            while (_elapsedTime >= _duration)
            {
                OnComplete?.Invoke();
                if (_autoReset)
                {
                    _elapsedTime -= _duration;
                }
                else _isRunning = false;
            }
        }

        /// <summary>
        /// 清理计时器状态，清空回调引用
        /// </summary>
        public void Dispose()
        {
            OnComplete = null;
            OnUpdate = null;
            _elapsedTime = 0f;
            _isRunning = false;
            _isPaused = false;
        }
    }
}