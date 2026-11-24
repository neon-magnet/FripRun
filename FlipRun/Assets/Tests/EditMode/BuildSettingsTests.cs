using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace FlipRun.Tests
{
    public class BuildSettingsTests
    {
        [Test]
        public void WebGL設定はunityroomの指定とブリーフ第9章に合う()
        {
            BuildScript.ApplyWebGLSettings();
            Assert.IsFalse(EditorUserBuildSettings.development, "Development Build はオフ");
            Assert.AreEqual(WebGLCompressionFormat.Gzip, PlayerSettings.WebGL.compressionFormat);
            Assert.IsFalse(PlayerSettings.WebGL.decompressionFallback, "unityroom は Decompression Fallback オフを指定");
            Assert.AreEqual(WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly, PlayerSettings.WebGL.exceptionSupport);
            Assert.AreEqual(ManagedStrippingLevel.Medium, PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL));
            Assert.AreEqual(960, PlayerSettings.defaultWebScreenWidth);
            Assert.AreEqual(540, PlayerSettings.defaultWebScreenHeight);
            Assert.IsFalse(PlayerSettings.SplashScreen.show, "起動を速くするためスプラッシュなし");
        }
    }
}
