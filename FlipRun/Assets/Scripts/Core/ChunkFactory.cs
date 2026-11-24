using System;
using System.Collections.Generic;

namespace FlipRun
{
    /// <summary>
    /// 難易度の指標（最少反転回数・反応時間）から、チャンクの難易度を見積もる。
    /// </summary>
    public static class DifficultyEstimator
    {
        // 反応時間（秒）→ 難易度 の対応表。値は ChunkFactory の較正（Calibrate）結果から決めた。
        // 反応時間が短いほど難しい。反転回数が多いと少し難しくする。
        public static float ReactionAt1 = 3.0f;
        public static float ReactionAt10 = 0.60f;
        public static float FlipWeight = 0.3f;

        public static int Estimate(int minFlips, float reactionTime)
        {
            // 反応時間の対数で1〜10に割り当てる
            float r = Math.Max(0.05f, reactionTime);
            float a = (float)Math.Log(ReactionAt1), b = (float)Math.Log(ReactionAt10);
            float x = 1f + 9f * ((float)Math.Log(r) - a) / (b - a);
            x += FlipWeight * Math.Max(0, minFlips - 2);
            int d = (int)Math.Round(x);
            return Math.Max(1, Math.Min(10, d));
        }
    }

    /// <summary>
    /// ジェネレーターで作ったチャンクをソルバーで確かめ、条件を満たすものだけを採用する。
    /// 条件：床スタート・天井スタートの両方でクリア可能、かつ見積もった難易度が指定と±1以内。
    /// シード値が同じなら、毎回同じチャンクが選ばれる。
    /// </summary>
    public static class ChunkFactory
    {
        public const int MaxAttempts = 400;

        public sealed class Entry
        {
            public ChunkData Chunk;
            public ChunkSolver.Metrics Metrics;
            public int EstimatedDifficulty;
            public int Attempts;   // 何個目の候補で合格したか
            public int Seed;       // 合格した候補のシード
        }

        public static Entry Generate(int difficulty, int index, int baseSeed)
        {
            float speed = GameConfig.SpeedForDifficulty(difficulty);
            string id = $"chunk_{difficulty:00}_{index:000}";
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int seed = CandidateSeed(baseSeed, difficulty, index, attempt);
                var chunk = ChunkGenerator.Build(difficulty, seed, id);
                var m = ChunkSolver.Measure(chunk, speed);
                if (!m.Clearable) continue;
                int est = DifficultyEstimator.Estimate(m.MinFlips, m.ReactionTime);
                if (Math.Abs(est - difficulty) > 1) continue;
                return new Entry { Chunk = chunk, Metrics = m, EstimatedDifficulty = est, Attempts = attempt + 1, Seed = seed };
            }
            throw new InvalidOperationException($"難易度{difficulty} #{index}: {MaxAttempts}回試しても条件を満たすチャンクができませんでした");
        }

        public static List<Entry> GenerateAll(int perDifficulty, int baseSeed)
        {
            var list = new List<Entry>();
            for (int d = 1; d <= 10; d++)
                for (int i = 1; i <= perDifficulty; i++)
                    list.Add(Generate(d, i, baseSeed));
            return list;
        }

        public static int CandidateSeed(int baseSeed, int difficulty, int index, int attempt)
        {
            unchecked
            {
                int h = baseSeed;
                h = h * 31 + difficulty;
                h = h * 31 + index;
                h = h * 31 + attempt;
                h ^= h >> 15;
                h *= 0x2c1b3c6d;
                h ^= h >> 12;
                return h & 0x7fffffff;
            }
        }
    }
}
