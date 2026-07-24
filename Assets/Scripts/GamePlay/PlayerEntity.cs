using System;
using System.Collections.Generic;
using GameMessage;
using Network;
using UnityEngine;
using UnityMath;

namespace GamePlay
{
    /// <summary>
    /// 玩家纯逻辑实体（与 Unity GameObject 解耦，可独立测试）
    /// 持有定点数位置、帧缓冲区和快照数据
    /// </summary>
    public class PlayerEntity
    {
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
                return false;
            
            _pendingFrames.Remove(nextFrameId);
            _preFrameId = sync.FrameId;
            
            var v = sync.InputMove;
            FixedPointVector3 realV = FixedPointVector3.FromRawValue(v.X, v.Y, v.Z);
            _position += (_speed * _gameFrameSpace * realV);
            // Debug.Log("[Debug][PlayerEntity]Position: " + _position.ToVector3());
            Debug.Log($"[Client][PlayerEntity] {_name}执行第{sync.FrameId}帧");
            return true;
        }
        
        /// <summary>
        /// 添加一帧的同步数据到缓冲区
        /// </summary>
        public void AddSyncMessage(PlayerSync sync)
        {
            _pendingFrames.Add(sync.FrameId, sync);
        }
        
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
