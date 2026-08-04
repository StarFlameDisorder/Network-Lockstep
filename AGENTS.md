# AGENTS.md

> 给 AI Agent 的项目速查手册。改动前先读本文件 + [Docs/](Docs/) 下相关文档。
> 本文件供 AI 编码助手使用，配合 .trae/rules/必读.md 一起生效。
> 更新于 2026-08-04。

## 项目定位

Unity (URP) 多人实时 **RTS 帧同步** 对战游戏。**唯一核心目标：网络同步**（Lockstep 确定性帧同步），架构保持简单，不做过度设计。当前含一个**可玩示范玩法**：协作搬运 demo（双人拾取/送达，见 [Docs/协作搬运demo设计.md](Docs/协作搬运demo设计.md)）。

## 技术栈

| 层 | 技术 |
|---|---|
| 引擎 | Unity (URP)，Input System |
| 序列化 | Google Protobuf（`Assets/Protobuf/proto/`，生成物在 `output/`，勿手改） |
| 客户端网络 | .NET TCP（握手/控制）+ KCP（UDP 可靠传输，帧同步/心跳/快照） |
| 服务端 | Unity C# SubSystemBase（原 C++/Qt 服务端已迁移，开发模式单进程内嵌） |
| 确定性数学 | 自实现 FixedPoint 定点数（逻辑内禁 float） |

## 核心架构（分层模型）

```
输入层   GamePlay.PlayerController  原始输入 → 语义命令（EnqueueCommand）
框架层   FrameSync.GameSync        主控：发送输入/接收广播/喂帧调度/快照/重连恢复
         FrameSync.FrameBuffer     每玩家帧缓冲（Push/消费/跳帧容错/追帧）
游戏逻辑 GamePlay.PlayerEntity     纯确定性模拟 Simulate(FrameInput)，不感知帧号/网络
表现层   GamePlay.PlayerView       MonoBehaviour，每帧读 entity.Position → transform
```

- 一句话：**框架决定"喂哪一帧"，实体只回答"这一帧我该干什么"**。
- 依赖方向单向：框架持有逻辑实体，实体不依赖框架。
- 帧号由**服务端统一分配**（严格 Lockstep：等所有在线玩家提交输入才广播完整帧）。
- 确定性：位置用 FixedPoint；实体内禁用 `Time.time`/`UnityEngine.Random`/浮点比较。

## 关键机制速查

- **缺口/离线**：缓冲无下一帧 → 喂 null → 实体冻结。
- **跳帧容错**：目标帧缺失但有更晚帧 → 跳到最早可用帧（补发场景）。
- **追帧**：缓冲帧数 > 3 时每帧多消费 `min(IntSqrt(缓冲数), 5)` 帧。
- **断线重连**：客户端收包超时 3s 检测 + TCP/KCP 双通道统一状态；服务端统一广播权威快照 + 全量补帧（所有客户端从同一快照点重置，根治位置漂移）。
- **Desync 检测**：客户端每 30 帧上报世界哈希 → 服务端跨客户端比对 → 不一致广播 DesyncNoticeMessage。

## 关键类

| 类 | 归属层 | 职责 |
|---|---|---|
| `GameSync` | FrameSync | 帧同步主控（房间/输入发送/快照/调度/重连恢复） |
| `FrameBuffer` | FrameSync | 每玩家帧缓冲（Push/消费/跳帧/追帧） |
| `FrameSimulation` | FrameSync | 确定性模拟核心 StepFrame（排序/追帧/缺口语义；GameSync 与回归测试共用同一份） |
| `FrameInput` / `InputCommand` | FrameSync | 一帧输入 = 命令列表；命令类型 MoveDirection/MoveTo/Interact |
| `PlayerEntity` | GamePlay | 纯确定性模拟（Simulate + 快照恢复/上报） |
| `ItemEntity` / `CargoConfig` | GamePlay | 协作搬运 demo：物品纯逻辑实体 / 世界常量（改布局改 CargoConfig） |
| `PlayerView` | GamePlay | 表现层（读 Position → transform） |
| `PlayerController` | GamePlay | 输入层（语义命令） |
| `GameServer` / `RoomManager` | Network/Server | 服务端主控 / 房间与帧广播 |
| `GameClient` / `KcpClient` | Network/Client | 客户端网络 |

## 约定与规则（必须遵守）

1. **动手前先读** README.md + Docs/ 相关文档；完成任务后更新文档（TODO.md 等），文档保持简洁。
2. **Debug.Log 前缀**必须标明 `[Server]` 或 `[Client]` + 所属类名。
3. **注释**：别删任何可读性注释（类/方法/属性注释）；空注释可删。
4. **git**：完成改动后提醒用户提交（用户手动提交），别攒一大堆。
5. **多提问**：不确定（如是否更新文档）时先问。
6. **架构**：仅实现网络同步核心目标，不要搭大架构。
7. **unity mcp**：仅执行 Unity 相关代码；沙盒不允许的代码必须让用户审核后执行。
8. **重要架构和功能写到文档里**（如 帧同步框架架构设计.md）。

## 如何验证改动

1. 改完 C# 后，确认 Unity 编译无报错（IDE 诊断/打开编辑器看 Console）。
2. **确定性回归测试**：菜单 Tools → 确定性回归测试（双世界哈希比对），或 unity MCP execute_code 调 `DevelopDebug.DeterminismRegressionTest.Run()`，必须 PASS。
3. 双开实测：编辑器实例 A + 打包实例 B（服务端内嵌 A）。
4. 验收标准：两画面帧号/位置/世界哈希一致 → 断线重连后状态恢复一致。

## 文档索引（Docs/）

- `TODO.md` — 阶段状态、问题备忘录、已落地明细（**先看这个**）
- `帧同步框架架构设计.md` — 分层模型 + 核心类 + 关键机制 + 扩展点
- `协作搬运demo设计.md` — 可玩示范玩法：确定性设计 + 新玩法接入框架的示范
- `全面重构方案.md` / `重构方案.md` / `服务器重构.md` — 历史重构规划
- `ToAgent.md` — 建议/工作流意见收集

## 常见操作

- **改协议**：改 `Assets/Protobuf/proto/*.proto` → 运行 `Protobuf/proto.bat` 重新生成 C#（脚本依赖同目录 `Protobuf/protoc.exe`；本机 PowerShell 拦截 shell，需经授权用 unity MCP execute_code 跑）→ 改 `CommandType`/`InputCommand`/`GameSync` 映射 case → `PlayerEntity.Simulate` 加 case。
- **加新操作命令**（攻击/建造/技能）：同上流程，proto `Command` oneof 加分支即可。
