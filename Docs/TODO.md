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

> 更新于 2026-08-02（已解决项已标注，见各条末尾）

## 已确认问题（2026-07-24 分析）

### 🔴 P0 帧号分配：每个客户端独立自增，非标准 Lockstep  ✅ 已解决（2026-07-24 阶段二）

**现象**：`GameSync._frameId` 在 [GameSync.cs#L231](Assets/Scripts/GamePlay/GameSync.cs#L231) 每帧本地自增，服务端 [RoomManager.cs](Assets/Scripts/Network/Server/RoomManager.cs) 透传不做统一分配。

**后果**：两客户端帧号线性增长但互不重合；没有"所有人都完成了第N帧"的概念；服务端 BroadcastGameSync() 只是从队列中 dump，各玩家帧号可能不同步。

**方案**：服务端统一分配帧号。每帧等待所有在线玩家提交输入 → 分配全局帧号 → 广播完整帧数据。客户端 `_frameId` 改为仅跟踪本地已发输入序列号，执行以服务端帧号为准。

### 🔴 P0 断线重连：状态清理不完整  ✅ 已解决（2026-08-02 阶段A6）

**现象**：[GameSync.ReceiveSnapshotMessage](Assets/Scripts/GamePlay/GameSync.cs#L299) 未清空 `_players` 字典，仅 `ContainsKey` 判断是否新增。
**后果**：断线前残留的 PlayerEntity 未清理；若快照中少了玩家则旧实体仍存活。

**方案**：重连时清空 `_players`，从快照全量重建。

### 🟡 P1 快照由房主客户端发送  ✅ 已解决（2026-08-02 阶段A3）

**现象**：[GameSync.SyncPlayerAction](Assets/Scripts/GamePlay/GameSync.cs#L226) 仅房主发快照。
**后果**：房主断线后快照停止生成。

**方案**：快照由服务端持有和发送（服务端已有 `_gameSnapshot`）。

### 🟡 P1 状态指示不更新  ✅ 已解决（2026-08-02 阶段A5）

**现象**：服务器关了客户端仍显示"已连接"。断线检测仅依赖服务端心跳超时，客户端无主动检测。

### 🟡 P2 GameFrame 职责不清晰  ✅ 已解决（2026-08-03 执行模型重构）

[GameFrame](Assets/Scripts/GamePlay/GameFrame.cs) 名为帧数据但含 PushFrames() 处理逻辑。
→ 已废弃：框架职责（缓冲/帧号/缺口/追帧）拆入 `FrameBuffer` + `GameSync.ApplyFrames`；实体仅保留 `Simulate(FrameInput)` 纯模拟。

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
| 阶段A | 断线重连修复 | ✅ 已完成（2026-08-02），已实测验证：断线停止发送/补发缺口/跳帧容错/防多对象 |
| 阶段B | 客户端网络层重构（INetworkTransport + MessageBus + NetworkClient） | 🔧 部分完成（消息主线程派发队列已做；INetworkTransport 抽象类未做，按"保持简单"原则暂缓） |
| 阶段C | GameSync 职责拆分 + 服务端广播改主线程驱动 | ✅ 已完成（2026-08-02）：帧处理逻辑重构（TickGame 职责分离、快照/补发/对齐拆分、命名修正 NotStarted/ApplyAll 等）+ 服务端主线程（阶段A 已做） |
| 阶段C2 | 执行模型重构 + 输入命令化（Quantum 风格：实体 Simulate + 框架喂帧） | ✅ 已完成（2026-08-03）：执行模型重构 + proto 命令化 + 命名空间分离，详见下方明细 |
| 阶段D | UI 修复与清理（旧面板移除、断线状态显示、LeaveRoom/EndRoom 派发） | ✅ 已完成（2026-08-03）：断线状态/UDP端口/加入离开消息已做（2026-08-02）；弃用面板脚本（StatusPanel/NetworkPanel/LobbyPanel/ButtonClick/MessagePanel/SubMessagePanel/ControlButton）已删除（场景旧面板用户已手动清理，MessageDispatcher 对 MessagePanel 的引用已移除） |
| 阶段E | C++ Server/ 目录废弃与清理 | ✅ 已完成（2026-08-02）：Server/ 目录已删除（用户确认）；UdpServer/UdpSocket/Player.cs 已标记废弃 |

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
- **重连帧号对齐（2026-08-02 追加）**：修复"重连后客户端帧号从0开始"——`_sendSeq` 恢复时与权威帧号（`_latestServerFrameId`）对齐（补发帧分支也会推进帧号）；服务端补发快照的恢复点统一用快照消息级帧号（与补发范围完全对齐，防追帧断档）；调试面板帧号标签明确为"服=服务端权威帧 发=本地发送序号"
- **重连帧缺口修复（2026-08-02 追加2）**：修复"重连后缓存炸了 + 不执行帧"（根因：补发起点用快照消息级帧号，跳过离线/停滞玩家恢复点之前的帧，造成帧缺口卡死）
  - 服务端补发起点改为**所有玩家恢复点（快照执行帧）的最小值+1**；每个玩家以各自执行帧为恢复点
  - 补发按玩家恢复点过滤，只发其恢复点之后的帧（避免旧帧灌入缓冲）
  - 补发只含各玩家恢复点之后的帧；其他在线客户端仅补**重连玩家**的缺失帧（保证各端视图一致，且不灌入其他玩家旧帧）
  - 客户端 `PlayerEntity.TryConsumeNextFrame` 增加**跳帧容错**：目标帧缺失时跳到最早可用帧（离线期冻结，符合语义）；`AddSyncMessage` 重复帧容错；`GameFrame` 输出帧缺口诊断日志
  - 服务端拒绝"快照帧超前于当前帧"的异常快照；重连时清空客户端残留消息队列
- **重连三问题修复（2026-08-02 追加3）**：
  - 断线后停止发送：`SyncPlayerAction`/`HeartBeat` 增加连接状态守卫（修复断线后 UI"发"序号持续增长的假象）
  - 补发广播只含重连玩家的帧：其他在线客户端只补该玩家缺失帧（避免灌入其他玩家旧帧导致"其他玩家缓冲暴涨"）
  - 快照重建防多对象：`PlayerEntity.Destroy` 改 `DestroyImmediate`（立即销毁，避免重建时新旧对象同帧重叠）+ `ReceiveSnapshotMessage` 快照去重（`_lastAppliedSnapshotFrameId`）

### 阶段B 已落地明细

- 客户端消息统一入队 + 主线程 Update 消费派发（消除跨线程调 Unity API）

### 阶段D 已落地明细

- ClientDebugPanel：TCP/KCP 统一状态显示、UDP 端口接入连接流程、断线提示手动重连
- MessageDispatcher：LeaveRoom/EndRoom 派发、保活/普通文本消息处理
- 大厅动态显示其他玩家加入/离开房间（DebugLogger）

### 阶段C 已落地明细（2026-08-02）

- `TickGame`（原 `UpdateGame`）职责分离为三步：`SendLocalInput`（发输入）→ `TryReportSnapshot`（快照上报）→ `GameFrame.ApplyAll`（帧消费），消除原 `SyncPlayerAction` 混装"输入+快照"的问题
- `ReceiveSnapshotMessage` 拆分为 `ApplySnapshot`（快照重建）/ `HandleReplayFrames`（补发帧）/ `AlignSendSeqToServerFrame`（帧号对齐），去掉两处重复的对齐逻辑
- 命名修正：`GameStatus.Notstarted`→`NotStarted`、`GameFrame.PushFrames`→`ApplyAll`（帧执行器语义，TODO P2 关闭）、`HeartBeat`→`SendHeartBeat`、PlayerEntity `_preFrameId`→`_lastExecutedFrameId`、`_preSnapshotFrameId`→`_lastAppliedSnapshotFrameId`
- 逻辑帧间隔 `GAME_TICK_INTERVAL` 从 `_gameFrameRate` 派生（单一来源）
- 旧 `Player.cs` 标记废弃（已被 PlayerEntity+PlayerView 替代，无引用）

### 阶段C2 执行模型重构已落地明细（2026-08-03）

> 目标（用户提出）：① 操作输入语义化（未来操作不止移动）② GameFrame 职责太乱 ③ 类似 Quantum 的执行模型——实体只处理自己的数据（不关心是哪个帧），框架只负责提供对应帧。已确认：每帧命令列表 + 保留双移动语义（方向向量=键盘调试，MoveTo=未来正式移动）+ 每步同步文档。

- **输入语义化（第一步，行为不变）**：新增 `FrameInput`（一帧输入 = 命令列表，可同时下达多条命令）+ `CommandType`（`MoveDirection`/`MoveTo`，语义区分；MoveTo 为 RTS 正式移动预留）+ `InputCommand`（类型 + 参数字段）
- **缓冲外置框架层**：新增 `FrameBuffer`（每玩家缓冲：`Push`/`TryPopNextFrame`（含跳帧容错）/`ResetNextFrameId`/`Clear`）；缓冲、帧号、缺口、追帧全部移出实体
- **实体纯净**：`PlayerEntity` 删掉 `_pendingFrames`/`TryConsumeNextFrame`/`AddSyncMessage`/帧号字段，只留 `Simulate(FrameInput)`（null=缺口/离线→冻结，由实体自行决定）+ 位置/快照状态；快照恢复不再碰帧号
- **框架调度**：删除 `GameFrame` 类（标记废弃，无引用）；替代为 `GameSync.ApplyFrames`（对每玩家取帧喂实体 + 追帧 + 缺口诊断日志）；追帧 sqrt 改整数开方 `IntSqrt`（修 `Math.Sqrt(double)` 浮点误差 bug，TODO 已知项顺手修）
- **快照适配**：恢复点 = 各玩家 `FrameBuffer.LastExecutedFrameId`（框架维护）；`GetSnapshotSync(帧号)` 由框架传参，实体不感知执行进度
- **调试面板**：ClientDebugPanel 帧信息改为从 `GameSync.FrameBuffers` 读取

### 阶段C2 第二步（proto 命令化 + 命名空间分离）已落地明细（2026-08-03）

- **proto 命令化（GameMessage.proto 重构）**：`PlayerSync` 由固定 `inputMove`（Vector3D）改为 `repeated Command` 命令列表；新增 `Command`（oneof：`MoveDirectionCommand`/`MoveToCommand`/`AttackCommand`/`BuildCommand`，扩展新操作加分支即可）；`HeartBeat` 删死字段 `time`；`GameSyncMessage`/`GameSnapshotMessage`/`GameSnapshot`/`PlayerSnapshotSync` 字段编号重排（删旧字段跳号）
- **proto 清理**：`ConnectMessage.proto` 删除弃用的 `HandShakeRequest` 与无引用的 `ClientConnectMessage`；`SyncMessage.ClientMessage.connectMessage` 字段同步删除；`proto.bat` 删除已失效的 `--cpp_out` 行（Server/ 已删）
- **命名空间/目录分离（框架 vs 游戏逻辑）**：`GameSync`/`FrameBuffer`/`FrameInput` 迁至新命名空间 `FrameSync` + 新目录 `Assets/Scripts/FrameSync/`（.meta 随迁，GUID 不变）；游戏逻辑 `PlayerEntity`/`PlayerView`/`PlayerController` 保留在 `GamePlay`；引用方（GameCore/ClientDebugPanel/LobbyPanel/PlayerController）已适配
- **输入语义化**：`PlayerController` 键盘方向 → `EnqueueCommand(MoveDirection)` 语义命令（原 `EnqueueInput(Vector2)` 过渡接口已删）；`GameSync` 每帧把本帧命令列表打包为 `PlayerSync.Commands` 发送；接收端 `ToFrameInput` 按命令列表解码（不再假设单一方向）
- **服务端适配**：RoomManager 广播日志改为输出命令数（原读 `InputMove` 已删）
- **废弃文件删除（2026-08-03 追加）**：`GameFrame.cs`/`Player.cs`/`FrameInputCommand.cs`/`UdpSocket.cs`/`UdpServer.cs` 已确认删除（含 .meta）；UI 弃用面板脚本（StatusPanel/NetworkPanel/LobbyPanel/ButtonClick/MessagePanel/SubMessagePanel/ControlButton）已确认删除（用户已在场景手动清理，MessageDispatcher 中 MessagePanel 引用已移除）
- **玩家操作修复（2026-08-03 追加）**：玩家按住 WASD 只动一帧——根因：InputSystem 的 action 仅在值变化时回调（按住方向不变只回调一次），命令列表发送后即清导致后续帧无命令。修复：命令分**持续型/事件型**——`MoveDirection` 缓存最后一次方向每帧重发（松开按键回调为 0 自然停止）；`MoveTo/Attack/...` 事件型发送后清空

> protoc 生成说明：本机 PowerShell 执行策略拦截 shell 命令，经用户授权通过 unity MCP `execute_code` 运行 protoc.exe 重新生成 C#（exit=0）。

### 阶段E 已落地明细（2026-08-02）

- `Server/`（C++/Qt 服务端，含 protobuf 生成物）已删除（用户确认）
- 客户端 `UdpSocket.cs` / 服务端 `UdpServer.cs` 标记废弃（KCP 已替换手写可靠UDP，无引用）

> 2026-07-24 附：房间逻辑已修复（玩家实体延迟到 StartRoom 创建）、调试UI已增强（帧信息+玩家状态+面板折叠）

---

## 帧同步框架待办（2026-08-02 盘点）

> 网络同步核心目标已达成（阶段A 完成），以下为框架层面的后续缺口，按优先级记录，暂不排期。

### 正确性（帧同步命根子）
- **Desync 检测闭环**（原 TODO P3，升级）✅ 已解决（2026-08-03）：客户端每 30 帧上报世界哈希（HashReportMessage），服务端跨客户端同帧（±1 帧容差）比对，不一致时广播 DesyncNoticeMessage（含分歧帧号）到所有客户端并打日志。已落地：proto 消息 + 服务端 RoomManager.ReceiveHashReport 比对 + 客户端 TryReportHash/ReceiveDesyncNotice
- **确定性 Random**：逻辑一旦用 `UnityEngine.Random`/系统随机，各端必然分歧；需提供"种子播种、可复现"的确定性随机
- **确定性时间**：逻辑内禁用 `Time.time`/`DateTime`（当前固定帧率累加器已满足）
- **确定性数学完善**：FixedPoint 已有，缺 sqrt/三角等确定性实现（RTS 寻路/弹道会用到）
- **自动化确定性回归测试**：同输入 → 同输出（防重构回归）

### 网络健壮性
- **快照漂移根治**（已知遗留）✅ 已解决（2026-08-03）：SendReconnectData 改为向**所有在线客户端**统一广播权威快照 + 全量补帧——重连玩家与在线玩家全部从同一快照点重置，消除"不同时间点恢复导致的位置漂移"（原先只给重连玩家发快照，在线玩家不回退，追帧期间哈希不一致）
- **弱网模拟器**：丢包/抖动/延迟注入，验证 KCP + 重连在真实弱网下的表现
- **RTT/延迟显示**：调试面板补充
- **输入校验/防重放**（服务端）：当前信任客户端输入，框架上留接口即可

### 工程能力
- **回放系统（Replay）**：记录输入流重放——帧同步标配 + 定位 desync 的最强工具（中成本，可后置）
- **观战模式**（可选，后置）

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

## 阶段二：严格帧同步 ✅ 已完成

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
