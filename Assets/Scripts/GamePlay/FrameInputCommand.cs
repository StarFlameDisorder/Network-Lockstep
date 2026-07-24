using GameMessage;
using Network;
using UnityMath;

namespace GamePlay
{
    /// <summary>
    /// 弃用
    /// 帧输入命令：把玩家操作封装为确定帧号的命令，替代隐式 _input 缓存
    /// </summary>
    public struct FrameInputCommand
    {
        public ulong FrameId;
        public string PlayerName;
        public FixedPointVector3 MoveDirection;

        public PlayerSync ToPlayerSync()
        {
            return new PlayerSync
            {
                FrameId = FrameId,
                Name = PlayerName,
                InputMove = new Vector3D
                {
                    X = MoveDirection.GetRawX(),
                    Y = MoveDirection.GetRawY(),
                    Z = MoveDirection.GetRawZ()
                }
            };
        }
    }
}
