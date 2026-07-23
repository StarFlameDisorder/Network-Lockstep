using System;
using System.Collections.Generic;
using Framework;
using GameMessage;
using Google.Protobuf;
using LobbyMessage;
using Network;
using Network.Server;
using SyncMessage;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Debug
{
    /// <summary>
    /// 服务端调试面板：启动/停止服务器、查看客户端/玩家、广播消息、查看日志
    /// 
    /// 使用方式：
    /// 1. 将此脚本挂载到一个有 UIDocument 组件的 GameObject 上
    /// 2. UIDocument 的 Source Asset 指向 ServerDebugPanel.uxml
    /// 3. 通过 UXML 元素的 name 属性绑定控件
    /// 
    /// UXML 元素命名约定（在 .uxml 中设置 name 属性）：
    ///   btn-start, btn-stop, label-server-status
    ///   area-clients, area-players
    ///   field-message, btn-sendtcp, btn-sendudp
    ///   area-messages, area-verbose
    ///   btn-clear-log, toggle-verbose
    /// </summary>
    public class ServerDebugPanel : MonoBehaviour
    {
        #region UIDocument 引用（在运行时从 UXML 中查找）

        // ─── 服务器控制区 ───
        private Button _btnStart;
        private Button _btnStop;
        private Label _labelServerStatus;
        private Label _labelPortInfo;
        private Label _labelFrameRate;

        // ─── 客户端/玩家列表 ───
        private ScrollView _areaClients;   // 已连接客户端列表
        private ScrollView _areaPlayers;   // 房间玩家列表

        // ─── 消息发送区 ───
        private TextField _fieldMessage;
        private Button _btnSendTcp;
        private Button _btnSendUdp;

        // ─── 日志区 ───
        private ScrollView _areaMessages;   // 普通消息列表
        private ScrollView _areaVerbose;    // 详细日志列表
        private Button _btnClearLog;
        private Toggle _toggleVerbose;

        #endregion

        /// <summary>UI 刷新间隔（秒）：客户端列表/玩家列表不需要每帧刷新</summary>
        private const float REFRESH_INTERVAL = 0.5f;
        private float _refreshTimer;

        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                UnityEngine.Debug.LogError("[ServerDebugPanel] 未找到 UIDocument 组件！");
                return;
            }

            var root = uiDoc.rootVisualElement;

            // ─── 按名称查找控件 ───
            _btnStart = root.Q<Button>("btn-start");
            _btnStop = root.Q<Button>("btn-stop");
            _labelServerStatus = root.Q<Label>("label-server-status");
            _labelPortInfo = root.Q<Label>("label-port-info");
            _labelFrameRate = root.Q<Label>("label-framerate");

            _areaClients = root.Q<ScrollView>("area-clients");
            _areaPlayers = root.Q<ScrollView>("area-players");

            _fieldMessage = root.Q<TextField>("field-message");
            _btnSendTcp = root.Q<Button>("btn-sendtcp");
            _btnSendUdp = root.Q<Button>("btn-sendudp");

            _areaMessages = root.Q<ScrollView>("area-messages");
            _areaVerbose = root.Q<ScrollView>("area-verbose");
            _btnClearLog = root.Q<Button>("btn-clear-log");
            _toggleVerbose = root.Q<Toggle>("toggle-verbose");
            _toggleVerbose.value = Core.GameConstants.VERBOSE_INFO;

            // ─── 绑定按钮事件 ───
            if (_btnStart != null) _btnStart.clicked += OnStartServer;
            if (_btnStop != null) _btnStop.clicked += OnStopServer;
            if (_btnSendTcp != null) _btnSendTcp.clicked += OnSendTcpBroadcast;
            if (_btnSendUdp != null) _btnSendUdp.clicked += OnSendUdpBroadcast;
            if (_btnClearLog != null) _btnClearLog.clicked += () => DebugLogger.ClearAll();

            // 详细日志默认隐藏
            if (_areaVerbose != null) _areaVerbose.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0) return;
            _refreshTimer = REFRESH_INTERVAL;

            RefreshServerStatus();
            RefreshClientList();
            RefreshPlayerList();
            RefreshLogs();
        }

        #region 状态刷新

        /// <summary>获取 GameServer 子系统实例</summary>
        private static GameServer GetServer() => Global.Get<GameServer>();

        private void RefreshServerStatus()
        {
            var server = GetServer();
            bool running = server != null && server.IsRunning;

            if (_labelServerStatus != null)
            {
                _labelServerStatus.text = running ? "● 运行中" : "○ 已停止";
                _labelServerStatus.style.color = running ? Color.green : Color.gray;
            }

            if (_labelPortInfo != null && server != null)
            {
                _labelPortInfo.text = $"TCP:{server.TcpPort}  UDP:{server.UdpPort}";
            }

            if (_labelFrameRate != null && server != null)
            {
                _labelFrameRate.text = $"帧率: {server.GameFrameRate}FPS";
            }

            // 按钮状态
            if (_btnStart != null) _btnStart.SetEnabled(!running);
            if (_btnStop != null) _btnStop.SetEnabled(running);
        }

        /// <summary>
        /// 刷新已连接客户端列表（从 ServerNetworkDispatcher 读取）
        /// </summary>
        private void RefreshClientList()
        {
            if (_areaClients == null) return;

            var server = GetServer();
            if (server == null || !server.IsRunning || server.Dispatcher == null)
            {
                if (_areaClients.childCount > 0) _areaClients.Clear();
                return;
            }

            var clients = server.Dispatcher.Clients;
            // 简单对比数量：如果数量没变则跳过重建（减少 GC）
            if (_areaClients.childCount == clients.Count + 1) return; // +1 for header

            _areaClients.Clear();

            // 表头
            _areaClients.Add(new Label
            {
                text = $"已连接客户端 ({clients.Count})",
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
            });

            foreach (var c in clients)
            {
                string tcpMark = c.HasTcp ? "TCP✔" : "TCP✘";
                string udpMark = c.HasUdp ? "UDP✔" : "UDP✘";
                _areaClients.Add(new Label
                {
                    text = $"  #{c.ClientId}  {c.TcpEndpoint}  {tcpMark}  {udpMark}",
                    style = { fontSize = 11, whiteSpace = WhiteSpace.Normal }
                });
            }
        }

        /// <summary>
        /// 刷新房间玩家列表（从 RoomManager 读取）
        /// </summary>
        private void RefreshPlayerList()
        {
            if (_areaPlayers == null) return;

            var server = GetServer();
            if (server == null || !server.IsRunning || server.Room == null)
            {
                if (_areaPlayers.childCount > 0) _areaPlayers.Clear();
                return;
            }

            var players = server.Room.Players;
            if (_areaPlayers.childCount == players.Count + 1) return; // +1 for header

            _areaPlayers.Clear();

            // 表头
            _areaPlayers.Add(new Label
            {
                text = $"房间玩家 ({players.Count})  房主: {server.Room.OwnerName}  运行: {(server.Room.IsRunning ? "是" : "否")}",
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
            });

            foreach (var p in players)
            {
                string online = p.Online ? "在线✔" : "离线✘";
                _areaPlayers.Add(new Label
                {
                    text = $"  {p.Name}(id={p.Id})  {online}  心跳: {p.SecondsSinceHeartbeat:F1}s前  帧#{p.LastFrameId}",
                    style = { fontSize = 11, whiteSpace = WhiteSpace.Normal }
                });
            }
        }

        /// <summary>
        /// 消费服务端日志并渲染
        /// </summary>
        private void RefreshLogs()
        {
            var entries = DebugLogger.ConsumeServerLogs();
            if (entries.Count == 0) return;

            // 控制详细日志区域显隐
            if (_areaVerbose != null && _toggleVerbose != null)
            {
                _areaVerbose.style.display = _toggleVerbose.value ? DisplayStyle.Flex : DisplayStyle.None;
            }

            foreach (var entry in entries)
            {
                if (entry.IsVerbose)
                {
                    if (_toggleVerbose != null && _toggleVerbose.value && _areaVerbose != null)
                        AppendLogLine(_areaVerbose, entry);
                }
                else
                {
                    if (_areaMessages != null)
                        AppendLogLine(_areaMessages, entry);
                }
            }
        }

        private void AppendLogLine(ScrollView area, DebugLogger.LogEntry entry)
        {
            var line = new Label
            {
                text = $"{entry.Time} {entry.Direction}{entry.Tag} {entry.Content}",
                style = { fontSize = 11, whiteSpace = WhiteSpace.Normal }
            };
            area.Add(line);
            while (area.childCount > 50)
                area.RemoveAt(0);
            area.schedule.Execute(() => area.ScrollTo(line));
        }

        #endregion

        #region 按钮事件

        private void OnStartServer()
        {
            var server = GetServer();
            if (server == null)
            {
                DebugLogger.ServerLog("[ERR]", "GameServer 子系统未注册！");
                return;
            }

            server.StartServer();
            DebugLogger.ServerLog("[SYS]", "服务器启动");
        }

        private void OnStopServer()
        {
            var server = GetServer();
            if (server == null) return;

            server.StopServer();
            DebugLogger.ServerLog("[SYS]", "服务器已停止");
        }

        private void OnSendTcpBroadcast()
        {
            var server = GetServer();
            if (server == null || !server.IsRunning) return;

            string text = _fieldMessage?.value ?? "";
            foreach (var c in server.Dispatcher.Clients)
            {
                var msg = new ServerMessage { CommonMessage = text };
                server.Dispatcher.SendTcp(c.ClientId, msg.ToByteArray());
            }
            DebugLogger.ServerLog("[TCP]", $"广播: {text}", "→");
            if (_fieldMessage != null) _fieldMessage.value = "";
        }

        private void OnSendUdpBroadcast()
        {
            var server = GetServer();
            if (server == null || !server.IsRunning) return;

            string text = _fieldMessage?.value ?? "";
            foreach (var c in server.Dispatcher.Clients)
            {
                if (!c.HasUdp) continue;
                var msg = new ServerMessage { CommonMessage = text };
                server.Dispatcher.SendUdp(c.ClientId, msg.ToByteArray());
            }
            DebugLogger.ServerLog("[UDP]", $"广播: {text}", "→");
            if (_fieldMessage != null) _fieldMessage.value = "";
        }

        #endregion
    }
}
