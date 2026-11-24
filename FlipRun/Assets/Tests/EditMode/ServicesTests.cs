using System.Collections.Generic;
using NUnit.Framework;

namespace FlipRun.Tests
{
    public class NearMissTests
    {
        sealed class RecordingAnalytics : IAnalytics
        {
            public readonly List<(string, Dictionary<string, object>)> Sent = new List<(string, Dictionary<string, object>)>();
            public void Log(string e, Dictionary<string, object> p = null) => Sent.Add((e, p));
        }

        /// <summary>床のトゲ（10列目）の上を、下端がトゲの先から gap だけ上の高さで横切る。</summary>
        static int PassOverSpike(float gap)
        {
            var world = new ChunkWorld(TestChunks.FloorSpike());
            var tracker = new NearMissTracker();
            float spikeTop = -3.5f + 0.6f;
            var s = PlayerState.Create(7f, false);
            s.Y = spikeTop + gap + GameConfig.PlayerHalfSize;
            int total = 0;
            for (float x = 7f; x < 13f; x += 0.1f)
            {
                s.X = x;
                total += tracker.Update(world, s);
            }
            return total;
        }

        [Test]
        public void トゲから0_3マス以内を通過したらニアミスが1回だけ成立する()
        {
            Assert.AreEqual(1, PassOverSpike(0.25f));
            Assert.AreEqual(1, PassOverSpike(0.05f));
        }

        [Test]
        public void トゲから離れて通過したらニアミスにならない()
        {
            Assert.AreEqual(0, PassOverSpike(0.35f));
            Assert.AreEqual(0, PassOverSpike(2f));
        }

        [Test]
        public void 死んだらニアミスにならない()
        {
            var world = new ChunkWorld(TestChunks.FloorSpike());
            var tracker = new NearMissTracker();
            var s = PlayerState.Create(7f, false);
            int total = 0;
            while (!s.IsDead && s.Frame < 400)
            {
                PlayerSim.Step(ref s, false, 6f, world);
                total += tracker.Update(world, s);
            }
            Assert.IsTrue(s.IsDead);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void ぎりぎりで反転してトゲを避けるとボーナス5点がスコアに入る()
        {
            // 床のトゲ（10列目）の手前で反転するタイミングを変えて試し、ニアミスになるものを探す
            int found = -1;
            for (int pressFrame = 0; pressFrame < 120 && found < 0; pressFrame++)
            {
                var s = new RunSession(new FixedSource(TestChunks.FloorSpike()), new RunOptions { SafeStart = false, StartX = 0.5f });
                s.UpdateChunks(0f, 60f);
                int events = 0;
                s.NearMiss += n => events += n;
                for (int f = 0; f < 200 && !s.IsDead; f++) s.Step(f == pressFrame);
                if (s.IsDead || events == 0) continue;
                found = pressFrame;
                Assert.AreEqual(1, events);
                Assert.AreEqual(NearMissTracker.Bonus, s.BonusScore);
                Assert.AreEqual(s.DistanceMeters + NearMissTracker.Bonus, s.Score);
            }
            Assert.GreaterOrEqual(found, 0, "トゲすれすれで反転するタイミングが見つからない");

            // 早めに反転すればニアミスにならない
            var early = new RunSession(new FixedSource(TestChunks.FloorSpike()), new RunOptions { SafeStart = false, StartX = 0.5f });
            early.UpdateChunks(0f, 60f);
            for (int f = 0; f < 200; f++) early.Step(f == 0);
            Assert.IsFalse(early.IsDead);
            Assert.AreEqual(0, early.BonusScore);
        }

        sealed class FixedSource : IChunkSource
        {
            readonly ChunkData _c;
            public FixedSource(ChunkData c) { _c = c; }
            public ChunkData Next(int d, string prev) => _c;
        }

        [Test]
        public void 広告スタブはリワードをすぐに渡し分析イベントを送る()
        {
            var a = new RecordingAnalytics();
            var ads = new StubAdService(a);
            bool rewarded = false;
            ads.ShowRewarded(AnalyticsEvents.PlacementRevive, () => rewarded = true);
            ads.ShowInterstitial(AnalyticsEvents.PlacementDeath3);
            Assert.IsTrue(rewarded);
            Assert.AreEqual(AnalyticsEvents.AdRewardedShown, a.Sent[0].Item1);
            Assert.AreEqual("revive", a.Sent[0].Item2["placement"]);
            Assert.AreEqual(AnalyticsEvents.AdInterstitialShown, a.Sent[1].Item1);
            Assert.AreEqual("death_3", a.Sent[1].Item2["placement"]);
        }

        [Test]
        public void level_failのパラメータに死因とチャンクIDが入る()
        {
            var s = new RunSession(new FixedSource(TestChunks.FloorSpike()), new RunOptions { SafeStart = false, StartX = 0.5f });
            s.UpdateChunks(0f, 60f);
            while (!s.IsDead) s.Step(false);
            var p = AnalyticsEvents.LevelFailParams(s);
            Assert.AreEqual("spike", p["death_cause"]);
            Assert.AreEqual("test", p["chunk_id"]);
            Assert.AreEqual(s.DistanceMeters, p["distance"]);
            Assert.AreEqual(0, p["coins"]);
            Assert.That((float)p["time_sec"], Is.GreaterThan(1f));
        }
    }
}
