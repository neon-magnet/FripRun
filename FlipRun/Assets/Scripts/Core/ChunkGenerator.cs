using System;

namespace FlipRun
{
    /// <summary>
    /// 難易度 1〜10 とシード値からチャンクを1つ作る。同じ入力なら必ず同じチャンクになる。
    /// 作るのは形だけで、クリアできるかどうかは ChunkSolver で確かめる（ChunkFactory を参照）。
    /// </summary>
    public static class ChunkGenerator
    {
        public const int SafeColumns = 2; // 左右の端の何列を「床と天井だけ」にするか

        enum Side { Floor, Ceiling }

        public static ChunkData Build(int difficulty, int seed, string id = null)
        {
            difficulty = Math.Max(1, Math.Min(10, difficulty));
            var rng = new Random(seed);
            float t = (difficulty - 1) / 9f; // 0（難易度1）〜 1（難易度10）
            int w = GameConfig.ChunkWidth, h = GameConfig.GridHeight;
            var g = new Tile[w, h];

            // 床と天井は全部ブロック
            for (int c = 0; c < w; c++)
            {
                Set(g, c, GameConfig.FloorRow, Tile.Block);
                Set(g, c, GameConfig.CeilingRow, Tile.Block);
            }

            int first = SafeColumns + 1;
            int last = w - SafeColumns - 1; // この列より手前に置く（last は含まない）
            int col = first + rng.Next(0, 3 - (int)(t * 2.5f));
            Side side = rng.Next(2) == 0 ? Side.Floor : Side.Ceiling;
            float alternate = Lerp(0.55f, 0.95f, t);

            while (col < last)
            {
                int width = PlaceFeature(g, rng, difficulty, t, side, col, last);
                if (width <= 0) break;
                col += width;

                // 次の仕掛けまでの間隔（秒で決めて列に直す）：難易度が上がるほど短い
                float speed = GameConfig.SpeedForDifficulty(difficulty);
                float gapSec = Lerp(1.1f, 0.35f, t) + (float)rng.NextDouble() * Lerp(0.6f, 0.25f, t);
                col += Math.Max(1, (int)Math.Round(gapSec * speed));
                if (rng.NextDouble() < alternate) side = side == Side.Floor ? Side.Ceiling : Side.Floor;
            }

            PlaceCoins(g, rng);
            return ChunkData.FromGrid(id ?? $"chunk_{difficulty:00}_s{seed}", difficulty, g);
        }

        /// <summary>仕掛けを1つ置き、使った列数を返す。置けなければ 0。</summary>
        static int PlaceFeature(Tile[,] g, Random rng, int d, float t, Side side, int col, int last)
        {
            int room = last - col;
            if (room < 1) return 0;

            // 仕掛けの種類（難易度で解禁）：0=穴, 1=トゲ, 2=壁, 3=トゲ付きの足場
            int kind;
            if (rng.NextDouble() < Lerp(0.2f, 0.6f, t)) kind = 2;
            else
            {
                kind = rng.Next(d >= 5 ? 3 : 2);
                if (kind == 2) kind = 3;
            }
            int surf = side == Side.Floor ? GameConfig.FloorRow : GameConfig.CeilingRow;
            int inward = side == Side.Floor ? 1 : -1;         // 通路側への向き
            Tile spike = side == Side.Floor ? Tile.SpikeUp : Tile.SpikeDown;

            switch (kind)
            {
                case 0: // 穴：床または天井を2〜5列抜く。難易度が上がるほど広い
                {
                    int minW = 2 + (int)(t * 2f);           // 2〜4
                    int maxW = Math.Min(5, 3 + (int)(t * 2.5f)); // 3〜5
                    int width = Math.Min(rng.Next(minW, maxW + 1), room);
                    if (width < 2) return 0;
                    for (int c = col; c < col + width; c++) Set(g, c, surf, Tile.Empty);
                    return width;
                }
                case 1: // トゲ：床・天井の上に並べる
                {
                    int width = Math.Min(rng.Next(1, 2 + (int)(t * 2.5f)), room); // 1〜3
                    for (int c = col; c < col + width; c++) Set(g, c, surf + inward, spike);
                    return width;
                }
                case 2: // 壁：床または天井から伸びるブロック。難易度が上がるほど高く、反対側に張り付いていないと抜けられない
                {
                    int minH = 1 + (int)(t * 3f);               // 1〜4
                    int maxH = 1 + (int)Math.Round(t * 4f);     // 1〜5
                    int height = rng.Next(minH, maxH + 1);
                    int width = Math.Min(rng.Next(1, 3), room);
                    for (int c = col; c < col + width; c++)
                        for (int k = 1; k <= height; k++) Set(g, c, surf + inward * k, Tile.Block);
                    return width;
                }
                default: // 足場：通路の途中に浮かぶブロック（空中を飛んでいると側面にぶつかる）。上にトゲ
                {
                    int width = Math.Min(rng.Next(2, 4), room);
                    if (width < 2) return 0;
                    int row = side == Side.Floor ? -1 : 1;
                    for (int c = col; c < col + width; c++)
                    {
                        Set(g, c, row, Tile.Block);
                        Set(g, c, row + inward, spike);
                    }
                    return width;
                }
            }
        }

        /// <summary>コインは天井側（y ≥ 0）の通り道に置く。</summary>
        static void PlaceCoins(Tile[,] g, Random rng)
        {
            int w = g.GetLength(0);
            int c = SafeColumns + rng.Next(0, 4);
            while (c < w - SafeColumns)
            {
                int run = rng.Next(2, 5);
                int row = rng.NextDouble() < 0.7 ? 3 : 2;
                for (int k = 0; k < run && c < w - SafeColumns; k++, c++)
                {
                    // 天井に穴がある列・トゲの近くは避ける
                    if (Get(g, c, row) != Tile.Empty) continue;
                    if (Get(g, c, GameConfig.CeilingRow) != Tile.Block) continue;
                    if (Get(g, c, row - 1) == Tile.SpikeUp || Get(g, c, row + 1) == Tile.SpikeDown) continue;
                    Set(g, c, row, Tile.Coin);
                }
                c += rng.Next(3, 7);
            }
        }

        static void Set(Tile[,] g, int col, int row, Tile t) => g[col, GameConfig.CeilingRow - row] = t;

        static Tile Get(Tile[,] g, int col, int row)
        {
            int r = GameConfig.CeilingRow - row;
            if (r < 0 || r >= g.GetLength(1)) return Tile.Block;
            return g[col, r];
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
