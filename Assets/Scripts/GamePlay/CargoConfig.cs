using Network;

namespace GamePlay
{
    /// <summary>
    /// 协作搬运 demo 世界配置（确定性常量，各端一致）。
    /// 所有客户端读同一组代码常量 → 物品/火车布局天然一致，不依赖场景摆放。
    /// 改这里 = 改玩法布局（物品位置/数量、火车位置、判定半径）。
    /// </summary>
    public static class CargoConfig
    {
        /// <summary>火车（送达区）位置</summary>
        public static readonly FixedPointVector3 TrainPos = FixedPointVector3.FromFloat(4f, 0f, 0f);
        /// <summary>送达判定半径（携带者进入此范围按 E 即送达）</summary>
        public static readonly FixedPoint TrainRadius = FixedPoint.FromFloat(1.8f);
        /// <summary>拾取判定半径（空闲玩家在此范围内按 E 拾取最近物品）</summary>
        public static readonly FixedPoint PickupRadius = FixedPoint.FromFloat(1.2f);
        /// <summary>携带时物品相对玩家的位置偏移</summary>
        public static readonly FixedPointVector3 CarryOffset = FixedPointVector3.FromFloat(0f, 0.8f, 0f);

        /// <summary>物品出生点（3 个；代码常量而非场景摆放，保证各端一致）</summary>
        public static readonly FixedPointVector3[] ItemSpawnPos =
        {
            FixedPointVector3.FromFloat(0f, 0.5f, 0f),
            FixedPointVector3.FromFloat(-2f, 0.5f, 2f),
            FixedPointVector3.FromFloat(-2f, 0.5f, -2f),
        };

        /// <summary>送达目标总次数（= 物品数 × 每物品需送达次数；用于计分/完成判定）</summary>
        public static int DeliverTarget => ItemSpawnPos.Length * 2;

        /// <summary>
        /// XZ 平面距离平方（纯整数运算，确定性；忽略 y，避免高度差干扰判定）
        /// </summary>
        public static long SqrDistXZ(FixedPointVector3 a, FixedPointVector3 b)
        {
            long dx = (long)a.GetRawX() - b.GetRawX();
            long dz = (long)a.GetRawZ() - b.GetRawZ();
            return dx * dx + dz * dz;
        }

        /// <summary>是否在火车送达区内（XZ 距离平方 &lt; 半径平方）</summary>
        public static bool IsNearTrain(FixedPointVector3 pos)
        {
            long r = TrainRadius.GetRawValue();
            return SqrDistXZ(pos, TrainPos) < r * r;
        }

        /// <summary>拾取半径平方（原始值平方；用 long 防溢出，Q16.16 下 1.2² 已超 int 范围）</summary>
        public static long PickupRadiusSqRaw
        {
            get
            {
                long r = PickupRadius.GetRawValue();
                return r * r;
            }
        }
    }
}
