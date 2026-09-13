#if UNITY_EDITOR
using System.IO;
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
        UnityEngine.Assertions.Assert.IsTrue(renderers.SelectMany(renderer => renderer.sharedMaterials).All(material => material != null && material.shader.isSupported));
        var rocks = renderers.Where(renderer => renderer.name == "GraniteOutcrop").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(20, rocks.Length);
        UnityEngine.Assertions.Assert.AreEqual(1, rocks.Select(renderer => renderer.sharedMaterial).Distinct().Count());
        UnityEngine.Assertions.Assert.AreEqual(1, rocks.Select(renderer => renderer.sharedMaterial.mainTexture).Distinct().Count());
        var distantMountain = renderers.Single(renderer => renderer.name == "DistantMountain");
        UnityEngine.Assertions.Assert.AreEqual("DistantScenery", distantMountain.transform.parent.name);
        UnityEngine.Assertions.Assert.IsFalse(distantMountain.isPartOfStaticBatch);
        UnityEngine.Assertions.Assert.AreEqual(3072, distantMountain.GetComponent<MeshFilter>().sharedMesh.vertexCount);
        UnityEngine.Assertions.Assert.AreEqual(3, distantMountain.sharedMaterials.Length);
        UnityEngine.Assertions.Assert.AreEqual(3, distantMountain.GetComponent<MeshFilter>().sharedMesh.subMeshCount);
        for (var face = 0; face < 3; face++) UnityEngine.Assertions.Assert.IsTrue(distantMountain.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(face) > 0);
        var rescueTrail = renderers.Single(renderer => renderer.name == "RescueTrail");
        UnityEngine.Assertions.Assert.AreEqual(384, rescueTrail.GetComponent<MeshFilter>().sharedMesh.vertexCount);
        UnityEngine.Assertions.Assert.IsFalse(rescueTrail.isPartOfStaticBatch);
        UnityEngine.Assertions.Assert.IsNull(rescueTrail.GetComponent<Collider>());
        var firs = renderers.Where(renderer => renderer.name == "SnowFir").ToArray();
        UnityEngine.Assertions.Assert.IsTrue(firs.Length > 20);
        UnityEngine.Assertions.Assert.IsTrue(firs.All(renderer => renderer.sharedMaterials.Length == 3));
        UnityEngine.Assertions.Assert.AreEqual(3, firs.SelectMany(renderer => renderer.sharedMaterials).Distinct().Count());
        UnityEngine.Assertions.Assert.IsTrue(firs.All(renderer => renderer.GetComponent<CapsuleCollider>() is { radius: 0.75f, height: 4.2f }));
        var markers = renderers.Where(renderer => renderer.name == "LandingMark").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(9, markers.Length);
        UnityEngine.Assertions.Assert.AreEqual(1, markers.Select(renderer => renderer.sharedMaterial).Distinct().Count());
        UnityEngine.Assertions.Assert.AreEqual(9, renderers.Count(renderer => renderer.name == "LandingPadCenter"));
        var pads = renderers.Where(renderer => renderer.name == "LandingPad").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(9, pads.Length);
        var terrain = Terrain.activeTerrain;
        UnityEngine.Assertions.Assert.IsTrue(pads.All(pad => Mathf.Abs(pad.bounds.center.y - terrain.SampleHeight(pad.transform.position) - terrain.transform.position.y - 0.02f) < 0.1f));
        UnityEngine.Assertions.Assert.AreEqual(3, pads.Count(pad => pad.transform.parent.parent.position.y - pad.bounds.center.y > 1f));
        var terrainNormal = typeof(PrototypeBootstrap).GetMethod("TerrainNormal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.IsTrue(pads.All(pad => Vector3.Dot(pad.transform.up, (Vector3)terrainNormal.Invoke(null, new object[] { terrain, pad.transform.position })) > 0.999f));
        UnityEngine.Assertions.Assert.AreEqual(3, renderers.Count(renderer => renderer.name == "HomeMark"));
        UnityEngine.Assertions.Assert.AreEqual(0, renderers.Count(renderer => renderer.name == "LandingGlow"));
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
        var startBody = drone.GetComponent<Rigidbody>();
        UnityEngine.Assertions.Assert.IsTrue(drone.IsStandby && startBody.isKinematic && Vector3.Dot(startBody.rotation * Vector3.up, Vector3.up) > 0.98f, $"start rotation {startBody.rotation.eulerAngles}");
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(startBody.position.y - Object.FindAnyObjectByType<MissionController>().homePad.position.y - 0.33f) < 0.01f);
        var packageCollider = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(collider => collider.name == "EmergencyMedicalPackage");
        UnityEngine.Assertions.Assert.IsTrue(packageCollider.bounds.min.y > Terrain.activeTerrain.SampleHeight(drone.transform.position) + Terrain.activeTerrain.transform.position.y + 0.1f);
        var distressReady = typeof(MissionController).GetMethod("DistressHoldReady", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.IsTrue((bool)distressReady.Invoke(null, new object[] { 8f, 1.99f }));
        UnityEngine.Assertions.Assert.IsFalse((bool)distressReady.Invoke(null, new object[] { 8.01f, 1f }));
        UnityEngine.Assertions.Assert.IsFalse((bool)distressReady.Invoke(null, new object[] { 5f, 2f }));
        var actionReady = typeof(MissionController).GetMethod("ActionHoldReady", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.IsTrue((bool)actionReady.Invoke(null, new object[] { true, true, true }));
        UnityEngine.Assertions.Assert.IsFalse((bool)actionReady.Invoke(null, new object[] { true, true, false }));
        UnityEngine.Assertions.Assert.IsFalse((bool)actionReady.Invoke(null, new object[] { true, false, true }));
        var fieldActionReady = typeof(MissionController).GetMethod("ActionReady", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.IsTrue((bool)fieldActionReady.Invoke(null, new object[] { StoryAction.Deliver, false, false, true }));
        UnityEngine.Assertions.Assert.IsFalse((bool)fieldActionReady.Invoke(null, new object[] { StoryAction.Deliver, true, true, false }));
        UnityEngine.Assertions.Assert.IsTrue((bool)fieldActionReady.Invoke(null, new object[] { StoryAction.Mark, true, true, false }));
        UnityEngine.Assertions.Assert.IsFalse((bool)fieldActionReady.Invoke(null, new object[] { StoryAction.Mark, true, false, true }));
        var shapeStick = typeof(DroneController).GetMethod("ShapeStick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        UnityEngine.Assertions.Assert.AreEqual(Vector2.zero, (Vector2)shapeStick.Invoke(drone, new object[] { new Vector2(0.02f, 0.02f) }));
        var manualRateTarget = typeof(DroneController).GetMethod("ManualRateTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var fullRate = (Vector3)manualRateTarget.Invoke(drone, new object[] { new Vector2(1f, 0f), Vector2.one });
        var expectedRate = new Vector3(-drone.manualRateDegrees, drone.manualYawRateDegrees, -drone.manualRateDegrees) * Mathf.Deg2Rad;
        UnityEngine.Assertions.Assert.IsTrue(Vector3.Distance(fullRate, expectedRate) < 0.0001f);
        UnityEngine.Assertions.Assert.AreEqual(Vector3.zero, (Vector3)manualRateTarget.Invoke(drone, new object[] { Vector2.zero, Vector2.zero }));
        var batteryLoad = typeof(DroneController).GetMethod("BatteryLoad", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var hoverLoad = (float)batteryLoad.Invoke(null, new object[] { 0f, 0f, 0f });
        UnityEngine.Assertions.Assert.IsTrue((float)batteryLoad.Invoke(null, new object[] { 0f, 3.6f, 1f }) > hoverLoad);
        UnityEngine.Assertions.Assert.IsTrue((float)batteryLoad.Invoke(null, new object[] { 1f, 8f, 6f }) > hoverLoad);
        var assistedMotorLoad = typeof(DroneController).GetMethod("AssistedMotorLoad", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var levelMotorLoad = (float)assistedMotorLoad.Invoke(null, new object[] { 0f, 1f });
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(levelMotorLoad - 1f) < 0.001f);
        UnityEngine.Assertions.Assert.IsTrue((float)assistedMotorLoad.Invoke(null, new object[] { 4f, 1f }) > levelMotorLoad);
        UnityEngine.Assertions.Assert.IsTrue((float)assistedMotorLoad.Invoke(null, new object[] { 0f, 0.6f }) > levelMotorLoad);
        var forwardTilt = Mathf.Cos(drone.maxTiltDegrees * Mathf.Deg2Rad);
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs((float)assistedMotorLoad.Invoke(null, new object[] { 0f, forwardTilt }) * forwardTilt - 1f) < 0.001f);
        var resetObject = new GameObject("ResetPoseCheck");
        var resetDrone = resetObject.AddComponent<DroneController>();
        var resetBody = resetObject.GetComponent<Rigidbody>();
        resetBody.rotation = Quaternion.Euler(180f, 0f, 0f);
        resetObject.transform.rotation = resetBody.rotation;
        resetDrone.ResetToIdle(Vector3.zero);
        UnityEngine.Assertions.Assert.IsTrue(Vector3.Dot(resetBody.rotation * Vector3.up, Vector3.up) > 0.999f && Vector3.Dot(resetObject.transform.up, Vector3.up) > 0.999f);
        resetDrone.BeginMission(new Vector3(0f, 200f, 0f));
        var holdAltitude = resetBody.position.y;
        var assistedFlight = typeof(DroneController).GetMethod("ApplyAssistedFlight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var simulationMode = Physics.simulationMode;
        var sceneBody = drone.GetComponent<Rigidbody>();
        var sceneWasKinematic = sceneBody.isKinematic;
        sceneBody.isKinematic = true;
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            for (var step = 0; step < 100; step++)
            {
                assistedFlight.Invoke(resetDrone, new object[] { Vector3.forward * resetDrone.maxHorizontalSpeed, holdAltitude, 0f });
                Physics.Simulate(0.02f);
            }
            UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(resetBody.position.y - holdAltitude) < 0.25f);
            resetBody.rotation = Quaternion.Euler(0f, 0f, 80f);
            resetObject.transform.rotation = resetBody.rotation;
            resetBody.angularVelocity = Vector3.zero;
            for (var step = 0; step < 100; step++)
            {
                assistedFlight.Invoke(resetDrone, new object[] { Vector3.forward * resetDrone.maxHorizontalSpeed, holdAltitude, 150f });
                Physics.Simulate(0.02f);
            }
            var planarVelocity = new Vector3(resetBody.linearVelocity.x, 0f, resetBody.linearVelocity.z);
            var maxAcceleration = Mathf.Tan(resetDrone.maxTiltDegrees * Mathf.Deg2Rad) * Physics.gravity.magnitude;
            var desiredAcceleration = Vector3.ClampMagnitude((Vector3.forward * resetDrone.maxHorizontalSpeed - planarVelocity) * 2.2f, maxAcceleration);
            var desiredUp = (Vector3.up + desiredAcceleration / Physics.gravity.magnitude).normalized;
            var recoveredUp = Vector3.Dot(resetBody.rotation * Vector3.up, desiredUp);
            UnityEngine.Assertions.Assert.IsTrue(recoveredUp > 0.995f, $"assist recovery alignment={recoveredUp:F3}, rotation={resetBody.rotation.eulerAngles}");
        }
        finally
        {
            Physics.simulationMode = simulationMode;
            sceneBody.isKinematic = sceneWasKinematic;
        }
        Object.DestroyImmediate(resetObject);
        var safeReturnAltitude = typeof(DroneController).GetMethod("SafeReturnAltitude", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var returnDirection = Vector3.forward;
        var safeAltitude = (float)safeReturnAltitude.Invoke(null, new object[] { Terrain.activeTerrain, drone.transform.position, returnDirection, -100f });
        for (var distance = 8f; distance <= 24f; distance += 8f)
            UnityEngine.Assertions.Assert.IsTrue(safeAltitude + 0.01f >= Terrain.activeTerrain.SampleHeight(drone.transform.position + returnDirection * distance) + Terrain.activeTerrain.transform.position.y + 12f);
        UnityEngine.Assertions.Assert.AreEqual(42f, (float)safeReturnAltitude.Invoke(null, new object[] { null, Vector3.zero, Vector3.forward, 42f }));
        UnityEngine.Assertions.Assert.AreEqual(3, drone.GetComponents<AudioSource>().Length);
        var dronePresentation = drone.GetComponent<DronePresentation>();
        UnityEngine.Assertions.Assert.AreEqual(0.15f, dronePresentation.navigationLight.intensity);
        UnityEngine.Assertions.Assert.AreEqual(0.8f, dronePresentation.navigationLight.range);
        dronePresentation.PlayWarning();
        dronePresentation.PlaySuccess();
        dronePresentation.PlayRadio();
        foreach (var renderer in drone.GetComponentsInChildren<MeshRenderer>(true))
            UnityEngine.Assertions.Assert.IsFalse(renderer.isPartOfStaticBatch, "Moving parts must remain movable");
        var rescueParts = renderers.Where(renderer => renderer.transform.parent != null && renderer.transform.parent.name == "RescueSignal").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(4, rescueParts.Length);
        UnityEngine.Assertions.Assert.IsTrue(rescueParts.All(renderer => !renderer.isPartOfStaticBatch && !renderer.gameObject.activeInHierarchy));
        UnityEngine.Assertions.Assert.IsTrue(rescueParts.All(renderer => renderer.GetComponent<Collider>() == null));
        var thermalTargets = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(renderer => renderer.name == "ThermalTrace").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(8, thermalTargets.Length);
        UnityEngine.Assertions.Assert.IsTrue(thermalTargets.All(renderer => !renderer.enabled && !renderer.isPartOfStaticBatch));
        var monitor = Object.FindAnyObjectByType<FpvMonitor>();
        var originalDronePosition = drone.transform.position;
        var originalDroneRotation = drone.transform.rotation;
        drone.transform.SetPositionAndRotation(new Vector3(12f, 18f, 30f), Quaternion.Euler(17f, 42f, -24f));
        typeof(FpvMonitor).GetMethod("UpdateGimbal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(monitor, null);
        UnityEngine.Assertions.Assert.IsTrue(Vector3.Distance(monitor.Gimbal.position, drone.transform.TransformPoint(monitor.cameraOffset)) < 0.001f);
        UnityEngine.Assertions.Assert.IsTrue(Quaternion.Angle(monitor.Gimbal.rotation, drone.transform.rotation * Quaternion.Euler(monitor.cameraTiltDegrees, 0f, 0f)) < 0.001f);
        drone.transform.SetPositionAndRotation(originalDronePosition, originalDroneRotation);
        typeof(FpvMonitor).GetMethod("UpdateGimbal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(monitor, null);
        monitor.SetThermalAvailable(true, 3);
        typeof(FpvMonitor).GetField("thermal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(monitor, true);
        typeof(FpvMonitor).GetMethod("UpdateThermalTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(monitor, null);
        UnityEngine.Assertions.Assert.IsTrue(monitor.thermalTargets[3].enabled && monitor.thermalTargets.Count(renderer => renderer.enabled) == 1);
        monitor.SetThermalAvailable(false);
        UnityEngine.Assertions.Assert.IsTrue(monitor.thermalTargets.All(renderer => !renderer.enabled));
        var landingLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(light => light.name == "LandingLight").ToArray();
        UnityEngine.Assertions.Assert.AreEqual(9, landingLights.Length);
        UnityEngine.Assertions.Assert.IsTrue(landingLights.All(light => light.type == LightType.Spot && light.intensity == 0f));

        var viewer = Camera.main;
        var viewerGround = terrain.SampleHeight(viewer.transform.position) + terrain.transform.position.y;
        UnityEngine.Assertions.Assert.IsTrue(viewer.transform.position.y - viewerGround > 1.5f);
        var fpv = GameObject.Find("FPVCamera").GetComponent<Camera>();
        var storm = Object.FindAnyObjectByType<ParticleSystem>();
        UnityEngine.Assertions.Assert.AreEqual(520, storm.main.maxParticles);
        UnityEngine.Assertions.Assert.AreEqual(ParticleSystemRenderMode.Billboard, storm.GetComponent<ParticleSystemRenderer>().renderMode);
        UnityEngine.Assertions.Assert.AreEqual(GameObject.Find("FPVCamera").transform, storm.transform.parent);
        var missions = Object.FindAnyObjectByType<MissionController>();
        var thermalOverlay = GameObject.Find("ThermalOverlay").GetComponent<Image>();
        UnityEngine.Assertions.Assert.IsFalse(thermalOverlay.enabled);
        monitor.SetThermalAvailable(true, 3);
        typeof(FpvMonitor).GetField("thermal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(monitor, true);
        typeof(FpvMonitor).GetMethod("UpdateThermalTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(monitor, null);
        typeof(MissionController).GetMethod("UpdateDisplay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsTrue(thermalOverlay.enabled);
        var fieldBeacon = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(renderer => renderer.name == "FieldBeacon");
        UnityEngine.Assertions.Assert.AreEqual(fieldBeacon.sharedMaterial, monitor.thermalTargets[3].sharedMaterial);
        monitor.SetThermalAvailable(false);
        typeof(MissionController).GetMethod("UpdateDisplay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsFalse(thermalOverlay.enabled);
        UnityEngine.Assertions.Assert.AreEqual(2, missions.recoveryWindows.Length);
        UnityEngine.Assertions.Assert.AreEqual(2, missions.recoveryWindows.Select(renderer => renderer.sharedMaterial).Distinct().Count());
        var recoveryColor = typeof(MissionController).GetMethod("RecoveryColor", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var dimWindow = (Color)recoveryColor.Invoke(null, new object[] { Color.white, false });
        var litWindow = (Color)recoveryColor.Invoke(null, new object[] { Color.white, true });
        UnityEngine.Assertions.Assert.IsTrue(litWindow.maxColorComponent > dimWindow.maxColorComponent * 4f);
        var updateDisplay = typeof(MissionController).GetMethod("UpdateDisplay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        drone.transform.rotation = Quaternion.Euler(0f, 0f, 23f);
        updateDisplay.Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(Mathf.DeltaAngle(missions.artificialHorizon.localEulerAngles.z, -23f)) < 0.01f);
        drone.transform.rotation = originalDroneRotation;
        updateDisplay.Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsFalse(missions.rescueTarget.gameObject.activeSelf);
        var showRescueTarget = typeof(MissionController).GetMethod("ShowRescueTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        showRescueTarget.Invoke(missions, new object[] { 3 });
        UnityEngine.Assertions.Assert.IsTrue(missions.rescueTarget.gameObject.activeSelf);
        var rescueTerrain = Terrain.activeTerrain;
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(missions.rescueTarget.position.y - rescueTerrain.SampleHeight(missions.rescueTarget.position) - rescueTerrain.transform.position.y) < 0.01f);
        missions.rescueTarget.gameObject.SetActive(false);
        UnityEngine.Assertions.Assert.IsFalse(missions.radioText.transform.parent.gameObject.activeSelf);
        var speak = typeof(MissionController).GetMethod("Speak", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        speak.Invoke(missions, new object[] { "SAR NET", "TEST LINE", 3f });
        UnityEngine.Assertions.Assert.IsTrue(missions.radioText.text.Contains("SAR NET") && missions.radioText.text.Contains("TEST LINE"));
        missions.radioText.transform.parent.gameObject.SetActive(false);
        var setStorm = typeof(MissionController).GetMethod("SetStorm", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        drone.SetWind(new Vector3(2f, 0f, -1f));
        setStorm.Invoke(missions, new object[] { 1f });
        UnityEngine.Assertions.Assert.AreEqual(240f, storm.emission.rateOverTime.constant);
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(storm.velocityOverLifetime.x.constantMin - 1.65f) < 0.01f);
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(storm.velocityOverLifetime.z.constantMax + 0.65f) < 0.01f);
        UnityEngine.Assertions.Assert.IsTrue(RenderSettings.ambientGroundColor.maxColorComponent > 0.35f);
        drone.SetWind(Vector3.zero);
        setStorm.Invoke(missions, new object[] { 0f });
        var images = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var name in new[] { "SpeedFill", "AltitudeFill", "BatteryFill", "ActionFill" })
            UnityEngine.Assertions.Assert.IsTrue(images.Any(image => image.name == name), name);
        var texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var name in new[] { "SpeedValue", "AltitudeValue", "BatteryValue", "WindValue", "NavigationValue", "WindNeedle", "NavigationNeedle", "ActionLabel" })
            UnityEngine.Assertions.Assert.IsTrue(texts.Any(text => text.name == name), name);
        UnityEngine.Assertions.Assert.IsFalse(images.Single(image => image.name == "ActionIndicator").gameObject.activeSelf);
        UnityEngine.Assertions.Assert.AreEqual(2f, images.Single(image => image.name == "ActionTrack").rectTransform.sizeDelta.y);
        foreach (var pair in new[] { ("SpeedLabel", "H.S"), ("AltitudeLabel", "ALT"), ("BatteryLabel", "BAT"), ("WindLabel", "WIND"), ("NavigationLabel", "DST") })
            UnityEngine.Assertions.Assert.AreEqual(pair.Item2, texts.Single(text => text.name == pair.Item1).text);
        var headingText = texts.Single(text => text.name == "HeadingValue").text;
        UnityEngine.Assertions.Assert.IsTrue(headingText.Length == 4 && headingText[3] == '°' && int.TryParse(headingText.Substring(0, 3), out var displayedHeading) && displayedHeading < 360);
        UnityEngine.Assertions.Assert.IsFalse(texts.Single(text => text.name == "SpeedValue").text.Contains("H.S"));
        foreach (var name in new[] { "SpeedTrack", "AltitudeTrack", "BatteryTrack", "WindTrack", "NavigationTrack" })
            UnityEngine.Assertions.Assert.AreEqual(2f, images.Single(image => image.name == name).rectTransform.sizeDelta.y);
        UnityEngine.Assertions.Assert.AreEqual(new Vector2(1f, 0f), images.Single(image => image.name == "AltitudeGauge").rectTransform.anchorMin);
        UnityEngine.Assertions.Assert.AreEqual(Vector2.one, images.Single(image => image.name == "BatteryGauge").rectTransform.anchorMin);
        var briefing = texts.Single(text => text.name == "StoryStatus");
        UnityEngine.Assertions.Assert.IsTrue(briefing.text.Contains("<size=32>") && briefing.text.Contains("DRONE RESCUE"));
        var menuButtons = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(image => image.name.StartsWith("MenuButton") && image.name.Length == 11).ToArray();
        UnityEngine.Assertions.Assert.IsTrue(menuButtons.Single(button => button.name == "MenuButton1").gameObject.activeSelf && menuButtons.Single(button => button.name == "MenuButton2").gameObject.activeSelf && menuButtons.Single(button => button.name == "MenuButton3").gameObject.activeSelf && !menuButtons.Single(button => button.name == "MenuButton4").gameObject.activeSelf);
        UnityEngine.Assertions.Assert.IsTrue(menuButtons.Single(button => button.name == "MenuButton1").color.r > 0.9f && menuButtons.Single(button => button.name == "MenuButton2").color.r < 0.2f);
        var moveMenu = typeof(MissionController).GetMethod("MoveMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var activateMenu = typeof(MissionController).GetMethod("ActivateMenuSelection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        moveMenu.Invoke(missions, new object[] { 1 });
        activateMenu.Invoke(missions, null);
        updateDisplay.Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsTrue(briefing.text.Contains("FREE FLIGHT") && texts.Single(text => text.name == "MenuButtonLabel1").text.Contains("BACK TO MENU") && !drone.IsStandby && drone.MotorsArmed && drone.Wind == Vector3.zero);
        activateMenu.Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsTrue(drone.IsStandby && !drone.MotorsArmed);
        moveMenu.Invoke(missions, new object[] { 2 });
        activateMenu.Invoke(missions, null);
        updateDisplay.Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsTrue(briefing.text.Contains("SETTINGS") && texts.Single(text => text.name == "MenuButtonLabel1").text.Contains("FLIGHT: ASSIST"));
        activateMenu.Invoke(missions, null);
        UnityEngine.Assertions.Assert.AreEqual(FlightMode.Manual, drone.flightMode);
        activateMenu.Invoke(missions, null);
        UnityEngine.Assertions.Assert.AreEqual(FlightMode.Assist, drone.flightMode);
        UnityEngine.Assertions.Assert.IsFalse(briefing.text.Contains("THREAT"));
        UnityEngine.Assertions.Assert.AreEqual("01", texts.Single(text => text.name == "BriefingIndex").text);
        CheckPreview(viewer);
        var uiMask = LayerMask.GetMask("UI");
        UnityEngine.Assertions.Assert.IsTrue((fpv.cullingMask & uiMask) != 0);
        UnityEngine.Assertions.Assert.IsTrue((viewer.cullingMask & uiMask) != 0);
        UnityEngine.Assertions.Assert.IsFalse(fpv.enabled);
        var hud = GameObject.Find("FpvHud").GetComponent<Canvas>();
        UnityEngine.Assertions.Assert.AreEqual(fpv, hud.worldCamera);
        UnityEngine.Assertions.Assert.AreEqual(viewer, GameObject.Find("MissionHud").GetComponent<Canvas>().worldCamera);
        var monitorPanel = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(transform => transform.name == "FPVMonitor").gameObject;
        UnityEngine.Assertions.Assert.IsFalse(monitorPanel.activeSelf);
        UnityEngine.Assertions.Assert.IsTrue(Mathf.Abs(monitorPanel.transform.localScale.x / monitorPanel.transform.localScale.y - 16f / 9f) < 0.01f);
        var views = Object.FindAnyObjectByType<ViewController>();
        UnityEngine.Assertions.Assert.AreEqual(ViewMode.Operator, views.CurrentMode);
        UnityEngine.Assertions.Assert.AreEqual(FlightMode.Assist, drone.flightMode);
        var comfortFrame = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(transform => transform.name == "ComfortFrame").gameObject;
        var setMode = typeof(ViewController).GetMethod("SetMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        setMode.Invoke(views, new object[] { ViewMode.Fpv });
        UnityEngine.Assertions.Assert.AreEqual(viewer, hud.worldCamera);
        UnityEngine.Assertions.Assert.IsFalse(views.missionHud.gameObject.activeSelf);
        UnityEngine.Assertions.Assert.IsFalse(comfortFrame.activeSelf);
        UnityEngine.Assertions.Assert.IsNull(GameObject.Find("FPVMonitor"));
        setMode.Invoke(views, new object[] { ViewMode.Chase });
        UnityEngine.Assertions.Assert.IsTrue(comfortFrame.activeSelf);
        setMode.Invoke(views, new object[] { ViewMode.Operator });
        updateDisplay.Invoke(missions, null);
        UnityEngine.Assertions.Assert.IsFalse(monitorPanel.activeSelf);
        UnityEngine.Assertions.Assert.AreEqual(fpv, hud.worldCamera);
        UnityEngine.Assertions.Assert.IsTrue(views.missionHud.gameObject.activeSelf);
        foreach (var root in new[] { viewer.transform, hud.transform, GameObject.Find("MissionHud").transform })
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
        const int width = 1280;
        const int height = 720;
        var target = RenderTexture.GetTemporary(width, height, 16);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        var previousTarget = viewer.targetTexture;
        try
        {
            viewer.targetTexture = target;
            viewer.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            UnityEngine.Assertions.Assert.IsTrue(image.GetPixels().Any(color => color.maxColorComponent > 0.05f), "Game preview rendered black");
            File.WriteAllBytes("/tmp/whiteout-preview.png", image.EncodeToPNG());
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
