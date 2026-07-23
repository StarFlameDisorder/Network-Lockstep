using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// TCP 服务器：监听端口，管理客户端连接，处理分包/黏包
    /// 移植自 C++/Qt TcpServer
    /// </summary>
    public class TcpServer : IDisposable//C# 中用于释放非托管资源,需要显式释放的资源
    {
        #region 属性
        
        private TcpListener _listener;
        private readonly int _port;
        private readonly Dictionary<TcpClient, List<byte>> _messageBuffers = new();
        private readonly object _lock = new();
        private bool _isRunning;

        public event Action<TcpClient> OnClientConnected;
        public event Action<TcpClient, byte[]> OnMessageReceived;
        public event Action<TcpClient> OnClientDisconnected;

        public TcpServer(int port = 1975)
        {
            _port = port;
        }

        #endregion

        #region 生命周期
        
        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;
            Debug.Log($"[Server][TcpServer] 启动 TCP 服务器，端口：{_port}");
            BeginAccept();
        }
        
        public void Stop()
        {
            _isRunning = false;

            lock (_lock)
            {
                foreach (var tcp in _messageBuffers.Keys)
                {
                    try { tcp.Close(); } catch { /* ignore */ }
                }
                _messageBuffers.Clear();
            }

            try { _listener?.Stop(); }
            catch { /* ignore */ }

            Debug.Log("[Server][TcpServer] TCP 服务器已停止");
        }

        public void Dispose()
        {
            Stop();
        }
        
        #endregion

        #region 连接管理
        private async void BeginAccept()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    lock (_lock)
                    {
                        _messageBuffers[client] = new List<byte>();
                    }
                    Debug.Log($"[Server][TcpServer] 新连接：{GetClientInfo(client)}");
                    OnClientConnected?.Invoke(client);
                    _ = ReceiveLoop(client);
                }
                catch (ObjectDisposedException)
                {
                    break; // 服务器已停止
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        Debug.LogError($"[Server][TcpServer] Accept 异常：{ex}");
                }
            }
        }
        
        public void DisconnectClient(TcpClient client)
        {
            OnDisconnected(client);
        }

        private void OnDisconnected(TcpClient client)
        {
            lock (_lock)
            {
                _messageBuffers.Remove(client);
            }

            try { client?.Close(); }
            catch { /* ignore */ }

            Debug.Log($"[Server][TcpServer] 断开连接：{GetClientInfo(client)}");
            OnClientDisconnected?.Invoke(client);
        }
        
        #endregion

        #region 消息收发
        private async System.Threading.Tasks.Task ReceiveLoop(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buf = new byte[2048];

            try
            {
                while (_isRunning && client.Connected)
                {
                    int len = await stream.ReadAsync(buf, 0, buf.Length);
                    if (len == 0) break; // 对方关闭连接

                    lock (_lock)
                    {
                        if (!_messageBuffers.TryGetValue(client, out var buffer)) break;
                        buffer.AddRange(new ArraySegment<byte>(buf, 0, len));

                        while (buffer.Count >= 4)
                        {
                            int msgLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer.ToArray(), 0));
                            if (msgLen <= 0 || msgLen > 1024)
                            {
                                Debug.LogError($"[Server][TcpServer] 无效消息长度：{msgLen}，断开客户端");
                                break;
                            }

                            if (buffer.Count < msgLen + 4) break;

                            byte[] message = new byte[msgLen];
                            buffer.CopyTo(4, message, 0, msgLen);
                            buffer.RemoveRange(0, msgLen + 4);

                            OnMessageReceived?.Invoke(client, message);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not ObjectDisposedException)
            {
                // 客户端断开通常是连接重置
            }

            OnDisconnected(client);
        }

        public void Send(TcpClient client, byte[] data)
        {
            if (client == null || !client.Connected) return;

            try
            {
                int msgLen = data.Length;
                byte[] head = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(msgLen));
                byte[] sendBuf = new byte[head.Length + data.Length];
                Buffer.BlockCopy(head, 0, sendBuf, 0, head.Length);
                Buffer.BlockCopy(data, 0, sendBuf, head.Length, data.Length);
                
                
                lock (client)
                {
                    client.GetStream().Write(sendBuf, 0, sendBuf.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server][TcpServer] 发送失败：{ex.Message}");
            }
        }
        #endregion

        public string GetClientInfo(TcpClient client)
        {
            if (client?.Client?.RemoteEndPoint is IPEndPoint ep)
                return $"{ep.Address}:{ep.Port}";
            return "unknown";
        }
    }
}
