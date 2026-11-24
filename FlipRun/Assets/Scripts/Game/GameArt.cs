using UnityEngine;
using UnityEngine.Tilemaps;

namespace FlipRun.Game
{
    /// <summary>
    /// SpriteCook で作ったドット絵（Resources/Art/）を読み込む。見つからなければグレーボックスの図形を使う。
    /// 32×32px、Pixels Per Unit = 32。どの絵が SpriteCook のどのアセットかは spritecook-assets.json に記録してある。
    /// </summary>
    public sealed class GameArt
    {
        public const int PPU = 32;
        public const int RunFrameCount = 4;

        public bool IsPixelArt { get; }
        public Sprite[] Run { get; }
        public Sprite Land { get; }
        public Sprite Particle { get; }
        public TileBase Block { get; }
        public TileBase SpikeUp { get; }
        public TileBase SpikeDown { get; }
        public TileBase Coin { get; }
        /// <summary>プレイヤーの絵の一辺（ワールド単位）。ドット絵は1マス、グレーボックスは当たり判定と同じ0.8。</summary>
        public float PlayerSize => IsPixelArt ? 1f : GameConfig.PlayerHalfSize * 2f;
        /// <summary>パーティクルや破片の色。</summary>
        public Color PlayerColor => GreyboxArt.Player;
        /// <summary>プレイヤーの絵に掛ける色（ドット絵はそのまま）。</summary>
        public Color PlayerTint => IsPixelArt ? Color.white : GreyboxArt.Player;

        public GameArt()
        {
            var run = Resources.Load<Texture2D>("Art/player_run");
            var land = Resources.Load<Texture2D>("Art/player_land");
            var block = Resources.Load<Texture2D>("Art/tile_block");
            var spike = Resources.Load<Texture2D>("Art/tile_spike");
            var coin = Resources.Load<Texture2D>("Art/coin");
            Particle = GreyboxArt.Square();

            IsPixelArt = run && land && block && spike && coin;
            if (!IsPixelArt)
            {
                Debug.LogWarning("[FlipRun] Resources/Art の絵が見つからないので、グレーボックスの図形を使います");
                var sq = Particle;
                Run = new[] { sq };
                Land = sq;
                Block = GreyboxArt.MakeTile(sq, Color.white);
                SpikeUp = GreyboxArt.MakeTile(GreyboxArt.SpikeUp(), GreyboxArt.Spike);
                SpikeDown = GreyboxArt.MakeTile(GreyboxArt.SpikeDown(), GreyboxArt.Spike);
                Coin = GreyboxArt.MakeTile(GreyboxArt.Circle(), GreyboxArt.Coin);
                return;
            }

            int frameW = run.width / RunFrameCount;
            Run = new Sprite[RunFrameCount];
            for (int i = 0; i < RunFrameCount; i++)
                Run[i] = Slice(run, new Rect(i * frameW, 0, frameW, run.height), "player_run_" + i);
            Land = Slice(land, new Rect(0, 0, land.width, land.height), "player_land");

            // ブロックは白のまま置き、色は Tilemap.color（パレット）で決める
            Block = GreyboxArt.MakeTile(Slice(block), Color.white);
            var spikeSprite = Slice(spike);
            SpikeUp = GreyboxArt.MakeTile(spikeSprite, Color.white);
            // 天井側のトゲは同じ絵を上下反転して使う
            var down = GreyboxArt.MakeTile(spikeSprite, Color.white);
            down.transform = Matrix4x4.Scale(new Vector3(1f, -1f, 1f));
            down.flags = TileFlags.LockColor | TileFlags.LockTransform;
            SpikeDown = down;
            Coin = GreyboxArt.MakeTile(Slice(coin), Color.white);
        }

        static Sprite Slice(Texture2D tex) => Slice(tex, new Rect(0, 0, tex.width, tex.height), tex.name);

        static Sprite Slice(Texture2D tex, Rect rect, string name)
        {
            var s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }
    }
}
