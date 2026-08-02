# 方向探索(禁止删除里面的内容，内容全为探索方向，不代表正确或者采用)

## 碰撞逻辑(待验证，且不知道有没有更好的方案)
BV18T7M6HE8
1. 确定性网格基底
所有逻辑坐标基于整数网格（抛弃浮点数与物理引擎），作为帧同步的绝对一致基础。

2. 实体体积占用
最小逻辑单位不再是一个点，而是至少固定占据 3×3 网格。

3. 强制间距缓冲
碰撞判定采用外扩边界（等效于强制单位间至少保留 1格 空隙），从底层杜绝模型穿插和临界模糊状态。

4. 单向硬性响应
发生贴近/重叠时，采用串行优先级处理（拒绝双向同时推挤），强制单方回退或停滞，确保碰撞结果唯一且无震荡。

网格逻辑
最简单方法：二值栅格
用0和1来区分自由区域和障碍物区域。障碍物所占据的栅格被标记为“占用”，反之则为“空闲”。这种表示方法最为直观和高效。

更精确方法：有符号距离场 (SDF)
除了标记障碍物，SDF还在每个栅格顶点存储一个数值，表示该顶点到最近障碍物表面的距离。这个值的符号代表了该点是在障碍物内部（负值） 还是外部（正值）。SDF提供了更丰富的环境信息，是实现平滑碰撞响应的关键。

## 寻路逻辑

流场寻路？

# 问题备忘录

> 更新于 2026-07-24

## 已确认问题（2026-07-24 分析）

### 🔴 P0 帧号分配：每个客户端独立自增，非标准 Lockstep

**现象**：`GameSync._frameId` 在 [GameSync.cs#L231](Assets/Scripts/GamePlay/GameSync.cs#L231) 每帧本地自增，服务端 [RoomManager.cs](Assets/Scripts/Network/Server/RoomManager.cs) 透传不做统一分配。

**后果**：两客户端帧号线性增长但互不重合；没有"所有人都完成了第N帧"的概念；服务端 BroadcastGameSync() 只是从队列中 dump，各玩家帧号可能不同步。

**方案**：服务端统一分配帧号。每帧等待所有在线玩家提交输入 → 分配全局帧号 → 广播完整帧数据。客户端 `_frameId` 改为仅跟踪本地已发输入序列号，执行以服务端帧号为准。

### 🔴 P0 断线重连：状态清理不完整

**现象**：[GameSync.ReceiveSnapshotMessage](Assets/Scripts/GamePlay/GameSync.cs#L299) 未清空 `_players` 字典，仅 `ContainsKey` 判断是否新增。
**后果**：断线前残留的 PlayerEntity 未清理；若快照中少了玩家则旧实体仍存活。

**方案**：重连时清空 `_players`，从快照全量重建。

### 🟡 P1 快照由房主客户端发送

**现象**：[GameSync.SyncPlayerAction](Assets/Scripts/GamePlay/GameSync.cs#L226) 仅房主发快照。
**后果**：房主断线后快照停止生成。

**方案**：快照由服务端持有和发送（服务端已有 `_gameSnapshot`）。

### 🟡 P1 状态指示不更新

**现象**：服务器关了客户端仍显示"已连接"。断线检测仅依赖服务端心跳超时，客户端无主动检测。

### 🟡 P2 GameFrame 职责不清晰

[GameFrame](Assets/Scripts/GamePlay/GameFrame.cs) 名为帧数据但含 PushFrames() 处理逻辑。

### 🟡 P3 无世界一致性校验

已有哈希值计算与显示（所有玩家位置+帧号XOR），但未做跨客户端一致性比对和自动修复。

## 调试UI
- [x] 显示服务端帧号、本地发送序号 ✅ 2026-07-24
- [x] 显示各玩家位置、缓冲区帧数、上次执行帧号 ✅ 2026-07-24
- [x] 面板显隐切换按钮（折叠时缩小为标题栏）✅ 2026-07-24
- [x] 连接状态实时刷新（TCP + UDP 独立显示）✅ 2026-07-24
- [x] 世界哈希值显示（全部玩家位置+帧号XOR）✅ 2026-07-24
- [x] 服务端面板同步显示游戏状态 ✅ 2026-07-24
- [x] 客户端显示当前TCP和UDP端口 ✅ 2026-07-25
- [x] 服务端客户端列表显示各客户端IP和端口 ✅ 2026-07-25

# TODO

> 更新于 2026-07-24 | 方向确认：**RTS 帧同步联机**（暂不做 TPS）

## 当前状态

阶段一、二、四已完成；**全面重构已定方案**，详见 [全面重构方案.md](全面重构方案.md)（断线重连根因已定位：KCP 会话懒创建导致重连补发被丢弃等 6 项根因）。

> 新阶段划分见下方「全面重构（2026-08-02 规划）」。

| 步骤 | 描述 | 状态 |
|---|---|---|
| 阶段一 | 架构改造（Quantum 借鉴 + GameSync子系统化） | ✅ 已完成（2026-07-24） |
| 阶段二 | 严格帧同步：服务端统一帧号 + Lockstep | ✅ 已完成（2026-07-24） |
| 阶段三 | 修复断线重连 + 快照由服务端持有 | ❌ 未开始（并入全面重构阶段A） |
| 阶段四 | KCP 替换手写可靠UDP层 | ✅ 已完成（2026-07-30） |
| 阶段五 | 客户端网络层重构 (INetworkTransport + MessageBus) | ❌ 未开始（并入全面重构阶段B） |
| 阶段六 | 适配与集成（替换旧 Instance 调用等） | ❌ 未开始（并入全面重构阶段B/C） |

## 全面重构（2026-08-02 规划）

> 方案：[全面重构方案.md](全面重构方案.md)。目标：断线重连（网络瞬断/客户端重启）双场景支持 + 网络层重构 + UI/逻辑修复 + C++ 目录清理。

| 阶段 | 内容 | 状态 |
|---|---|---|
| 阶段A | 断线重连修复 | 🔧 基本完成（2026-08-02），已运行时验证：断线检测/清理/重连恢复/帧续播 |
| 阶段B | 客户端网络层重构（INetworkTransport + MessageBus + NetworkClient） | 🔧 部分完成（消息主线程派发队列已做；INetworkTransport 抽象类未做） |
| 阶段C | GameSync 职责拆分 + 服务端广播改主线程驱动 | 🔧 部分完成（服务端广播已改主线程驱动；GameSync 拆分未做） |
| 阶段D | UI 修复与清理（旧面板移除、断线状态显示、LeaveRoom/EndRoom 派发） | 🔧 部分完成（断线状态/UDP端口/加入离开消息已做；场景旧面板移除未做） |
| 阶段E | C++ Server/ 目录废弃与清理 | ❌ 未开始 |

### 阶段A 已落地明细（2026-08-02）

- **A1 KCP 会话注册**：服务端 KcpServer 增加"待发缓存"（conv 会话未创建时暂存，会话创建后补发）；客户端握手后立即发 KCP 注册包 → 重连补发不再被静默丢弃
- **A2 房间级帧缓存**：断线重连复用原 PlayerSession（保留 Frames 历史帧缓存）；SendReconnectData 重写为按服务端帧号整帧补发 [快照帧+1, 当前帧]
- **A3 服务端快照**：任意客户端定时上报（D1），触发条件由"帧号取模"改为"超过上次上报帧+间隔"（修复帧号跳变漏报）
- **A4 事件订阅泄漏**：KCP 消息回调只在 Init 注册一次
- **A5 断线检测**：客户端收包超时（3s，D5）+ TCP/KCP 双通道统一 ConnectionState（D7）+ 手动重连（D2）
- **A6 重连恢复**：快照全量重建 + `_sendSeq`/`_lastSnapshotFrameId` 恢复 + 中途加入自建实体
- **A7 心跳配置+线程模型**：`Initialize` 心跳超时参数生效；帧广播/心跳检测/保活全部改主线程驱动（GameServer.Update）；Dispatcher 网络事件统一主线程派发；KcpSession 加线程安全锁
- **A8 中途加入**：服务端对运行中房间的新玩家也补发快照+帧；会话新增 `Joining` 状态，恢复期间不参与 Lockstep 帧等待（避免卡住全房间）
- **额外修复**：TcpServer.GetClientInfo 对已销毁 TcpClient 抛异常导致死客户端无法清理；KcpServer 接收循环单包异常不再终止；DeleteClient 清理异常不阻止移除；服务端保活包（`ServerMessage.keepAlive` 新字段）

### 阶段B 已落地明细

- 客户端消息统一入队 + 主线程 Update 消费派发（消除跨线程调 Unity API）

### 阶段D 已落地明细

- ClientDebugPanel：TCP/KCP 统一状态显示、UDP 端口接入连接流程、断线提示手动重连
- MessageDispatcher：LeaveRoom/EndRoom 派发、保活/普通文本消息处理
- 大厅动态显示其他玩家加入/离开房间（DebugLogger）

> 2026-07-24 附：房间逻辑已修复（玩家实体延迟到 StartRoom 创建）、调试UI已增强（帧信息+玩家状态+面板折叠）

---

## 阶段一：架构改造 ✅ 已完成

### 1.1 逻辑/表现分离

- [x] `PlayerEntity`（纯逻辑）：Position、_preFrameId、SortedDictionary 帧缓冲区、快照方法
- [x] `PlayerView`（MonoBehaviour）：Update() 轮询 PlayerEntity.Position → transform
- [x] 删除旧 `Player.cs`

### 1.2 帧上下文 + 单帧消费

- [x] `GameFrame`：PushFrames() 含追帧逻辑（追帧从 PlayerEntity 内部移出）
- [x] `PlayerEntity.TryConsumeNextFrame()`：每次只处理一帧
- [x] `Dictionary` → `SortedDictionary`（有序帧缓冲区）

### 1.3 输入命令模式

- [x] `FrameInputCommand` 结构体（FrameId, PlayerName, MoveDirection）
- [x] `EnqueueInput()` 替代 `PlayerAction(Vector2)` 的隐式 `_input` 缓存

### 1.4 GameSync → SubSystemBase

- [x] GameSync 继承 SubSystemBase（移除 MonoBehaviour 依赖）
- [x] 协程 TimerHandle → Update(float) 累加器驱动
- [x] TimerHandle fallback 从 GameSync.Instance → GameCore.Instance
- [x] `_playerPrefab` 由 GameCore [SerializeField] 注入
- [x] 在 GameCore.InitSubSystems 中注册并 PostInit
- [x] 新增 `SubSystemPriority.GamePlay = -90`

### 文件变更

| 新增 | 修改 | 删除 |
|---|---|---|
| PlayerEntity.cs | GameSync.cs | Player.cs |
| PlayerView.cs | GameCore.cs | |
| GameFrame.cs | SubSystemPriority.cs | |
| FrameInputCommand.cs | TimerHandle.cs | |
| | PlayerController.cs | |
| | ClientDebugPanel.cs（无需改）| |
| | LobbyPanel.cs（无需改）| |

> **场景注意事项**：原 GameSync 作为 MonoBehaviour 挂在场景 GameObject 上，现已改为 SubSystemBase（由 GameCore 通过 SystemMgr 创建）。需要在场景中移除旧的 GameSync 组件，并在 GameCore 的 Inspector 中拖入 Player 预制体。

---

## 阶段二：严格帧同步 🔧 进行中

### 目标

将当前"客户端各自分配帧号 + 服务端透传"改为标准 Lockstep：**服务端统一分配帧号，等所有在线玩家提交输入后广播完整帧**。

### 改动点

#### 2.1 服务端 RoomManager

- [x] 新增 `_serverFrameId` 全局帧计数器
- [x] `PlayerSession.ReceiveMessages` → `InputQueue`（每帧消费一个）
- [x] `ReceiveGameSync()` 改为入队到 `InputQueue`，不直接广播
- [x] `BroadcastGameSync()` 改为：检查所有在线玩家是否都已提交 → 是则 `_serverFrameId++`，统一设置 `PlayerSync.FrameId` 和 `GameSyncMessage.FrameId`，广播，各消费一个

#### 2.2 客户端 GameSync

- [x] `_frameId` → `_sendSeq`（本地发送序号）+ `_latestServerFrameId`（服务端权威帧号）
- [x] `SyncPlayerAction()` 移除本地预写入，只发送到服务器
- [x] `ReceiveMessage()` 统一处理所有玩家（含本地），按服务端帧号写入
- [x] `ReceiveSnapshotMessage()` 清空 `_players` 后全量重建
- [x] `UpdateGame()` 中 GameFrame.FrameId 使用 `_latestServerFrameId`

#### 2.3 GameFrame / PlayerEntity

- [x] PlayerEntity 帧消费逻辑不变（已验证）
- [x] GameFrame.PushFrames() 保持不变（已验证）

### 文件变更

| 修改 | 说明 |
|---|---|
| RoomManager.cs | 服务端帧号统一分配 + 等待所有玩家输入 |
| GameSync.cs | 客户端适配服务端帧号，不再本地自增 |
| PlayerEntity.cs | 无改动（帧消费逻辑不变） |
| GameFrame.cs | 无改动 |

---
