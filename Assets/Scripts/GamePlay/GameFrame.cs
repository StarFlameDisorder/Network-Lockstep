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
                        // Debug.LogWarning($"[Client][GameFrame] {player.Name}:无第{player.LastExecutedFrameId + 1}帧");
                        break;
                    }
                }
            }
        }
    }
}
