# TODO

> 更新于 2026-07-23 | 方向确认：**RTS 帧同步联机**（暂不做 TPS）

## 当前状态

重构处于**第二步完成（代码编写）、待联调验证**阶段：

| 步骤 | 描述 | 状态 |
|---|---|---|
| 第一步 | 基础骨架 (Global/GameCore/SystemManager) | ✅ 已完成 |
| 第二步 | 服务端迁移 C++/Qt → C# SubSystemBase | ✅ 代码完成，⏳ 待联调 |
| 第三步 | KCP 替换手写可靠UDP层 🔴 最高优 | ❌ 未开始 |
| 第四步 | 客户端网络层重构 (INetworkTransport + MessageBus) | ❌ 未开始 |
| 第五步 | 适配与集成（替换旧 Instance 调用） | ❌ 未开始 |
| — | 代码风格清理（Debug.Log前缀/命名/拼写） | ✅ 已完成（2026-07-23） |

---

## 下一步：联调验证第二步

**目标**：Unity Editor 一键 Play，客户端连接同进程 GameServer，走通完整流程。

- [ ] 在场景中放置 GameCore 预制体（含 SystemManager + GameServer）
- [ ] ServerDebugPanel 启动 GameServer
- [ ] ClientDebugPanel 连接本地服务器
- [ ] 验证 TCP 握手 / UDP 帧同步 / 心跳 / 快照断线重连

---

## 第三步：KCP 替换手写可靠UDP层 🔴 最高优先级

当前 UDP 之上手写 SEQ/ACK + 重排序 + 指数退避重传——本质是重新发明 TCP。

- 客户端 [UdpSocket.cs](Assets/Scripts/Network/Client/UdpSocket.cs) 和服务端 [UdpServer.cs](Assets/Scripts/Network/Server/UdpServer.cs) 各实现一套，线程不安全、缓冲区硬编码 600 字节
- KCP 是成熟的游戏级可靠 UDP 协议，自动处理 SEQ/ACK/重传/排序/流控

### 执行

1. 引入 KCP C# 库
2. 服务端用 KCP 替换 UdpServer 的可靠层（Udp 退化为不可靠通道）
3. 客户端用 KCP 替换 UdpSocket 的可靠层，删除 PendingPacket/_pendingPackets/_receiveBuf 等
4. KCP tick 驱动接入 TimerHandle 或 Unity Update

### 影响

- 删除 ~350 行手动可靠协议代码（客户端+服务端）
- 新增 KcpTransport.cs + KcpServer.cs

---

## 第四步：客户端网络层重构

前提：KCP 替换完成。
内容：定义 INetworkTransport → MessageBus 替代 MessageDispatcher → 清理旧 NetworkManager。

---

## 第五步：适配与集成

替换散落的 `NetworkManager.Instance` / `GameSync.Instance` 为 `Global.Get<T>()`。

---

## RTS vs TPS 决策

**已确定**：优先做 RTS 帧同步。当前 Lockstep + 定点数架构天然适配。

---

## 代码风格清理 ✅ 已完成

| 问题 | 修复量 |
|---|---|
| Debug.Log 前缀 `[Client]/[Server]` | ~80 处 |
| 拼写错误 `Messgae`/`tranformation` | 14 处引用 |
| 空 XML 注释删除 | 2 处 |
| 字段命名 `_camelCase` 统一 | ~15 字段（EventBus/NetworkManager/MessagePanel/ControlButton/MonoSingleton/NetworkPanel）|

### 待后续处理（随相关文件重构时清理）

| 问题 | 数量 |
|---|---|
| 注释掉的代码块 | 16 处 |
| TODO 待跟进 | 6 处 |
| 中英文混用日志 | 大量 |
