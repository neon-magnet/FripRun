using System;
using System.Collections.Generic;

namespace FlipRun
{
    /// <summary>ワールドに置かれた1チャンク。StartCol はチャンクの左端の列（ワールドのx）。</summary>
    public sealed class PlacedChunk
    {
        public ChunkData Chunk;
        public int StartCol;
        public int EndCol => StartCol + Chunk.width; // この列は含まない
    }

    /// <summary>
    /// ラン中の地形：チャンクを右へ次々につないだもの。
    /// 読み込んだ範囲の外は、床と天井だけの平らな地形として扱う（検証ツールの ChunkWorld と同じ）。
    /// </summary>
    public sealed class RunWorld : ITileWorld
    {
        readonly List<PlacedChunk> _chunks = new List<PlacedChunk>();
        readonly HashSet<long> _collected = new HashSet<long>();

        public IReadOnlyList<PlacedChunk> Chunks => _chunks;
        public int EndCol => _chunks.Count == 0 ? 0 : _chunks[_chunks.Count - 1].EndCol;

        public PlacedChunk Append(ChunkData chunk)
        {
            var p = new PlacedChunk { Chunk = chunk, StartCol = EndCol };
            _chunks.Add(p);
            return p;
        }

        /// <summary>右端が col より左にあるチャンクを先頭から外す。</summary>
        public void RemoveBefore(int col, Action<PlacedChunk> onRemoved)
        {
            while (_chunks.Count > 1 && _chunks[0].EndCol < col)
            {
                var p = _chunks[0];
                _chunks.RemoveAt(0);
                _collected.RemoveWhere(k => (int)(k >> 8) < p.EndCol);
                onRemoved?.Invoke(p);
            }
        }

        public PlacedChunk ChunkAt(int col)
        {
            for (int i = 0; i < _chunks.Count; i++)
                if (col >= _chunks[i].StartCol && col < _chunks[i].EndCol) return _chunks[i];
            return null;
        }

        public Tile GetTile(int col, int row)
        {
            if (row < GameConfig.FloorRow || row > GameConfig.CeilingRow) return Tile.Empty;
            var p = ChunkAt(col);
            if (p == null)
                return (row == GameConfig.FloorRow || row == GameConfig.CeilingRow) ? Tile.Block : Tile.Empty;
            var t = p.Chunk.Get(col - p.StartCol, row);
            if (t == Tile.Coin && _collected.Contains(Key(col, row))) return Tile.Empty;
            return t;
        }

        /// <summary>コインを拾った印をつける。まだ拾われていなければ true。</summary>
        public bool MarkCollected(int col, int row) => _collected.Add(Key(col, row));

        static long Key(int col, int row) => ((long)col << 8) | (uint)(row + 128);
    }

    /// <summary>次に置くチャンクを選ぶ。</summary>
    public interface IChunkSource
    {
        ChunkData Next(int difficulty, string previousId);
    }

    /// <summary>
    /// 書き出したチャンクの一覧から、難易度に合うものをランダムに選ぶ。
    /// 同じチャンクが2回連続で出ないようにする。
    /// </summary>
    public sealed class ChunkLibrary : IChunkSource
    {
        readonly List<ChunkData>[] _byDifficulty = new List<ChunkData>[11];
        readonly Random _rng;

        public int Count { get; }

        public ChunkLibrary(IEnumerable<ChunkData> chunks, int seed)
        {
            for (int i = 0; i < _byDifficulty.Length; i++) _byDifficulty[i] = new List<ChunkData>();
            int n = 0;
            foreach (var c in chunks)
            {
                int d = Math.Max(1, Math.Min(10, c.difficulty));
                _byDifficulty[d].Add(c);
                n++;
            }
            Count = n;
            _rng = new Random(seed);
        }

        public ChunkData Next(int difficulty, string previousId)
        {
            difficulty = Math.Max(1, Math.Min(10, difficulty));
            // その難易度が無ければ、近い難易度（低い方を優先）から選ぶ
            List<ChunkData> pool = null;
            for (int off = 0; off <= 10 && pool == null; off++)
            {
                if (difficulty - off >= 1 && _byDifficulty[difficulty - off].Count > 0) pool = _byDifficulty[difficulty - off];
                else if (difficulty + off <= 10 && _byDifficulty[difficulty + off].Count > 0) pool = _byDifficulty[difficulty + off];
            }
            if (pool == null) throw new InvalidOperationException("チャンクが1つもありません");
            if (pool.Count == 1) return pool[0];

            int i = _rng.Next(pool.Count);
            if (pool[i].id == previousId) i = (i + 1 + _rng.Next(pool.Count - 1)) % pool.Count;
            return pool[i];
        }
    }
}
