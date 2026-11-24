using System.Linq;
using FlipRun.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlipRun.Tests
{
    public class UiFontTests
    {
        [Test]
        public void UIの文字がすべてフォントに入っている()
        {
            var font = Resources.Load<Font>(UiText.FontResource);
            Assert.IsNotNull(font, "Resources/" + UiText.FontResource);
            Assert.IsFalse(font.dynamic, "WebGLでOSのフォントに頼らないよう、静的フォントにする");
            var missing = UiText.FontCharacters.Where(c => !font.HasCharacter(c)).ToArray();
            Assert.IsEmpty(missing, "フォントに無い文字: " + new string(missing) + "（FlipRun > Setup UI Font を実行）");
        }

        [Test]
        public void フォントファイル本体はビルドに入れない()
        {
            var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath("Assets/Resources/Fonts/NotoSansJP-Regular.otf");
            Assert.IsFalse(importer.includeFontData);
            Assert.AreEqual(FontTextureCase.CustomSet, importer.fontTextureCase);
        }
    }
}
