# 问题备忘录

未开始时完全没UDP心跳(不影响就是了)
联调验证基本正常，但是快照恢复断线重连后帧号有问题，导致只恢复了快照
## 调试UI
显示本地帧号、各玩家位置及当前世界哈希值
状态指示应随时更新(比如连接状态，服务器关了还显示已连接)

# TODO

> 更新于 2026-07-24 | 方向确认：**RTS 帧同步联机**（暂不做 TPS）

## 当前状态

阶段一已完成（架构改造全部完成，包括 GameSync SubSystemBase 化）。

| 步骤 | 描述 | 状态 |
|---|---|---|
| 阶段一 | 架构改造（Quantum 借鉴 + GameSync子系统化） | ✅ 已完成（2026-07-24） |
| 阶段二 | 修复快照帧号 Bug 🔴 | ⏳ 待验证 |
| 阶段三 | KCP 替换手写可靠UDP层 | ❌ 未开始 |
| 阶段四 | 客户端网络层重构 (INetworkTransport + MessageBus) | ❌ 未开始 |
| 阶段五 | 适配与集成（替换旧 Instance 调用） | ❌ 未开始 |

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
