using FlipRun.Game;
using UnityEditor;
using UnityEngine;

namespace FlipRun.EditorTools
{
    /// <summary>
    /// Editorメニュー「FlipRun > Setup UI Font」。
    /// Noto Sans JP を「UIで使う文字だけ」の静的フォントとして読み込む。
    /// WebGL には OS のフォントがないため、日本語を出すにはフォントを同梱する必要がある。
    /// 文字を絞るので、フォントファイル本体（4.5MB）はビルドに入らない。
    /// </summary>
    public static class FontSetup
    {
        public const string FontPath = "Assets/Resources/Fonts/NotoSansJP-Regular.otf";
        public const int ImportSize = 64;

        [MenuItem("FlipRun/Setup UI Font")]
        public static void Setup()
        {
            var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(FontPath);
            if (importer == null)
            {
                Debug.LogError("[FlipRun] フォントが見つかりません: " + FontPath);
                return;
            }
            importer.fontSize = ImportSize;
            importer.fontTextureCase = FontTextureCase.CustomSet;
            importer.customCharacters = UiText.FontCharacters;
            importer.fontRenderingMode = FontRenderingMode.Smooth;
            importer.includeFontData = false;
            importer.SaveAndReimport();
            Debug.Log($"[FlipRun] UIフォントを作り直しました（{UiText.FontCharacters.Length}文字）");
        }

        public static void SetupFromCommandLine()
        {
            Setup();
            EditorApplication.Exit(0);
        }
    }
}
