#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ProjectConfigurator
{
    private const string PipelinePath = "Assets/Settings/QuestUniversalRenderPipeline.asset";
    private static int smokeFrames;
    private static bool smokeFailed;
    private static bool previousFastPlay;
    private static EnterPlayModeOptions previousPlayOptions;

    [InitializeOnLoadMethod]
    private static void ConfigureRendering()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            pipeline.name = "QuestUniversalRenderPipeline";
            pipeline.LoadBuiltinRendererData();
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = false;
            pipeline.shadowDistance = 40f;
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
    }

    public static void SmokePlayMode()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
        smokeFrames = 0;
        smokeFailed = false;
        previousFastPlay = EditorSettings.enterPlayModeOptionsEnabled;
        previousPlayOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += CapturePlayError;
        EditorApplication.update += TickSmokePlayMode;
        EditorApplication.isPlaying = true;
    }

    private static void TickSmokePlayMode()
    {
        if (!EditorApplication.isPlaying || ++smokeFrames < 90) return;
        EditorApplication.isPlaying = false;
        EditorApplication.update -= TickSmokePlayMode;
        Application.logMessageReceived -= CapturePlayError;
        EditorSettings.enterPlayModeOptionsEnabled = previousFastPlay;
        EditorSettings.enterPlayModeOptions = previousPlayOptions;
        EditorApplication.delayCall += () => EditorApplication.Exit(smokeFailed ? 1 : 0);
    }

    private static void CapturePlayError(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Assert) smokeFailed = true;
    }
}
#endif
