using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FlipRun.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FlipRun.Tests
{
    public class GameLoopTests
    {
        const string TestHighScoreKey = "FlipRun.Test.HighScore";

        /// <summary>テストから「押した」を1回だけ送る入力。</summary>
        sealed class ScriptedInput : IFlipInput
        {
            public bool Flip, Retry;
            public bool FlipPressed() { bool v = Flip; Flip = false; return v; }
            public bool RetryPressed() { bool v = Retry; Retry = false; return v; }
            public bool Mute;
            public bool MutePressed() { bool v = Mute; Mute = false; return v; }
        }

        /// <summary>最初に指定のチャンク、そのあとは平らなチャンクを返す。</summary>
        sealed class FirstThenFlat : IChunkSource
        {
            readonly ChunkData _first;
            bool _used;
            public FirstThenFlat(ChunkData first) { _first = first; }
            public ChunkData Next(int difficulty, string previousId)
            {
                if (_used) { var f = RunSession.SafeChunk(); f.id = "flat"; return f; }
                _used = true;
                return _first;
            }
        }

        static ChunkData SpikeChunk()
        {
            return new ChunkData
            {
                id = "spike_test", difficulty = 1, width = 20, height = 9,
                rows = new[]
                {
                    "11111111111111111111",
                    "00000000000000000000", "00000000000000000000", "00000000000000000000",
                    "00000000000000000000", "00000000000000000000", "00000000000000000000",
                    "00000000002000000000",
                    "11111111111111111111",
                },
            };
        }

        readonly List<GameObject> _spawned = new List<GameObject>();
        HighScoreStore _scores;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 4f;
            _scores = new HighScoreStore(TestHighScoreKey);
            _scores.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            _scores.Clear();
            foreach (var go in _spawned) if (go) UnityEngine.Object.Destroy(go);
            _spawned.Clear();
            if (Camera.main) UnityEngine.Object.Destroy(Camera.main.gameObject);
        }

        FlipRunGame CreateGame(IFlipInput input, Func<IChunkSource> source, Func<RunOptions> options = null)
        {
            var go = new GameObject("FlipRunGame (test)");
            _spawned.Add(go);
            var game = go.AddComponent<FlipRunGame>();
            game.Controls = input;
            game.ChunkSourceFactory = source;
            if (options != null) game.OptionsFactory = options;
            game.HighScores = _scores;
            game.Analytics = new DebugAnalytics();
            return game;
        }

        static IEnumerator WaitUntil(Func<bool> cond, float realSeconds)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(cond(), "時間内に条件を満たしませんでした");
        }

        [UnityTest]
        public IEnumerator 入力なしでトゲのチャンクを走ると死亡してリザルトに移りリトライできる()
        {
            var input = new ScriptedInput();
            var game = CreateGame(input, () => new FirstThenFlat(SpikeChunk()));
            var phases = new List<GamePhase>();
            game.PhaseChanged += phases.Add;

            yield return null; // Start → タイトル
            Assert.AreEqual(GamePhase.Title, game.Phase);
            Assert.IsTrue(game.Hud.TitlePanel.activeSelf);

            input.Flip = true; // クリック/スペースでラン開始
            yield return null;
            Assert.AreEqual(GamePhase.Running, game.Phase);
            Assert.IsFalse(game.Hud.TitlePanel.activeSelf);

            yield return WaitUntil(() => game.Phase == GamePhase.Dying, 15f);
            Assert.AreEqual(DeathCause.Spike, game.LastDeathCause);
            Assert.AreEqual("spike_test", game.LastDeathChunkId);
            int meters = game.Session.DistanceMeters;
            Assert.That(meters, Is.InRange(25, 30), "安全区間18マス＋トゲまで約10マス");

            // ヒットストップ：死亡した瞬間に時間が止まり、0.1秒後に元の速さに戻る
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(game.transform.Find("Player").gameObject.activeSelf, "プレイヤーは消えて");
            Assert.Greater(game.transform.Find("Shards").GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled), 8, "破片に砕ける");
            float diedAt = Time.time;
            float diedAtReal = Time.unscaledTime;
            yield return WaitUntil(() => Time.timeScale > 0f, 2f);
            Assert.GreaterOrEqual(Time.unscaledTime - diedAtReal, FlipRunGame.HitStopTime - 0.02f);
            Assert.AreEqual(4f, Time.timeScale, "ヒットストップのあとは元の timeScale に戻る");
            yield return WaitUntil(() => game.Phase == GamePhase.Result, 5f);
            Assert.GreaterOrEqual(Time.time - diedAt + FlipRunGame.HitStopTime, FlipRunGame.ResultDelay - 0.05f, "死亡から0.6秒後にリザルト");
            Assert.Contains(SfxId.Death, game.Sound.History);
            Assert.Contains(SfxId.HighScore, game.Sound.History);
            var sent = ((DebugAnalytics)game.Analytics).Sent;
            var start = sent.Find(e => e.name == AnalyticsEvents.LevelStart);
            Assert.AreEqual(1, start.p["run_index"]);
            var fail = sent.Find(e => e.name == AnalyticsEvents.LevelFail);
            Assert.AreEqual("spike", fail.p["death_cause"]);
            Assert.AreEqual("spike_test", fail.p["chunk_id"]);
            Assert.AreEqual(meters, fail.p["distance"]);
            Assert.IsTrue(sent.Exists(e => e.name == AnalyticsEvents.LevelComplete), "ハイスコア更新時に level_complete");
            Assert.IsTrue(game.Hud.ResultPanel.activeSelf);
            StringAssert.Contains($"距離 {meters}m", game.Hud.ResultText);
            StringAssert.Contains($"スコア {meters}", game.Hud.ResultText);
            Assert.IsTrue(game.Hud.ResultIsNew, "初回はハイスコア更新");
            Assert.AreEqual(meters, _scores.Best);
            CollectionAssert.AreEqual(new[] { GamePhase.Title, GamePhase.Running, GamePhase.Dying, GamePhase.Result }, phases);

            // R で即リトライ（タイトルには戻らない）
            input.Retry = true;
            yield return null;
            Assert.AreEqual(GamePhase.Running, game.Phase);
            Assert.AreEqual(2, game.RunIndex);
            Assert.AreEqual(0, game.Session.DistanceMeters);
            Assert.IsFalse(game.Hud.ResultPanel.activeSelf);

            // 2回目は同じスコアなので NEW は出ない
            yield return WaitUntil(() => game.Phase == GamePhase.Result, 20f);
            Assert.IsFalse(game.Hud.ResultIsNew);

            // スペース/クリックでもリトライできる
            input.Flip = true;
            yield return null;
            Assert.AreEqual(GamePhase.Running, game.Phase);
            Assert.AreEqual(3, game.RunIndex);

            // 3回死ぬとインタースティシャル広告のスタブが呼ばれる
            Assert.IsFalse(sent.Exists(e => e.name == AnalyticsEvents.AdInterstitialShown));
            yield return WaitUntil(() => game.Phase == GamePhase.Dying, 20f);
            Assert.AreEqual(3, game.DeathCount);
            var ad = sent.Find(e => e.name == AnalyticsEvents.AdInterstitialShown);
            Assert.AreEqual("death_3", ad.p["placement"]);
        }

        [UnityTest]
        public IEnumerator 検証ツールが見つけた入力列を再生するとチャンクを抜けられる()
        {
            var all = ChunkLoader.LoadAll();
            Assert.AreEqual(60, all.Count, "Resources/Chunks に60チャンクがある");

            foreach (string id in new[] { "chunk_04_001", "chunk_07_003", "chunk_10_001" })
            {
                var chunk = all.Single(c => c.id == id);
                float speed = GameConfig.SpeedForDifficulty(chunk.difficulty);
                var solved = ChunkSolver.Solve(chunk, new ChunkSolver.Options { Speed = speed });
                Assert.IsTrue(solved.Clearable, id);
                Assert.Greater(solved.Flips, 0, id + " は反転が必要なチャンク");
                var presses = new HashSet<int>(solved.PressFrames);
                float exitX = chunk.width + GameConfig.PlayerHalfSize;

                foreach (bool withInput in new[] { true, false })
                {
                    var game = CreateGame(new NoInput(), () => new FirstThenFlat(chunk),
                        () => new RunOptions { SafeStart = false, StartX = 0.5f, FixedSpeed = speed });
                    if (withInput) game.StepInputOverride = f => presses.Contains(f);
                    game.StartRun();
                    yield return WaitUntil(() => game.Session.IsDead || game.Session.State.X >= exitX + 1f, 15f);
                    if (withInput)
                        Assert.IsFalse(game.Session.IsDead, $"{id}: {game.LastDeathCause} at x={game.Session.State.X:0.00}");
                    else
                        Assert.IsTrue(game.Session.IsDead, $"{id}: 入力なしでは抜けられない");
                    UnityEngine.Object.Destroy(game.gameObject);
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator 反転すると色が入れ替わり着地でスクワッシュと効果音が出る()
        {
            var game = CreateGame(new NoInput(), () => new FirstThenFlat(RunSession.SafeChunk()),
                () => new RunOptions { SafeStart = false, StartX = 0.5f });
            game.StepInputOverride = f => f == 10;
            game.StartRun();
            Assert.AreEqual(0f, game.Palette.Blend);
            var player = game.transform.Find("Player");

            yield return WaitUntil(() => game.Session.State.Frame > 10, 5f);
            Assert.AreEqual(1, game.Session.State.GravityDir);
            Assert.Contains(SfxId.Flip, game.Sound.History);
            yield return WaitUntil(() => game.Palette.Blend >= 1f, 2f); // 0.08秒で入れ替わる
            Assert.AreEqual(PaletteFx.BackgroundB, Camera.main.backgroundColor);
            Assert.IsTrue(player.GetComponent<SpriteRenderer>().flipY, "重力が上なら絵を上下反転");

            yield return WaitUntil(() => game.Session.State.Grounded, 5f);
            Assert.Contains(SfxId.Land, game.Sound.History);
            Assert.Greater(player.localScale.x, player.localScale.y, "着地の直後は横に潰れている");
            yield return WaitUntil(() => Mathf.Approximately(player.localScale.x, player.localScale.y), 2f); // 0.1秒で戻る
        }

        [UnityTest]
        public IEnumerator Mキーでミュートが切り替わる()
        {
            var input = new ScriptedInput();
            var game = CreateGame(input, () => new FirstThenFlat(RunSession.SafeChunk()));
            yield return null;
            bool before = game.Sound.Muted;
            input.Mute = true;
            yield return null;
            Assert.AreNotEqual(before, game.Sound.Muted);
            Assert.AreEqual(game.Sound.Muted ? 0f : 1f, AudioListener.volume);
            game.ToggleMute();
            Assert.AreEqual(before, game.Sound.Muted);
        }
    }
}
