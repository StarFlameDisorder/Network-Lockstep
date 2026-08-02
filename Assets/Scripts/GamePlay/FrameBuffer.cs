using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlay
{
    /// <summary>
    /// 玩家帧缓冲（框架层）：按帧号暂存服务端广播的输入帧，供调度器按序消费。
    /// 归属框架职责（缓冲/帧号/缺口/追帧），与游戏逻辑完全解耦——实体不感知本类。
    /// </summary>
    public class FrameBuffer
    {
        readonly SortedDictionary<UInt64, FrameInput> _frames = new();
        /// <summary>下一帧要消费的帧号（框架维护的"执行进度"）</summary>
        UInt64 _nextFrameId = 1;

        /// <summary>缓冲帧数（供追帧/调试面板使用）</summary>
        public int Count => _frames.Count;
        /// <summary>下一帧号（= 上次执行帧号 + 1；快照上报用）</summary>
        public UInt64 NextFrameId => _nextFrameId;
        /// <summary>缓冲中最早的帧号（用于诊断帧缺口；空缓冲返回 0）</summary>
        public UInt64 MinFrameId => _frames.Count > 0 ? _frames.Keys.First() : 0;
        /// <summary>上次执行到的帧号（快照上报恢复点；未执行过返回 0）</summary>
        public UInt64 LastExecutedFrameId => _nextFrameId > 0 ? _nextFrameId - 1 : 0;

        /// <summary>
        /// 写入一帧输入（由 ReceiveMessage/补发帧调用）。
        /// 容错：重复帧直接忽略（补发与实时广播在极端时序下可能重叠），避免重复消费破坏确定性。
        /// </summary>
        public void Push(UInt64 frameId, FrameInput input)
        {
            if (!_frames.ContainsKey(frameId))
                _frames.Add(frameId, input);
        }

        /// <summary>
        /// 消费下一帧（_nextFrameId）。成功则推进帧号并返回输入。
        /// 缺帧但缓冲内有更晚帧时跳帧容错（补发时离线/停滞玩家的帧本就不存在）：
        /// 跳到最早可用帧继续，缺口期间保持冻结，符合"离线期不移动"语义；
        /// 所有客户端收到同一份补发，跳帧一致，不破坏确定性。
        /// 返回 null = 无可用帧（缓冲空或均为过期帧），调用方按"缺口/离线"处理。
        /// </summary>
        public FrameInput? TryPopNextFrame()
        {
            if (_frames.TryGetValue(_nextFrameId, out var input))
            {
                _frames.Remove(_nextFrameId);
                _nextFrameId++;
                return input;
            }

            if (_frames.Count > 0)
            {
                UInt64 first = MinFrameId;
                if (first > _nextFrameId)
                {
                    _nextFrameId = first; // 跳到最早可用帧
                    return TryPopNextFrame();
                }
            }
            return null;
        }

        /// <summary>清空缓冲（快照重建时调用）</summary>
        public void Clear()
        {
            _frames.Clear();
        }

        /// <summary>以指定帧号为执行进度起点（快照恢复：从 LastFrameId 的下一帧继续）</summary>
        public void ResetNextFrameId(UInt64 lastExecutedFrameId)
        {
            _nextFrameId = lastExecutedFrameId + 1;
        }
    }
}
