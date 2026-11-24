using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FlipRun.EditorTools
{
    /// <summary>
    /// Editorメニュー「FlipRun > Generate Chunks」。
    /// 難易度1〜10で各6個、合計60チャンクを作り、全部をソルバーで確かめてから JSON に書き出す。
    /// </summary>
    public static class ChunkGeneratorMenu
    {
        public const int PerDifficulty = 6;
        public const int BaseSeed = 20261124;
        public const string OutputFolder = "Assets/Resources/Chunks";

        public sealed class Report
        {
            public List<ChunkFactory.Entry> Entries;
            public bool AllClearable;
            public bool AllWithinOne;
            public bool ReactionMonotonic;
            public string Table;
            public bool Passed => AllClearable && AllWithinOne && ReactionMonotonic;
        }

        [MenuItem("FlipRun/Generate Chunks")]
        public static void GenerateMenu()
        {
            var report = GenerateAndWrite();
            if (report.Passed) Debug.Log(report.Table);
            else Debug.LogError(report.Table);
        }

        /// <summary>コマンドラインから実行する入口。失敗したら終了コード1。</summary>
        public static void GenerateFromCommandLine()
        {
            Report report;
            try { report = GenerateAndWrite(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log(report.Table);
            EditorApplication.Exit(report.Passed ? 0 : 1);
        }

        public static Report GenerateAndWrite()
        {
            var report = Build();
            Directory.CreateDirectory(OutputFolder);
            foreach (var old in Directory.GetFiles(OutputFolder, "chunk_*.json"))
            {
                File.Delete(old);
                if (File.Exists(old + ".meta")) File.Delete(old + ".meta");
            }
            foreach (var e in report.Entries)
                File.WriteAllText(Path.Combine(OutputFolder, e.Chunk.id + ".json"), JsonUtility.ToJson(e.Chunk, true) + "\n");
            AssetDatabase.Refresh();
            return report;
        }

        /// <summary>生成と検証だけを行う（ファイルは書かない）。テストからも使う。</summary>
        public static Report Build()
        {
            var entries = ChunkFactory.GenerateAll(PerDifficulty, BaseSeed);
            var r = new Report { Entries = entries };
            r.AllClearable = entries.All(e => e.Metrics.Clearable);
            r.AllWithinOne = entries.All(e => Math.Abs(e.EstimatedDifficulty - e.Chunk.difficulty) <= 1);

            var sb = new StringBuilder();
            sb.AppendLine("[FlipRun] チャンク生成結果");
            sb.AppendLine("難易度 | 個数 | クリア可能 | 速度 | 平均反転回数 | 平均反応時間(秒) | 最短〜最長 | 見積り難易度 | 平均試行回数");
            float prev = float.MaxValue;
            r.ReactionMonotonic = true;
            for (int d = 1; d <= 10; d++)
            {
                var g = entries.Where(e => e.Chunk.difficulty == d).ToList();
                float avgR = g.Average(e => e.Metrics.ReactionTime);
                if (!(avgR < prev)) r.ReactionMonotonic = false;
                prev = avgR;
                sb.AppendLine(string.Format("{0,6} | {1,4} | {2,4}/{1} | {3,5:0.00} | {4,12:0.0} | {5,16:0.000} | {6:0.00}〜{7:0.00} | {8} | {9:0.0}",
                    d, g.Count, g.Count(e => e.Metrics.Clearable), GameConfig.SpeedForDifficulty(d),
                    g.Average(e => e.Metrics.MinFlips), avgR,
                    g.Min(e => e.Metrics.ReactionTime), g.Max(e => e.Metrics.ReactionTime),
                    string.Join(",", g.Select(e => e.EstimatedDifficulty)),
                    g.Average(e => e.Attempts)));
            }
            sb.AppendLine($"全部クリア可能: {(r.AllClearable ? "はい" : "いいえ")} / 見積り難易度が±1以内: {(r.AllWithinOne ? "はい" : "いいえ")} / 反応時間が単調に減少: {(r.ReactionMonotonic ? "はい" : "いいえ")}");
            sb.Append(r.Passed ? "合格：60チャンクすべてクリア可能" : "不合格");
            r.Table = sb.ToString();
            return r;
        }

        /// <summary>
        /// 較正用：ジェネレーターの生の出力（ふるいにかける前）の指標を難易度ごとに集計する。
        /// DifficultyEstimator の対応表を決めるときに使う。
        /// </summary>
        public static void CalibrateFromCommandLine()
        {
            var sb = new StringBuilder();
            sb.AppendLine("d | clearRate | flips(avg) | reaction p10/p50/p90 | est hist");
            for (int d = 1; d <= 10; d++)
            {
                float speed = GameConfig.SpeedForDifficulty(d);
                var rs = new List<float>();
                var fl = new List<int>();
                var hist = new int[11];
                int ok = 0, n = 120;
                for (int i = 0; i < n; i++)
                {
                    var c = ChunkGenerator.Build(d, ChunkFactory.CandidateSeed(777, d, i, 0));
                    var m = ChunkSolver.Measure(c, speed);
                    if (!m.Clearable) continue;
                    ok++;
                    rs.Add(m.ReactionTime);
                    fl.Add(m.MinFlips);
                    hist[DifficultyEstimator.Estimate(m.MinFlips, m.ReactionTime)]++;
                }
                rs.Sort();
                float P(float p) => rs.Count == 0 ? 0 : rs[Math.Min(rs.Count - 1, (int)(p * rs.Count))];
                sb.AppendLine($"{d} | {ok}/{n} | {(fl.Count > 0 ? fl.Average() : 0):0.0} | {P(0.1f):0.00}/{P(0.5f):0.00}/{P(0.9f):0.00} | {string.Join(" ", hist.Skip(1))}");
            }
            File.WriteAllText("Logs/calibration.txt", sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
