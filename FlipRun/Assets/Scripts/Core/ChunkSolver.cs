using System;
using System.Collections.Generic;

namespace FlipRun
{
    /// <summary>
    /// 検証ツール。PlayerSim を使い、0.05秒（3フレーム）ごとに「反転する／しない」を分岐させる幅優先探索を行う。
    /// 同じ状態（位置・速度・重力の向きなど）は丸めて1つにまとめ、探索の爆発を防ぐ。
    /// x は時間で決まる（速度一定）ので、同じ層の中では y と速度などだけを比べればよい。
    /// </summary>
    public static class ChunkSolver
    {
        public const int DecisionFrames = 3; // 0.05秒
        const float StartX = 0.5f;

        public struct Options
        {
            public float Speed;
            public bool StartOnCeiling;
            /// <summary>反転と反転のあいだを最低何フレーム空けるか（0 = 制限なし）。反応時間の計算に使う。</summary>
            public int MinGapFrames;
        }

        public sealed class Result
        {
            public bool Clearable;
            /// <summary>見つかった入力列のうち、反転回数が最少のもの。押したフレーム番号のリスト。</summary>
            public List<int> PressFrames = new List<int>();
            public int Flips;
            public int Frames;
            public int StatesExplored;
        }

        struct Node
        {
            public PlayerState State;
            public int Parent;
            public bool Pressed;
            public int Flips;
            public int SinceFlip; // 最後に反転してからのフレーム数（反転なしは大きな値）
        }

        /// <summary>チャンクを左端から右端まで死なずに抜けられるかを調べる。</summary>
        public static Result Solve(ChunkData chunk, Options opt)
        {
            var world = new ChunkWorld(chunk);
            var nodes = new List<Node>(4096);
            var result = new Result();

            float exitX = chunk.width + GameConfig.PlayerHalfSize; // 当たり判定が右端を完全に抜けたら成功
            int maxFrames = (int)Math.Ceiling((exitX - StartX) / (opt.Speed * GameConfig.Dt)) + DecisionFrames;
            int gapCap = opt.MinGapFrames > 0 ? opt.MinGapFrames : 0;

            nodes.Add(new Node
            {
                State = PlayerState.Create(StartX, opt.StartOnCeiling),
                Parent = -1,
                Flips = 0,
                SinceFlip = 1 << 20,
            });
            var layer = new List<int> { 0 };
            var next = new Dictionary<long, int>();

            for (int frame = 0; frame < maxFrames && layer.Count > 0; frame += DecisionFrames)
            {
                next.Clear();
                foreach (int idx in layer)
                {
                    for (int choice = 0; choice < 2; choice++)
                    {
                        bool press = choice == 1;
                        Node n = nodes[idx];
                        var s = n.State;
                        int flips = n.Flips;
                        int since = n.SinceFlip;
                        bool ok = true;
                        for (int f = 0; f < DecisionFrames; f++)
                        {
                            PlayerSim.Step(ref s, press && f == 0, opt.Speed, world);
                            if (s.IsDead) { ok = false; break; }
                            if (s.FlippedThisStep)
                            {
                                if (flips > 0 && since < gapCap) { ok = false; break; } // 間隔が短すぎる
                                flips++;
                                since = 0;
                            }
                            else if (since < (1 << 20)) since++;
                            if (s.X >= exitX) break;
                        }
                        if (!ok) continue;
                        result.StatesExplored++;

                        var child = new Node { State = s, Parent = idx, Pressed = press, Flips = flips, SinceFlip = since };
                        if (s.X >= exitX)
                        {
                            // 同じ層で先に見つかった解より反転が少なければ差し替える
                            nodes.Add(child);
                            int ci = nodes.Count - 1;
                            if (!result.Clearable || flips < result.Flips)
                            {
                                result.Clearable = true;
                                result.Flips = flips;
                                result.Frames = s.Frame;
                                Reconstruct(nodes, ci, result.PressFrames);
                            }
                            continue;
                        }

                        long key = Key(s, since, gapCap);
                        if (next.TryGetValue(key, out int existing))
                        {
                            if (nodes[existing].Flips > flips) nodes[existing] = child;
                        }
                        else
                        {
                            nodes.Add(child);
                            next[key] = nodes.Count - 1;
                        }
                    }
                }
                if (result.Clearable) break; // 全員が同じ時刻に進むので、最初に抜けた層で終わり
                layer = new List<int>(next.Values);
            }
            return result;
        }

        static long Key(in PlayerState s, int since, int gapCap)
        {
            long y = (long)Math.Round(s.Y * 100f) + 1000;              // 0.01マス単位
            long vy = (long)Math.Round(s.VY * 20f) + 1000;             // 0.05マス/秒単位
            long g = s.GravityDir > 0 ? 1 : 0;
            long gr = s.Grounded ? 1 : 0;
            long air = Math.Min(s.AirFrames, GameConfig.CoyoteFrames + 1);
            long buf = s.BufferFrames;
            long sf = gapCap > 0 ? Math.Min(since, gapCap) : 0;
            long k = y;
            k = k * 2048 + vy;
            k = k * 2 + g;
            k = k * 2 + gr;
            k = k * 16 + air;
            k = k * 16 + buf;
            k = k * 1024 + sf;
            return k;
        }

        static void Reconstruct(List<Node> nodes, int leaf, List<int> pressFrames)
        {
            pressFrames.Clear();
            var stack = new List<int>();
            for (int i = leaf; nodes[i].Parent >= 0; i = nodes[i].Parent)
                if (nodes[i].Pressed) stack.Add(nodes[nodes[i].Parent].State.Frame);
            stack.Reverse();
            pressFrames.AddRange(stack);
        }

        /// <summary>
        /// 入力列をそのまま PlayerSim で再生し、抜けられるかを確かめる（探索で丸めた影響がないことの確認用）。
        /// </summary>
        public static bool Replay(ChunkData chunk, float speed, bool startOnCeiling, IList<int> pressFrames, out PlayerState final)
        {
            var world = new ChunkWorld(chunk);
            var s = PlayerState.Create(StartX, startOnCeiling);
            float exitX = chunk.width + GameConfig.PlayerHalfSize;
            var presses = new HashSet<int>(pressFrames);
            int limit = (int)Math.Ceiling((exitX - StartX) / (speed * GameConfig.Dt)) + 10;
            while (!s.IsDead && s.X < exitX && s.Frame < limit)
                PlayerSim.Step(ref s, presses.Contains(s.Frame), speed, world);
            final = s;
            return !s.IsDead && s.X >= exitX;
        }

        /// <summary>難易度の指標：最少反転回数と、必要な反応時間（秒）。</summary>
        public struct Metrics
        {
            public bool Clearable;          // 床スタート・天井スタートの両方で抜けられる
            public bool ClearableFromFloor;
            public bool ClearableFromCeiling;
            public int MinFlips;            // 床スタートで抜けるのに必要な最少反転回数
            public float ReactionTime;      // 反転と反転の間隔を最大限空けた解での、最短の間隔（秒）
            public List<int> FloorPressFrames;
        }

        /// <summary>
        /// 反応時間 = 「反転どうしの間隔をできるだけ空けて抜けたとき、それでも一番詰まっている間隔」。
        /// 反転が1回以下で抜けられるなら、チャンクを抜ける時間を上限とする。
        /// 間隔の制限を2分探索して求める。
        /// </summary>
        public static Metrics Measure(ChunkData chunk, float speed)
        {
            var m = new Metrics();
            var floor = Solve(chunk, new Options { Speed = speed, StartOnCeiling = false });
            var ceil = Solve(chunk, new Options { Speed = speed, StartOnCeiling = true });
            m.ClearableFromFloor = floor.Clearable && Replay(chunk, speed, false, floor.PressFrames, out _);
            m.ClearableFromCeiling = ceil.Clearable && Replay(chunk, speed, true, ceil.PressFrames, out _);
            m.Clearable = m.ClearableFromFloor && m.ClearableFromCeiling;
            m.FloorPressFrames = floor.PressFrames;
            if (!m.ClearableFromFloor) return m;

            m.MinFlips = floor.Flips;
            int cap = (int)Math.Round(chunk.width / speed * GameConfig.StepsPerSecond);
            int lo = 0, hi = cap;
            if (Solve(chunk, new Options { Speed = speed, MinGapFrames = cap }).Clearable) lo = cap;
            else
            {
                // lo は可能、hi は不可能
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) / 2;
                    if (Solve(chunk, new Options { Speed = speed, MinGapFrames = mid }).Clearable) lo = mid;
                    else hi = mid;
                }
            }
            m.ReactionTime = lo / (float)GameConfig.StepsPerSecond;
            return m;
        }
    }
}
