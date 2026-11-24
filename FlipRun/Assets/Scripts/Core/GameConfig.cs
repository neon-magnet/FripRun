namespace FlipRun
{
    /// <summary>
    /// ブリーフ第3章の数値をまとめた場所。M2の調整ではここだけを変える。
    /// </summary>
    public static class GameConfig
    {
        // シミュレーションの時間刻み
        public const int StepsPerSecond = 60;
        public const float Dt = 1f / StepsPerSecond;

        // 速度（マス/秒）
        public const float StartSpeed = 6f;
        public const float SpeedStepPer100m = 0.25f;
        public const float MaxSpeed = 12f;

        // 重力（マス/秒²）と落下速度の上限
        public const float Gravity = 40f;
        public const float MaxFallSpeed = 20f;

        // 反転入力の猶予
        public const float CoyoteTime = 0.08f;
        public const float InputBufferTime = 0.1f;

        // プレイヤーの当たり判定（0.8 × 0.8 の半分）
        public const float PlayerHalfSize = 0.4f;

        // 画面外判定
        public const float KillYMin = -5.5f;
        public const float KillYMax = 5.5f;

        // グリッド：y = -4（床）〜 +4（天井）
        public const int FloorRow = -4;
        public const int CeilingRow = 4;
        public const int GridHeight = 9;
        public const int ChunkWidth = 20;

        // 時間をフレーム数に直したもの（判定を整数で行い、ゲームと検証で結果をそろえる）
        // コヨーテタイム：離れてから CoyoteFrames フレーム目までは反転できる（4/60 ≒ 0.067秒 ≤ 0.08秒）
        public static readonly int CoyoteFrames = (int)(CoyoteTime * StepsPerSecond + 1e-4f);
        // 入力バッファ：押してから BufferFrames フレームのあいだ有効（6/60 = 0.1秒）
        public static readonly int BufferFrames = (int)(InputBufferTime * StepsPerSecond + 0.5f);

        /// <summary>ゲーム中の速度：100mごとに +0.25、上限12。</summary>
        public static float SpeedForDistance(float distance)
        {
            if (distance < 0f) distance = 0f;
            float s = StartSpeed + SpeedStepPer100m * (int)(distance / 100f);
            return s > MaxSpeed ? MaxSpeed : s;
        }

        /// <summary>検証で想定する速度：難易度1 = 6、難易度10 = 12。</summary>
        public static float SpeedForDifficulty(int difficulty)
        {
            if (difficulty < 1) difficulty = 1;
            if (difficulty > 10) difficulty = 10;
            return StartSpeed + (difficulty - 1) * (MaxSpeed - StartSpeed) / 9f;
        }

        /// <summary>ゲーム中に出すチャンクの難易度：1 + 距離 ÷ 150、上限10。</summary>
        public static int DifficultyForDistance(float distance)
        {
            if (distance < 0f) distance = 0f;
            int d = 1 + (int)(distance / 150f);
            return d > 10 ? 10 : d;
        }
    }
}
