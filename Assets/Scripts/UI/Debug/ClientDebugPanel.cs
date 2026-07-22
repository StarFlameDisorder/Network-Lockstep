using System.Collections.Generic;
using System.Text;
using Framework;
using GamePlay;
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
    /// 客户端调试面板：连接服务器、加入房间、发送消息、查看日志
    /// 
    /// 使用方式：
    /// 1. 将此脚本挂载到一个有 UIDocument 组件的 GameObject 上
    /// 2. UIDocument 的 Source Asset 指向 ClientDebugPanel.uxml
    /// 3. 通过 UXML 元素的 name 属性绑定控件
    /// 
    /// UXML 元素命名约定（在 .uxml 中设置 name 属性）：
    ///   btn-connect, btn-disconnect, btn-join, btn-leave, btn-start
    ///   field-ip, field-tcpport, field-udpport, field-name, field-message
    ///   label-status, label-room, label-players, label-clientid
    ///   area-messages, area-verbose
    ///   btn-sendtcp, btn-sendudp, btn-clear-log
    ///   toggle-verbose
    /// </summary>
    public class ClientDebugPanel : MonoBehaviour
    {
        #region UIDocument 引用（在运行时从 UXML 中查找）

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

        // ─── 消息发送区 ───
        private TextField _fieldMessage;
        private Button _btnSendTcp;
        private Button _btnSendUdp;

        // ─── 日志区 ───
        private ScrollView _areaMessages;     // 普通消息列表
        private ScrollView _areaVerbose;      // 详细日志列表
        private Button _btnClearLog;
        private Toggle _toggleVerbose;        // 是否显示详细日志

        #endregion

        /// <summary>UI 刷新间隔（秒）</summary>
        private const float REFRESH_INTERVAL = 0.5f;
        private float _refreshTimer;

        private void OnEnable()
        {
            // 获取 UIDocument 组件并绑定 UI 元素
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                UnityEngine.Debug.LogError("[ClientDebugPanel] 未找到 UIDocument 组件！");
                return;
            }

            var root = uiDoc.rootVisualElement;

            // ─── 按名称查找控件（名称须与 UXML 中的 name 属性一致）───

            // 连接区
            _fieldIp = root.Q<TextField>("field-ip");
            _fieldTcpPort = root.Q<TextField>("field-tcpport");
            _fieldUdpPort = root.Q<TextField>("field-udpport");
            _btnConnect = root.Q<Button>("btn-connect");
            _btnDisconnect = root.Q<Button>("btn-disconnect");
            _labelStatus = root.Q<Label>("label-status");
            _labelClientId = root.Q<Label>("label-clientid");

            // 大厅区
            _fieldName = root.Q<TextField>("field-name");
            _btnJoinRoom = root.Q<Button>("btn-join");
            _btnLeaveRoom = root.Q<Button>("btn-leave");
            _btnStartRoom = root.Q<Button>("btn-start");
            _labelRoom = root.Q<Label>("label-room");
            _labelPlayers = root.Q<Label>("label-players");

            // 消息区
            _fieldMessage = root.Q<TextField>("field-message");
            _btnSendTcp = root.Q<Button>("btn-sendtcp");
            _btnSendUdp = root.Q<Button>("btn-sendudp");

            // 日志区
            _areaMessages = root.Q<ScrollView>("area-messages");
            _areaVerbose = root.Q<ScrollView>("area-verbose");
            _btnClearLog = root.Q<Button>("btn-clear-log");
            _toggleVerbose = root.Q<Toggle>("toggle-verbose");

            // ─── 绑定按钮事件 ───
            if (_btnConnect != null) _btnConnect.clicked += OnConnect;
            if (_btnDisconnect != null) _btnDisconnect.clicked += OnDisconnect;
            if (_btnJoinRoom != null) _btnJoinRoom.clicked += OnJoinRoom;
            if (_btnLeaveRoom != null) _btnLeaveRoom.clicked += OnLeaveRoom;
            if (_btnStartRoom != null) _btnStartRoom.clicked += OnStartRoom;
            if (_btnSendTcp != null) _btnSendTcp.clicked += OnSendTcp;
            if (_btnSendUdp != null) _btnSendUdp.clicked += OnSendUdp;
            if (_btnClearLog != null) _btnClearLog.clicked += () => DebugLogger.ClearAll();

            // 默认值
            if (_fieldIp != null) _fieldIp.value = "127.0.0.1";
            if (_fieldTcpPort != null) _fieldTcpPort.value = "1975";
            if (_fieldUdpPort != null) _fieldUdpPort.value = "1975";

            // 详细日志默认隐藏
            if (_areaVerbose != null) _areaVerbose.style.display = DisplayStyle.None;

            // 订阅 NetworkManager 状态事件（用于自动记录日志）
            // 注意：NetworkManager 还是旧单体，暂不做侵入修改，面板自己查询状态
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0) return;
            _refreshTimer = REFRESH_INTERVAL;

            RefreshStatus();
            RefreshRoomInfo();
            RefreshLogs();
        }

        #region 状态刷新

        private void RefreshStatus()
        {
            if (_labelStatus == null) return;

            var nm = NetworkManager.Instance;
            bool tcpOk = nm != null && nm.TcpIsConnected();

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

            if (_labelClientId != null && nm != null)
            {
                _labelClientId.text = $"clientId: {nm.GetClientId()}";
            }
        }

        private void RefreshRoomInfo()
        {
            var gs = GameSync.Instance;
            if (gs == null) return;

            if (_labelRoom != null)
                _labelRoom.text = $"状态: {gs.GetStatus()}";

            // 玩家列表从 GameSync 获取（它维护了 _players 字典，但是 private）
            // 暂通过 StatusPanel 间接读取，后续解耦后直接查 GameSync
        }

        /// <summary>
        /// 消费 DebugLogger 中的客户端日志，渲染到 UI
        /// 消息始终显示，详细日志由 toggle 控制
        /// </summary>
        private void RefreshLogs()
        {
            var entries = DebugLogger.ConsumeClientLogs();
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
                    // 详细日志 → 添加到 area-verbose
                    if (_toggleVerbose != null && _toggleVerbose.value && _areaVerbose != null)
                    {
                        AppendLogLine(_areaVerbose, entry);
                    }
                }
                else
                {
                    // 普通消息 → 添加到 area-messages
                    if (_areaMessages != null)
                    {
                        AppendLogLine(_areaMessages, entry);
                    }
                }
            }
        }

        /// <summary>向 ScrollView 追加一行日志</summary>
        private void AppendLogLine(ScrollView area, DebugLogger.LogEntry entry)
        {
            var line = new Label
            {
                text = $"{entry.Time} {entry.Direction}{entry.Tag} {entry.Content}",
                style = { fontSize = 11, whiteSpace = WhiteSpace.Normal }
            };
            area.Add(line);

            // 限制最大行数，避免 UI 元素过多
            while (area.childCount > 50)
            {
                area.RemoveAt(0);
            }

            // 自动滚动到底部
            area.schedule.Execute(() => area.ScrollTo(line));
        }

        #endregion

        #region 按钮事件

        private void OnConnect()
        {
            string ip = _fieldIp?.value ?? "127.0.0.1";
            int tcpPort = int.TryParse(_fieldTcpPort?.value, out var tp) ? tp : 1975;
            int udpPort = int.TryParse(_fieldUdpPort?.value, out var up) ? up : 1975;

            NetworkManager.Instance.StartLink(ip, tcpPort);
            DebugLogger.ClientLog("[SYS]", $"连接服务器 {ip}:{tcpPort}(TCP) {udpPort}(UDP)");
        }

        private void OnDisconnect()
        {
            NetworkManager.Instance.StopLink();
            DebugLogger.ClientLog("[SYS]", "断开连接");
        }

        private void OnJoinRoom()
        {
            string name = _fieldName?.value ?? "Player";
            var nm = NetworkManager.Instance;

            if (!nm.TcpIsConnected())
            {
                DebugLogger.ClientLog("[ERR]", "未连接到服务器");
                return;
            }

            // 发送加入房间请求（复用现有协议）
            var msg = new ClientMessage
            {
                ClientId = nm.GetClientId(),
                LobbySync = new LobbySyncRequest
                {
                    JoinRoom = new PlayerJoinRoomRequest { Name = name }
                }
            };
            nm.TcpSendMessage(msg.ToByteArray());

            // 通知 GameSync 设置本地玩家名
            if (GameSync.Instance != null)
                GameSync.Instance.SetName(name);

            DebugLogger.ClientLog("[TCP]", $"加入房间: {name}", "→");
        }

        private void OnLeaveRoom()
        {
            string name = _fieldName?.value ?? "Player";
            var nm = NetworkManager.Instance;

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
            var nm = NetworkManager.Instance;

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
            var nm = NetworkManager.Instance;
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
            var nm = NetworkManager.Instance;
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
