# Factory

一个基于 **Unity (URP)** 的多人实时 RTS 帧同步网络对战游戏项目。

> **当前方向：RTS 帧同步**。TPS 后续另起项目或单独评估。详见 [Docs/TODO.md](Docs/TODO.md)。

## 项目定位

RTS 多人实时对战游戏的客户端-服务端完整实现，核心玩法为确定性 Lockstep 帧同步网络对战（非 TPS 状态同步）。

## 技术栈

| 层 | 技术 |
|---|---|
| 引擎 | Unity (URP) |
| 输入 | Unity Input System |
| 序列化 | Google Protobuf |
| 客户端网络 | .NET TCP/UDP Socket（传输层抽象，可切换 KCP） |
| 服务端 | Unity C# SubSystemBase（原 C++/Qt 服务端已迁移至此） |
| 确定性数学 | 自实现 FixedPoint 定点数库 |

## 核心架构

- **帧同步 (Lockstep + State Sync)**：客户端通过 UDP 上传输入，服务器收集所有玩家输入后广播回客户端
- **固定逻辑帧率**：30 FPS，配合定点数确保各客户端计算结果一致
- **快照机制**：支持断线重连，重连后可恢复完整游戏状态
- **心跳检测**：监测玩家在线状态
- **大厅/房间系统**：支持创建/加入/离开房间，2 人 / 4 人匹配模式
- **服务端嵌入式**：C++/Qt 服务端已重构为 Unity C# SubSystemBase，支持开发模式单进程运行

## 项目文档

项目相关的文档、问题追踪、TODO 等统一放在 [Docs/](Docs/) 目录下，便于协作查阅。

- [重构方案](Docs/重构方案.md) — 当前架构问题分析、目标架构设计与重构执行计划
- [全面重构方案](Docs/全面重构方案.md) — 断线重连修复 + 网络层重构 + UI/逻辑修复（2026-08-02 规划）

---

## 项目结构

- `Docs/` — 项目文档（重构方案、问题追踪、TODO 等）
- `Server/` — （已废弃）C++/Qt 服务端源码，确认迁移完整后删除
- `Assets/Scripts/Core/` — 游戏核心（GameCore、GameConstants）
- `Assets/Scripts/Framework/` — 框架基础设施（SubSystemBase、Global、DataProxySystem）
- `Assets/Scripts/Network/` — 网络层（待重构为 INetworkTransport 体系）
- `Assets/Scripts/GamePlay/` — 核心游戏逻辑（帧同步、玩家实体、输入处理）
- `Assets/Scripts/UI/` — UI 面板（大厅、连接、状态等）
- `Assets/Protobuf/` — Protobuf 自动生成的 C# 消息类
- `Assets/Prefab/` — 预制体
- `Assets/Art/` — 材质资源
- `Assets/Scenes/` — 场景文件
