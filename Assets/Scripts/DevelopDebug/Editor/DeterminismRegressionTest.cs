using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FrameSync;
using GamePlay;
using Network;
using UnityEditor;
using UnityEngine;

namespace DevelopDebug
{
    /// <summary>
    /// 确定性回归测试：
    /// 1. 脚本化场景——双人冲突裁决/送达/放到地上的确定性语义
    /// 2. fuzz 随机场景——多 seed 随机玩家/物品布局 + 随机指令（含空帧/null 缺口碑）/多帧
    /// 3. 基准哈希——固定配置下终态哈希必须与基准一致（任何模拟行为变化 → 漂移 → FAIL）
    /// 4. 变异自检（单独菜单）——注入已知分歧，验证测试确实能抓到（防"永远通过"）
    /// 每个场景都在两个独立模拟世界间逐帧断言：全状态位一致 + 世界哈希一致（与线上 Desync 同一实现）。
    /// </summary>
    public static class DeterminismRegressionTest
    {
        #region 配置

        /// <summary>脚本化场景帧数</summary>
        private const int SCRIPTED_FRAMES = 40;

        /// <summary>逐帧哈希明细输出开关（默认关；失败时自动输出分歧帧 + 该帧输入）</summary>
        private const bool VERBOSE = true;

        /// <summary>逻辑帧间隔/速度（与 GameSync 一致，保证模拟参数真实）</summary>
        private static readonly FixedPoint FrameSpace = FixedPoint.FromFloat(1f / 30f);
        private static readonly FixedPoint Speed = FixedPoint.FromFloat(10f);

        /// <summary>
        /// 基准哈希基准：seed=12345, 4玩家6物品1000帧 的终态哈希（实测 2026-08-04 = 0xFFF36A30）。
        /// 任何确定性破坏/行为变化都会使其漂移 → FAIL；若为预期重构，改完后重新实测并更新此值。
        /// </summary>
        private const uint REFERENCE_HASH = 0xFFF36A30;
        private const string REFERENCE_INFO = "[fuzz seed=12345 4玩家/6物品/1000帧]";

        private struct FuzzConfig
        {
            public int Seed;
            public int Players;
            public int Items;
            public int Frames;
        }

        /// <summary>fuzz 场景列表（覆盖不同玩家数/物品数/时长）</summary>
        private static readonly FuzzConfig[] FUZZ_CONFIGS =
        {
            new FuzzConfig { Seed = 12345, Players = 4, Items = 6, Frames = 1000 }, // 基准哈希场景
            new FuzzConfig { Seed = 2, Players = 3, Items = 5, Frames = 800 },
            new FuzzConfig { Seed = 3, Players = 4, Items = 4, Frames = 600 },
            new FuzzConfig { Seed = 4, Players = 2, Items = 3, Frames = 500 },
            new FuzzConfig { Seed = 5, Players = 5, Items = 7, Frames = 1200 },
        };

        #endregion

        #region 入口

        [MenuItem("Tools/测试（双世界哈希比对）")]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        [MenuItem("Tools/测试（变异自检）")]
        public static void MutationCheckFromMenu()
        {
            Debug.Log(MutationCheck());
        }

        /// <summary>主入口：脚本化 + fuzz×5 + 基准哈希，返回 PASS/FAIL 汇总</summary>
        public static string Run()
        {
            try { return RunAll(); }
            catch (Exception ex) { return $"FAIL 异常: {ex}"; }
        }

        /// <summary>变异自检入口：注入已知分歧，验证测试确实能抓到</summary>
        public static string MutationCheck()
        {
            try { return MutationCheckInternal(); }
            catch (Exception ex) { return $"FAIL 异常: {ex}"; }
        }

        #endregion

        #region 世界构造

        private class World
        {
            public readonly Dictionary<string, PlayerEntity> Players = new();
            public readonly Dictionary<string, FrameBuffer> Buffers = new();
            public readonly List<ItemEntity> Items = new();
        }

        /// <summary>
        /// 脚本化世界：两玩家（名字 A&lt;B，验证 Ordinal 排序冲突裁决）+ 全部 CargoConfig 物品。
        /// 出生点：Alice (0,-1) 与 Bob (0.5,-0.8) 都在物品0（(0,0)）拾取半径（1.2）内。
        /// reversePlayerOrder=true：反序插入玩家，模拟"跨客户端玩家加入顺序不同"——
        /// 若 StepFrame 的 Ordinal 排序被删，两个世界将按不同字典顺序迭代 → 测试立刻 FAIL。
        /// </summary>
        private static World CreateScriptedWorld(bool reversePlayerOrder = false)
        {
            var w = new World();
            if (reversePlayerOrder)
            {
                w.Players.Add("Bob", new PlayerEntity("Bob", FixedPointVector3.FromFloat(0.5f, 1.2f, -0.8f), FrameSpace, Speed));
                w.Players.Add("Alice", new PlayerEntity("Alice", FixedPointVector3.FromFloat(0f, 1.2f, -1f), FrameSpace, Speed));
            }
            else
            {
                w.Players.Add("Alice", new PlayerEntity("Alice", FixedPointVector3.FromFloat(0f, 1.2f, -1f), FrameSpace, Speed));
                w.Players.Add("Bob", new PlayerEntity("Bob", FixedPointVector3.FromFloat(0.5f, 1.2f, -0.8f), FrameSpace, Speed));
            }
            w.Buffers.Add("Alice", new FrameBuffer());
            w.Buffers.Add("Bob", new FrameBuffer());
            for (int i = 0; i < CargoConfig.ItemSpawnPos.Length; i++)
                w.Items.Add(new ItemEntity(i, CargoConfig.ItemSpawnPos[i]));
            return w;
        }

        /// <summary>fuzz 世界：随机玩家数/物品布局（固定 seed → 两世界配置必然一致且跨运行可复现）。
        /// reversePlayerOrder=true 时玩家以相反顺序插入字典（坐标仍按名字一致），
        /// 模拟"跨客户端玩家加入顺序不同"——若 StepFrame 的 Ordinal 排序被删，测试立刻 FAIL。</summary>
        private static World CreateFuzzWorld(int seed, int itemCount, int playerCount, bool reversePlayerOrder = false)
        {
            var rng = new System.Random(seed);
            var w = new World();

            // 先固定生成各玩家坐标（顺序固定），再按正向/反向插入字典 → 同名玩家两世界坐标必然一致
            var xs = new float[playerCount];
            var zs = new float[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                xs[i] = (float)(rng.NextDouble() * 8 - 4);
                zs[i] = (float)(rng.NextDouble() * 8 - 4);
            }
            int start = reversePlayerOrder ? playerCount - 1 : 0;
            int end = reversePlayerOrder ? -1 : playerCount;
            int step = reversePlayerOrder ? -1 : 1;
            for (int i = start; i != end; i += step)
            {
                string name = "P" + i;
                w.Players.Add(name, new PlayerEntity(name, FixedPointVector3.FromFloat(xs[i], 1.2f, zs[i]), FrameSpace, Speed));
                w.Buffers.Add(name, new FrameBuffer());
            }
            for (int i = 0; i < itemCount; i++)
            {
                float x = (float)(rng.NextDouble() * 8 - 4);
                float z = (float)(rng.NextDouble() * 8 - 4);
                w.Items.Add(new ItemEntity(i, FixedPointVector3.FromFloat(x, 0.5f, z)));
            }
            return w;
        }

        /// <summary>
        /// 推送一帧输入到缓冲并推进世界（与生产路径完全一致）：
        /// 1. 按帧号 Push 进各玩家 FrameBuffer（null = 缺口/离线 → 不推送 → TryPopNextFrame 返回 null → 实体冻结）
        /// 2. 调用 FrameSimulation.StepFrame（GameSync.ApplyFrames 委托的同一份生产代码）
        /// </summary>
        private static void PushAndStep(World w, ulong frameId, IReadOnlyDictionary<string, FrameInput> inputs)
        {
            if (inputs != null)
            {
                foreach (var kv in inputs)
                {
                    if (kv.Value == null) continue; // 缺口/离线：本帧无输入
                    w.Buffers[kv.Key].Push(frameId, kv.Value);
                }
            }
            FrameSimulation.StepFrame(w.Players, w.Buffers, w.Items);
        }

        /// <summary>
        /// 逐帧全状态位比较（比哈希更强的确定性断言）：
        /// 哈希对"携带物与玩家同 XZ"存在 XOR 抵消盲区，直接逐字段比较才能抓到细微分歧。
        /// 返回 null = 一致；否则返回首个不一致描述。
        /// </summary>
        private static string CompareState(World a, World b)
        {
            if (a.Players.Count != b.Players.Count) return "玩家数不一致";
            foreach (var kv in a.Players)
            {
                if (!b.Players.TryGetValue(kv.Key, out var other)) return $"缺玩家 {kv.Key}";
                var pa = kv.Value.Position;
                var pb = other.Position;
                if (pa.GetRawX() != pb.GetRawX() || pa.GetRawY() != pb.GetRawY() || pa.GetRawZ() != pb.GetRawZ())
                    return $"玩家 {kv.Key} 位置不一致 {pa} vs {pb}";
            }
            if (a.Items.Count != b.Items.Count) return "物品数不一致";
            for (int i = 0; i < a.Items.Count; i++)
            {
                var ia = a.Items[i];
                var ib = b.Items[i];
                if (ia.Position.GetRawX() != ib.Position.GetRawX()
                    || ia.Position.GetRawY() != ib.Position.GetRawY()
                    || ia.Position.GetRawZ() != ib.Position.GetRawZ())
                    return $"物品{i} 位置不一致";
                if (ia.Owner != ib.Owner) return $"物品{i} 持有者不一致";
                if (ia.DeliverCount != ib.DeliverCount) return $"物品{i} 送达数不一致";
            }
            // 帧缓冲进度一致（消费是否正确推进）
            if (a.Buffers.Count != b.Buffers.Count) return "缓冲数不一致";
            foreach (var kv in a.Buffers)
            {
                if (!b.Buffers.TryGetValue(kv.Key, out var ob)) return $"缺缓冲 {kv.Key}";
                var ba = kv.Value;
                var bb = ob;
                if (ba.Count != bb.Count) return $"玩家 {kv.Key} 缓冲帧数不一致 {ba.Count} vs {bb.Count}";
                if (ba.NextFrameId != bb.NextFrameId) return $"玩家 {kv.Key} 缓冲NextFrameId不一致 {ba.NextFrameId} vs {bb.NextFrameId}";
                if (ba.LastExecutedFrameId != bb.LastExecutedFrameId) return $"玩家 {kv.Key} 缓冲LastExecutedFrameId不一致 {ba.LastExecutedFrameId} vs {bb.LastExecutedFrameId}";
            }
            return null;
        }

        #endregion

        #region 指令构造

        private static FrameInput Move(float x, float z) =>
            new FrameInput { Commands = { new InputCommand { Type = CommandType.MoveDirection, MoveDirection = FixedPointVector3.FromFloat(x, 0f, z) } } };

        private static FrameInput Interact() =>
            new FrameInput { Commands = { new InputCommand { Type = CommandType.Interact } } };

        private static FrameInput MoveAndInteract() =>
            new FrameInput
            {
                Commands =
                {
                    new InputCommand { Type = CommandType.MoveDirection, MoveDirection = FixedPointVector3.FromFloat(1f, 0f, 0f) },
                    new InputCommand { Type = CommandType.Interact }
                }
            };

        /// <summary>
        /// 脚本化指令序列：
        /// 帧1   双人同帧 Interact → 冲突裁决（Alice 先得物品0）
        /// 帧2-11 Alice 携物品0 向右移动到火车（4,0）附近
        /// 帧12  Alice Interact → 送达（计数1，物品回出生点）
        /// 帧15  Bob Interact → 拾取回生的物品0
        /// 帧16  Bob Interact → 放到地上（远离火车）
        /// </summary>
        private static IReadOnlyDictionary<string, FrameInput> InputsForFrame(int frame)
        {
            var dict = new Dictionary<string, FrameInput> { ["Alice"] = new FrameInput(), ["Bob"] = new FrameInput() };
            switch (frame)
            {
                case 1:
                    dict["Alice"] = Interact();
                    dict["Bob"] = Interact();
                    break;
                case 12:
                    dict["Alice"] = Interact();
                    break;
                case 15:
                case 16:
                    dict["Bob"] = Interact();
                    break;
                default:
                    if (frame >= 2 && frame <= 11) dict["Alice"] = Move(1f, 0f);
                    break;
            }
            return dict;
        }

        /// <summary>
        /// 预生成 fuzz 指令序列（只依赖 seed，不依赖世界状态 → 两世界收到同一份输入）：
        /// 10% null(缺口冻结) / 20% 空命令 / 55% 随机方向移动 / 10% 交互 / 5% 移动+交互同帧。
        /// </summary>
        private static IReadOnlyDictionary<string, FrameInput>[] GenerateFuzzInputs(int seed, int playerCount, int frames)
        {
            var rng = new System.Random(seed);
            var plan = new IReadOnlyDictionary<string, FrameInput>[frames];
            for (int f = 0; f < frames; f++)
            {
                var dict = new Dictionary<string, FrameInput>();
                for (int i = 0; i < playerCount; i++)
                {
                    string name = "P" + i;
                    int roll = rng.Next(100);
                    if (roll < 10) dict[name] = null;                       // 缺口/离线：冻结
                    else if (roll < 30) dict[name] = new FrameInput();       // 空命令
                    else if (roll < 85) dict[name] = Move(rng.Next(3) - 1, rng.Next(3) - 1);
                    else if (roll < 95) dict[name] = Interact();
                    else dict[name] = MoveAndInteract();                     // 多命令同帧
                }
                plan[f] = dict;
            }
            return plan;
        }

        /// <summary>失败时输出该帧输入（可复现定位）</summary>
        private static string DescribeInputs(IReadOnlyDictionary<string, FrameInput> inputs)
        {
            if (inputs == null) return "null";
            var sb = new StringBuilder();
            foreach (var kv in inputs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string desc = kv.Value == null ? "null(冻结)"
                    : kv.Value.Commands.Count == 0 ? "空"
                    : string.Join(",", kv.Value.Commands.Select(c => c.Type.ToString()));
                sb.Append($"{kv.Key}={desc} ");
            }
            return sb.ToString().Trim();
        }

        #endregion

        #region 场景

        /// <summary>脚本化场景：确定性语义验证（冲突/送达/放下）</summary>
        private static string RunScriptedScenario(StringBuilder sb)
        {
            var w1 = CreateScriptedWorld();
            var w2 = CreateScriptedWorld(reversePlayerOrder: true); // 玩家反序插入：验证 Ordinal 排序确实在起作用

            for (int f = 1; f <= SCRIPTED_FRAMES; f++)
            {
                var inputs = InputsForFrame(f);
                PushAndStep(w1, (ulong)f, inputs);
                PushAndStep(w2, (ulong)f, inputs);

                // 断言1：逐帧全状态位一致
                string diff = CompareState(w1, w2);
                if (diff != null)
                    return $"FAIL 帧{f}: 双世界状态不一致 - {diff}\n该帧输入: {DescribeInputs(inputs)}";

                // 断言2：世界哈希一致（与线上 Desync 比对同一实现）
                uint h1 = WorldStateHash.Compute(w1.Players, w1.Items);
                uint h2 = WorldStateHash.Compute(w2.Players, w2.Items);
                if (h1 != h2)
                    return $"FAIL 帧{f}: 双世界哈希不一致 w1={h1:X8} w2={h2:X8}\n该帧输入: {DescribeInputs(inputs)}";

                if (VERBOSE) sb.AppendLine($"帧{f}: 哈希一致 {h1:X8}");

                // 关键确定性语义断言
                switch (f)
                {
                    case 1:
                        if (w1.Items[0].Owner != "Alice")
                            return $"FAIL 帧1: 冲突裁决错误（Alice 应先得物品0）实际持有者={w1.Items[0].Owner}";
                        break;
                    case 12:
                        if (w1.Items[0].DeliverCount != 1)
                            return $"FAIL 帧12: 送达计数错误，期望1 实际={w1.Items[0].DeliverCount}";
                        break;
                    case 16:
                    {
                        var expected = FixedPointVector3.FromFloat(0.5f, CargoConfig.ItemGroundY.ToFloat(), -0.8f);
                        var pos = w1.Items[0].Position;
                        if (pos.GetRawX() != expected.GetRawX() || pos.GetRawY() != expected.GetRawY() || pos.GetRawZ() != expected.GetRawZ())
                            return $"FAIL 帧16: 放下位置错误 实际={pos} 期望={expected}";
                        if (w1.Items[0].Owner != "")
                            return $"FAIL 帧16: 放下后应无持有者 实际={w1.Items[0].Owner}";
                        break;
                    }
                }
            }

            // 终态抽查：Alice 确实移动过（携带移动 10 帧，X 应从 0 到约 3.33）
            float aliceX = w1.Players["Alice"].Position.ToVector3().x;
            if (aliceX < 3f || aliceX > 3.5f)
                return $"FAIL 终态: Alice 移动异常 x={aliceX}";

            return "PASS（冲突裁决/送达/放下语义）";
        }

        /// <summary>fuzz 随机场景：两独立世界跑同一随机输入序列，逐帧断言一致</summary>
        private static string RunFuzzScenario(int seed, int playerCount, int itemCount, int frames, out uint finalHash)
        {
            var w1 = CreateFuzzWorld(seed, itemCount, playerCount);
            var w2 = CreateFuzzWorld(seed, itemCount, playerCount, reversePlayerOrder: true); // 反序插入：验证 Ordinal 排序
            var plan = GenerateFuzzInputs(seed, playerCount, frames);
            finalHash = 0;

            for (int f = 0; f < frames; f++)
            {
                var inputs = plan[f];
                PushAndStep(w1, (ulong)(f + 1), inputs); // 帧号从1起，匹配 FrameBuffer 初始 nextFrameId
                PushAndStep(w2, (ulong)(f + 1), inputs);

                string diff = CompareState(w1, w2);
                if (diff != null)
                    return $"FAIL 帧{f}: {diff}\n该帧输入: {DescribeInputs(inputs)}";

                uint h1 = WorldStateHash.Compute(w1.Players, w1.Items);
                uint h2 = WorldStateHash.Compute(w2.Players, w2.Items);
                if (h1 != h2)
                    return $"FAIL 帧{f}: 哈希不一致 w1={h1:X8} w2={h2:X8}\n该帧输入: {DescribeInputs(inputs)}";
            }

            finalHash = WorldStateHash.Compute(w1.Players, w1.Items);
            return $"PASS 终态哈希={finalHash:X8}";
        }

        /// <summary>基准哈希场景：固定配置的终态哈希必须与基准一致（防回归）</summary>
        private static string RunReferenceHashScenario()
        {
            string r = RunFuzzScenario(12345, 4, 6, 1000, out uint hash);
            if (!r.StartsWith("PASS")) return r;
            return hash == REFERENCE_HASH
                ? $"PASS 基准哈希一致 {hash:X8}"
                : $"FAIL 基准哈希漂移 期望={REFERENCE_HASH:X8} 实际={hash:X8}——模拟行为已变化，若为预期重构请更新 GOLDEN_HASH";
        }

        #endregion

        #region 主流程

        private static string RunAll()
        {
            var sb = new StringBuilder();
            int pass = 0, total = 0;

            // 1. 脚本化语义场景
            total++;
            string scripted = RunScriptedScenario(sb);
            if (scripted.StartsWith("PASS")) pass++;
            sb.AppendLine($"[脚本化场景] {scripted}");

            // 2. fuzz 随机场景
            foreach (var c in FUZZ_CONFIGS)
            {
                total++;
                string r = RunFuzzScenario(c.Seed, c.Players, c.Items, c.Frames, out _);
                if (r.StartsWith("PASS")) pass++;
                sb.AppendLine($"[fuzz seed={c.Seed} {c.Players}玩家/{c.Items}物品/{c.Frames}帧] {r}");
            }

            // 3. 基准哈希
            total++;
            string reference = RunReferenceHashScenario();
            if (reference.StartsWith("PASS")) pass++;
            sb.AppendLine($"[基准哈希] {reference} {REFERENCE_INFO}");

            return $"{(pass == total ? "PASS" : "FAIL")} 汇总 {pass}/{total} 通过\n" + sb;
        }

        /// <summary>
        /// 变异自检：帧100 时在 w2 注入物品位置偏移（模拟确定性被破坏），
        /// 断言测试能检测到分歧——证明测试不是"永远通过"的摆设。
        /// </summary>
        private static string MutationCheckInternal()
        {
            const int seed = 777, players = 3, items = 4, frames = 300;
            const int INJECT_AT_FRAME = 100;

            var w1 = CreateFuzzWorld(seed, items, players);
            var w2 = CreateFuzzWorld(seed, items, players, reversePlayerOrder: true); // 反序插入：验证 Ordinal 排序
            var plan = GenerateFuzzInputs(seed, players, frames);

            for (int f = 0; f < frames; f++)
            {
                var inputs = plan[f];
                PushAndStep(w1, (ulong)(f + 1), inputs); // 帧号从1起，匹配 FrameBuffer 初始 nextFrameId
                PushAndStep(w2, (ulong)(f + 1), inputs);

                // 注入分歧：w2 物品0 位置偏移 1 原始单位（确定性被破坏的模拟）
                if (f == INJECT_AT_FRAME)
                {
                    var it = w2.Items[0];
                    it.Position = FixedPointVector3.FromRawValue(it.Position.GetRawX() + 1, it.Position.GetRawY(), it.Position.GetRawZ());
                }

                string diff = CompareState(w1, w2);
                if (diff != null)
                    return $"PASS 变异在帧{f}被全状态比较检测到: {diff}";

                uint h1 = WorldStateHash.Compute(w1.Players, w1.Items);
                uint h2 = WorldStateHash.Compute(w2.Players, w2.Items);
                if (h1 != h2)
                    return $"PASS 变异在帧{f}被哈希比对检测到: w1={h1:X8} w2={h2:X8}";
            }

            return "FAIL 变异（确定性破坏）未被检测到——测试失明，必须加强断言";
        }

        #endregion
    }
}
