using UnityEditor;
using UnityEngine;

namespace FlipRun.EditorTools
{
    /// <summary>
    /// Assets/Resources/Art/ のドット絵を、ブリーフ第9章の設定で読み込む：
    /// Filter Mode = Point、Compression = None、Pixels Per Unit = 32、ミップマップなし。
    /// 走りの4フレームは実行時に GameArt が切り分ける。
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 32;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
