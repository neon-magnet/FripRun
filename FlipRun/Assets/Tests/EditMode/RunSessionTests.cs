using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FlipRun.Tests
{
    public class RunSessionTests
    {
        /// <summary>決まったチャンクを順に返す。最後のものを繰り返す。</summary>
        sealed class ListSource : IChunkSource
        {
            readonly ChunkData[] _list;
            int _i;
            public List<int> RequestedDifficulties = new List<int>();
            public ListSource(params ChunkData[] list) { _list = list; }
            public ChunkData Next(int difficulty, string previousId)
            {
                RequestedDifficulties.Add(difficulty);
                return _list[System.Math.Min(_i++, _list.Length - 1)];
            }
        }

        static ChunkData Coins()
        {
            return TestChunks.Make(
                "11111111111111111111",
                "00000000000000000000",
                "00000000000000000000",
                "00000000000000000000",
                "00000000000000000000",
                "00000000000000000000",
                "00000000000000000000",
                "00000444000000000000",
                "11111111111111111111");
        }

        [Test]
        public void 最初の約18マスは障害物のない安全区間()
        {
            var s = new RunSession(new ListSource(TestChunks.FloorSpike()));
            s.UpdateChunks(0f, 40f);
            Assert.AreEqual(RunSession.SafeChunkId, s.World.Chunks[0].Chunk.id);
            Assert.AreEqual(20, s.World.Chunks[1].StartCol);
            // 何もしなくても 3秒（180フレーム）は生きている
            for (int i = 0; i < 180; i++) s.Step(false);
            Assert.IsFalse(s.IsDead);
            Assert.GreaterOrEqual(s.DistanceMeters, 17);
            // そのまま進むと次のチャンクのトゲで死ぬ
            for (int i = 0; i < 300 && !s.IsDead; i++) { s.Step(false); s.UpdateChunks(s.State.X - 10f, s.State.X + 30f); }
            Assert.AreEqual(DeathCause.Spike, s.State.Death);
            Assert.AreEqual("test", s.CurrentChunkId);
        }

        [Test]
        public void 距離とスコアが正しく増える()
        {
            var s = new RunSession(new ListSource(Coins()), new RunOptions { SafeStart = false, StartX = 0.5f });
            s.UpdateChunks(0f, 60f);
            for (int i = 0; i < 30; i++) s.Step(false); // 0.5秒 = 3マス（コインの手前）
            Assert.AreEqual(3f, s.Distance, 1e-3f);
            Assert.AreEqual(0, s.Coins);
            Assert.AreEqual(s.DistanceMeters, s.Score, "コインを拾う前はスコア＝距離");
            for (int i = 0; i < 95; i++) s.Step(false);
            Assert.AreEqual(12.5f, s.Distance, 1e-3f);
            Assert.AreEqual(12, s.DistanceMeters, "距離は切り捨て");
            // 床すれすれのコイン3枚を拾っている（y=-3 の行）
            Assert.AreEqual(3, s.Coins);
            Assert.AreEqual(12 + 3 * 10, s.Score);
        }

        [Test]
        public void 同じコインは2回拾えずマスから消える()
        {
            var s = new RunSession(new ListSource(Coins()), new RunOptions { SafeStart = false, StartX = 0.5f });
            var collected = new List<(int, int)>();
            s.CoinCollected += (c, r) => collected.Add((c, r));
            s.UpdateChunks(0f, 60f);
            for (int i = 0; i < 200; i++) s.Step(false);
            Assert.AreEqual(3, collected.Count);
            Assert.AreEqual(3, collected.Distinct().Count());
            Assert.AreEqual(Tile.Empty, s.World.GetTile(5, -3));
            Assert.AreEqual(Tile.Coin, s.World.GetTile(25, -3), "次のチャンクの同じ位置のコインは別物");
        }

        [Test]
        public void 画面の左外に出たチャンクは消える()
        {
            var s = new RunSession(new ListSource(TestChunks.Flat()), new RunOptions { SafeStart = false });
            var removed = new List<PlacedChunk>();
            s.ChunkRemoved += removed.Add;
            s.UpdateChunks(0f, 100f);
            Assert.AreEqual(5, s.World.Chunks.Count);
            s.UpdateChunks(45f, 100f);
            Assert.AreEqual(2, removed.Count);
            Assert.AreEqual(40, s.World.Chunks[0].StartCol);
        }

        [Test]
        public void 距離に応じた難易度のチャンクを要求する()
        {
            var src = new ListSource(TestChunks.Flat());
            var s = new RunSession(src, new RunOptions { SafeStart = false, StartX = 0f });
            s.UpdateChunks(0f, 400f);
            // チャンクの左端 0,20,...,380 → 難易度 = 1 + 距離/150
            var expected = Enumerable.Range(0, 20).Select(i => GameConfig.DifficultyForDistance(i * 20)).ToList();
            CollectionAssert.AreEqual(expected, src.RequestedDifficulties);
            Assert.AreEqual(3, src.RequestedDifficulties.Last());
        }

        [Test]
        public void ライブラリは同じチャンクを2回連続で出さない()
        {
            var chunks = Enumerable.Range(0, 3).Select(i =>
            {
                var c = TestChunks.Flat();
                c.id = "c" + i;
                c.difficulty = 2;
                return c;
            }).ToList();
            var lib = new ChunkLibrary(chunks, 1);
            string prev = null;
            var seen = new HashSet<string>();
            for (int i = 0; i < 300; i++)
            {
                var c = lib.Next(2, prev);
                Assert.AreNotEqual(prev, c.id);
                seen.Add(c.id);
                prev = c.id;
            }
            Assert.AreEqual(3, seen.Count);
            Assert.AreEqual(2, lib.Next(9, null).difficulty, "無い難易度は近いものから選ぶ");
        }
    }
}
