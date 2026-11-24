using FlipRun.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FlipRun.EditorTools
{
    /// <summary>
    /// Editorメニュー「FlipRun > Build Main Scene」。
    /// Main シーン（カメラ・2Dの全体ライト・ゲーム本体）を作り、ビルド対象のシーンに登録する。
    /// 地形・プレイヤー・UIは実行時に FlipRunGame が組み立てる。
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("FlipRun/Build Main Scene")]
        public static void BuildMenu() => Build();

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GreyboxArt.Background;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            // ドットがにじまないように：576×324（32px/マスで縦10.1マス）で描いてから、点補間で画面サイズに拡大する
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 32;
            ppc.refResolutionX = 576;
            ppc.refResolutionY = 324;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;

            var gameGo = new GameObject("FlipRunGame");
            gameGo.AddComponent<FlipRunGame>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[FlipRun] {ScenePath} を作成し、ビルド対象に登録しました");
        }
    }
}
