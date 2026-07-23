
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
        //Queue<PlayerSync> _playerSyncMessage = new();
        Dictionary<UInt64,PlayerSync> _playerSyncMessage = new();
        
        PlayerSnapshotSync _playerSnapshotSync;
        string _name;
        private FixedPoint _gameFrameSpace;
        private FixedPoint _speed;
        UInt64 _preSnapshotFrameId;//上次应用的快照的帧id
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
            return _playerSyncMessage.Count;
        }

        #region 帧同步
        
        public void UpdateFrame()
        {
            int times = 1;
            if (_playerSyncMessage.Count > GameSync.BufferSize)
            {
                times = Math.Min((int)Math.Sqrt(_playerSyncMessage.Count), GameSync.MaxCatchupTime);
                
                Debug.Log($"[Client][Player] {_name}追帧{times-1}");
            }
            
            for(int i=0;i<times;i++)
            {
                if (_playerSyncMessage.Count == 0) return;
                if (!_playerSyncMessage.ContainsKey(_preFrameId + 1))
                {
                    Debug.LogWarning($"[Client][Player] {_name}:无第{_preFrameId+1}帧");
                    return;
                }
                
                PlayerSync sync = _playerSyncMessage[_preFrameId+1];
                _playerSyncMessage.Remove(_preFrameId + 1);
                
                Debug.Log($"[Client][Player] {_name}执行第{sync.FrameId}帧");
                _preFrameId = sync.FrameId;
                var v = sync.InputMove;
                FixedPointVector3 realV = FixedPointVector3.FromRawValue(v.X, v.Y, v.Z);
                _position += (_speed * _gameFrameSpace * realV);
                _gameObject.transform.position = _position.ToVector3();
            }
        }
        
        public void AddSyncMessage(PlayerSync sync)//应用快照时注意存在有的有的玩家没记录位置
        {
            //_playerSyncMessage.Enqueue(sync);
            _playerSyncMessage.Add(sync.FrameId,sync);
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
                Debug.Log($"[Client][Player] {_name}恢复快照时最新执行到{_preFrameId}");
            }
            else
            {
                Debug.LogError("[Client][Player] No snapshot sync");
            }
        }
        
        public void SetSnapshotSync(PlayerSnapshotSync sync)
        {
            Debug.Log($"[Client][Player] {_name}SetSnapshotSync");
            _playerSnapshotSync = sync;
            ApplySnapShot();
        }

        public PlayerSnapshotSync GetSnapshotSync()
        {
            Debug.Log($"[Client][Player] {_name}记录快照时最新执行到{_preFrameId}");
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