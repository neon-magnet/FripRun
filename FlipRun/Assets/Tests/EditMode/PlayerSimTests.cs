using NUnit.Framework;

namespace FlipRun.Tests
{
    public class PlayerSimTests
    {
        const float Speed = 6f;

        static PlayerSim NewSim(bool onCeiling = false, float x = 0.5f) => new PlayerSim(PlayerState.Create(x, onCeiling));

        static void Run(PlayerSim sim, ITileWorld w, int frames)
        {
            for (int i = 0; i < frames && !sim.State.IsDead; i++) sim.Step(false, Speed, w);
        }

        [Test]
        public void 平らな地形では死なずに右へ進み接地し続ける()
        {
            var w = new ChunkWorld(TestChunks.Flat());
            var sim = NewSim();
            Run(sim, w, 120);
            Assert.IsFalse(sim.State.IsDead);
            Assert.IsTrue(sim.State.Grounded);
            Assert.AreEqual(0.5f + Speed * 2f, sim.State.X, 1e-3f);
            Assert.AreEqual(-3.1f, sim.State.Y, 1e-4f);
        }

        [Test]
        public void 接地中に押すと重力が反転し天井に着く()
        {
            var w = new ChunkWorld(TestChunks.Flat());
            var sim = NewSim();
            sim.Step(true, Speed, w);
            Assert.IsTrue(sim.State.FlippedThisStep);
            Assert.AreEqual(1, sim.State.GravityDir);
            Run(sim, w, 60);
            Assert.IsTrue(sim.State.Grounded);
            Assert.AreEqual(3.1f, sim.State.Y, 1e-4f);
        }

        [Test]
        public void 空中では反転できない()
        {
            var w = new ChunkWorld(TestChunks.Flat());
            var sim = NewSim();
            sim.Step(true, Speed, w);          // 天井へ向かう
            Run(sim, w, 10);                    // 空中（コヨーテタイムも過ぎている）
            Assert.IsFalse(sim.State.Grounded);
            sim.Step(true, Speed, w);
            Assert.IsFalse(sim.State.FlippedThisStep);
            Assert.AreEqual(1, sim.State.GravityDir);
            // バッファ（0.1秒）が切れたあとに着地しても反転しない
            Run(sim, w, 60);
            Assert.AreEqual(1, sim.State.GravityDir);
            Assert.AreEqual(3.1f, sim.State.Y, 1e-4f);
        }

        /// <summary>床の穴の縁から離れたフレームを探す。</summary>
        static PlayerSim RunUntilLeavesGround(ITileWorld w)
        {
            var sim = NewSim();
            while (sim.State.Grounded) sim.Step(false, Speed, w);
            return sim; // AirFrames == 1
        }

        [Test]
        public void コヨーテタイム内なら足場を離れた直後でも反転できる()
        {
            var w = new ChunkWorld(TestChunks.FloorHole());
            var sim = RunUntilLeavesGround(w);
            Assert.AreEqual(1, sim.State.AirFrames);
            // 離れてから 0.08秒以内（4フレーム目）に押す
            Run(sim, w, GameConfig.CoyoteFrames - 1);
            Assert.AreEqual(GameConfig.CoyoteFrames, sim.State.AirFrames);
            sim.Step(true, Speed, w);
            Assert.IsTrue(sim.State.FlippedThisStep);
            Assert.AreEqual(1, sim.State.GravityDir);
        }

        [Test]
        public void コヨーテタイムを過ぎると反転できず穴に落ちる()
        {
            var w = new ChunkWorld(TestChunks.FloorHole());
            var sim = RunUntilLeavesGround(w);
            Run(sim, w, GameConfig.CoyoteFrames); // AirFrames = Coyote + 1（0.083秒 > 0.08秒）
            sim.Step(true, Speed, w);
            Assert.IsFalse(sim.State.FlippedThisStep);
            Assert.AreEqual(-1, sim.State.GravityDir);
            Run(sim, w, 120);
            Assert.AreEqual(DeathCause.OutOfBounds, sim.State.Death);
        }

        [Test]
        public void 着地の直前に押した入力は着地した瞬間に反転になる()
        {
            var w = new ChunkWorld(TestChunks.Flat());
            // まず着地するフレームを調べる
            var probe = NewSim();
            probe.Step(true, Speed, w);
            int landFrame = -1;
            while (landFrame < 0) { probe.Step(false, Speed, w); if (probe.State.LandedThisStep) landFrame = probe.State.Frame - 1; }

            // 着地の3フレーム（0.05秒）前に押す
            var sim = NewSim();
            sim.Step(true, Speed, w);
            while (sim.State.Frame < landFrame - 3) sim.Step(false, Speed, w);
            sim.Step(true, Speed, w);
            Assert.IsFalse(sim.State.FlippedThisStep, "空中ではまだ反転しない");
            while (sim.State.Frame <= landFrame) sim.Step(false, Speed, w);
            Assert.IsTrue(sim.State.LandedThisStep);
            Assert.IsTrue(sim.State.FlippedThisStep, "着地した瞬間に反転する");
            Assert.AreEqual(-1, sim.State.GravityDir);
        }

        [Test]
        public void バッファより前に押した入力は着地しても反転にならない()
        {
            var w = new ChunkWorld(TestChunks.Flat());
            var sim = NewSim();
            sim.Step(true, Speed, w);
            Run(sim, w, 5);
            sim.Step(true, Speed, w); // 着地のずっと前
            while (!sim.State.Grounded) sim.Step(false, Speed, w);
            Assert.AreEqual(1, sim.State.GravityDir);
        }

        [Test]
        public void トゲに触れると死ぬ()
        {
            var w = new ChunkWorld(TestChunks.FloorSpike());
            var sim = NewSim();
            Run(sim, w, 300);
            Assert.AreEqual(DeathCause.Spike, sim.State.Death);
            Assert.Less(sim.State.X, 11f);
        }

        [Test]
        public void 画面の外に出ると死ぬ()
        {
            var w = new ChunkWorld(TestChunks.FloorHole());
            var sim = NewSim();
            Run(sim, w, 300);
            Assert.AreEqual(DeathCause.OutOfBounds, sim.State.Death);
            Assert.Less(sim.State.Y, GameConfig.KillYMin);
        }

        [Test]
        public void ブロックの側面に正面からぶつかると死ぬ()
        {
            var w = new ChunkWorld(TestChunks.FloorWall());
            var sim = NewSim();
            Run(sim, w, 300);
            Assert.AreEqual(DeathCause.SideHit, sim.State.Death);
            Assert.AreEqual(10f, sim.State.X + GameConfig.PlayerHalfSize, 0.11f);
        }

        [Test]
        public void 天井に立っていればトゲも壁も通り抜けられる()
        {
            foreach (var chunk in new[] { TestChunks.FloorSpike(), TestChunks.FloorWall() })
            {
                var w = new ChunkWorld(chunk);
                var sim = NewSim(onCeiling: true);
                Run(sim, w, 300);
                Assert.IsFalse(sim.State.IsDead, chunk.rows[7]);
            }
        }

        [Test]
        public void 速度は100mごとに増えて12で止まる()
        {
            Assert.AreEqual(6f, GameConfig.SpeedForDistance(0f));
            Assert.AreEqual(6f, GameConfig.SpeedForDistance(99.9f));
            Assert.AreEqual(6.25f, GameConfig.SpeedForDistance(100f));
            Assert.AreEqual(12f, GameConfig.SpeedForDistance(100000f));
            Assert.AreEqual(1, GameConfig.DifficultyForDistance(0f));
            Assert.AreEqual(2, GameConfig.DifficultyForDistance(150f));
            Assert.AreEqual(10, GameConfig.DifficultyForDistance(5000f));
        }
    }
}
