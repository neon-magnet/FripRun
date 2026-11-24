using System;
using System.IO;
using System.Linq;
using FlipRun.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace FlipRun.Tests
{
    public class GeneratorTests
    {
        [Test]
        public void 同じシードなら同じチャンクになる()
        {
            for (int d = 1; d <= 10; d++)
            {
                var a = ChunkGenerator.Build(d, 12345);
                var b = ChunkGenerator.Build(d, 12345);
                CollectionAssert.AreEqual(a.rows, b.rows, $"難易度{d}");
            }
            Assert.IsTrue(Enumerable.Range(0, 20).Any(i =>
                !ChunkGenerator.Build(5, i).rows.SequenceEqual(ChunkGenerator.Build(5, i + 100).rows)),
                "シードが違えば違うチャンクもできる");
        }

        [Test]
        public void ファクトリーも同じシードなら同じ結果になる()
        {
            var a = ChunkFactory.Generate(4, 1, 99);
            var b = ChunkFactory.Generate(4, 1, 99);
            CollectionAssert.AreEqual(a.Chunk.rows, b.Chunk.rows);
            Assert.AreEqual(a.Seed, b.Seed);
        }

        [Test]
        public void 左右の端は床と天井がブロックでつなげられる形()
        {
            for (int d = 1; d <= 10; d++)
                for (int seed = 0; seed < 30; seed++)
                {
                    var c = ChunkGenerator.Build(d, seed);
                    Assert.AreEqual(20, c.width);
                    Assert.AreEqual(9, c.height);
                    Assert.AreEqual(9, c.rows.Length);
                    foreach (int col in new[] { 0, c.width - 1 })
                    {
                        Assert.AreEqual(Tile.Block, c.Get(col, GameConfig.FloorRow), $"d{d} seed{seed} col{col} 床");
                        Assert.AreEqual(Tile.Block, c.Get(col, GameConfig.CeilingRow), $"d{d} seed{seed} col{col} 天井");
                        for (int row = GameConfig.FloorRow + 1; row < GameConfig.CeilingRow; row++)
                            Assert.AreEqual(Tile.Empty, c.Get(col, row), $"d{d} seed{seed} col{col} row{row}");
                    }
                }
        }

        [Test]
        public void コインは天井側に置かれる()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var c = ChunkGenerator.Build(5, seed);
                for (int col = 0; col < c.width; col++)
                    for (int row = GameConfig.FloorRow; row <= GameConfig.CeilingRow; row++)
                        if (c.Get(col, row) == Tile.Coin) Assert.GreaterOrEqual(row, 0);
            }
        }

        [Test]
        public void JSONで書き出して読み戻せる()
        {
            var c = ChunkGenerator.Build(3, 7, "chunk_03_007");
            var back = JsonUtility.FromJson<ChunkData>(JsonUtility.ToJson(c));
            Assert.AreEqual(c.id, back.id);
            Assert.AreEqual(c.difficulty, back.difficulty);
            CollectionAssert.AreEqual(c.rows, back.rows);
        }
    }

    public class SolverTests
    {
        static ChunkSolver.Result Solve(ChunkData c, bool ceiling = false, float speed = 6f) =>
            ChunkSolver.Solve(c, new ChunkSolver.Options { Speed = speed, StartOnCeiling = ceiling });

        [Test]
        public void 平らなチャンクはクリア可能で反転は0回()
        {
            var r = Solve(TestChunks.Flat());
            Assert.IsTrue(r.Clearable);
            Assert.AreEqual(0, r.Flips);
            Assert.IsTrue(Solve(TestChunks.Flat(), ceiling: true).Clearable);
        }

        [Test]
        public void わざと作ったクリア不可能なチャンクは不可能と判定する()
        {
            Assert.IsFalse(Solve(TestChunks.BlockedWall()).Clearable, "通路をふさぐ壁");
            Assert.IsFalse(Solve(TestChunks.BlockedWall(), ceiling: true).Clearable, "通路をふさぐ壁（天井スタート）");
            Assert.IsFalse(Solve(TestChunks.DoubleHole()).Clearable, "床と天井の両方に穴");
            Assert.IsFalse(ChunkSolver.Measure(TestChunks.BlockedWall(), 6f).Clearable);
        }

        [Test]
        public void 必要な反転回数を数える()
        {
            Assert.AreEqual(1, Solve(TestChunks.FloorSpike()).Flips);
            Assert.AreEqual(0, Solve(TestChunks.FloorSpike(), ceiling: true).Flips);
            var two = Solve(TestChunks.NeedsTwoFlips());
            Assert.IsTrue(two.Clearable);
            Assert.AreEqual(2, two.Flips);
        }

        [Test]
        public void 見つけた入力列を再生すると本当に抜けられる()
        {
            foreach (var c in new[] { TestChunks.FloorSpike(), TestChunks.FloorWall(), TestChunks.NeedsTwoFlips() })
            {
                var r = Solve(c);
                Assert.IsTrue(r.Clearable);
                Assert.IsTrue(ChunkSolver.Replay(c, 6f, false, r.PressFrames, out var final), $"{final.Death}");
                // 入力なしでは死ぬことも確認（入力列に意味がある）
                Assert.IsFalse(ChunkSolver.Replay(c, 6f, false, Array.Empty<int>(), out _));
            }
        }

        [Test]
        public void 反応時間は仕掛けの間隔が狭いほど短い()
        {
            // 床の穴（3〜5列）のあとの天井の穴の位置だけを変えた2つのチャンク
            var wide = TestChunks.Make(
                "11111111111111100011",
                "00000000000000000000", "00000000000000000000", "00000000000000000000",
                "00000000000000000000", "00000000000000000000", "00000000000000000000",
                "00000000000000000000",
                "11100011111111111111");
            var narrow = TestChunks.Make(
                "11111111110001111111",
                "00000000000000000000", "00000000000000000000", "00000000000000000000",
                "00000000000000000000", "00000000000000000000", "00000000000000000000",
                "00000000000000000000",
                "11100011111111111111");
            var mw = ChunkSolver.Measure(wide, 6f);
            var mn = ChunkSolver.Measure(narrow, 6f);
            Assert.IsTrue(mw.ClearableFromFloor && mn.ClearableFromFloor);
            Assert.Greater(mw.ReactionTime, mn.ReactionTime);
        }
    }

    public class GeneratedChunksTests
    {
        static ChunkGeneratorMenu.Report _report;
        static ChunkGeneratorMenu.Report Report => _report ??= ChunkGeneratorMenu.Build();

        [Test]
        public void 六十チャンクすべてがクリア可能()
        {
            Assert.AreEqual(60, Report.Entries.Count);
            foreach (var e in Report.Entries)
            {
                Assert.IsTrue(e.Metrics.ClearableFromFloor, e.Chunk.id + " 床スタート");
                Assert.IsTrue(e.Metrics.ClearableFromCeiling, e.Chunk.id + " 天井スタート");
            }
        }

        [Test]
        public void 見積もった難易度が指定と1以内で一致する()
        {
            foreach (var e in Report.Entries)
                Assert.LessOrEqual(Math.Abs(e.EstimatedDifficulty - e.Chunk.difficulty), 1, e.Chunk.id);
        }

        [Test]
        public void 難易度が上がるほど平均の反応時間が短い()
        {
            float prev = float.MaxValue;
            for (int d = 1; d <= 10; d++)
            {
                float avg = Report.Entries.Where(e => e.Chunk.difficulty == d).Average(e => e.Metrics.ReactionTime);
                Assert.Less(avg, prev, $"難易度{d}\n{Report.Table}");
                prev = avg;
            }
        }

        [Test]
        public void 書き出したJSONファイルもすべてクリア可能()
        {
            var files = Directory.GetFiles(ChunkGeneratorMenu.OutputFolder, "chunk_*.json");
            if (files.Length == 0) Assert.Ignore("まだ FlipRun > Generate Chunks を実行していません");
            Assert.AreEqual(60, files.Length);
            foreach (var f in files)
            {
                var c = JsonUtility.FromJson<ChunkData>(File.ReadAllText(f));
                Assert.AreEqual(Path.GetFileNameWithoutExtension(f), c.id);
                Assert.AreEqual(20, c.width);
                Assert.AreEqual(9, c.height);
                var m = ChunkSolver.Measure(c, GameConfig.SpeedForDifficulty(c.difficulty));
                Assert.IsTrue(m.Clearable, c.id);
            }
        }
    }
}
