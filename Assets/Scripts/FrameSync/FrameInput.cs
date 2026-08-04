using System.Collections.Generic;
using Network;

namespace FrameSync
{
    /// <summary>
    /// 命令类型：语义化操作命令（区别于原始按键流）。
    /// 扩展新操作（Attack/Build/技能等）时在此增加枚举值 + 对应参数字段。
    /// </summary>
    public enum CommandType
    {
        /// <summary>持续方向移动（键盘直接操纵，调试方便；每帧重发方向）</summary>
        MoveDirection,
        /// <summary>移动到目标点（未来 RTS 正式移动方式，预留；本帧实现留 TODO）</summary>
        MoveTo,
        /// <summary>上下文交互（协作搬运 demo：拾取/放下；无参数，由 Simulate 按位置解析）</summary>
        Interact,
    }

    /// <summary>
    /// 一条确定性命令：命令类型 + 对应参数。
    /// 由客户端输入层（PlayerController）产生，经服务端广播后由实体 Simulate 确定性执行。
    /// </summary>
    public struct InputCommand
    {
        public CommandType Type;
        /// <summary>MoveDirection 参数：移动方向</summary>
        public FixedPointVector3 MoveDirection;
        /// <summary>MoveTo 参数：目标点（预留）</summary>
        public FixedPointVector3 MoveToTarget;
    }

    /// <summary>
    /// 一帧的玩家输入 = 本帧命令列表（可同时下达多条命令，如移动+攻击）。
    /// 帧数据（是谁的、第几帧）由框架层 FrameBuffer 管理，实体只消费本对象。
    /// null 表示该帧无输入（缺口/离线），由实体自行决定冻结语义。
    /// </summary>
    public class FrameInput
    {
        public List<InputCommand> Commands { get; } = new();
    }
}
