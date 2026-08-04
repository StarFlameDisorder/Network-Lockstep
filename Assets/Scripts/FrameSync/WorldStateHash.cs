using System.Collections.Generic;
using GamePlay;

namespace FrameSync
{
    /// <summary>
    /// 世界状态哈希计算：所有玩家与物品的位置原始值 XOR（Q16.16 位模式，确定性）。
    /// 客户端哈希上报/Desync 检测与确定性回归测试共用此实现——
    /// 保证"同一哈希 = 同一世界状态"，测试断言的就是线上比对的那个值。
    /// </summary>
    public static class WorldStateHash
    {
        public static uint Compute(IReadOnlyDictionary<string, PlayerEntity> players, IReadOnlyList<ItemEntity> items)
        {
            int hash = 0;
            foreach (var kv in players)
            {
                var pos = kv.Value.Position;
                hash ^= pos.GetRawX();
                hash ^= pos.GetRawY();
                hash ^= pos.GetRawZ();
            }
            foreach (var item in items)
            {
                hash ^= item.Position.GetRawX();
                hash ^= item.Position.GetRawY();
                hash ^= item.Position.GetRawZ();
            }
            return unchecked((uint)hash);
        }
    }
}
