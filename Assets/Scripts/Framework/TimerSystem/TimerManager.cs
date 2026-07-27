using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Framework.TimerSystem
{
    public class TimerManager:SubSystemBase
    {
        public override SubSystemPriority Priority => SubSystemPriority.TimerManager;
        
        private List<Timer> _timers = new List<Timer>();

        /// <summary>
        /// <para>创建新的计时器并开始计时</para>
        /// </summary>
        /// <param name="duration">时间间隔</param>
        /// <param name="autoReset">自动重置</param>
        /// <param name="onComplete">计时完成回调函数</param>
        /// <param name="onUpdate">更新回调函数</param>
        /// <returns>计时器对象，需要保存以供移除</returns>
        public Timer CreateTimer(float duration,bool autoReset, Action onComplete = null, Action<float> onUpdate = null)
        {
            Timer t = new Timer();
            t.Init(duration, autoReset, onComplete, onUpdate);
            _timers.Add(t);
            return t;
        }
        
        /// <summary>
        /// 移除计时器对象，归还对象池，适用于中断计时器操作
        /// </summary>
        public void RemoveTimer(Timer t)
        {
            _timers.Remove(t);
            t.Dispose();
        }
        
        /// <summary>
        /// 清空所有计时器，全部归还对象池
        /// </summary>
        public void ClearAllTimers()
        {
            foreach (var timer in _timers)
            {
                timer.Dispose();
            }
            _timers.Clear();
        }

        #region 生命周期

        public override void Update(float deltaTime)
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                Timer timer = _timers[i];
                timer.Update(deltaTime);
            }
        }

        public override void Destroy()
        {
            ClearAllTimers();
        }

        #endregion
    }
}