#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ProjectConfigurator
{
    private const string PipelinePath = "Assets/Settings/QuestUniversalRenderPipeline.asset";

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
}
#endif
