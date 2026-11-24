using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// コマンドラインからのビルド：
/// Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildWebGL -logFile build.log
/// 出力先は Builds/WebGL。
/// </summary>
public static class BuildScript
{
    public const string OutputPath = "Builds/WebGL";

    [MenuItem("FlipRun/Build WebGL")]
    public static void BuildWebGL()
    {
        ApplyWebGLSettings();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"[FlipRun] WebGL build: {s.result}, {s.totalSize / (1024f * 1024f):0.0}MB, {s.totalTime.TotalSeconds:0}s, errors={s.totalErrors}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>ブリーフ第9章の WebGL 設定。</summary>
    public static void ApplyWebGLSettings()
    {
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 540;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        AssetDatabase.SaveAssets();
    }
}
