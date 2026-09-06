using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;

public sealed class PrototypeBootstrap : MonoBehaviour
{
    private static readonly Color GroundColor = new(0.18f, 0.24f, 0.20f);
    private static readonly Color PadColor = new(0.1f, 0.55f, 0.9f);

    private void Awake()
    {
        Application.targetFrameRate = 72;
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 40f;
        QualitySettings.shadows = ShadowQuality.HardOnly;
        XRSettings.eyeTextureResolutionScale = 1f;
        gameObject.AddComponent<QuestInputDriver>();

        var camera = CreateViewer();
        CreateLighting();
        CreateGround();
        CreateEnvironment();
        var homePad = CreateLandingLight("HomeLight", new Vector3(0f, 0.05f, -2f), 1.4f, PadColor);
        var deliveryZone = CreateLandingLight("DeliveryLight", new Vector3(0f, 0.05f, 14f), 1.8f, new Color(0.95f, 0.65f, 0.12f));
        var rescueTarget = CreateRescueTarget(new Vector3(18f, 0.9f, 10f));
        CreateRescueTarget(new Vector3(-16f, 0.9f, 11f), false);
        CreateRescueTarget(new Vector3(12f, 0.9f, 24f), false);
        var inspectionPoints = CreateInspectionTower(new Vector3(-18f, 0f, 18f));

        var drone = CreateDrone(homePad.transform.position + Vector3.up * 0.42f);
        var package = CreatePackage(drone.transform);
        var monitor = gameObject.AddComponent<FpvMonitor>();
        monitor.drone = drone;
        monitor.thermalTargets = new[] { rescueTarget.GetComponent<Renderer>() };
        monitor.Build(camera);
        var views = gameObject.AddComponent<ViewController>();
        views.drone = drone;
        views.pose = camera.GetComponent<QuestPoseDriver>();
        views.monitor = monitor;
        views.Build(camera);

        var missions = gameObject.AddComponent<MissionController>();
        missions.drone = drone;
        missions.fpv = monitor;
        missions.views = views;
        missions.homePad = homePad.transform;
        missions.deliveryZone = deliveryZone.transform;
        missions.rescueTarget = rescueTarget.transform;
        missions.inspectionPoints = inspectionPoints;
        missions.package = package.transform;
        missions.statusText = CreateStatusText(camera);
    }

    private static Camera CreateViewer()
    {
        var viewer = new GameObject("QuestCamera");
        viewer.tag = "MainCamera";
        viewer.transform.position = new Vector3(0f, 1.6f, -8f);
        var camera = viewer.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.42f, 0.67f, 0.86f);
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 250f;
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        viewer.AddComponent<AudioListener>();
        var pose = viewer.AddComponent<QuestPoseDriver>();
        pose.trackingOrigin = viewer.transform.position;
        return camera;
    }

    private static void CreateLighting()
    {
        var lightObject = new GameObject("Sun");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = new Color(1f, 0.93f, 0.78f);
        light.shadows = LightShadows.Hard;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.48f, 0.65f, 0.75f);
        RenderSettings.fogStartDistance = 55f;
        RenderSettings.fogEndDistance = 170f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.58f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.38f, 0.34f);
        RenderSettings.ambientGroundColor = new Color(0.13f, 0.16f, 0.13f);
    }

    private static void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "TrainingGround";
        ground.transform.position = new Vector3(0f, -0.15f, 10f);
        ground.transform.localScale = new Vector3(70f, 0.3f, 70f);
        ground.GetComponent<Renderer>().material = CreateSurfaceMaterial(GroundColor, 0.08f, new Vector2(18f, 18f));
    }

    private static void CreateEnvironment()
    {
        // ponytail: procedural low-poly assets keep the prototype self-contained; replace with authored LOD prefabs after the art direction is locked.
        var road = CreateBox("Road", new Vector3(0f, 0.02f, 14f), new Vector3(7f, 0.08f, 65f), Color.white);
        road.GetComponent<Renderer>().material = CreateSurfaceMaterial(new Color(0.12f, 0.14f, 0.16f), 0.045f, new Vector2(3f, 22f));
        CreateBox("Warehouse", new Vector3(-14f, 3f, 7f), new Vector3(12f, 6f, 10f), new Color(0.55f, 0.59f, 0.62f));
        CreateBox("WarehouseRoof", new Vector3(-14f, 6.15f, 7f), new Vector3(12.8f, 0.3f, 10.8f), new Color(0.17f, 0.21f, 0.24f));
        CreateBox("Clinic", new Vector3(13f, 2.3f, 18f), new Vector3(9f, 4.6f, 8f), new Color(0.83f, 0.78f, 0.67f));
        CreateBox("ClinicRoof", new Vector3(13f, 4.72f, 18f), new Vector3(9.6f, 0.25f, 8.6f), new Color(0.18f, 0.28f, 0.34f));
        for (var i = 0; i < 3; i++)
        {
            CreateDecoration("WarehouseDoor", new Vector3(-17f + i * 3f, 2.1f, 1.94f), new Vector3(2.2f, 3.8f, 0.08f), new Color(0.18f, 0.23f, 0.27f));
            CreateDecoration("ClinicWindow", new Vector3(10f + i * 3f, 2.7f, 13.96f), new Vector3(1.7f, 1.35f, 0.08f), new Color(0.12f, 0.38f, 0.55f));
        }
        CreateDecoration("ClinicCross", new Vector3(13f, 3.1f, 13.9f), new Vector3(0.45f, 2.2f, 0.12f), new Color(0.85f, 0.08f, 0.08f));
        CreateDecoration("ClinicCross", new Vector3(13f, 3.1f, 13.84f), new Vector3(1.5f, 0.45f, 0.12f), new Color(0.85f, 0.08f, 0.08f));
        CreateBox("RescueTent", new Vector3(18f, 1.1f, 7f), new Vector3(4f, 2.2f, 4f), new Color(0.92f, 0.38f, 0.12f), Quaternion.Euler(0f, 18f, 0f));
        CreateBox("River", new Vector3(27f, -0.02f, 16f), new Vector3(10f, 0.12f, 70f), new Color(0.08f, 0.42f, 0.62f));

        for (var z = -12; z < 46; z += 7)
            CreateDecoration("RoadMarking", new Vector3(0f, 0.08f, z), new Vector3(0.18f, 0.03f, 3.5f), new Color(0.88f, 0.82f, 0.54f));

        for (var i = 0; i < 12; i++)
        {
            var side = i % 2 == 0 ? -1f : 1f;
            CreateTree(new Vector3(side * (25f + i % 3 * 3f), 0f, -10f + i * 5f), 1f + i % 3 * 0.15f);
        }

        for (var i = 0; i < 8; i++)
        {
            var angle = i / 8f * Mathf.PI * 2f;
            var position = new Vector3(Mathf.Cos(angle) * 85f, 8f, 15f + Mathf.Sin(angle) * 85f);
            var mountain = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mountain.name = "Mountain";
            mountain.transform.position = position;
            mountain.transform.localScale = new Vector3(36f, 28f + i % 3 * 6f, 36f);
            mountain.GetComponent<Renderer>().material.color = new Color(0.22f + i % 2 * 0.025f, 0.3f, 0.27f);
            Object.Destroy(mountain.GetComponent<Collider>());
        }

        CreateStreetLight(new Vector3(-4f, 0f, 4f));
        CreateStreetLight(new Vector3(4f, 0f, 16f));
        CreateStreetLight(new Vector3(-4f, 0f, 28f));
    }

    private static void CreateTree(Vector3 position, float scale)
    {
        var root = new GameObject("Tree");
        root.transform.position = position;
        root.transform.localScale = Vector3.one * scale;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        trunk.transform.localScale = new Vector3(0.32f, 1.5f, 0.32f);
        trunk.GetComponent<Renderer>().material.color = new Color(0.28f, 0.16f, 0.08f);
        for (var i = 0; i < 3; i++)
        {
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3((i - 1) * 0.7f, 3.5f + (i % 2) * 0.55f, (i % 2) * 0.4f);
            crown.transform.localScale = new Vector3(1.9f, 2.5f, 1.9f);
            crown.GetComponent<Renderer>().material.color = new Color(0.1f + i * 0.015f, 0.36f + i * 0.035f, 0.15f);
            Object.Destroy(crown.GetComponent<Collider>());
        }
    }

    private static void CreateStreetLight(Vector3 position)
    {
        var pole = CreateBox("StreetLight", position + Vector3.up * 2.5f, new Vector3(0.12f, 5f, 0.12f), new Color(0.12f, 0.14f, 0.15f));
        var lamp = CreateBox("Lamp", position + new Vector3(0.35f, 4.9f, 0f), new Vector3(0.8f, 0.18f, 0.35f), new Color(1f, 0.85f, 0.45f));
        Object.Destroy(pole.GetComponent<Collider>());
        Object.Destroy(lamp.GetComponent<Collider>());
    }

    private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Color color, Quaternion rotation = default)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.localScale = scale;
        box.transform.rotation = rotation == default ? Quaternion.identity : rotation;
        box.GetComponent<Renderer>().material.color = color;
        return box;
    }

    private static void CreateDecoration(string name, Vector3 position, Vector3 scale, Color color)
    {
        var decoration = CreateBox(name, position, scale, color);
        Object.Destroy(decoration.GetComponent<Collider>());
    }

    private static Material CreateSurfaceMaterial(Color color, float variation, Vector2 tiling)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader);
        var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
        var pixels = new Color[64 * 64];
        var random = new System.Random(color.GetHashCode());
        for (var i = 0; i < pixels.Length; i++)
        {
            var value = ((float)random.NextDouble() - 0.5f) * variation;
            pixels[i] = new Color(Mathf.Clamp01(color.r + value), Mathf.Clamp01(color.g + value), Mathf.Clamp01(color.b + value));
        }
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(false, true);
        material.mainTexture = texture;
        material.mainTextureScale = tiling;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        return material;
    }

    private static Transform[] CreateInspectionTower(Vector3 position)
    {
        CreateBox("InspectionTower", position + Vector3.up * 5f, new Vector3(5f, 10f, 5f), new Color(0.36f, 0.39f, 0.42f));
        var points = new Transform[3];
        var offsets = new[] { new Vector3(2.7f, 8f, 0f), new Vector3(-2.7f, 5f, 0f), new Vector3(0f, 2f, 2.7f) };
        for (var i = 0; i < points.Length; i++)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"InspectionPoint{i + 1}";
            marker.transform.position = position + offsets[i];
            marker.transform.localScale = Vector3.one * 0.75f;
            marker.GetComponent<Renderer>().material.color = new Color(1f, 0.75f, 0.12f);
            Object.Destroy(marker.GetComponent<Collider>());
            points[i] = marker.transform;
        }
        return points;
    }

    private static GameObject CreateLandingLight(string name, Vector3 position, float diameter, Color color)
    {
        var zone = new GameObject(name);
        zone.transform.position = position;
        var glowMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        glowMaterial.color = color;
        if (glowMaterial.HasProperty("_BaseColor")) glowMaterial.SetColor("_BaseColor", color);
        for (var i = 0; i < 16; i++)
        {
            var angle = i / 16f * Mathf.PI * 2f;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "LandingGlow";
            marker.transform.SetParent(zone.transform, false);
            marker.transform.localPosition = new Vector3(Mathf.Cos(angle) * diameter * 0.5f, 0.09f, Mathf.Sin(angle) * diameter * 0.5f);
            marker.transform.localScale = Vector3.one * 0.14f;
            marker.GetComponent<Renderer>().sharedMaterial = glowMaterial;
            Object.Destroy(marker.GetComponent<Collider>());
        }
        var lightObject = new GameObject("LandingLight");
        lightObject.transform.SetParent(zone.transform, false);
        lightObject.transform.localPosition = Vector3.up * 5f;
        lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = 20f;
        light.range = 8f;
        light.spotAngle = Mathf.Atan(diameter * 0.1f) * Mathf.Rad2Deg * 2f;
        light.innerSpotAngle = light.spotAngle * 0.65f;
        light.shadows = LightShadows.None;
        return zone;
    }

    private static DroneController CreateDrone(Vector3 position)
    {
        var droneObject = new GameObject("Drone");
        droneObject.name = "Drone";
        droneObject.transform.position = position;
        droneObject.AddComponent<Rigidbody>().mass = 0.65f;
        var controller = droneObject.AddComponent<DroneController>();
        var collider = droneObject.GetComponent<BoxCollider>();
        collider.size = new Vector3(0.82f, 0.36f, 0.82f);
        collider.center = new Vector3(0f, -0.07f, 0f);

        var carbon = new Color(0.025f, 0.04f, 0.055f);
        var metal = new Color(0.22f, 0.25f, 0.27f);
        CreateDronePart(PrimitiveType.Sphere, "CarbonBody", droneObject.transform, Vector3.zero, new Vector3(0.68f, 0.22f, 0.54f), carbon);
        CreateDronePart(PrimitiveType.Cube, "LowerChassis", droneObject.transform, new Vector3(0f, -0.07f, 0f), new Vector3(0.5f, 0.1f, 0.4f), new Color(0.06f, 0.075f, 0.085f));
        CreateDronePart(PrimitiveType.Cube, "Battery", droneObject.transform, new Vector3(0f, 0.14f, -0.04f), new Vector3(0.34f, 0.12f, 0.34f), new Color(0.11f, 0.13f, 0.14f));
        CreateDronePart(PrimitiveType.Sphere, "Canopy", droneObject.transform, new Vector3(0f, 0.1f, 0.1f), new Vector3(0.4f, 0.17f, 0.34f), new Color(0.07f, 0.24f, 0.34f));
        for (var x = -1; x <= 1; x++)
            CreateDronePart(PrimitiveType.Cube, "CoolingVent", droneObject.transform, new Vector3(x * 0.09f, 0.205f, -0.04f), new Vector3(0.045f, 0.015f, 0.22f), metal);
        CreateDroneArm(droneObject.transform, 45f);
        CreateDroneArm(droneObject.transform, -45f);

        var rotors = new List<Transform>();
        for (var x = -1; x <= 1; x += 2)
        for (var z = -1; z <= 1; z += 2)
        {
            var rotorPosition = new Vector3(x * 0.43f, 0.08f, z * 0.43f);
            CreateDronePart(PrimitiveType.Cylinder, "BrushlessMotor", droneObject.transform, rotorPosition, new Vector3(0.12f, 0.07f, 0.12f), metal);
            var rotor = new GameObject("Rotor");
            rotor.transform.SetParent(droneObject.transform, false);
            rotor.transform.localPosition = rotorPosition + Vector3.up * 0.09f;
            for (var bladeIndex = 0; bladeIndex < 2; bladeIndex++)
                CreateDronePart(PrimitiveType.Capsule, "Propeller", rotor.transform, Vector3.zero, new Vector3(0.035f, 0.34f, 0.012f), new Color(0.08f, 0.09f, 0.1f), Quaternion.Euler(90f, bladeIndex * 90f, 0f));
            CreateDronePart(PrimitiveType.Cylinder, "PropellerHub", rotor.transform, Vector3.zero, new Vector3(0.075f, 0.04f, 0.075f), metal);
            rotors.Add(rotor.transform);
        }

        for (var x = -1; x <= 1; x += 2)
        {
            CreateDronePart(PrimitiveType.Capsule, "LandingSkid", droneObject.transform, new Vector3(x * 0.28f, -0.23f, 0f), new Vector3(0.026f, 0.34f, 0.026f), carbon, Quaternion.Euler(90f, 0f, 0f));
            for (var z = -1; z <= 1; z += 2)
                CreateDronePart(PrimitiveType.Cube, "LandingStrut", droneObject.transform, new Vector3(x * 0.28f, -0.14f, z * 0.2f), new Vector3(0.035f, 0.2f, 0.035f), metal);
        }

        CreateDronePart(PrimitiveType.Cylinder, "GimbalYawMotor", droneObject.transform, new Vector3(0f, -0.13f, 0.16f), new Vector3(0.09f, 0.045f, 0.09f), metal);
        CreateDronePart(PrimitiveType.Cube, "GimbalCamera", droneObject.transform, new Vector3(0f, -0.19f, 0.31f), new Vector3(0.22f, 0.16f, 0.18f), new Color(0.055f, 0.065f, 0.075f));
        CreateDronePart(PrimitiveType.Cylinder, "CameraLens", droneObject.transform, new Vector3(0f, -0.19f, 0.41f), new Vector3(0.065f, 0.04f, 0.065f), new Color(0.03f, 0.16f, 0.24f), Quaternion.Euler(90f, 0f, 0f));
        CreateDronePart(PrimitiveType.Capsule, "Antenna", droneObject.transform, new Vector3(-0.12f, 0.1f, -0.32f), new Vector3(0.015f, 0.11f, 0.015f), metal, Quaternion.Euler(-25f, 0f, 0f));
        CreateDronePart(PrimitiveType.Capsule, "Antenna", droneObject.transform, new Vector3(0.12f, 0.1f, -0.32f), new Vector3(0.015f, 0.11f, 0.015f), metal, Quaternion.Euler(-25f, 0f, 0f));
        CreateDronePart(PrimitiveType.Sphere, "FrontLED", droneObject.transform, new Vector3(-0.22f, 0.03f, 0.28f), Vector3.one * 0.045f, Color.green);
        CreateDronePart(PrimitiveType.Sphere, "FrontLED", droneObject.transform, new Vector3(0.22f, 0.03f, 0.28f), Vector3.one * 0.045f, Color.red);

        var lightObject = new GameObject("NavigationLight");
        lightObject.transform.SetParent(droneObject.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.08f, -0.3f);
        var navigationLight = lightObject.AddComponent<Light>();
        navigationLight.type = LightType.Point;
        navigationLight.range = 2.5f;
        navigationLight.intensity = 1.2f;
        navigationLight.color = Color.red;

        var presentation = droneObject.AddComponent<DronePresentation>();
        presentation.rotors = rotors.ToArray();
        presentation.navigationLight = navigationLight;
        return controller;
    }

    private static void CreateDroneArm(Transform parent, float yaw)
    {
        CreateDronePart(PrimitiveType.Cube, "CarbonArm", parent, Vector3.zero, new Vector3(1.15f, 0.055f, 0.07f), new Color(0.04f, 0.055f, 0.065f), Quaternion.Euler(0f, yaw, 0f));
    }

    private static void CreateDronePart(PrimitiveType primitive, string name, Transform parent, Vector3 position, Vector3 scale, Color color, Quaternion rotation = default)
    {
        var part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material.color = color;
        Object.Destroy(part.GetComponent<Collider>());
    }

    private static GameObject CreatePackage(Transform parent)
    {
        var package = GameObject.CreatePrimitive(PrimitiveType.Cube);
        package.name = "MedicalPackage";
        package.transform.SetParent(parent, false);
        package.transform.localPosition = new Vector3(0f, -0.28f, 0f);
        package.transform.localScale = new Vector3(0.25f, 0.18f, 0.25f);
        package.GetComponent<Renderer>().material.color = new Color(0.95f, 0.95f, 0.8f);
        Object.Destroy(package.GetComponent<Collider>());
        return package;
    }

    private static GameObject CreateRescueTarget(Vector3 position, bool target = true)
    {
        var person = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        person.name = target ? "RescueTarget" : "RescueDecoy";
        person.transform.position = position;
        person.transform.localScale = new Vector3(0.42f, 0.72f, 0.42f);
        person.GetComponent<Renderer>().material.color = target ? new Color(0.95f, 0.42f, 0.08f) : new Color(0.3f, 0.34f, 0.38f);
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(person.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        head.transform.localScale = Vector3.one * 0.72f;
        head.GetComponent<Renderer>().material.color = new Color(0.72f, 0.48f, 0.31f);
        Object.Destroy(head.GetComponent<Collider>());
        var vest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vest.name = "ReflectiveVest";
        vest.transform.SetParent(person.transform, false);
        vest.transform.localPosition = new Vector3(0f, 0.25f, 0.4f);
        vest.transform.localScale = new Vector3(0.8f, 0.45f, 0.08f);
        vest.GetComponent<Renderer>().material.color = target ? new Color(1f, 0.9f, 0.12f) : new Color(0.15f, 0.2f, 0.24f);
        Object.Destroy(vest.GetComponent<Collider>());
        return person;
    }

    private static Text CreateStatusText(Camera camera)
    {
        var canvasObject = new GameObject("StatusCanvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 0.5f;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var backgroundObject = new GameObject("Panel");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        var background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.04f, 0.82f);
        background.rectTransform.anchorMin = new Vector2(0f, 1f);
        background.rectTransform.anchorMax = new Vector2(0f, 1f);
        background.rectTransform.pivot = new Vector2(0f, 1f);
        background.rectTransform.anchoredPosition = new Vector2(32f, -32f);
        background.rectTransform.sizeDelta = new Vector2(720f, 430f);
        var textObject = new GameObject("StatusText");
        textObject.transform.SetParent(backgroundObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK JP", "Noto Sans JP", "Arial" }, 30)
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 32;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(24f, 24f);
        text.rectTransform.offsetMax = new Vector2(-24f, -24f);
        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }
}
