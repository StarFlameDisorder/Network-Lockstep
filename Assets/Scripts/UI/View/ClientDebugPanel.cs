using System.Text;
using Framework;
using GamePlay;
using Google.Protobuf;
using LobbyMessage;
using Network.Client;
using Network.Server;
using SyncMessage;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.View
{
    /// <summary>
    /// 客户端调试面板：连接服务器、加入房间、发送消息、查看日志、游戏状态
    /// </summary>
    public class ClientDebugPanel : MonoBehaviour
    {
        #region UIDocument 引用（运行时从 UXML 查找）

        // ─── 面板控制 ───
        private Button _btnTogglePanel;
        private VisualElement _panelContent;
        private bool _panelVisible = true;

        // ─── 连接区 ───
        private TextField _fieldIp;
        private TextField _fieldTcpPort;
        private TextField _fieldUdpPort;
        private Button _btnConnect;
        private Button _btnDisconnect;
        private Label _labelStatus;
        private Label _labelClientId;

        // ─── 大厅区 ───
        private TextField _fieldName;
        private Button _btnJoinRoom;
        private Button _btnLeaveRoom;
        private Button _btnStartRoom;
        private Label _labelRoom;
        private Label _labelPlayers;

        // ─── 游戏状态区 ───
        private Label _labelFrameInfo;
        private Label _labelPlayersInfo;

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

        private const float REFRESH_INTERVAL = 0.3f;
        private float _refreshTimer;

        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                Debug.LogError("[ClientDebugPanel] 未找到 UIDocument 组件！");
                return;
            }

            var root = uiDoc.rootVisualElement;

            // ─── 面板控制 ───
            _btnTogglePanel = root.Q<Button>("btn-toggle-panel");
            _panelContent = root.Q<VisualElement>("panel-content");

            // ─── 连接区 ───
            _fieldIp = root.Q<TextField>("field-ip");
            _fieldTcpPort = root.Q<TextField>("field-tcpport");
            _fieldUdpPort = root.Q<TextField>("field-udpport");
            _btnConnect = root.Q<Button>("btn-connect");
            _btnDisconnect = root.Q<Button>("btn-disconnect");
            _labelStatus = root.Q<Label>("label-status");
            _labelClientId = root.Q<Label>("label-clientid");

            // ─── 大厅区 ───
            _fieldName = root.Q<TextField>("field-name");
            _btnJoinRoom = root.Q<Button>("btn-join");
            _btnLeaveRoom = root.Q<Button>("btn-leave");
            _btnStartRoom = root.Q<Button>("btn-start");
            _labelRoom = root.Q<Label>("label-room");
            _labelPlayers = root.Q<Label>("label-players");

            // ─── 游戏状态区 ───
            _labelFrameInfo = root.Q<Label>("label-frame-info");
            _labelPlayersInfo = root.Q<Label>("label-players-info");

            // ─── 消息区 ───
            _fieldMessage = root.Q<TextField>("field-message");
            _btnSendTcp = root.Q<Button>("btn-sendtcp");
            _btnSendUdp = root.Q<Button>("btn-sendudp");

            // ─── 日志区 ───
            _areaMessages = root.Q<ScrollView>("area-messages");
            _areaVerbose = root.Q<ScrollView>("area-verbose");
            _btnClearLog = root.Q<Button>("btn-clear-log");
            _toggleVerbose = root.Q<Toggle>("toggle-verbose");
            _toggleVerbose.value = Core.GameConstants.VERBOSE_INFO;

            // ─── 事件绑定 ───
            if (_btnTogglePanel != null) _btnTogglePanel.clicked += TogglePanel;
            if (_btnConnect != null) _btnConnect.clicked += OnConnect;
            if (_btnDisconnect != null) _btnDisconnect.clicked += OnDisconnect;
            if (_btnJoinRoom != null) _btnJoinRoom.clicked += OnJoinRoom;
            if (_btnLeaveRoom != null) _btnLeaveRoom.clicked += OnLeaveRoom;
            if (_btnStartRoom != null) _btnStartRoom.clicked += OnStartRoom;
            if (_btnSendTcp != null) _btnSendTcp.clicked += OnSendTcp;
            if (_btnSendUdp != null) _btnSendUdp.clicked += OnSendUdp;
            if (_btnClearLog != null) _btnClearLog.clicked += () => DebugLogger.ClearAll();

            // ─── 默认值 ───
            if (_fieldIp != null) _fieldIp.value = "127.0.0.1";
            if (_fieldTcpPort != null) _fieldTcpPort.value = "1975";
            if (_fieldUdpPort != null) _fieldUdpPort.value = "1975";

            if (_areaVerbose != null) _areaVerbose.style.display = DisplayStyle.None;
        }

        private GameClient _gameClient;
        private GameSync _gameSync;

        private void Start()
        {
            if (!Global.TryGet(out _gameClient))
            {
                Debug.LogError("[Client][ClientDebugPanel] 获取GameClient子系统错误");
                return;
            }
            if (!Global.TryGet(out _gameSync))
            {
                Debug.LogError("[Client][ClientDebugPanel] 获取GameSync子系统错误");
                return;
            }
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0) return;
            _refreshTimer = REFRESH_INTERVAL;

            RefreshStatus();
            RefreshRoomInfo();
            RefreshGameInfo();
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
        }

        #endregion

        #region 状态刷新

        private void RefreshStatus()
        {
            if (_labelStatus == null) return;

            if (_gameClient == null)
                Global.TryGet(out _gameClient);

            bool tcpOk = _gameClient != null && _gameClient.TcpIsConnected();

            if (tcpOk)
            {
                _labelStatus.text = "● 已连接";
                _labelStatus.style.color = Color.green;
            }
            else
            {
                _labelStatus.text = "○ 未连接";
                _labelStatus.style.color = Color.gray;
            }

            if (_labelClientId != null && _gameClient != null)
            {
                _labelClientId.text = $"clientId: {_gameClient.GetClientId()}";
            }
        }

        private void RefreshRoomInfo()
        {
            if (_labelRoom != null)
                _labelRoom.text = $"状态: {_gameSync.GetStatus()}";

            // 大厅阶段显示待加入的玩家名
            if (_labelPlayers != null)
            {
                var players = _gameSync.Players;
                if (players.Count > 0)
                {
                    _labelPlayers.text = $"玩家({players.Count}): {string.Join(", ", players.Keys)}";
                }
                else
                {
                    _labelPlayers.text = "玩家: -";
                }
            }
        }

        private void RefreshGameInfo()
        {
            if (_labelFrameInfo == null || _labelPlayersInfo == null) return;

            // 帧号信息
            _labelFrameInfo.text = $"帧号(服/发): {_gameSync.LatestServerFrameId}/{_gameSync.SendSeq}";

            // 玩家详细信息
            var players = _gameSync.Players;
            if (players.Count == 0)
            {
                var status = _gameSync.GetStatus();
                _labelPlayersInfo.text = status == GameStatus.Started
                    ? "-- 无玩家数据 --"
                    : "-- 等待游戏开始 --";
                return;
            }

            var sb = new StringBuilder();
            foreach (var kv in players)
            {
                var entity = kv.Value;
                var pos = entity.Position;
                int bufferCount = entity.FrameCount;
                ulong lastFrame = entity.LastExecutedFrameId;

                sb.AppendLine($"{kv.Key}: 帧{lastFrame} 缓冲{bufferCount} ({pos.ToVector3():F1})");
            }
            _labelPlayersInfo.text = sb.ToString().TrimEnd();
        }

        private void RefreshLogs()
        {
            var entries = DebugLogger.ConsumeClientLogs();
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
                    {
                        AppendLogLine(_areaVerbose, entry);
                    }
                }
                else
                {
                    if (_areaMessages != null)
                    {
                        AppendLogLine(_areaMessages, entry);
                    }
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
            {
                area.RemoveAt(0);
            }

            area.schedule.Execute(() => area.ScrollTo(line));
        }

        #endregion

        #region 按钮事件

        private void OnConnect()
        {
            string ip = _fieldIp?.value ?? "127.0.0.1";
            int tcpPort = int.TryParse(_fieldTcpPort?.value, out var tp) ? tp : 1975;
            int udpPort = int.TryParse(_fieldUdpPort?.value, out var up) ? up : 1975;

            _gameClient.StartLink(ip, tcpPort);
            DebugLogger.ClientLog("[SYS]", $"连接服务器 {ip}:{tcpPort}(TCP) {udpPort}(UDP)");
        }

        private void OnDisconnect()
        {
            _gameClient.StopLink();
            DebugLogger.ClientLog("[SYS]", "断开连接");
        }

        private void OnJoinRoom()
        {
            string name = _fieldName?.value ?? "Player";
            var nm = _gameClient;

            if (!nm.TcpIsConnected())
            {
                DebugLogger.ClientLog("[ERR]", "未连接到服务器");
                return;
            }

            var msg = new ClientMessage
            {
                ClientId = nm.GetClientId(),
                LobbySync = new LobbySyncRequest
                {
                    JoinRoom = new PlayerJoinRoomRequest { Name = name }
                }
            };
            nm.TcpSendMessage(msg.ToByteArray());

            if (GameSync.Instance != null)
                GameSync.Instance.SetName(name);

            DebugLogger.ClientLog("[TCP]", $"加入房间: {name}", "→");
        }

        private void OnLeaveRoom()
        {
            string name = _fieldName?.value ?? "Player";
            var nm = _gameClient;

            var msg = new ClientMessage
            {
                ClientId = nm.GetClientId(),
                LobbySync = new LobbySyncRequest
                {
                    LeaveRoom = new PlayerLeaveRoomRequest { Name = name }
                }
            };
            nm.TcpSendMessage(msg.ToByteArray());
            DebugLogger.ClientLog("[TCP]", $"离开房间: {name}", "→");
        }

        private void OnStartRoom()
        {
            string name = _fieldName?.value ?? "Player";
            var nm = _gameClient;

            var msg = new ClientMessage
            {
                ClientId = nm.GetClientId(),
                LobbySync = new LobbySyncRequest
                {
                    StartRoom = new PlayerStartRoomRequest { Name = name }
                }
            };
            nm.TcpSendMessage(msg.ToByteArray());
            DebugLogger.ClientLog("[TCP]", $"开始游戏", "→");
        }

        private void OnSendTcp()
        {
            var nm = _gameClient;
            if (!nm.TcpIsConnected()) return;

            string text = _fieldMessage?.value ?? "";
            var msg = new ClientMessage
            {
                ClientId = nm.GetClientId(),
                CommonMessage = text
            };
            nm.TcpSendMessage(msg.ToByteArray());
            DebugLogger.ClientLog("[TCP]", $"发送: {text}", "→");
            if (_fieldMessage != null) _fieldMessage.value = "";
        }

        private void OnSendUdp()
        {
            var nm = _gameClient;
            if (!nm.UdpIsConnected()) return;

            string text = _fieldMessage?.value ?? "";
            var msg = new ClientMessage
            {
                ClientId = nm.GetClientId(),
                CommonMessage = text
            };
            nm.UdpSendMessage(msg.ToByteArray());
            DebugLogger.ClientLog("[UDP]", $"发送: {text}", "→");
            if (_fieldMessage != null) _fieldMessage.value = "";
        }

        #endregion
    }
}
