using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// 待确认的 UDP 包
    /// </summary>
    internal class PendingPacket
    {
        public long Index;
        public byte[] SendData;
        public long PreviousTime;
        public int Times;
        public bool IsAck;
    }

    /// <summary>
    /// UDP 服务器：可靠 UDP（SEQ/ACK + 指数退避重传 + 接收重排序）
    /// 移植自 C++/Qt UdpServer
    /// </summary>
    public class UdpServer : IDisposable
    {
        #region 属性

        private UdpClient _socket;
        private readonly int _port;
        private bool _isRunning;

        // 客户端端点标识
        private readonly Dictionary<IPEndPoint, long> _udpIndex = new();                     // 发送序号
        private readonly Dictionary<IPEndPoint, Dictionary<long, PendingPacket>> _pendingPackets = new(); // 发送缓存
        private readonly Dictionary<IPEndPoint, SortedDictionary<long, byte[]>> _receiveBuf = new();     // 接收缓存（排序）
        private readonly Dictionary<IPEndPoint, long> _invokeIndex = new();                  // 下一个应投递的序号
        private readonly object _lock = new();

        private System.Timers.Timer _resendTimer;
        private readonly float _resendIntervalMs;
        private const int MAX_RETRIES = 3;
        private const int BASE_DELAY_MS = 1000;
        private readonly int _maxPacketBuffer;

        public event Action<IPEndPoint, byte[]> OnMessageReceived;

        #endregion

        #region 生命周期

        public UdpServer(int port = 1975, float resendIntervalSec = 0.5f, int maxPacketBuffer = 600)
        {
            _port = port;
            _resendIntervalMs = resendIntervalSec * 1000f;
            _maxPacketBuffer = maxPacketBuffer;
        }

        public void Start()
        {
            _socket = new UdpClient(_port);
            _isRunning = true;
            Debug.Log($"[Server][UdpServer] 启动 UDP 服务器，端口：{_port}");

            // 启动重传定时器
            _resendTimer = new Timer(_resendIntervalMs);
            _resendTimer.Elapsed += (_, __) => CheckAndResend();
            _resendTimer.AutoReset = true;
            _resendTimer.Start();

            ReceiveLoop();
        }

        public void Stop()
        {
            _isRunning = false;
            _resendTimer?.Stop();
            _resendTimer?.Dispose();
            _resendTimer = null;

            try { _socket?.Close(); }
            catch { /* ignore */ }

            lock (_lock)
            {
                _udpIndex.Clear();
                _pendingPackets.Clear();
                _receiveBuf.Clear();
                _invokeIndex.Clear();
            }

            Debug.Log("[Server][UdpServer] UDP 服务器已停止");
        }

        public void Dispose()
        {
            Stop();
        }

        #endregion

        #region 消息收发

        private async void ReceiveLoop()
        {
            while (_isRunning)
            {
                try
                {
                    UdpReceiveResult result = await _socket.ReceiveAsync();
                    ProcessReceived(result.Buffer, result.RemoteEndPoint);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex) when (_isRunning)
                {
                    Debug.LogError($"[Server][UdpServer] 接收异常：{ex}");
                }
            }
        }

        private void ProcessReceived(byte[] data, IPEndPoint remoteEp)
        {
            if (data.Length < 15) return; // 最小帧：3(header) + 8(index) + 4(length) = 15

            string header = Encoding.UTF8.GetString(data, 0, 3);
            long index = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(data, 3));

            lock (_lock)
            {
                if (header == "SEQ")
                {
                    int msgLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 11));
                    if (msgLen <= 0 || msgLen > data.Length - 15) return;

                    byte[] message = new byte[msgLen];
                    Array.Copy(data, 15, message, 0, msgLen);

                    SendAck(remoteEp, index);

                    if (!_receiveBuf.TryGetValue(remoteEp, out var buf))
                    {
                        buf = new SortedDictionary<long, byte[]>();
                        _receiveBuf[remoteEp] = buf;
                        _invokeIndex[remoteEp] = 0;
                    }

                    if (index >= _invokeIndex[remoteEp])
                    {
                        buf[index] = message; // SortedDictionary 自动排序
                    }
                    else
                    {
                        Debug.LogWarning($"[Server][UdpServer] 收到旧包 {GetEndpointInfo(remoteEp)} index={index} invokeIndex={_invokeIndex[remoteEp]}");
                    }

                    // 按序投递
                    while (buf.TryGetValue(_invokeIndex[remoteEp], out byte[] msg))
                    {
                        OnMessageReceived?.Invoke(remoteEp, msg);
                        buf.Remove(_invokeIndex[remoteEp]);
                        _invokeIndex[remoteEp]++;
                    }

                    // 缓冲区过载清理
                    while (buf.Count > _maxPacketBuffer)
                    {
                        long firstKey = _invokeIndex[remoteEp];
                        buf.Remove(firstKey);
                        _invokeIndex[remoteEp]++;
                        Debug.LogWarning($"[Server][UdpServer] UDP 缓冲区过载 {GetEndpointInfo(remoteEp)} 跳过 index={firstKey}");
                    }
                }
                else if (header == "ACK")
                {
                    if (_pendingPackets.TryGetValue(remoteEp, out var packets) && packets.TryGetValue(index, out var pkt))
                    {
                        pkt.IsAck = true;
                    }
                }
            }
        }

        private void SendAck(IPEndPoint remoteEp, long index)
        {
            byte[] header = Encoding.UTF8.GetBytes("ACK");
            byte[] indexBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(index));
            byte[] sendBuf = new byte[header.Length + indexBytes.Length];
            Buffer.BlockCopy(header, 0, sendBuf, 0, header.Length);
            Buffer.BlockCopy(indexBytes, 0, sendBuf, header.Length, indexBytes.Length);

            try { _socket.Send(sendBuf, sendBuf.Length, remoteEp); }
            catch { /* ignore */ }
        }

        public void Send(IPEndPoint remoteEp, byte[] data)
        {
            lock (_lock)
            {
                if (!_udpIndex.TryGetValue(remoteEp, out long index))
                {
                    _udpIndex[remoteEp] = 0;
                    _pendingPackets[remoteEp] = new Dictionary<long, PendingPacket>();
                    index = 0;
                }

                int msgLen = data.Length;

                byte[] header = Encoding.UTF8.GetBytes("SEQ");
                byte[] indexBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(index));
                byte[] lenBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(msgLen));

                byte[] sendBuf = new byte[3 + 8 + 4 + msgLen];
                int offset = 0;
                Buffer.BlockCopy(header, 0, sendBuf, 0, header.Length);
                offset += header.Length;
                Buffer.BlockCopy(indexBytes, 0, sendBuf, offset, indexBytes.Length);
                offset += indexBytes.Length;
                Buffer.BlockCopy(lenBytes, 0, sendBuf, offset, lenBytes.Length);
                offset += lenBytes.Length;
                Buffer.BlockCopy(data, 0, sendBuf, offset, msgLen);

                try { _socket.Send(sendBuf, sendBuf.Length, remoteEp); }
                catch (Exception ex)
                {
                    Debug.LogError($"[Server][UdpServer] 发送失败：{ex.Message}");
                    return;
                }

                _pendingPackets[remoteEp][index] = new PendingPacket
                {
                    Index = index,
                    SendData = sendBuf,
                    PreviousTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Times = 0,
                    IsAck = false
                };

                _udpIndex[remoteEp] = index + 1;
            }
        }

        private void CheckAndResend()
        {
            lock (_lock)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var toRemove = new List<(IPEndPoint, long)>();

                foreach (var (ep, packets) in _pendingPackets)
                {
                    foreach (var (idx, pkt) in packets)
                    {
                        if (pkt.IsAck)
                        {
                            toRemove.Add((ep, idx));
                            continue;
                        }

                        long delayMs = BASE_DELAY_MS * (1L << pkt.Times);
                        if (now - pkt.PreviousTime < delayMs) continue;

                        if (pkt.Times >= MAX_RETRIES)
                        {
                            Debug.LogWarning($"[Server][UdpServer] 重传 {MAX_RETRIES} 次失败 {GetEndpointInfo(ep)} index={idx}");
                            toRemove.Add((ep, idx));
                        }
                        else
                        {
                            pkt.Times++;
                            pkt.PreviousTime = now;
                            try { _socket.Send(pkt.SendData, pkt.SendData.Length, ep); }
                            catch { toRemove.Add((ep, idx)); }
                            Debug.LogWarning($"[Server][UdpServer] 重传 {GetEndpointInfo(ep)} index={idx} 第{pkt.Times}次");
                        }
                    }
                }

                foreach (var (ep, idx) in toRemove)
                {
                    if (_pendingPackets.TryGetValue(ep, out var dict))
                        dict.Remove(idx);
                }
            }
        }

        #endregion

        #region 客户端管理

        public void CleanClient(IPEndPoint remoteEp)
        {
            lock (_lock)
            {
                Debug.Log($"[Server][UdpServer] 断开 {GetEndpointInfo(remoteEp)}");
                _udpIndex.Remove(remoteEp);
                _pendingPackets.Remove(remoteEp);
                _receiveBuf.Remove(remoteEp);
                _invokeIndex.Remove(remoteEp);
            }
        }

        public string GetEndpointInfo(IPEndPoint ep)
        {
            if (ep == null) return "null";
            string addr = ep.Address.ToString();
            if (addr.StartsWith("::ffff:")) addr = addr.Replace("::ffff:", "");
            return $"{addr}:{ep.Port}";
        }

        #endregion
    }
}
