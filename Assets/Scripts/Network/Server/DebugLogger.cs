using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Network.Server
{
    /// <summary>
    /// 共享调试日志系统：线程安全的日志队列，支持"消息"和"详细日志"两级分类
    /// 
    /// 消息：玩家能感知的游戏事件（连接、加入房间、开始游戏等），始终显示
    /// 详细日志：原始网络包内容（帧数据、ACK等），默认隐藏，勾选后才渲染
    /// </summary>
    public static class DebugLogger
    {
        // 线程安全队列：服务端网络线程写入，Unity 主线程读取
        private static readonly ConcurrentQueue<LogEntry> _serverLogs = new();
        private static readonly ConcurrentQueue<LogEntry> _clientLogs = new();
        
        /// <summary>最大保留条数（超出后丢弃旧条目）</summary>
        private const int MAX_ENTRIES = 50;

        /// <summary>
        /// 单条日志
        /// </summary>
        public class LogEntry
        {
            /// <summary>时间戳（本地时间 HH:mm:ss）</summary>
            public string Time;
            /// <summary>消息类型标签：[TCP]/[UDP]/[SYS]</summary>
            public string Tag;
            /// <summary>日志内容</summary>
            public string Content;
            /// <summary>是否为详细日志（false=普通消息）</summary>
            public bool IsVerbose;
            /// <summary>方向：→发送  ←接收  ·系统</summary>
            public string Direction;
        }

        #region 服务端日志

        /// <summary>
        /// 记录服务端消息（始终显示）
        /// </summary>
        /// <param name="tag">标签，如 [TCP]/[UDP]/[SYS]</param>
        /// <param name="content">内容</param>
        /// <param name="direction">方向箭头</param>
        public static void ServerLog(string tag, string content, string direction = "·")
        {
            AddLog(_serverLogs, tag, content, direction, false);
        }

        /// <summary>
        /// 记录服务端详细日志（默认隐藏，勾选后才显示）
        /// </summary>
        public static void ServerVerbose(string tag, string content, string direction = "·")
        {
            AddLog(_serverLogs, tag, content, direction, true);
        }

        /// <summary>消费服务端日志（取出并移除所有待显示条目）</summary>
        public static List<LogEntry> ConsumeServerLogs()
        {
            return ConsumeAll(_serverLogs);
        }

        #endregion

        #region 客户端日志

        /// <summary>记录客户端消息</summary>
        public static void ClientLog(string tag, string content, string direction = "·")
        {
            AddLog(_clientLogs, tag, content, direction, false);
        }

        /// <summary>记录客户端详细日志</summary>
        public static void ClientVerbose(string tag, string content, string direction = "·")
        {
            AddLog(_clientLogs, tag, content, direction, true);
        }

        /// <summary>消费客户端日志</summary>
        public static List<LogEntry> ConsumeClientLogs()
        {
            return ConsumeAll(_clientLogs);
        }

        #endregion

        #region 内部实现

        private static void AddLog(ConcurrentQueue<LogEntry> queue, string tag, string content, string direction, bool isVerbose)
        {
            var entry = new LogEntry
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Tag = tag,
                Content = content,
                Direction = direction,
                IsVerbose = isVerbose
            };

            queue.Enqueue(entry);

            // 队列过长时丢弃最旧的
            while (queue.Count > MAX_ENTRIES)
            {
                queue.TryDequeue(out _);
            }
        }

        private static List<LogEntry> ConsumeAll(ConcurrentQueue<LogEntry> queue)
        {
            var list = new List<LogEntry>();
            while (queue.TryDequeue(out var entry))
            {
                list.Add(entry);
            }
            return list;
        }

        /// <summary>清空所有日志</summary>
        public static void ClearAll()
        {
            while (_serverLogs.TryDequeue(out _)) { }
            while (_clientLogs.TryDequeue(out _)) { }
        }

        #endregion
    }
}
