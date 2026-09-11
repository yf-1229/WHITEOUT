#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class ProjectConfigurator
{
    private const string PipelinePath = "Assets/Settings/QuestUniversalRenderPipeline.asset";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
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
        EnsureRuntimeShaders();
    }

    private static void EnsureRuntimeShaders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        foreach (var entry in new[]
                 {
                     ("RuntimeLit", "Universal Render Pipeline/Lit"),
                     ("RuntimeUnlit", "Universal Render Pipeline/Unlit"),
                     ("RuntimeParticles", "Universal Render Pipeline/Particles/Unlit"),
                     ("RuntimeSky", "Skybox/Procedural")
                 })
        {
            var path = $"Assets/Resources/{entry.Item1}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find(entry.Item2)), path);
        }
    }

    public static void SmokePlayMode()
    {
        EditorSceneManager.OpenScene(ScenePath);
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
        try { CheckRendering(); }
        catch (System.Exception exception)
        {
            smokeFailed = true;
            Debug.LogException(exception);
        }
        EditorApplication.isPlaying = false;
        EditorApplication.update -= TickSmokePlayMode;
        Application.logMessageReceived -= CapturePlayError;
        EditorSettings.enterPlayModeOptionsEnabled = previousFastPlay;
        EditorSettings.enterPlayModeOptions = previousPlayOptions;
        EditorApplication.delayCall += () => EditorApplication.Exit(smokeFailed ? 1 : 0);
    }

    private static void CheckRendering()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var rocks = renderers.Where(renderer => renderer.name == "GraniteOutcrop").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(20, rocks.Length);
        UnityEngine.Assertions.Assert.AreEqual(1, rocks.Select(renderer => renderer.sharedMaterial).Distinct().Count());
        UnityEngine.Assertions.Assert.AreEqual(1, rocks.Select(renderer => renderer.sharedMaterial.mainTexture).Distinct().Count());
        var firs = renderers.Where(renderer => renderer.transform.parent != null && renderer.transform.parent.name == "SnowFir").ToArray();
        UnityEngine.Assertions.Assert.IsTrue(firs.Length > 7);
        UnityEngine.Assertions.Assert.AreEqual(3, firs.Select(renderer => renderer.sharedMaterial).Distinct().Count());
        var markers = renderers.Where(renderer => renderer.name == "LandingGlow").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(144, markers.Length);
        UnityEngine.Assertions.Assert.AreEqual(6, markers.Select(renderer => renderer.sharedMaterial).Distinct().Count());
        var propellers = renderers.Where(renderer => renderer.name == "Propeller").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(8, propellers.Length);
        UnityEngine.Assertions.Assert.AreEqual(1, propellers.Select(renderer => renderer.sharedMaterial).Distinct().Count());
        UnityEngine.Assertions.Assert.AreEqual(1, propellers.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).Distinct().Count());
        var fieldMarker = renderers.Where(renderer => renderer.transform.parent != null && renderer.transform.parent.name == "FieldMarker").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(2, fieldMarker.Length);
        UnityEngine.Assertions.Assert.IsTrue(fieldMarker.All(renderer => !renderer.gameObject.activeInHierarchy && !renderer.isPartOfStaticBatch));
        UnityEngine.Assertions.Assert.AreEqual(3, renderers.Count(renderer => renderer.name == "IceCrack"));
        UnityEngine.Assertions.Assert.AreEqual(2, renderers.Count(renderer => renderer.name == "MinePost"));
        foreach (var name in new[] { "CabinWindow", "CabinDoor", "WeatherDish", "ShelterWindow" })
            UnityEngine.Assertions.Assert.IsNotNull(GameObject.Find(name), name);

        var scenery = GameObject.Find("StaticScenery").GetComponentsInChildren<MeshRenderer>();
        UnityEngine.Assertions.Assert.IsTrue(scenery.Length > markers.Length);
        foreach (var renderer in scenery)
        {
            UnityEngine.Assertions.Assert.IsTrue(renderer.isPartOfStaticBatch, renderer.name);
            UnityEngine.Assertions.Assert.IsTrue(renderer.bounds.size.magnitude < 25f, "Keep individual culling bounds");
        }
        var drone = Object.FindAnyObjectByType<DroneController>();
        var distressReady = typeof(MissionController).GetMethod("DistressHoldReady", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.IsTrue((bool)distressReady.Invoke(null, new object[] { 8f, 1.99f }));
        UnityEngine.Assertions.Assert.IsFalse((bool)distressReady.Invoke(null, new object[] { 8.01f, 1f }));
        UnityEngine.Assertions.Assert.IsFalse((bool)distressReady.Invoke(null, new object[] { 5f, 2f }));
        var freshSuccess = typeof(MissionController).GetMethod("HasFreshSuccess", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.IsTrue((bool)freshSuccess.Invoke(null, new object[] { 2, 2, 5, true, false }));
        UnityEngine.Assertions.Assert.IsTrue((bool)freshSuccess.Invoke(null, new object[] { 5, 2, 5, false, true }));
        UnityEngine.Assertions.Assert.IsFalse((bool)freshSuccess.Invoke(null, new object[] { 4, 2, 5, true, true }));
        var shapeStick = typeof(DroneController).GetMethod("ShapeStick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.AreEqual(Vector2.zero, (Vector2)shapeStick.Invoke(drone, new object[] { new Vector2(0.02f, 0.02f) }));
        var batteryLoad = typeof(DroneController).GetMethod("BatteryLoad", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var hoverLoad = (float)batteryLoad.Invoke(null, new object[] { 0f, 0f, 0f });
        UnityEngine.Assertions.Assert.IsTrue((float)batteryLoad.Invoke(null, new object[] { 0f, 3.6f, 1f }) > hoverLoad);
        UnityEngine.Assertions.Assert.IsTrue((float)batteryLoad.Invoke(null, new object[] { 1f, 8f, 6f }) > hoverLoad);
        UnityEngine.Assertions.Assert.AreEqual(3, drone.GetComponents<AudioSource>().Length);
        drone.GetComponent<DronePresentation>().PlayWarning();
        drone.GetComponent<DronePresentation>().PlaySuccess();
        foreach (var renderer in drone.GetComponentsInChildren<MeshRenderer>(true))
            UnityEngine.Assertions.Assert.IsFalse(renderer.isPartOfStaticBatch, "Moving parts must remain movable");
        UnityEngine.Assertions.Assert.IsFalse(GameObject.Find("RescueLeader").GetComponent<Renderer>().isPartOfStaticBatch);
        var thermalTargets = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(renderer => renderer.name == "ThermalTrace").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(8, thermalTargets.Length);
        UnityEngine.Assertions.Assert.IsTrue(thermalTargets.All(renderer => !renderer.enabled && !renderer.isPartOfStaticBatch));
        var monitor = Object.FindAnyObjectByType<FpvMonitor>();
        monitor.SetThermalAvailable(true, 3);
        typeof(FpvMonitor).GetField("thermal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(monitor, true);
        typeof(FpvMonitor).GetMethod("UpdateThermalTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(monitor, null);
        UnityEngine.Assertions.Assert.IsTrue(monitor.thermalTargets[3].enabled && monitor.thermalTargets.Count(renderer => renderer.enabled) == 1);
        monitor.SetThermalAvailable(false);
        UnityEngine.Assertions.Assert.IsTrue(monitor.thermalTargets.All(renderer => !renderer.enabled));
        UnityEngine.Assertions.Assert.IsTrue(GameObject.Find("HomeBase").GetComponentInChildren<Light>().intensity > 18f);

        var viewer = Camera.main;
        var fpv = GameObject.Find("FPVCamera").GetComponent<Camera>();
        var storm = Object.FindAnyObjectByType<ParticleSystem>();
        UnityEngine.Assertions.Assert.AreEqual(520, storm.main.maxParticles);
        UnityEngine.Assertions.Assert.AreEqual(ParticleSystemRenderMode.Billboard, storm.GetComponent<ParticleSystemRenderer>().renderMode);
        var images = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var name in new[] { "SpeedFill", "AltitudeFill", "BatteryFill" })
            UnityEngine.Assertions.Assert.IsTrue(images.Any(image => image.name == name), name);
        var texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var name in new[] { "SpeedValue", "AltitudeValue", "BatteryValue", "WindNeedle", "NavigationNeedle" })
            UnityEngine.Assertions.Assert.IsTrue(texts.Any(text => text.name == name), name);
        CheckPreview(viewer);
        var uiMask = LayerMask.GetMask("UI");
        UnityEngine.Assertions.Assert.AreEqual(0, fpv.cullingMask & uiMask);
        UnityEngine.Assertions.Assert.IsTrue((viewer.cullingMask & uiMask) != 0);
        UnityEngine.Assertions.Assert.IsFalse(fpv.enabled);
        foreach (var root in new[] { viewer.transform, GameObject.Find("FpvHud").transform })
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.GetComponent<MeshRenderer>() != null || child.GetComponent<CanvasRenderer>() != null)
                UnityEngine.Assertions.Assert.AreEqual(LayerMask.NameToLayer("UI"), child.gameObject.layer);
        }
        UnityEngine.Assertions.Assert.AreEqual(1f, ((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale);
        UnityEngine.Assertions.Assert.AreEqual(720f, viewer.farClipPlane);
        UnityEngine.Assertions.Assert.AreEqual(4, ((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).msaaSampleCount);
        var materials = renderers.Select(renderer => renderer.sharedMaterial).Distinct().ToArray();
        Debug.Log($"RENDER CHECK: renderers={renderers.Length}, materials={materials.Length}, " +
                  $"textures={materials.Where(material => material.HasProperty("_MainTex") || material.HasProperty("_BaseMap")).Select(material => material.mainTexture).Where(texture => texture != null).Distinct().Count()}, " +
                  $"staticBatched={scenery.Length}");
    }

    private static void CheckPreview(Camera viewer)
    {
        var target = RenderTexture.GetTemporary(32, 32, 16);
        var image = new Texture2D(32, 32, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        var previousTarget = viewer.targetTexture;
        try
        {
            viewer.targetTexture = target;
            viewer.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            image.Apply();
            UnityEngine.Assertions.Assert.IsTrue(image.GetPixels().Any(color => color.maxColorComponent > 0.05f), "Game preview rendered black");
        }
        finally
        {
            viewer.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void CapturePlayError(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error) smokeFailed = true;
    }
}
#endif
