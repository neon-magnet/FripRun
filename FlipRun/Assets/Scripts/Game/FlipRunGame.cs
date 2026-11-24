using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FlipRun.Game
{
    public enum GamePhase { Title, Running, Dying, Result }

    /// <summary>
    /// ゲーム全体の進行：タイトル → ラン → 死亡（0.6秒）→ リザルト → 即リトライ。
    /// プレイヤーの動きは PlayerSim（RunSession の中）を 1/60 秒刻みで進めるだけで、Rigidbody2D は使わない。
    /// 地形・プレイヤー・UI・演出は起動時にコードで組み立てる。
    /// </summary>
    public sealed class FlipRunGame : MonoBehaviour
    {
        public const float ResultDelay = 0.6f;    // 死亡からリザルトまで（ヒットストップを含む）
        public const float HitStopTime = 0.1f;    // 死亡時のヒットストップ（Time.timeScale = 0）
        public const float ShakeTime = 0.2f;
        public const float ShakeAmplitude = 0.3f;
        public const int InterstitialEveryDeaths = 3;

        const int MaxStepsPerFrame = 8;
        const float CameraLead = 5f;   // プレイヤーを画面の左寄りに置く
        const float FillAhead = 22f;   // カメラの右端よりこれだけ先までチャンクを置く
        const float TrailInterval = 0.03f;

        // ---- 外から差し替えられるもの（テスト用） ----
        public IFlipInput Controls { get; set; } = new InputSystemFlipInput();
        public Func<IChunkSource> ChunkSourceFactory { get; set; }
        public Func<RunOptions> OptionsFactory { get; set; } = () => new RunOptions();
        /// <summary>設定するとシミュレーションの各フレームでこれを入力として使う（引数はランのフレーム番号）。</summary>
        public Func<int, bool> StepInputOverride { get; set; }
        public HighScoreStore HighScores { get; set; } = new HighScoreStore();
        public IAnalytics Analytics { get; set; }
        /// <summary>未設定なら、初めて使うときに StubAdService（今の Analytics に記録する）を作る。</summary>
        public IAdService Ads
        {
            get => _ads ??= new StubAdService(Analytics, Debug.Log);
            set => _ads = value;
        }
        IAdService _ads;

        // ---- 状態 ----
        public GamePhase Phase { get; private set; } = GamePhase.Title;
        public RunSession Session { get; private set; }
        public int RunIndex { get; private set; }
        public int DeathCount { get; private set; }
        public GameHud Hud { get; private set; }
        public Sfx Sound { get; private set; }
        public PaletteFx Palette { get; } = new PaletteFx();
        public DeathCause LastDeathCause { get; private set; }
        public string LastDeathChunkId { get; private set; }
        public int NearMissCount { get; private set; }

        public event Action<GamePhase> PhaseChanged;

        Camera _cam;
        GameArt _art;
        ChunkView _view;
        Transform _player;
        SpriteRenderer _playerRenderer;
        PlayerVisual _visual;
        SpriteParticles _trail, _shards;
        readonly ScreenShake _shake = new ScreenShake();
        Vector3 _camBase;
        float _accumulator;
        float _phaseTimer;
        float _trailTimer;
        float _hitStop;
        float _savedTimeScale = 1f;
        bool _pendingFlip;
        List<ChunkData> _chunks;
        bool _built;
        static bool _sessionStarted;

        void Awake()
        {
            Build();
        }

        void Start()
        {
            if (!_sessionStarted)
            {
                _sessionStarted = true;
                Analytics.Log(AnalyticsEvents.SessionStart);
            }
            if (Phase == GamePhase.Title) EnterTitle();
        }

        void OnApplicationQuit()
        {
            Analytics?.Log(AnalyticsEvents.SessionEnd);
        }

        void OnDestroy()
        {
            if (_hitStop > 0f) Time.timeScale = _savedTimeScale;
        }

        void Build()
        {
            if (_built) return;
            _built = true;

            Analytics ??= new DebugAnalytics();

            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = camGo.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = 5f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = PaletteFx.BackgroundA;
            _camBase = new Vector3(0f, 0f, -10f);
            _cam.transform.position = _camBase;

            _art = new GameArt();
            var tiles = new Dictionary<FlipRun.Tile, TileBase>
            {
                // ブロックは白にして、色は Tilemap.color（パレット）で決める
                { FlipRun.Tile.Block, _art.Block },
                { FlipRun.Tile.SpikeUp, _art.SpikeUp },
                { FlipRun.Tile.SpikeDown, _art.SpikeDown },
                { FlipRun.Tile.Coin, _art.Coin },
            };
            _view = new ChunkView(transform, tiles);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            _playerRenderer = playerGo.AddComponent<SpriteRenderer>();
            _playerRenderer.sortingOrder = 5;
            _player = playerGo.transform;
            _visual = new PlayerVisual(_player, _playerRenderer, _art);
            _player.gameObject.SetActive(false);

            _trail = new SpriteParticles(transform, "Trail", _art.Particle, 64, 4);
            _shards = new SpriteParticles(transform, "Shards", _art.Particle, 32, 6);

            Hud = new GameHud(transform);
            Sound = new Sfx(gameObject);
            Hud.SetMuted(Sound.Muted);
            Hud.MuteClicked += ToggleMute;
            ApplyPalette();
        }

        // ---- 状態の切り替え ----

        void EnterTitle()
        {
            Build();
            SetPhase(GamePhase.Title);
            Hud.ShowTitle(HighScores.Best);
            // タイトルの背景として安全区間だけ見せておく
            _view.Clear();
            _view.Place(new PlacedChunk { Chunk = RunSession.SafeChunk(), StartCol = -GameConfig.ChunkWidth });
            _view.Place(new PlacedChunk { Chunk = RunSession.SafeChunk(), StartCol = 0 });
            _camBase = new Vector3(2f + CameraLead, 0f, -10f);
            Palette.Snap(false);
            ApplyPalette();
        }

        /// <summary>ランを始める（リトライも同じ）。</summary>
        public void StartRun()
        {
            Build();
            if (_hitStop > 0f)
            {
                _hitStop = 0f;
                Time.timeScale = _savedTimeScale;
            }
            if (Session != null)
            {
                Session.ChunkPlaced -= _view.Place;
                Session.ChunkRemoved -= _view.Remove;
                Session.CoinCollected -= OnCoin;
                Session.NearMiss -= OnNearMiss;
            }
            _view.Clear();
            // スタート地点より左（RunWorld では平らな地形）も画面に見えるので描いておく
            _view.Place(new PlacedChunk { Chunk = RunSession.SafeChunk(), StartCol = -GameConfig.ChunkWidth });
            var options = OptionsFactory();
            Session = new RunSession(CreateSource(), options);
            Session.ChunkPlaced += _view.Place;
            Session.ChunkRemoved += _view.Remove;
            Session.CoinCollected += OnCoin;
            Session.NearMiss += OnNearMiss;

            _accumulator = 0f;
            _pendingFlip = false;
            LastDeathCause = DeathCause.None;
            LastDeathChunkId = null;
            NearMissCount = 0;
            RunIndex++;
            _trail.Clear();
            _shards.Clear();
            _visual.Reset();
            Palette.Snap(options.StartOnCeiling);
            _player.gameObject.SetActive(true);
            UpdateChunks();
            SyncVisuals(0f);
            Hud.ShowRun();
            Hud.UpdateRun(0, 0, 0);
            SetPhase(GamePhase.Running);
            Analytics.Log(AnalyticsEvents.LevelStart, new Dictionary<string, object> { { "run_index", RunIndex } });
        }

        IChunkSource CreateSource()
        {
            if (ChunkSourceFactory != null) return ChunkSourceFactory();
            if (_chunks == null) _chunks = ChunkLoader.LoadAll();
            if (_chunks.Count == 0)
            {
                Debug.LogError("[FlipRun] Resources/Chunks にチャンクがありません。FlipRun > Generate Chunks を実行してください。");
                _chunks.Add(RunSession.SafeChunk());
            }
            return new ChunkLibrary(_chunks, Environment.TickCount);
        }

        void SetPhase(GamePhase p)
        {
            Phase = p;
            _phaseTimer = 0f;
            PhaseChanged?.Invoke(p);
        }

        public void ToggleMute()
        {
            Sound.ToggleMute();
            Hud.SetMuted(Sound.Muted);
        }

        /// <summary>
        /// リザルト画面の「復活する」ボタンの呼び出し口（ブリーフ第7章）。MVPではボタンを出さない。
        /// </summary>
        public void RequestRevive()
        {
            Ads.ShowRewarded(AnalyticsEvents.PlacementRevive, () => Debug.Log("[FlipRun] revive: MVPでは未実装"));
        }

        // ---- 毎フレーム ----

        void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;

            // ヒットストップ中は時間が止まっている（unscaled で数える）
            if (_hitStop > 0f)
            {
                _hitStop -= udt;
                if (_hitStop <= 0f) Time.timeScale = _savedTimeScale;
            }

            if (Controls.MutePressed()) ToggleMute();
            bool flip = Controls.FlipPressed();
            switch (Phase)
            {
                case GamePhase.Title:
                    if (flip) StartRun();
                    break;

                case GamePhase.Running:
                    if (flip) _pendingFlip = true;
                    Simulate(dt);
                    break;

                case GamePhase.Dying:
                    if (_hitStop <= 0f) _phaseTimer += dt;
                    if (_phaseTimer >= ResultDelay - HitStopTime) EnterResult();
                    break;

                case GamePhase.Result:
                    if (flip || Controls.RetryPressed()) StartRun();
                    break;
            }

            Palette.Tick(dt);
            ApplyPalette();
            _trail.Tick(dt);
            _shards.Tick(dt);
            Hud.Tick(dt);
        }

        void LateUpdate()
        {
            Vector2 shake = _shake.Tick(Time.unscaledDeltaTime);
            _cam.transform.position = _camBase + new Vector3(shake.x, shake.y, 0f);
        }

        void Simulate(float dt)
        {
            _accumulator += dt;
            int steps = 0;
            while (_accumulator >= GameConfig.Dt && steps < MaxStepsPerFrame)
            {
                _accumulator -= GameConfig.Dt;
                steps++;
                bool press = StepInputOverride != null ? StepInputOverride(Session.State.Frame) : _pendingFlip;
                _pendingFlip = false;
                Session.Step(press);

                var s = Session.State;
                if (s.IsDead)
                {
                    OnDeath();
                    break;
                }
                if (s.LandedThisStep) OnLand(s);
                if (s.FlippedThisStep) OnFlip(s);
            }
            if (steps == MaxStepsPerFrame) _accumulator = 0f; // 処理落ちしたら追いつこうとしない
            if (Phase != GamePhase.Running) return;

            UpdateChunks();
            SyncVisuals(dt);
            EmitTrail(dt);
            Hud.UpdateRun(Session.DistanceMeters, Session.Coins, Session.Score);
        }

        // ---- 出来事ごとの演出 ----

        void OnFlip(in PlayerState s)
        {
            Palette.SetTarget(s.GravityUp);
            Sound.Play(SfxId.Flip);
        }

        void OnLand(in PlayerState s)
        {
            _visual.Land();
            Sound.Play(SfxId.Land);
            // 着地の土ぼこり
            var feet = new Vector2(s.X, s.Y + s.GravityDir * GameConfig.PlayerHalfSize);
            for (int i = 0; i < 6; i++)
            {
                var v = new Vector2(UnityEngine.Random.Range(-2.5f, 1f), -s.GravityDir * UnityEngine.Random.Range(0.5f, 1.8f));
                _trail.Emit(feet, v, 0.3f, 0.2f, ParticleColor(0.9f));
            }
        }

        void OnCoin(int col, int row)
        {
            Sound.Play(SfxId.Coin);
            _view.ClearCell(col, row);
            var p = new Vector2(col + 0.5f, row);
            for (int i = 0; i < 6; i++)
                _trail.Emit(p, UnityEngine.Random.insideUnitCircle * 3f, 0.3f, 0.12f, GreyboxArt.Coin);
        }

        void OnNearMiss(int count)
        {
            NearMissCount += count;
            Hud.ShowNearMiss();
            Sound.Play(SfxId.NearMiss);
        }

        void OnDeath()
        {
            var s = Session.State;
            LastDeathCause = s.Death;
            LastDeathChunkId = Session.CurrentChunkId;
            DeathCount++;
            _player.gameObject.SetActive(false);
            SetPhase(GamePhase.Dying);

            // プレイヤーが破片に砕ける
            var center = new Vector2(s.X, s.Y);
            var color = _art.PlayerColor;
            for (int i = 0; i < 16; i++)
            {
                var offset = new Vector2((i % 4 - 1.5f) * 0.2f, (i / 4 - 1.5f) * 0.2f);
                var v = offset.normalized * UnityEngine.Random.Range(3f, 7f) + new Vector2(UnityEngine.Random.Range(-1f, 2f), 0f);
                _shards.Emit(center + offset, v, UnityEngine.Random.Range(0.5f, 0.9f), 0.2f, color,
                    gravity: s.GravityDir * 20f, spin: UnityEngine.Random.Range(-720f, 720f));
            }
            _shake.Start(ShakeTime, ShakeAmplitude);
            Sound.Play(SfxId.Death);

            // ヒットストップ
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : _savedTimeScale;
            Time.timeScale = 0f;
            _hitStop = HitStopTime;

            Analytics.Log(AnalyticsEvents.LevelFail, AnalyticsEvents.LevelFailParams(Session));
            if (DeathCount % InterstitialEveryDeaths == 0) Ads.ShowInterstitial(AnalyticsEvents.PlacementDeath3);
        }

        void EnterResult()
        {
            int score = Session.Score;
            bool isNew = HighScores.Submit(score);
            Hud.ShowResult(Session.DistanceMeters, Session.Coins, score, HighScores.Best, isNew);
            if (isNew)
            {
                Sound.Play(SfxId.HighScore);
                Analytics.Log(AnalyticsEvents.LevelComplete, new Dictionary<string, object> { { "score", score } });
            }
            SetPhase(GamePhase.Result);
        }

        // ---- 表示 ----

        void UpdateChunks()
        {
            float halfWidth = _cam.orthographicSize * _cam.aspect;
            float camX = Session.State.X + CameraLead;
            Session.UpdateChunks(camX - halfWidth - 2f, camX + halfWidth + FillAhead);
        }

        void SyncVisuals(float dt)
        {
            var s = Session.State;
            _visual.Apply(s, dt);
            _camBase = new Vector3(s.X + CameraLead, 0f, -10f);
        }

        void EmitTrail(float dt)
        {
            var s = Session.State;
            if (!s.Grounded) return;
            _trailTimer -= dt;
            if (_trailTimer > 0f) return;
            _trailTimer = TrailInterval;
            var feet = new Vector2(s.X - 0.25f, s.Y + s.GravityDir * (GameConfig.PlayerHalfSize - 0.06f));
            var v = new Vector2(UnityEngine.Random.Range(-1.5f, -0.5f), -s.GravityDir * UnityEngine.Random.Range(0.2f, 0.9f));
            _trail.Emit(feet, v, 0.4f, UnityEngine.Random.Range(0.16f, 0.24f), ParticleColor(0.85f));
        }

        Color ParticleColor(float alpha)
        {
            var c = _art.PlayerColor;
            c.a = alpha;
            return c;
        }

        void ApplyPalette()
        {
            _cam.backgroundColor = Palette.Background;
            _view.Terrain.color = Palette.Terrain;
        }
    }
}
