
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

        public Player(String name,GameObject o,FixedPoint gameFrameSpace,FixedPoint speed)
        {
            _gameObject = o;
            _name = name;
            _gameFrameSpace = gameFrameSpace;
            _speed = speed;
            _position = FixedPointVector3.FromVector3(o.transform.position);
        }

        public void UpdateFrame()
        {
            if(_playerSyncMessgae.Count== 0)return;
            PlayerSync sync = _playerSyncMessgae.Dequeue();
            var v = sync.InputMove;
            FixedPointVector3 realV = FixedPointVector3.FromRawValue(v.X,v.Y,v.Z);
            _position += (_speed * _gameFrameSpace * realV);
            _gameObject.transform.position=_position.ToVector3();
        }

        public FixedPointVector3 GetPosition()
        {
            return _position;
        }

        public int GetFrameCount()
        {
            return _playerSyncMessgae.Count;
        }

        public void ApplySnapShot()
        {
            if (_playerSnapshotSync != null)
            {
                Vector3D v3=_playerSnapshotSync.Pos;
                _position = FixedPointVector3.FromRawValue(v3.X, v3.Y, v3.Z);
                _preSnapshotFrameId = _playerSnapshotSync.FrameId;
                _gameObject.transform.position=_position.ToVector3();
            }
            else
            {
                Debug.LogError("No snapshot sync");
            }
        }
        
        public void AddSyncMessage(PlayerSync sync)
        {
            _playerSyncMessgae.Enqueue(sync);
        }
        
        public void SetSnapshotSync(PlayerSnapshotSync sync)
        {
            _playerSnapshotSync = sync;
        }

        public PlayerSnapshotSync GetSnapshotSync(UInt64 frameId)
        {
            return new PlayerSnapshotSync
            {
                Name = _name,
                FrameId = frameId,
                Pos = new Vector3D
                {
                    X = _position.GetRawX(),
                    Y = _position.GetRawY(),
                    Z = _position.GetRawZ()
                }
            };
        }
        
    }
}