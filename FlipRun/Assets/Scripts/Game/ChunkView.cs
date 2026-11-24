using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FlipRun.Game
{
    /// <summary>
    /// RunWorld のチャンクを Tilemap に並べる／消す。
    /// Grid を y = -0.5 にずらし、マス (col, row) がワールドの y ∈ [row-0.5, row+0.5] に来るようにする。
    /// 地形（ブロック）とそれ以外（トゲ・コイン）は別の Tilemap に置く。地形だけ色を入れ替えるため。
    /// </summary>
    public sealed class ChunkView
    {
        /// <summary>ブロック。色は Tilemap.color で決める（パレットの「地形色」）。</summary>
        public Tilemap Terrain { get; }
        /// <summary>トゲとコイン。</summary>
        public Tilemap Objects { get; }

        readonly Dictionary<FlipRun.Tile, TileBase> _tiles;

        public ChunkView(Transform parent, Dictionary<FlipRun.Tile, TileBase> tiles)
        {
            _tiles = tiles;
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(parent, false);
            gridGo.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            gridGo.AddComponent<Grid>();
            Terrain = MakeTilemap(gridGo.transform, "Terrain", 0);
            Objects = MakeTilemap(gridGo.transform, "Objects", 1);
        }

        static Tilemap MakeTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return tm;
        }

        public void Place(PlacedChunk p)
        {
            var c = p.Chunk;
            int n = c.width * c.height;
            var positions = new Vector3Int[n];
            var terrain = new TileBase[n];
            var objects = new TileBase[n];
            int i = 0;
            for (int col = 0; col < c.width; col++)
                for (int row = GameConfig.FloorRow; row <= GameConfig.CeilingRow; row++, i++)
                {
                    positions[i] = new Vector3Int(p.StartCol + col, row, 0);
                    var t = c.Get(col, row);
                    if (t == FlipRun.Tile.Empty) continue;
                    _tiles.TryGetValue(t, out var tile);
                    if (t == FlipRun.Tile.Block) terrain[i] = tile;
                    else objects[i] = tile;
                }
            Terrain.SetTiles(positions, terrain);
            Objects.SetTiles(positions, objects);
        }

        public void Remove(PlacedChunk p)
        {
            var bounds = new BoundsInt(p.StartCol, GameConfig.FloorRow, 0, p.Chunk.width, GameConfig.GridHeight, 1);
            var empty = new TileBase[bounds.size.x * bounds.size.y];
            Terrain.SetTilesBlock(bounds, empty);
            Objects.SetTilesBlock(bounds, empty);
        }

        public void ClearCell(int col, int row) => Objects.SetTile(new Vector3Int(col, row, 0), null);

        public void Clear()
        {
            Terrain.ClearAllTiles();
            Objects.ClearAllTiles();
        }
    }
}
