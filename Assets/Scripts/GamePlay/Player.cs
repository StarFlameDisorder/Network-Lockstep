
using System;
using System.Collections.Generic;
using GameMessage;
using Network;
using UnityEngine;
using UnityMath;

namespace GamePlay
{
    public class Player
    {
        GameObject _gameObject;
        FixedPointVector3 _position;
        Queue<PlayerSync> _playerSyncMessgae = new();
        PlayerSnapshotSync _playerSnapshotSync;
        string _name;
        private FixedPoint _gameFrameSpace;
        private FixedPoint _speed;
        UInt64 _preSnapshotFrameId;//上次应用的快照帧id
        UInt64 _preFrameId;//上次执行的帧序号

        public Player(String name,GameObject o,FixedPoint gameFrameSpace,FixedPoint speed)
        {
            _gameObject = o;
            _name = name;
            _gameFrameSpace = gameFrameSpace;
            _speed = speed;
            _position = FixedPointVector3.FromVector3(o.transform.position);
        }
        public FixedPointVector3 GetPosition()
        {
            return _position;
        }

        public int GetFrameCount()
        {
            return _playerSyncMessgae.Count;
        }

        #region 帧同步
        
        public void UpdateFrame()
        {
            int times = 1;
            if (_playerSyncMessgae.Count > GameSync.BufferSize)
            {
                times = Math.Min((int)Math.Sqrt(_playerSyncMessgae.Count), GameSync.MaxCatchupTime);
                
                Debug.Log($"{_name}追帧{times-1}");
            }
            for(int i=0;i<times;i++)
            {
                if (_playerSyncMessgae.Count == 0) return;
                PlayerSync sync = _playerSyncMessgae.Dequeue();

                if (_preFrameId + 1 != sync.FrameId)
                {
                    Debug.LogError($"{_name}: 帧id错误 旧{_preFrameId} 新{sync.FrameId}");
                }

                _preFrameId = sync.FrameId;
                var v = sync.InputMove;
                FixedPointVector3 realV = FixedPointVector3.FromRawValue(v.X, v.Y, v.Z);
                _position += (_speed * _gameFrameSpace * realV);
                _gameObject.transform.position = _position.ToVector3();
            }
        }
        
        public void AddSyncMessage(PlayerSync sync)//应用快照时注意存在有的有的玩家没记录位置
        {
            _playerSyncMessgae.Enqueue(sync);
        }
        
        #endregion
        
        #region 快照
        
        private void ApplySnapShot()//TODO:位置无法应用
        {
            if (_playerSnapshotSync != null)
            {
                Vector3D v3=_playerSnapshotSync.Pos;
                _position = FixedPointVector3.FromRawValue(v3.X, v3.Y, v3.Z);
                _preSnapshotFrameId = _playerSnapshotSync.FrameId;
                _gameObject.transform.position=_position.ToVector3();
                _preFrameId=_playerSnapshotSync.FrameId;
            }
            else
            {
                Debug.LogError("No snapshot sync");
            }
        }
        
        public void SetSnapshotSync(PlayerSnapshotSync sync)
        {
            Debug.Log($"{_name}SetSnapshotSync");
            _playerSnapshotSync = sync;
            ApplySnapShot();
        }

        public PlayerSnapshotSync GetSnapshotSync()
        {
            return new PlayerSnapshotSync
            {
                Name = _name,
                FrameId = _preFrameId,
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