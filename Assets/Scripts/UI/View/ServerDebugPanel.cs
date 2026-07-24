using System.Linq;
using System.Text;
using Framework;
using Google.Protobuf;
using Network.Server;
using SyncMessage;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.View
{
    /// <summary>
    /// 服务端调试面板：启动/停止服务器、查看客户端/玩家/游戏状态、广播消息、查看日志
    /// </summary>
    public class ServerDebugPanel : MonoBehaviour
    {
        #region UIDocument 引用

        // ─── 面板控制 ───
        private VisualElement _panelRoot;
        private Button _btnTogglePanel;
        private VisualElement _panelContent;
        private bool _panelVisible = true;

        // ─── 服务器控制区 ───
        private TextField _fieldTcpPort;
        private TextField _fieldUdpPort;
        private TextField _fieldFrameRate;
        private Button _btnStart;
        private Button _btnStop;
        private Label _labelServerStatus;
        private Label _labelPortInfo;

        // ─── 客户端/玩家列表 ───
        private ScrollView _areaClients;
        private ScrollView _areaPlayers;

        // ─── 游戏状态区 ───
        private Label _labelFrameInfo;
        private Label _labelGameState;

        // ─── 消息发送区 ───
        private TextField _fieldMessage;
        private Button _btnSendTcp;
        private Button _btnSendUdp;

        // ─── 日志区 ───
        private ScrollView _areaMessages;
        private ScrollView _areaVerbose;
        private Button _btnClearLog;
        private Toggle _toggleVerbose;

        #endregion

        private const float REFRESH_INTERVAL = 0.5f;
        private float _refreshTimer;

        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                Debug.LogError("[Server][ServerDebugPanel] 未找到 UIDocument 组件！");
                return;
            }

            var root = uiDoc.rootVisualElement;

            // ─── 面板控制 ───
            _panelRoot = root.Q<VisualElement>("panel-root");
            _btnTogglePanel = root.Q<Button>("btn-toggle-panel");
            _panelContent = root.Q<VisualElement>("panel-content");

            // ─── 服务器控制 ───
            _fieldTcpPort = root.Q<TextField>("field-tcpport");
            _fieldUdpPort = root.Q<TextField>("field-udpport");
            _fieldFrameRate = root.Q<TextField>("field-framerate");
            _btnStart = root.Q<Button>("btn-start");
            _btnStop = root.Q<Button>("btn-stop");
            _labelServerStatus = root.Q<Label>("label-server-status");
            _labelPortInfo = root.Q<Label>("label-port-info");

            // ─── 客户端/玩家 ───
            _areaClients = root.Q<ScrollView>("area-clients");
            _areaPlayers = root.Q<ScrollView>("area-players");

            // ─── 游戏状态 ───
            _labelFrameInfo = root.Q<Label>("label-frame-info");
            _labelGameState = root.Q<Label>("label-game-state");

            // ─── 消息 ───
            _fieldMessage = root.Q<TextField>("field-message");
            _btnSendTcp = root.Q<Button>("btn-sendtcp");
            _btnSendUdp = root.Q<Button>("btn-sendudp");

            // ─── 日志 ───
            _areaMessages = root.Q<ScrollView>("area-messages");
            _areaVerbose = root.Q<ScrollView>("area-verbose");
            _btnClearLog = root.Q<Button>("btn-clear-log");
            _toggleVerbose = root.Q<Toggle>("toggle-verbose");
            _toggleVerbose.value = Core.GameConstants.VERBOSE_INFO;

            // ─── 事件绑定 ───
            if (_btnTogglePanel != null) _btnTogglePanel.clicked += TogglePanel;
            if (_btnStart != null) _btnStart.clicked += OnStartServer;
            if (_btnStop != null) _btnStop.clicked += OnStopServer;
            if (_btnSendTcp != null) _btnSendTcp.clicked += OnSendTcpBroadcast;
            if (_btnSendUdp != null) _btnSendUdp.clicked += OnSendUdpBroadcast;
            if (_btnClearLog != null) _btnClearLog.clicked += () => DebugLogger.ClearAll();

            if (_areaVerbose != null) _areaVerbose.style.display = DisplayStyle.None;
            // 详细日志相关默认折叠
            if (_toggleVerbose != null) _toggleVerbose.style.display = DisplayStyle.None;
            if (_btnClearLog != null) _btnClearLog.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0) return;
            _refreshTimer = REFRESH_INTERVAL;

            RefreshServerStatus();
            RefreshClientList();
            RefreshPlayerList();
            RefreshGameState();
            RefreshLogs();
        }

        #region 面板显隐

        private void TogglePanel()
        {
            _panelVisible = !_panelVisible;
            if (_panelContent != null)
                _panelContent.style.display = _panelVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_btnTogglePanel != null)
                _btnTogglePanel.text = _panelVisible ? "▼" : "▶";

            if (_panelRoot != null)
            {
                if (_panelVisible)
                {
                    _panelRoot.style.bottom = 0;
                    _panelRoot.style.height = StyleKeyword.Auto;
                }
                else
                {
                    _panelRoot.style.bottom = StyleKeyword.Auto;
                    _panelRoot.style.height = 32;
                    _panelRoot.style.overflow = Overflow.Hidden;
                }
            }
        }

        #endregion

        #region 状态刷新

        private static GameServer GetServer() => Global.Get<GameServer>();

        private void RefreshServerStatus()
        {
            var server = GetServer();
            bool running = server != null && server.IsRunning;

            if (_labelServerStatus != null)
            {
                _labelServerStatus.text = running ? "●" : "○";
                _labelServerStatus.style.color = running ? Color.green : Color.gray;
            }

            if (_labelPortInfo != null && server != null)
            {
                _labelPortInfo.text = $"TCP:{server.TcpPort}  UDP:{server.UdpPort}  帧率:{server.GameFrameRate}FPS";
            }

            if (_btnStart != null) _btnStart.SetEnabled(!running);
            if (_btnStop != null) _btnStop.SetEnabled(running);
        }

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

            _areaClients.Clear();

            foreach (var c in clients)
            {
                string tcpMark = c.HasTcp ? "TCP✔" : "TCP✘";
                string udpMark = c.HasUdp ? "UDP✔" : "UDP✘";
                _areaClients.Add(new Label
                {
                    text = $"#{c.ClientId}  {tcpMark}  {udpMark}",
                    style = { fontSize = 11, whiteSpace = WhiteSpace.Normal }
                });
            }
        }

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

            _areaPlayers.Clear();

            foreach (var p in players)
            {
                string online = p.Online ? "在线✔" : "离线✘";
                _areaPlayers.Add(new Label
                {
                    text = $"{p.Name}  {online}  帧#{p.LastFrameId}  缓冲{p.InputQueueCount}",
                    style = { fontSize = 11, whiteSpace = WhiteSpace.Normal }
                });
            }
        }

        private void RefreshGameState()
        {
            if (_labelFrameInfo == null) return;

            var server = GetServer();
            if (server == null || !server.IsRunning || server.Room == null)
            {
                _labelFrameInfo.text = "帧号: -  在线: -/-";
                if (_labelGameState != null)
                    _labelGameState.text = "-- 服务器未运行 --";
                return;
            }

            var room = server.Room;
            var players = room.Players;
            int onlineCount = players.Count(p => p.Online);

            _labelFrameInfo.text = $"帧号: {room.ServerFrameId}  在线: {onlineCount}/{players.Count}";

            if (_labelGameState != null)
            {
                if (!room.IsRunning)
                {
                    _labelGameState.text = "-- 未开始 --";
                }
                else
                {
                    var sb = new StringBuilder();
                    foreach (var p in players)
                    {
                        string online = p.Online ? "在线" : "离线";
                        sb.AppendLine($"{p.Name}: {online} 缓冲{p.InputQueueCount} 帧#{p.LastFrameId}");
                    }
                    _labelGameState.text = sb.ToString().TrimEnd();
                }
            }
        }

        private void RefreshLogs()
        {
            var entries = DebugLogger.ConsumeServerLogs();
            if (entries.Count == 0) return;

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

            // 从 UI 读取端口/帧率写入 ServerConfig（在启动前生效）
            var config = Resources.Load<ServerConfig>("ServerConfig");
            if (config != null)
            {
                if (_fieldTcpPort != null && int.TryParse(_fieldTcpPort.value, out int tcp))
                    config.TcpPort = tcp;
                if (_fieldUdpPort != null && int.TryParse(_fieldUdpPort.value, out int udp))
                    config.UdpPort = udp;
                if (_fieldFrameRate != null && int.TryParse(_fieldFrameRate.value, out int fps))
                    config.GameFrameRate = fps;
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
