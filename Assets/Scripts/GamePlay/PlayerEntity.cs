using System;
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
        }

        /// <summary>
        /// 执行本帧输入（确定性模拟）。input 为 null = 缺口/离线 → 冻结（不移动）。
        /// 实体只消费框架喂给的帧数据，不关心是第几帧。
        /// </summary>
        public void Simulate(FrameInput input)
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
                }
            }
            // Debug.Log("[Debug][PlayerEntity]Position: " + _position.ToVector3());
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
