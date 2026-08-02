

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Text;
using Framework;
using Framework.TimerSystem;
using UnityEngine;

namespace Network.Base
{
    /// <summary>
    /// 单个客户端信息和KCP
    /// 库为KumoKyaku/kcp
    /// </summary>
    public class KcpSession:IKcpCallback,IDisposable
    {
        public readonly uint conv;
        public IPEndPoint endPoint=>_endPoint;
        private IPEndPoint _endPoint;
        private SimpleSegManager.Kcp _kcp;
        private bool _isRunning=false;

        private Timer _timer;
        private readonly int _resendIntervalMs;

        /// <summary>
        /// KCP 实例线程安全锁：_kcp 会被主线程 Timer 与后台 UDP 接收线程并发调用，必须串行化。
        /// </summary>
        private readonly object _kcpLock = new();
        
        public event Action<IPEndPoint,byte[]> OnUdpReceive;
        public event Action<uint,byte[]> OnMessageReceived;

        public KcpSession(uint conv, IPEndPoint endPoint, int resendIntervalMs=100)
        {
            this.conv = conv;
            this._endPoint = endPoint;
            _resendIntervalMs = resendIntervalMs;
            
            _kcp=new SimpleSegManager.Kcp(conv,this);

            if (!Global.TryGet(out TimerManager timerManager))
            {
                Debug.LogError("[KcpSession]获取TimerManager错误");
                return;
            }

            //更新KCP时钟
            _timer = timerManager.CreateTimer(0.015f,true);
            _timer.OnComplete += () =>
            {
                lock (_kcpLock)
                {
                    _kcp.Update(DateTimeOffset.UtcNow);
                    TryReceiveInternal();
                }
            };
            _timer.Start();
            
            _isRunning=true;
        }

        public void Dispose()
        {
            _isRunning=false;
            Global.TryGet(out TimerManager timerManager);
            timerManager?.RemoveTimer(_timer);
            _timer=null;
        }

        #region 对接UDP
            
        //KCP消息通过UDP发送
        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            var s = buffer.Memory.Span.Slice(0, avalidLength).ToArray();
            OnUdpReceive?.Invoke(_endPoint,s);
            // Debug.Log($"UDP发送 {Encoding.UTF8.GetString(s)}");
            buffer.Dispose();
        }
            
        //UDP接收的消息交给KCP
        public void Input(byte[] buffer)
        {
            // Debug.Log($"UDP接收 {Encoding.UTF8.GetString(buffer)}");
            lock (_kcpLock)
            {
                _kcp.Input(buffer);
                TryReceiveInternal();
            }
        }
            
        #endregion

        #region 对接上层

        public void Send(byte[] datagram)
        {
            // Debug.Log($"发送 {Encoding.UTF8.GetString(datagram)}");
            lock (_kcpLock)
            {
                _kcp.Send(datagram);
            }
        }

        /// <summary>
        /// 尝试接收 KCP 解包后的完整消息（供外部主动调用，如发送后立即收取回包）
        /// </summary>
        public void TryReceive()
        {
            lock (_kcpLock)
            {
                TryReceiveInternal();
            }
        }

        /// <summary>
        /// 接收内部实现：必须在持有 _kcpLock 时调用
        /// </summary>
        private void TryReceiveInternal()
        {
            try
            {
                while(_isRunning)
                {
                    var (buffer, avalidLength) = _kcp.TryRecv();
                    if (buffer == null) break;

                    var data = buffer.Memory.Slice(0, avalidLength).ToArray();
                    // Debug.Log($"接收 {Encoding.UTF8.GetString(data)}");
                    buffer.Dispose();
                    OnMessageReceived?.Invoke(conv, data);
                }
            }
            catch (Exception e)
            {
                if(_isRunning)Debug.LogError("[KcpSession] 消息接收错误"+e);
            }
        }

        #endregion
    }
}