// #define Log_Debug

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UI;
using UnityEngine;
using UnityEngine.Events;
using SyncMessage;
using ConnectMessage;
using Google.Protobuf;


namespace Network
{
    class PendingPacket //避免直接拷贝 类当结构体用
    {
        public Int64 index; //包序号
        public byte[] sendBuf;
        public Int64 previousTime; //上次发送时间(包括重传)
        public int times; //发送尝试次数
        public bool isAck; //是否确认
    }
    
    public class UdpSocket
    {
        private IPEndPoint _ipEndPoint;
        private Socket _socketUdp;
        private UInt64 _clientId = 0;
        private Int64 _index = 0;

        public UdpSocket()
        {
            _timerHandle.OnTimeTriggerEvent += CheckAndResend;
        }
        
        
        private TimerHandle _timerHandle = new TimerHandle(1);
        
        public void StartLink(string ip, int port)
        {
            try
            {
                if (IsConnected()) CloseLink();
                Debug.Log("初始化UDP客户端" + ip + ":" + port);
                _ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                _socketUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socketUdp.Connect(_ipEndPoint);
                _timerHandle.StartTimer();
            }
            catch (SocketException e)
            {
                Debug.LogError(e);
                throw;
            }

            _cancelTokenSource = new CancellationTokenSource();
            ReceiveAsync(message =>
            {
                // Debug.Log("Udp:收到消息");
                NetworkManager.Instance.HandleMessage(message);
                // Debug.Log(Encoding.UTF8.GetString(message));
                //MessagePanel.Instance?.AddMessage("Udp:收到消息");
            },_cancelTokenSource.Token);
        }

        public void CloseLink()
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource = null;
            
            _socketUdp.Shutdown(SocketShutdown.Both);
            _socketUdp.Close();
            _socketUdp = null;
            //_sendQueue.Clear();
            _pendingPackets.Clear();
            _timerHandle.StopTimer();
        }

        Dictionary<Int64, PendingPacket> _pendingPackets = new Dictionary<Int64, PendingPacket>(); //发送消息缓存
        //Queue<Int64> _sendQueue = new Queue<Int64>();

        
        private void CheckAndResend() //发送消息后会检查旧数据是否发送 
        {
            //TODO:是否添加独立计时器
            if (IsConnected())
            {
                Int64 time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                var remove=new List<Int64>();//待删除 (遍历时不能修改元素)
                foreach (var pair in _pendingPackets)
                {
                    Int64 index = pair.Key;
                    if (pair.Value.isAck)//收到确认的移除
                    {
                        remove.Add(pair.Key);
                    }
                    else
                    {
                        PendingPacket packet = pair.Value;
                        if (time - packet.previousTime < 1000 * (1 << (packet.times)))//未到时间，等到超时时间 指数退避
                        {
                            continue;
                        }
                        
                        if (packet.times > 3) //超过次数的报错
                        {
                            Debug.LogWarning("重传3次失败，序号:" + index);
                            remove.Add(pair.Key);
                        }
                        else
                        {
                            //重传 重新排队
                            packet.times++;
                            packet.previousTime = time;
                            _socketUdp.Send(packet.sendBuf);
                            Debug.LogWarning("重传，序号:" + index);
                        }
                    }
                }

                foreach (var index in remove)
                {
                    _pendingPackets.Remove(index);
                }
            }
        }


        public void Send(byte[] buf)
        {
            if (IsConnected())
            {
                byte[] headerBuf = Encoding.ASCII.GetBytes("SEQ");
                int length = buf.Length;
                byte[] indexBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(_index));
                byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
                if (length > 548) Debug.LogWarning("UDP包过长，可能出现分包！");

                byte[] sendBuf = new byte[buf.Length + indexBytes.Length + lengthBytes.Length + headerBuf.Length];

                int offset = 0;
                Buffer.BlockCopy(headerBuf, 0, sendBuf, 0, headerBuf.Length); //类型

                offset += headerBuf.Length;
                Buffer.BlockCopy(indexBytes, 0, sendBuf, offset, indexBytes.Length); //序号

                offset += indexBytes.Length;
                Buffer.BlockCopy(lengthBytes, 0, sendBuf, offset, lengthBytes.Length); //长度

                offset += lengthBytes.Length;
                Buffer.BlockCopy(buf, 0, sendBuf, offset, buf.Length); //数据

#if Log_Debug
                Debug.Log($"发送-长度:{length}-原始有效字节(十六进制): {BitConverter.ToString(buf, 0, length)}"); //有效载荷长度
                Debug.Log($"发送-序号{_index}-Socket长度:{sendBuf.Length}-总字节:{BitConverter.ToString(sendBuf, 0, sendBuf.Length)}");
#endif

                _socketUdp.Send(sendBuf);

                PendingPacket packet = new PendingPacket
                {
                    index = _index,
                    isAck = false,
                    previousTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    sendBuf = sendBuf,
                    times = 0
                };
                _pendingPackets.Add(_index, packet);
                //_sendQueue.Enqueue(_index);

                _index++;
                //CheckAndResend();
            }
        }

        public void SendAck(Int64 index)
        {
            byte[] headerBuf = Encoding.ASCII.GetBytes("ACK");
            byte[] indexBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(index));

            byte[] sendBuf = new byte[headerBuf.Length + indexBytes.Length];
            int offset = 0;
            Buffer.BlockCopy(headerBuf, 0, sendBuf, 0, headerBuf.Length); //类型
            offset += headerBuf.Length;
            Buffer.BlockCopy(indexBytes, 0, sendBuf, offset, indexBytes.Length); //序号

#if Log_Debug
            Debug.Log(
                $"发送-序号{index}-Socket长度:{sendBuf.Length}-总字节:{BitConverter.ToString(sendBuf, 0, sendBuf.Length)}");
            Debug.Log("发送ACK-序号" + index);
#endif

            _socketUdp?.Send(sendBuf);
        }

        private Dictionary<Int64,byte[]> _receiveBuf = new Dictionary<Int64, byte[]>();
        private Int64 _invokeIndex=0;//下一个应传出的序号
        
        private CancellationTokenSource _cancelTokenSource;
        private async void ReceiveAsync(UnityAction<byte[]> callback,CancellationToken token)
        {
            while (IsConnected()&&!token.IsCancellationRequested)
            {
                byte[] buf = new byte[600];
                int originalLength = await _socketUdp.ReceiveAsync(buf, SocketFlags.None);

                string t = Encoding.UTF8.GetString(buf, 0, 3);
                Int64 index = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buf, 3));

#if Log_Debug
                Debug.Log(
                    $"接收-序号{_index}-Socket长度:{originalLength}-总字节:{BitConverter.ToString(buf, 0, originalLength)}");
#endif
                if (t == "SEQ")
                {
                    int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 11));
                    byte[] actualData = new byte[length];
                    Array.Copy(buf, 15, actualData, 0, length);

#if Log_Debug
                    Debug.Log($"接收-长度{length}-原始有效字节(十六进制): {BitConverter.ToString(actualData, 0, length)}");
#endif
                    SendAck(index);
                    
                    //重复判断 排序
                    if (index >= _invokeIndex)
                    {
                        if (!_receiveBuf.TryAdd(index, actualData)) Debug.LogWarning("重复包" + index);
                    }
                    else Debug.LogWarning("接收到旧包"+index);
                    
                    while (_receiveBuf.TryGetValue(_invokeIndex, out byte[] data))
                    {
                        callback?.Invoke(data);
                        _receiveBuf.Remove(_invokeIndex);
                        _invokeIndex++;
                    }
                    while(_receiveBuf.Count>600)
                    {
                        Debug.LogWarning("UDP缓冲区包过多" + _receiveBuf.Count+"跳过"+_invokeIndex);
                        _receiveBuf.Remove(_invokeIndex);
                        _invokeIndex++;
                    };
                }
                else
                {
                    if (t == "ACK")
                    {
                        if(_pendingPackets.ContainsKey(index))_pendingPackets[index].isAck = true;
                        else Debug.Log($"接收-接收到旧ACK序号{index}");
#if Log_Debug
                        Debug.Log($"接收-ACK序号{index}");
#endif
                    }
                    else Debug.LogWarning("接收未知类型:" + t);
                }
            }
        }

        public bool IsConnected()
        {
            return _socketUdp != null && _socketUdp.Connected;
        }

        public void BindClientId(UInt64 clientId)
        {
            _clientId = clientId;
            Debug.Log("Udp:服务器分配id:" + clientId);
            ClientMessage message = new ClientMessage
            {
                ClientId = _clientId,
                CommonMessage = "UDP-这是客户端，绑定id"
            };
            Send(message.ToByteArray());
        }

        public void Destroy()
        {
            CloseLink();
        }
    }
}