using GameMessage;
using Network;
using UnityMath;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GamePlay
{
    /// <summary>
    /// 物品纯逻辑实体（协作搬运 demo）：出生点/位置/持有者/送达次数。
    /// 与 PlayerEntity 相同模式——不感知帧号/网络/缓冲，由框架（GameSync）在每端确定性模拟；
    /// 所有客户端收到同一份帧输入、执行同一套逻辑，物品状态天然一致。
    /// </summary>
    public class ItemEntity
    {
        private ItemView _view;

        /// <summary>物品 Id（确定性排序/平局裁决用）</summary>
        public readonly int Id;
        /// <summary>出生点（送达后回此位置）</summary>
        public readonly FixedPointVector3 SpawnPos;
        /// <summary>当前逻辑位置</summary>
        public FixedPointVector3 Position;
        /// <summary>当前持有者（空 = 自由，可被拾取）</summary>
        public string Owner = "";
        /// <summary>已送达次数（计分）</summary>
        public int DeliverCount;

        /// <summary>是否自由（无持有者）</summary>
        public bool IsFree => Owner.Length == 0;

        public ItemEntity(int id, FixedPointVector3 spawnPos)
        {
            Id = id;
            SpawnPos = spawnPos;
            Position = spawnPos;
        }

        public void SetView(ItemView view)
        {
            _view = view;
        }

        /// <summary>立即销毁表现层（防快照重建时新旧对象同帧重叠，同 PlayerEntity.Destroy 的考虑）</summary>
        public void Destroy()
        {
            if (_view != null)
            {
                Object.DestroyImmediate(_view.gameObject);
                _view = null;
            }
        }

        /// <summary>拾取（仅自由状态可拾取）：设置持有者。返回是否成功。</summary>
        public bool TryClaim(string playerName)
        {
            if (!IsFree) return false;
            Owner = playerName;
            return true;
        }

        /// <summary>释放持有（玩家离开/快照中持有者已不在房间时归还为自由）</summary>
        public void Release()
        {
            Owner = "";
        }

        /// <summary>送达：次数+1，位置回出生点，恢复自由</summary>
        public void Deliver()
        {
            DeliverCount++;
            Owner = "";
            Position = SpawnPos;
        }

        /// <summary>生成快照（断线重连/中途加入恢复用）</summary>
        public ItemSnapshotSync GetSnapshotSync()
        {
            return new ItemSnapshotSync
            {
                ObjectId = (uint)Id,
                Pos = new Vector3D
                {
                    X = Position.GetRawX(),
                    Y = Position.GetRawY(),
                    Z = Position.GetRawZ()
                },
                Owner = Owner,
                DeliverCount = (uint)DeliverCount
            };
        }

        /// <summary>从快照恢复（位置/持有者/送达次数全量重建）</summary>
        public void SetSnapshotSync(ItemSnapshotSync sync)
        {
            Position = FixedPointVector3.FromRawValue(sync.Pos.X, sync.Pos.Y, sync.Pos.Z);
            Owner = sync.Owner ?? "";
            DeliverCount = (int)sync.DeliverCount;
        }
    }
}
