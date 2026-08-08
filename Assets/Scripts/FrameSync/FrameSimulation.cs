using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using GamePlay;

namespace FrameSync
{
    /// <summary>
    /// 确定性模拟核心：推进"一帧世界"的调度逻辑（框架与确定性回归测试共用同一份代码）。
    /// 保证回归测试测的是生产代码（GameSync.ApplyFrames 委托本类）而不是复刻品——
    /// 排序/追帧/缺口语义只实现一次，测试与线上永远一致。
    /// </summary>
    public static class FrameSimulation
    {
        /// <summary>
        /// 推进一帧：按玩家名 Ordinal 排序迭代（冲突裁决顺序各端一致），
        /// 从每玩家缓冲取帧喂给实体 Simulate；缓冲超阈值时追帧；缺口/离线喂 null 由实体冻结。
        /// </summary>
        /// <param name="players">玩家实体表</param>
        /// <param name="buffers">每玩家帧缓冲表</param>
        /// <param name="items">共享物品世界</param>
        /// <param name="catchupLog">追帧回调（默认不打，防刷屏）</param>
        /// <param name="gapLog">帧缺口回调（异常缺口定位用）</param>
        public static void StepFrame(
            IReadOnlyDictionary<string, PlayerEntity> players,
            IReadOnlyDictionary<string, FrameBuffer> buffers,
            List<ItemEntity> items,
            Action<string> catchupLog = null,
            Action<string, FrameBuffer> gapLog = null)
        {
            // Ordinal 排序保证跨端一致（默认字符串比较受文化影响，不可用于确定性）
            foreach (var name in players.Keys.OrderBy(n => n, StringComparer.Ordinal))
            {
                var entity = players[name];
                var buffer = buffers[name];

                int catchupTarget = 1;
                if (buffer.Count > GameConstants.BUFFER_SIZE)
                {
                    catchupTarget = Math.Min(IntSqrt(buffer.Count), GameConstants.MAX_CATCHUP);
                    catchupLog?.Invoke(name);
                }

                for (int i = 0; i < catchupTarget; i++)
                {
                    FrameInput input = buffer.TryPopNextFrame();
                    if (input == null)
                    {
                        // 缓冲非空但无可用帧 = 异常缺口（正常等待时缓冲为空不触发），
                        // 持续卡住时此日志会重复出现，用于定位
                        if (buffer.Count > 0)
                            gapLog?.Invoke(name, buffer);
                        break;
                    }
                    entity.Simulate(input, items);
                }
            }
        }

        /// <summary>整数开方（避免 Math.Sqrt(double) 在完全平方数上的浮点误差取小）</summary>
        private static int IntSqrt(int n)
        {
            if (n <= 1) return n;
            int x = n, y = (x + 1) / 2;
            while (y < x) { x = y; y = (x + n / x) / 2; }
            return x;
        }
    }
}
