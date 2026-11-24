using System;

namespace FlipRun
{
    public sealed class RunOptions
    {
        /// <summary>プレイヤーのスタート位置（x）。距離はここから数える。</summary>
        public float StartX = 2f;
        /// <summary>最初に障害物なしの平らなチャンクを置くか（安全区間：スタートから約18マス ≒ 3秒）。</summary>
        public bool SafeStart = true;
        /// <summary>0より大きければ、距離に関係なくこの速度で走る（テスト用）。</summary>
        public float FixedSpeed = 0f;
        /// <summary>天井に立った状態で始める（テスト用）。</summary>
        public bool StartOnCeiling = false;
    }

    /// <summary>
    /// 1回のラン（スタートから死亡まで）。地形・プレイヤー・距離・コイン・スコアを持つ。
    /// UnityEngine に依存しないので、EditMode テストでそのまま動かせる。
    /// </summary>
    public sealed class RunSession : ICoinSink
    {
        public const int CoinScore = 10;
        public const string SafeChunkId = "safe";

        readonly IChunkSource _source;
        readonly RunOptions _opt;
        string _lastChunkId;

        public RunWorld World { get; } = new RunWorld();
        public PlayerSim Player { get; }
        public int Coins { get; private set; }
        public int BonusScore { get; private set; }

        public event Action<PlacedChunk> ChunkPlaced;
        public event Action<PlacedChunk> ChunkRemoved;
        public event Action<int, int> CoinCollected;
        /// <summary>ニアミスが成立した（引数は同じフレームで成立した数）。ボーナスは加算済み。</summary>
        public event Action<int> NearMiss;

        readonly NearMissTracker _nearMiss = new NearMissTracker();

        public RunSession(IChunkSource source, RunOptions options = null)
        {
            _source = source;
            _opt = options ?? new RunOptions();
            Player = new PlayerSim(PlayerState.Create(_opt.StartX, _opt.StartOnCeiling));
        }

        public PlayerState State => Player.State;
        public bool IsDead => Player.State.IsDead;

        /// <summary>距離（m）＝ スタート地点からのxの差。1マス = 1m。</summary>
        public float Distance => Math.Max(0f, Player.State.X - _opt.StartX);
        public int DistanceMeters => (int)Math.Floor(Distance);

        /// <summary>スコア ＝ 距離（m、切り捨て）＋ コイン数 × 10 ＋ ニアミスのボーナス（1回 +5）。</summary>
        public int Score => DistanceMeters + Coins * CoinScore + BonusScore;

        public float Speed => _opt.FixedSpeed > 0f ? _opt.FixedSpeed : GameConfig.SpeedForDistance(Distance);

        /// <summary>今プレイヤーがいるチャンクのID（分析イベントの chunk_id 用）。</summary>
        public string CurrentChunkId => World.ChunkAt((int)Math.Floor(Player.State.X))?.Chunk.id ?? "";

        /// <summary>
        /// 必要なチャンクを置き、いらなくなったチャンクを外す。
        /// keepFromX より左で終わるチャンクは外し、右端が fillToX に届くまで足す。
        /// </summary>
        public void UpdateChunks(float keepFromX, float fillToX)
        {
            if (World.Chunks.Count == 0 && _opt.SafeStart) Place(SafeChunk());
            while (World.EndCol < fillToX)
            {
                float startDistance = Math.Max(0f, World.EndCol - _opt.StartX);
                var chunk = _source.Next(GameConfig.DifficultyForDistance(startDistance), _lastChunkId);
                Place(chunk);
            }
            World.RemoveBefore((int)Math.Floor(keepFromX), p => ChunkRemoved?.Invoke(p));
        }

        void Place(ChunkData chunk)
        {
            var p = World.Append(chunk);
            _lastChunkId = chunk.id;
            ChunkPlaced?.Invoke(p);
        }

        /// <summary>1フレーム（1/60秒）進める。</summary>
        public void Step(bool flipPressed)
        {
            if (IsDead) return;
            Player.Step(flipPressed, Speed, World, this);
            int n = _nearMiss.Update(World, Player.State);
            if (n > 0)
            {
                AddBonus(NearMissTracker.Bonus * n);
                NearMiss?.Invoke(n);
            }
        }

        public void AddBonus(int points) => BonusScore += points;

        bool ICoinSink.Collect(int col, int row)
        {
            if (!World.MarkCollected(col, row)) return false;
            Coins++;
            CoinCollected?.Invoke(col, row);
            return true;
        }

        /// <summary>障害物なしの平らなチャンク（床と天井だけ）。</summary>
        public static ChunkData SafeChunk()
        {
            var g = new Tile[GameConfig.ChunkWidth, GameConfig.GridHeight];
            for (int c = 0; c < GameConfig.ChunkWidth; c++)
            {
                g[c, 0] = Tile.Block;
                g[c, GameConfig.GridHeight - 1] = Tile.Block;
            }
            return ChunkData.FromGrid(SafeChunkId, 0, g);
        }
    }
}
