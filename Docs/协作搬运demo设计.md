# 协作搬运 Demo（帧同步框架的可玩示范）

> 创建于 2026-08-04 | 目的：给帧同步框架一个"可现场演示的完整玩法闭环"，同时作为**新玩法接入现有框架的示范文档**。
> 原则：玩法最小（移动+拾取+搬运+送达），全程复用现有确定性帧同步链路，不引入新架构。

## 一、玩法定义

- 双人协作：两个玩家各用 WASD 移动（复用现有 MoveDirection 命令）。
- 3 个物品（黄色方块）散落在场地，1 个火车（灰色方块，送达区）。
- 按 **E 键**上下文交互：
  - 空闲且靠近自由物品 → 拾取最近者（携带时物品跟随玩家头顶）。
  - 携带中且靠近火车 → 送达（计数+1，物品回出生点）。
  - 携带中且不在火车旁 → **放到地上**（物品落在当前 XZ 位置，可被任何人再拾取）。
- 目标：总送达次数达到 `DeliverTarget`（= 物品数 × 2 = 6）→ HUD 显示"任务完成"。
- 计分/位置/物品状态全部确定性，各端一致；世界哈希已纳入物品位置，Desync 检测覆盖本玩法。
- 简易 HUD（`CargoHud`，OnGUI 零资源依赖）：计分 + 操作提示 + 完成提示。

## 二、为什么用"物品纯逻辑模拟"（核心设计决策）

**物品不直接联网**，而是像 PlayerEntity 一样在每个客户端做确定性模拟：

- 所有客户端收到同一份帧输入（PickUp/送达等事件型命令走现有 Command 管道）。
- 每个客户端跑同一套拾取/搬运/送达逻辑 → 物品状态天然一致，永不漂移。
- 只有"交互意图"（E 键）需要联网，物品的"状态变化"是模拟结果，不需要传输。

这正是帧同步的标准模式：**传输意图，不传输结果**。

## 三、确定性保障（本 demo 的两个坑位）

### 1. 拾取冲突裁决（两个玩家同帧抢同一物品）
- 物品 `TryClaim` 只在"自由"状态可拾取 → 先处理者成功，后者失败。
- **处理顺序必须各端一致**：`ApplyFrames` 改为按玩家名 `StringComparer.Ordinal` 排序迭代
  （默认字符串比较受文化影响，不可用于确定性）。
- 平局裁决（等距物品）：遍历列表取距离平方**更小**者，严格 `<` → Id 小者（列表顺序）胜出。

### 2. 布局来源（出生点/火车位置）
- 全部写死在 `CargoConfig` 代码常量（FixedPoint），**不依赖场景摆放** → 各端布局必然一致。
- 距离判定用 XZ 平面距离平方（纯 int/long 运算），判定半径的平方用 long 防溢出。

## 四、分层实现（对应框架扩展点）

| 层 | 文件 | 改动 |
|---|---|---|
| 协议 | `GameMessage.proto` | `Command` oneof 加 `InteractCommand`；`GameSnapshot` 加 `repeated ItemSnapshotSync itemSSs`（objectId/pos/owner/deliverCount） |
| 框架 | `GameSync.cs` | 物品列表/创建/销毁；`ApplyFrames` 确定性排序 + 传物品世界；哈希含物品；快照收发含物品 |
| 逻辑 | `ItemEntity.cs`（新） | 纯逻辑物品：位置/持有者/送达次数/快照 |
| 逻辑 | `PlayerEntity.cs` | `Simulate(input, items)` 加 `Interact` case：拾取/送达/放下/携带跟随 |
| 配置 | `CargoConfig.cs`（新） | 世界常量 + 确定性距离判定 |
| 表现 | `ItemView.cs`（新） | 物品表现层（读逻辑位置 → transform） |
| 表现 | `CargoHud.cs`（新） | 简易 HUD（OnGUI 计分/提示/完成，由 GameSync 创建） |
| 表现 | `PlayerController.cs` | E 键 → `EnqueueCommand(Interact)` |
| UI | `ClientDebugPanel.cs` | 追加"搬运: 已送达 X/Y"计分行 |
| 服务端 | `RoomManager.cs` | `SendReconnectData` 补发快照时同步复制 `ItemSSs`（重连后物品不丢） |

## 五、重连/中途加入

- 客户端快照上报已含物品状态；服务端缓存并原样转发。
- `ApplySnapshot` → `RebuildItemsFromSnapshot`：清空重建物品 + **恢复携带关系**
  （持有者仍在房间 → `RestoreCarriedItem` 挂回；已不在 → `Release` 释放为自由）。

## 六、文件索引

| 路径 | 说明 |
|---|---|
| Assets/Scripts/GamePlay/CargoConfig.cs | 世界配置（改布局改这里） |
| Assets/Scripts/GamePlay/ItemEntity.cs | 物品纯逻辑实体 |
| Assets/Scripts/GamePlay/ItemView.cs | 物品表现层 |
| Assets/Scripts/UI/View/CargoHud.cs | 简易 HUD（计分/提示/完成） |
| Assets/Scripts/GamePlay/PlayerEntity.cs | 玩家实体（Interact 交互） |
| Assets/Scripts/FrameSync/GameSync.cs | 框架主控（物品世界/调度/快照/哈希/HUD 生命周期） |
| Assets/Protobuf/proto/GameMessage.proto | Interact 命令 + 物品快照协议 |

## 七、演示脚本（双开验证）

1. 编辑器实例 A：启动服务端 → 连接（127.0.0.1:1975）→ 加入房间（如 A）→ 开始游戏。
2. 打包/编辑器实例 B：连接 → 加入房间（如 B）。
3. 验收：两画面玩家/物品/火车位置一致，面板帧号与世界哈希一致。
4. A 走到物品旁按 E 拾取 → 搬到火车旁按 E 送达 → 两画面"已送达"同步 +1。
5. 双人同时按 E 抢同一物品 → 只一人拾取（确定性冲突裁决）。
6. 断掉 B → 重连 → 位置/物品/计分从快照恢复一致。

## 八、确定性回归测试

[`DevelopDebug/Editor/DeterminismRegressionTest.cs`](../Assets/Scripts/DevelopDebug/Editor/DeterminismRegressionTest.cs)
——所有场景都在**两个独立模拟世界**间逐帧断言：① 全状态位一致（含帧缓冲进度）② 世界哈希一致（`WorldStateHash.Compute`，与线上 Desync 比对同一实现）。**测试驱动的是生产代码**：输入按帧号 Push 进真实 `FrameBuffer`，再由 `FrameSimulation.StepFrame` 推进（`GameSync.ApplyFrames` 委托的同一份实现）——不是复刻品。四层结构：

1. **脚本化场景**：双人同帧抢物品（冲突裁决）/携带移动/送达/放到地上的确定性语义
2. **fuzz 随机场景 ×5 seed**：随机玩家数(2~5)/物品布局 + 随机指令（10% null 缺口冻结 / 移动 / 交互 / 多命令同帧），共 ~4100 帧
3. **黄金哈希**：seed=12345 场景的终态哈希基准（当前 `0xFFF36A30`）——任何模拟行为变化 → 漂移 → FAIL，防回归
4. **变异自检**（单独菜单）：帧100 注入已知分歧，验证测试确实能抓到（防"永远通过"）

**跨客户端加入顺序模拟**：w2 玩家**反序插入**字典（坐标按名字一致）——模拟"不同客户端玩家加入顺序不同"。已验证（2026-08-04）：临时删除 StepFrame 的 Ordinal 排序 → 脚本化场景帧1 立刻 FAIL（冲突裁决分歧），恢复后 7/7 PASS。**排序被删是测试红的第一现场。**

- 运行：菜单 **Tools → 确定性回归测试（双世界哈希比对）** / **（变异自检）**，或 execute_code 调 `Run()`/`MutationCheck()`
- 输出：默认只输出每场景摘要 + 终态哈希；失败自动输出**分歧帧 + 该帧输入**（可复现定位）；逐帧明细由 `VERBOSE` 开关控制
- 已实测（2026-08-04）：**7/7 PASS**（脚本化 + fuzz×5 + 黄金哈希），变异自检 PASS。**重构后跑一次，同输入 → 同输出有硬证据**
