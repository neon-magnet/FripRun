using System;

namespace FlipRun
{
    /// <summary>マスの値（JSONの数字と同じ）。</summary>
    public enum Tile : byte
    {
        Empty = 0,
        Block = 1,
        SpikeUp = 2,   // 床側トゲ（上向き）
        SpikeDown = 3, // 天井側トゲ（下向き）
        Coin = 4,
    }

    /// <summary>
    /// PlayerSim が地形を問い合わせるための窓口。
    /// col はワールドのx（1マス = 1）、row はワールドのy（-4 〜 +4）。
    /// マス (col, row) は x ∈ [col, col+1)、y ∈ [row-0.5, row+0.5] を占める。
    /// </summary>
    public interface ITileWorld
    {
        Tile GetTile(int col, int row);
    }

    /// <summary>
    /// 1チャンク（幅20 × 高さ9）。JsonUtility でそのまま読み書きできる形。
    /// rows[0] が一番上（y = +4）、rows[8] が一番下（y = -4）。
    /// </summary>
    [Serializable]
    public class ChunkData
    {
        public string id;
        public int difficulty;
        public int width;
        public int height;
        public string[] rows;

        /// <summary>ワールドの行（-4〜+4）でマスを取得する。範囲外は Empty。</summary>
        public Tile Get(int col, int row)
        {
            int r = GameConfig.CeilingRow - row;
            if (col < 0 || col >= width || r < 0 || r >= height) return Tile.Empty;
            return (Tile)(rows[r][col] - '0');
        }

        public static ChunkData FromGrid(string id, int difficulty, Tile[,] grid)
        {
            int w = grid.GetLength(0);
            int h = grid.GetLength(1);
            var rows = new string[h];
            var chars = new char[w];
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++) chars[c] = (char)('0' + (int)grid[c, r]);
                rows[r] = new string(chars);
            }
            return new ChunkData { id = id, difficulty = difficulty, width = w, height = h, rows = rows };
        }

        /// <summary>grid[col, rowIndex]（rowIndex 0 = 上）に展開する。</summary>
        public Tile[,] ToGrid()
        {
            var g = new Tile[width, height];
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                    g[c, r] = (Tile)(rows[r][c] - '0');
            return g;
        }
    }

    /// <summary>
    /// 1チャンクだけの世界（検証用）。チャンクの左右の外側は、床と天井だけの平らな地形として扱う。
    /// </summary>
    public sealed class ChunkWorld : ITileWorld
    {
        readonly ChunkData _chunk;
        public ChunkWorld(ChunkData chunk) { _chunk = chunk; }

        public Tile GetTile(int col, int row)
        {
            if (row < GameConfig.FloorRow || row > GameConfig.CeilingRow) return Tile.Empty;
            if (col < 0 || col >= _chunk.width)
                return (row == GameConfig.FloorRow || row == GameConfig.CeilingRow) ? Tile.Block : Tile.Empty;
            return _chunk.Get(col, row);
        }
    }
}
