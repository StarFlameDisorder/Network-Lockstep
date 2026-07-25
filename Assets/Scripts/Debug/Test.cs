using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets.Kcp;
using System.Text;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// KCP 简单通信测试：双向模拟网络
    /// </summary>
    public class Test : MonoBehaviour
    {
        private Kcp<KcpSegment> _peerA;
        private Kcp<KcpSegment> _peerB;
        private readonly List<byte[]> _pendingToB = new();
        private readonly List<byte[]> _pendingToA = new();
        private bool _sent;

        private void Awake()
        {
            // PeerA：发送方
            _peerA = new Kcp<KcpSegment>(
                conv_: 1,
                callback: new KcpCallback((buf, len) =>
                {
                    var data = new byte[len];
                    buf.Memory.Span.Slice(0, len).CopyTo(data);
                    _pendingToB.Add(data);
                    buf.Dispose();
                }),
                rentable: null
            );
            _peerA.NoDelay(1, 20, 2, 1);   // 极速模式

            // PeerB：接收方
            _peerB = new Kcp<KcpSegment>(
                conv_: 1,
                callback: new KcpCallback((buf, len) =>
                {
                    var data = new byte[len];
                    buf.Memory.Span.Slice(0, len).CopyTo(data);
                    _pendingToA.Add(data);
                    buf.Dispose();
                }),
                rentable: null
            );
            _peerB.NoDelay(1, 20, 2, 1);

            // 发送一条消息
            byte[] message = Encoding.UTF8.GetBytes("Hello from KCP!");
            _peerA.Send(message);
            _sent = true;
            Debug.Log($"[Test] PeerA 发送: {Encoding.UTF8.GetString(message)}");
        }

        private void Update()
        {
            if (!_sent) return;

            var now = DateTimeOffset.UtcNow;

            // 1. 驱动两端 KCP 状态机
            _peerA.Update(now);
            _peerB.Update(now);

            // 2. 将 KCP 产出的网络包投递到对端的 Input
            FlushPending();

            // 3. PeerB 尝试接收应用层消息
            TryRecvFromB();
        }

        /// <summary>将缓冲的网络包投递到对端的 Input</summary>
        private void FlushPending()
        {
            foreach (var data in _pendingToB)
                _peerB.Input(data);
            _pendingToB.Clear();

            foreach (var data in _pendingToA)
                _peerA.Input(data);
            _pendingToA.Clear();
        }

        /// <summary>PeerB 尝试取出已组装的消息</summary>
        private void TryRecvFromB()
        {
            var (buffer, len) = _peerB.TryRecv();
            if (len > 0)
            {
                string received = Encoding.UTF8.GetString(buffer.Memory.Span.Slice(0, len));
                buffer.Dispose();
                Debug.Log($"[Test] PeerB 接收: {received}");
                _sent = false; // 完成，不再 Update
            }
        }
    }

    /// <summary>
    /// KCP Output 回调实现：将 KCP 要发送的网络数据包交给外部处理
    /// </summary>
    internal class KcpCallback : IKcpCallback
    {
        private readonly Action<IMemoryOwner<byte>, int> _onOutput;

        public KcpCallback(Action<IMemoryOwner<byte>, int> onOutput)
        {
            _onOutput = onOutput;
        }

        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            _onOutput(buffer, avalidLength);
        }
    }
}
