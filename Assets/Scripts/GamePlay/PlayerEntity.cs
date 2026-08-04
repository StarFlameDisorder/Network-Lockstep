using System;
using System.Collections.Generic;
using FrameSync;
using GameMessage;
using Network;
using UnityEngine;
using UnityMath;
using Object = UnityEngine.Object;

namespace GamePlay
{
    /// <summary>
    /// 玩家纯逻辑实体（与 Unity GameObject 解耦，可独立测试）。
    /// 只处理自己的确定性模拟数据（位置等），不感知帧号/网络/缓冲——
    /// 缓冲、帧号、缺口/追帧等框架职责由 GameSync + FrameBuffer 承担。
    /// </summary>
    public class PlayerEntity
    {
        PlayerView _view;
        
        FixedPointVector3 _position;
        string _name;
        private FixedPoint _gameFrameSpace;
        private FixedPoint _speed;

        /// <summary>当前携带的物品（协作搬运 demo；null = 未携带）</summary>
        ItemEntity _carriedItem;

        public FixedPointVector3 Position => _position;
        public string Name => _name;

        public PlayerEntity(String name, FixedPointVector3 startPos, FixedPoint gameFrameSpace, FixedPoint speed)
        {
            _name = name;
            _gameFrameSpace = gameFrameSpace;
            _speed = speed;
            _position = startPos;
        }

        public void SetView(PlayerView view)
        {
            _view = view;
        }

        public void Destroy()
        {
            // 立即销毁表现层（而非 Object.Destroy 延迟到帧末）：
            // 防止重建快照时旧对象未销毁、新对象已创建，导致同一玩家出现多个对象
            if (_view != null)
            {
                Object.DestroyImmediate(_view.gameObject);
                _view = null;
            }

            // 离开房间/断线销毁时，释放携带的物品（避免物品永远被"持有"卡死）
            if (_carriedItem != null)
            {
                _carriedItem.Release();
                _carriedItem = null;
            }
        }

        /// <summary>
        /// 快照恢复后重建携带关系（重连/中途加入时物品快照里有持有者，需重新挂到玩家身上）
        /// </summary>
        public void RestoreCarriedItem(ItemEntity item)
        {
            _carriedItem = item;
        }

        /// <summary>
        /// 执行本帧输入（确定性模拟）。input 为 null = 缺口/离线 → 冻结（不移动）。
        /// 实体只消费框架喂给的帧数据，不关心是第几帧。
        /// items = 共享物品世界（协作搬运 demo；实体按同一帧输入确定性操作它们）。
        /// </summary>
        public void Simulate(FrameInput input, List<ItemEntity> items)
        {
            if (input == null) return;

            foreach (var cmd in input.Commands)
            {
                switch (cmd.Type)
                {
                    case CommandType.MoveDirection:
                        _position += (_speed * _gameFrameSpace * cmd.MoveDirection);
                        break;
                    case CommandType.MoveTo:
                        // TODO: 未来实现目标点移动（RTS 正式移动方式，需寻路/到达判定）
                        break;
                    case CommandType.Interact:
                        InteractWithItems(items);
                        break;
                }
            }

            // 携带中：物品跟随玩家（确定性位置偏移，各端一致）
            if (_carriedItem != null)
            {
                _carriedItem.Position = _position + CargoConfig.CarryOffset;
            }
            // Debug.Log("[Debug][PlayerEntity]Position: " + _position.ToVector3());
        }

        /// <summary>
        /// 上下文交互（协作搬运 demo）：携带中靠近火车 → 送达；空闲时靠近自由物品 → 拾取最近者。
        /// 所有客户端收到同一帧输入、按同一顺序执行，结果必然一致；
        /// 冲突裁决（两个玩家同帧抢同一物品）由框架层 ApplyFrames 按玩家名排序保证先后顺序。
        /// </summary>
        private void InteractWithItems(List<ItemEntity> items)
        {
            // 携带中：靠近火车 → 送达
            if (_carriedItem != null)
            {
                if (CargoConfig.IsNearTrain(_position))
                {
                    _carriedItem.Deliver();
                    _carriedItem = null;
                    Debug.Log($"[Client][PlayerEntity] {_name} 送达物品");
                }
                // 未靠近火车：继续携带（忽略本次交互）
                return;
            }

            // 空闲：找最近的自由物品（XZ 距离平方比较；平局取 Id 小者——列表顺序，保证各端一致）
            ItemEntity best = null;
            long bestDistSq = long.MaxValue;
            foreach (var item in items)
            {
                if (!item.IsFree) continue;
                long distSq = CargoConfig.SqrDistXZ(_position, item.Position);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = item;
                }
            }

            // 距离内才可拾取
            if (best != null && bestDistSq <= CargoConfig.PickupRadiusSqRaw)
            {
                if (best.TryClaim(_name))
                {
                    _carriedItem = best;
                    Debug.Log($"[Client][PlayerEntity] {_name} 拾取物品 {best.Id}");
                }
            }
        }
        
        #region 快照
        
        /// <summary>
        /// 恢复快照位置（断线重连/中途加入）。帧号/缓冲恢复由框架层（GameSync/FrameBuffer）负责
        /// </summary>
        public void SetSnapshotSync(PlayerSnapshotSync sync)
        {
            Vector3D v3 = sync.Pos;
            _position = FixedPointVector3.FromRawValue(v3.X, v3.Y, v3.Z);
            Debug.Log($"[Client][PlayerEntity] {_name}恢复快照位置");
        }

        /// <summary>
        /// 生成当前玩家的快照数据。帧号由框架层传入（实体不感知执行进度）
        /// </summary>
        public PlayerSnapshotSync GetSnapshotSync(UInt64 lastExecutedFrameId)
        {
            return new PlayerSnapshotSync
            {
                Name = _name,
                FrameId = lastExecutedFrameId,
                LastFrameId = lastExecutedFrameId,
                Pos = new Vector3D
                {
                    X = _position.GetRawX(),
                    Y = _position.GetRawY(),
                    Z = _position.GetRawZ()
                }
            };
        }
        
        #endregion
        
    }
}
