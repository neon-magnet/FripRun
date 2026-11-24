using System;

namespace FlipRun
{
    public enum DeathCause : byte
    {
        None = 0,
        Spike = 1,       // トゲに触れた
        OutOfBounds = 2, // 画面の外（穴に落ちた）
        SideHit = 3,     // ブロックの側面に正面からぶつかった
    }

    /// <summary>
    /// プレイヤーの状態。値型なので、検証ツールはコピーして分岐できる。
    /// </summary>
    public struct PlayerState
    {
        public float X;           // 当たり判定の中心
        public float Y;
        public float VY;          // 縦の速度（上が正）
        public int GravityDir;    // -1 = 下向き（床に立つ）、+1 = 上向き（天井に立つ）
        public bool Grounded;
        public int AirFrames;     // 最後に接地していたフレームから何フレーム経ったか
        public int BufferFrames;  // 入力バッファの残りフレーム
        public int Frame;         // 経過フレーム数
        public DeathCause Death;

        // このフレームに起きたこと（演出用）
        public bool FlippedThisStep;
        public bool LandedThisStep;

        public bool IsDead => Death != DeathCause.None;
        public bool GravityUp => GravityDir > 0;

        /// <summary>床（gravity 下）または天井（gravity 上）に立った状態で作る。</summary>
        public static PlayerState Create(float x, bool onCeiling)
        {
            float surface = onCeiling ? GameConfig.CeilingRow - 0.5f : GameConfig.FloorRow + 0.5f;
            return new PlayerState
            {
                X = x,
                Y = onCeiling ? surface - GameConfig.PlayerHalfSize : surface + GameConfig.PlayerHalfSize,
                VY = 0f,
                GravityDir = onCeiling ? 1 : -1,
                Grounded = true,
                AirFrames = 0,
                BufferFrames = 0,
                Frame = 0,
                Death = DeathCause.None,
            };
        }
    }

    /// <summary>コインを拾ったときに呼ばれる。ゲーム側でマスを消す。</summary>
    public interface ICoinSink
    {
        /// <returns>まだ拾われていないコインだった場合 true。</returns>
        bool Collect(int col, int row);
    }

    /// <summary>
    /// プレイヤーの動きを固定時間刻み（1/60秒）で計算する純粋なC#クラス。
    /// ゲーム本体と検証ツールの両方がこれを使う。
    /// </summary>
    public sealed class PlayerSim
    {
        const float Eps = 1e-4f;
        public const float SpikeHalfWidth = 0.3f; // トゲの判定は見た目より少し小さく（幅0.6、高さ0.6）
        const float SpikeHeight = 0.6f;
        const float CoinHalf = 0.35f;

        public PlayerState State;

        public PlayerSim(PlayerState initial) { State = initial; }

        /// <summary>1フレーム進める。flipPressed はそのフレームに反転ボタンが押されたかどうか。</summary>
        public void Step(bool flipPressed, float speed, ITileWorld world, ICoinSink coins = null)
        {
            Step(ref State, flipPressed, speed, world, coins);
        }

        public static void Step(ref PlayerState s, bool flipPressed, float speed, ITileWorld world, ICoinSink coins = null)
        {
            if (s.IsDead) return;
            s.FlippedThisStep = false;
            s.LandedThisStep = false;

            // 1. 入力：押されたらバッファに入れ、反転できるなら反転する
            if (flipPressed) s.BufferFrames = GameConfig.BufferFrames;
            TryFlip(ref s);

            // 2. 横移動。ブロックにめり込んだら＝側面に正面からぶつかった
            s.X += speed * GameConfig.Dt;
            if (OverlapsSolid(world, s.X, s.Y))
            {
                s.Death = DeathCause.SideHit;
                s.Frame++;
                return;
            }

            // 3. 縦移動
            s.VY += s.GravityDir * GameConfig.Gravity * GameConfig.Dt;
            if (s.VY > GameConfig.MaxFallSpeed) s.VY = GameConfig.MaxFallSpeed;
            if (s.VY < -GameConfig.MaxFallSpeed) s.VY = -GameConfig.MaxFallSpeed;
            s.Y += s.VY * GameConfig.Dt;

            bool wasGrounded = s.Grounded;
            s.Grounded = false;
            if (OverlapsSolid(world, s.X, s.Y))
            {
                bool movingDown = s.VY < 0f;
                ResolveVertical(world, ref s, movingDown);
                bool withGravity = (s.VY < 0f && s.GravityDir < 0) || (s.VY > 0f && s.GravityDir > 0);
                s.VY = 0f;
                if (withGravity) s.Grounded = true;
            }

            if (s.Grounded)
            {
                if (!wasGrounded) s.LandedThisStep = true;
                s.AirFrames = 0;
            }
            else if (s.AirFrames < 10000)
            {
                s.AirFrames++;
            }

            // 4. 着地した瞬間にバッファが残っていれば反転
            if (s.LandedThisStep) TryFlip(ref s);
            if (s.BufferFrames > 0) s.BufferFrames--;

            // 5. 危険物
            if (TouchesSpike(world, s.X, s.Y)) s.Death = DeathCause.Spike;
            else if (s.Y < GameConfig.KillYMin || s.Y > GameConfig.KillYMax) s.Death = DeathCause.OutOfBounds;

            // 6. コイン
            if (coins != null && !s.IsDead) CollectCoins(world, s.X, s.Y, coins);

            s.Frame++;
        }

        public static bool CanFlip(in PlayerState s)
        {
            return s.Grounded || s.AirFrames <= GameConfig.CoyoteFrames;
        }

        static void TryFlip(ref PlayerState s)
        {
            if (s.BufferFrames <= 0 || !CanFlip(s)) return;
            s.GravityDir = -s.GravityDir;
            s.VY = 0f;
            s.Grounded = false;
            s.AirFrames = GameConfig.CoyoteFrames + 1; // 同じ足場から2回は反転できない
            s.BufferFrames = 0;
            s.FlippedThisStep = true;
        }

        // ---- 当たり判定 ----

        static void CellRange(float x, float y, float half, out int c0, out int c1, out int r0, out int r1)
        {
            c0 = (int)Math.Floor(x - half + Eps);
            c1 = (int)Math.Floor(x + half - Eps);
            r0 = (int)Math.Floor(y - half + Eps + 0.5f);
            r1 = (int)Math.Floor(y + half - Eps + 0.5f);
        }

        public static bool OverlapsSolid(ITileWorld world, float x, float y)
        {
            CellRange(x, y, GameConfig.PlayerHalfSize, out int c0, out int c1, out int r0, out int r1);
            for (int c = c0; c <= c1; c++)
                for (int r = r0; r <= r1; r++)
                    if (world.GetTile(c, r) == Tile.Block) return true;
            return false;
        }

        static void ResolveVertical(ITileWorld world, ref PlayerState s, bool movingDown)
        {
            CellRange(s.X, s.Y, GameConfig.PlayerHalfSize, out int c0, out int c1, out int r0, out int r1);
            if (movingDown)
            {
                // 重なっているブロックのうち一番高い上面に乗せる
                float top = float.NegativeInfinity;
                for (int c = c0; c <= c1; c++)
                    for (int r = r0; r <= r1; r++)
                        if (world.GetTile(c, r) == Tile.Block && r + 0.5f > top) top = r + 0.5f;
                s.Y = top + GameConfig.PlayerHalfSize;
            }
            else
            {
                float bottom = float.PositiveInfinity;
                for (int c = c0; c <= c1; c++)
                    for (int r = r0; r <= r1; r++)
                        if (world.GetTile(c, r) == Tile.Block && r - 0.5f < bottom) bottom = r - 0.5f;
                s.Y = bottom - GameConfig.PlayerHalfSize;
            }
        }

        /// <summary>トゲの当たり判定の箱（幅0.6・高さ0.6、根元はマスの床側／天井側）。</summary>
        public static bool SpikeBox(Tile t, int col, int row, out float xMin, out float xMax, out float yMin, out float yMax)
        {
            xMin = col + 0.5f - SpikeHalfWidth;
            xMax = col + 0.5f + SpikeHalfWidth;
            if (t == Tile.SpikeUp)
            {
                yMin = row - 0.5f;
                yMax = row - 0.5f + SpikeHeight;
                return true;
            }
            if (t == Tile.SpikeDown)
            {
                yMax = row + 0.5f;
                yMin = row + 0.5f - SpikeHeight;
                return true;
            }
            yMin = yMax = 0f;
            return false;
        }

        static bool TouchesSpike(ITileWorld world, float x, float y)
        {
            float h = GameConfig.PlayerHalfSize;
            CellRange(x, y, h, out int c0, out int c1, out int r0, out int r1);
            for (int c = c0; c <= c1; c++)
                for (int r = r0; r <= r1; r++)
                {
                    if (!SpikeBox(world.GetTile(c, r), c, r, out float ax, out float bx, out float ay, out float by)) continue;
                    if (x - h < bx - Eps && x + h > ax + Eps && y - h < by - Eps && y + h > ay + Eps) return true;
                }
            return false;
        }

        static void CollectCoins(ITileWorld world, float x, float y, ICoinSink coins)
        {
            float h = GameConfig.PlayerHalfSize;
            CellRange(x, y, h, out int c0, out int c1, out int r0, out int r1);
            for (int c = c0; c <= c1; c++)
                for (int r = r0; r <= r1; r++)
                {
                    if (world.GetTile(c, r) != Tile.Coin) continue;
                    float cx = c + 0.5f, cy = r;
                    if (Math.Abs(x - cx) < h + CoinHalf && Math.Abs(y - cy) < h + CoinHalf) coins.Collect(c, r);
                }
        }
    }
}
