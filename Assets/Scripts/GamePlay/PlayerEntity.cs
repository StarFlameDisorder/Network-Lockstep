using System;
using System.Collections.Generic;
using System.Linq;
using GameMessage;
using Network;
using Unity.VisualScripting;
using UnityEngine;
using UnityMath;
using Object = UnityEngine.Object;

namespace GamePlay
{
    /// <summary>
    /// 玩家纯逻辑实体（与 Unity GameObject 解耦，可独立测试）
    /// 持有定点数位置、帧缓冲区和快照数据
    /// </summary>
    public class PlayerEntity
    {
        PlayerView _view;
        
        FixedPointVector3 _position;
        SortedDictionary<UInt64, PlayerSync> _pendingFrames = new();
        
        PlayerSnapshotSync _playerSnapshotSync;
        string _name;
        private FixedPoint _gameFrameSpace;
        private FixedPoint _speed;
        UInt64 _preSnapshotFrameId;//上次应用的快照的帧id
        UInt64 _preFrameId;//上次执行的帧序号

        public FixedPointVector3 Position => _position;
        public string Name => _name;
        public int FrameCount => _pendingFrames.Count;
        /// <summary>上次执行的帧序号，GameFrame 用于判断下一帧是否存在</summary>
        public UInt64 LastExecutedFrameId => _preFrameId;

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

        #region 帧同步
        
        /// <summary>
        /// 尝试消费下一帧（_preFrameId + 1）。
        /// 由 GameFrame.ApplyAll 驱动，每次调用只处理一帧。
        /// </summary>
        /// <returns>true 表示成功消费一帧</returns>
        public bool TryConsumeNextFrame()
        {
            UInt64 nextFrameId = _preFrameId + 1;
            if (!_pendingFrames.TryGetValue(nextFrameId, out var sync))
            {
                // 帧缺口容错：目标帧缺失但缓冲内有更晚的帧（补发时离线/停滞玩家的帧本就不存在），
                // 跳到最早可用帧继续。缺口期间该玩家保持冻结，符合"离线期不移动"的语义；
                // 所有客户端收到同一份补发，跳帧一致，不破坏确定性。
                if (_pendingFrames.Count > 0)
                {
                    UInt64 first = _pendingFrames.Keys.First();
                    if (first > nextFrameId)
                    {
                        Debug.LogWarning($"[Client][PlayerEntity] {_name} 帧缺口跳帧: 缺{nextFrameId} 跳至{first} (跳过{first - nextFrameId}帧)");
                        _preFrameId = first - 1;
                        return TryConsumeNextFrame();
                    }
                }
                return false;
            }
            
            _pendingFrames.Remove(nextFrameId);
            _preFrameId = sync.FrameId;
            
            var v = sync.InputMove;
            FixedPointVector3 realV = FixedPointVector3.FromRawValue(v.X, v.Y, v.Z);
            _position += (_speed * _gameFrameSpace * realV);
            // Debug.Log("[Debug][PlayerEntity]Position: " + _position.ToVector3());
            // Debug.Log($"[Client][PlayerEntity] {_name}执行第{sync.FrameId}帧");
            return true;
        }
        
        /// <summary>
        /// 添加一帧的同步数据到缓冲区
        /// </summary>
        public void AddSyncMessage(PlayerSync sync)
        {
            // 容错：重复帧直接忽略（补发与实时广播在极端时序下可能重叠），
            // 避免 SortedDictionary.Add 抛异常导致整个补发包中断、产生帧缺口
            if (!_pendingFrames.ContainsKey(sync.FrameId))
                _pendingFrames.Add(sync.FrameId, sync);
        }
        
        /// <summary>
        /// 缓冲区中最早的帧号（用于诊断帧缺口；空缓冲返回 0）
        /// </summary>
        public UInt64 FirstPendingFrameId => _pendingFrames.Count > 0 ? _pendingFrames.Keys.First() : 0;
        
        #endregion
        
        #region 快照
        
        private void ApplySnapShot()
        {
            if (_playerSnapshotSync != null)
            {
                Vector3D v3 = _playerSnapshotSync.Pos;
                _position = FixedPointVector3.FromRawValue(v3.X, v3.Y, v3.Z);
                _preSnapshotFrameId = _playerSnapshotSync.LastFrameId;
                _preFrameId = _playerSnapshotSync.LastFrameId;
                // 清除旧帧缓冲区，以快照帧号为起点重新开始
                _pendingFrames.Clear();
                
                Debug.Log($"[Client][PlayerEntity] {_name}恢复快照时最新执行到{_preFrameId}");
            }
            else
            {
                Debug.LogError("[Client][PlayerEntity] No snapshot sync");
            }
        }
        
        public void SetSnapshotSync(PlayerSnapshotSync sync)
        {
            Debug.Log($"[Client][PlayerEntity] {_name}SetSnapshotSync");
            _playerSnapshotSync = sync;
            ApplySnapShot();
        }

        /// <summary>
        /// 生成当前玩家的快照数据
        /// </summary>
        public PlayerSnapshotSync GetSnapshotSync()
        {
            Debug.Log($"[Client][PlayerEntity] {_name}记录快照时最新执行到{_preFrameId}");
            return new PlayerSnapshotSync
            {
                Name = _name,
                FrameId = _preFrameId,
                LastFrameId = _preFrameId,
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
