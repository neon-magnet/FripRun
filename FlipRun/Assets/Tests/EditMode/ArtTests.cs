using FlipRun.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlipRun.Tests
{
    public class ArtTests
    {
        static readonly string[] Files =
        {
            "player_run.png", "player_land.png", "tile_block.png", "tile_spike.png", "coin.png",
        };

        [Test]
        public void ドット絵はPointフィルター_無圧縮_PPU32で読み込まれる()
        {
            foreach (var f in Files)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/" + f);
                Assert.IsNotNull(ti, f);
                Assert.AreEqual(FilterMode.Point, ti.filterMode, f);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, ti.textureCompression, f);
                Assert.AreEqual(32f, ti.spritePixelsPerUnit, f);
                Assert.IsFalse(ti.mipmapEnabled, f);
            }
        }

        [Test]
        public void 走り4フレームと着地1フレームを32pxで切り出す()
        {
            var art = new GameArt();
            Assert.IsTrue(art.IsPixelArt);
            Assert.AreEqual(4, art.Run.Length);
            foreach (var s in art.Run)
            {
                Assert.AreEqual(32f, s.rect.width);
                Assert.AreEqual(32f, s.rect.height);
                Assert.AreEqual(32f, s.pixelsPerUnit);
            }
            Assert.AreEqual(32f, art.Land.rect.width);
            Assert.AreEqual(1f, art.PlayerSize, "ドット絵は1マス＝32pxで表示（拡大縮小しない）");
            Assert.IsNotNull(art.Block);
            Assert.IsNotNull(art.SpikeUp);
            Assert.IsNotNull(art.SpikeDown);
            Assert.IsNotNull(art.Coin);
        }
    }
}
