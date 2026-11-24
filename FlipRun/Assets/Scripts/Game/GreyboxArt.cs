using UnityEngine;
using UnityEngine.Tilemaps;

namespace FlipRun.Game
{
    /// <summary>
    /// M1 のグレーボックス用の絵（四角・三角・丸）を実行時に作る。
    /// 32×32px、Pixels Per Unit = 32、Filter Mode = Point。M2 で SpriteCook の絵に差し替える。
    /// </summary>
    public static class GreyboxArt
    {
        public const int Size = 32;

        public static readonly Color Background = new Color32(0x14, 0x1B, 0x3A, 0xFF); // 濃紺
        public static readonly Color Terrain = new Color32(0xE6, 0xEA, 0xF5, 0xFF);    // 明るい色
        public static readonly Color Spike = new Color32(0xFF, 0x4F, 0x6D, 0xFF);
        public static readonly Color Coin = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        public static readonly Color Player = new Color32(0x4D, 0xE8, 0xFF, 0xFF);

        public static Sprite Square() => Make("Square", (x, y) => true);

        /// <summary>上向きの三角（根元がマスの下側）。高さはマスの0.7。</summary>
        public static Sprite SpikeUp() => Make("SpikeUp", (x, y) =>
        {
            float h = Size * 0.7f;
            if (y >= h) return false;
            float half = (Size * 0.45f) * (1f - y / h);
            return Mathf.Abs(x + 0.5f - Size / 2f) <= half;
        });

        public static Sprite SpikeDown() => Make("SpikeDown", (x, y) =>
        {
            int yy = Size - 1 - y;
            float h = Size * 0.7f;
            if (yy >= h) return false;
            float half = (Size * 0.45f) * (1f - yy / h);
            return Mathf.Abs(x + 0.5f - Size / 2f) <= half;
        });

        public static Sprite Circle() => Make("Circle", (x, y) =>
        {
            float dx = x + 0.5f - Size / 2f, dy = y + 0.5f - Size / 2f;
            return dx * dx + dy * dy <= (Size * 0.3f) * (Size * 0.3f);
        });

        static Sprite Make(string name, System.Func<int, int, bool> inside)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            s.name = name;
            return s;
        }

        public static UnityEngine.Tilemaps.Tile MakeTile(Sprite sprite, Color color)
        {
            var t = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            t.sprite = sprite;
            t.color = color;
            t.flags = TileFlags.LockColor;
            t.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            return t;
        }
    }
}
