
// [废弃] 旧"帧上下文+帧执行器"类，已被框架层 FrameBuffer（缓冲/帧号/缺口）+ GameSync.ApplyFrames（调度/追帧）替代。
// 职责拆分背景：旧类名为"帧"但实为一次性推进器（构造传入的 FrameId 从未使用），
// 且与 proto 的 GameMessage.GameFrame 同名不同类，易混淆。
// 无任何引用，保留仅供参考，待确认后删除。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay
{
    public class GameFrame
    {
    }
}
