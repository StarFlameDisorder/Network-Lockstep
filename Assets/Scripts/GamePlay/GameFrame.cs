using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 帧上下文：持有当前帧的玩家集合，统一驱动帧消费。
    /// 追帧逻辑在此处而非 PlayerEntity 内部，由调用方决定推进多少帧。
    /// 
    /// 输入分发由 GameSync（本地输入）和 ReceiveMessage（远程输入）分别完成，
    /// GameFrame 只负责从缓冲区逐帧消费。
    /// </summary>
    public class GameFrame
    {
        public ulong FrameId;
        public Dictionary<string, PlayerEntity> Players;

        public GameFrame(ulong frameId, Dictionary<string, PlayerEntity> players)
        {
            FrameId = frameId;
            Players = players;
        }

        /// <summary>
        /// 推动所有玩家从缓冲区逐帧消费（含追帧逻辑）
        /// </summary>
        public void PushFrames()
        {
            foreach (var player in Players.Values)
            {
                int catchupTarget = 1;
                if (player.FrameCount > GameSync.BufferSize)
                {
                    catchupTarget = Math.Min((int)Math.Sqrt(player.FrameCount), GameSync.MaxCatchupTime);
                    Debug.Log($"[Client][GameFrame] {player.Name}追帧{catchupTarget - 1}");
                }
                
                for (int i = 0; i < catchupTarget; i++)
                {
                    if (!player.TryConsumeNextFrame())
                    {
                        // 缓冲非空但目标帧缺失 = 帧缺口（补发不完整/丢包）：
                        // 正常等待（缓冲为空）不触发，持续卡住时此日志会重复出现，用于定位
                        if (player.FrameCount > 0)
                            Debug.LogWarning($"[Client][GameFrame] {player.Name} 帧缺口: 需帧{player.LastExecutedFrameId + 1} 但缓冲最早为{player.FirstPendingFrameId} (缓冲{player.FrameCount}帧)");
                        break;
                    }
                }
            }
        }
    }
}
