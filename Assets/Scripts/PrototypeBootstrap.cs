using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;

public sealed class PrototypeBootstrap : MonoBehaviour
{
    private static readonly Color Cyan = new(0.1f, 0.9f, 1f);
    private static readonly Color Orange = new(1f, 0.35f, 0.08f);
    private readonly Dictionary<(Material, Color), Material> partMaterials = new();
    private readonly Dictionary<(Color, float), Material> surfaceMaterials = new();
    private readonly Dictionary<Color, Material> glowMaterials = new();
    private Transform staticScenery;

    private void Awake()
    {
        Application.targetFrameRate = 72;
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 55f;
        QualitySettings.shadows = ShadowQuality.HardOnly;
        XRSettings.eyeTextureResolutionScale = 1f;
        gameObject.AddComponent<QuestInputDriver>();

        var terrain = CreateMountainTerrain();
        var homePosition = OnTerrain(terrain, new Vector3(0f, 0f, -4f)) + Vector3.up * 0.05f;
        var groundSites = new[]
        {
            OnTerrain(terrain, new Vector3(24f, 0f, 90f)) + Vector3.up * 0.05f,
            OnTerrain(terrain, new Vector3(102f, 0f, 186f)) + Vector3.up * 0.05f,
            OnTerrain(terrain, new Vector3(-102f, 0f, 204f)) + Vector3.up * 0.05f,
            OnTerrain(terrain, new Vector3(150f, 0f, 60f)) + Vector3.up * 0.05f,
            OnTerrain(terrain, new Vector3(-156f, 0f, 120f)) + Vector3.up * 0.05f,
            OnTerrain(terrain, new Vector3(-15f, 0f, 258f)) + Vector3.up * 0.05f,
            OnTerrain(terrain, new Vector3(162f, 0f, 225f)) + Vector3.up * 0.05f,
            OnTerrain(terrain, new Vector3(-90f, 0f, 54f)) + Vector3.up * 0.05f
        };
        var camera = CreateViewer(homePosition + new Vector3(0f, 1.55f, -6f));

        CreateLighting();
        staticScenery = new GameObject("StaticScenery").transform;
        CreateAlpineEnvironment(terrain, homePosition, groundSites);
        var homePad = CreateLandingLight("HomeBase", homePosition, 2.2f, Cyan);
        var sitePositions = (Vector3[])groundSites.Clone();
        sitePositions[0] += Vector3.up * 5f;
        sitePositions[2] += Vector3.up * 4f;
        sitePositions[6] += Vector3.up * 2f;
        var siteNames = new[] { "MountainPass", "RescueCamp", "RadioRidge", "FrozenLake", "OldMine", "WeatherStation", "AvalancheShelf", "ForestShelter" };
        var siteColors = new[] { Cyan, Orange, Cyan, new Color(0.3f, 0.65f, 1f), new Color(1f, 0.7f, 0.15f), Color.green, Orange, new Color(0.7f, 0.4f, 1f) };
        var missionSites = new Transform[sitePositions.Length];
        for (var i = 0; i < missionSites.Length; i++)
            missionSites[i] = CreateLandingLight(siteNames[i], sitePositions[i], 2.8f, siteColors[i]).transform;
        // Keep each renderer's culling bounds, including FPV views and off-screen shadow casters.
        // ponytail: static batching copies geometry; use instancing if scenery grows enough to pressure Quest memory.
        StaticBatchingUtility.Combine(staticScenery.gameObject);
        var rescueTarget = CreateRescueTarget(groundSites[1] + new Vector3(2.4f, 0.9f, 1.5f));

        var drone = CreateDrone(homePosition + Vector3.up * 0.42f);
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

        CreateHud(camera, out var status, out var telemetry, out var horizon);
        var missions = gameObject.AddComponent<MissionController>();
        missions.drone = drone;
        missions.fpv = monitor;
        missions.views = views;
        missions.homePad = homePad.transform;
        missions.missionSites = missionSites;
        missions.package = package.transform;
        missions.statusText = status;
        missions.telemetryText = telemetry;
        missions.artificialHorizon = horizon;
        missions.blizzard = CreateBlizzard(camera.transform);
    }

    private static Terrain CreateMountainTerrain()
    {
        const int resolution = 257;
        var data = new TerrainData
        {
            heightmapResolution = resolution,
            alphamapResolution = 64,
            size = new Vector3(540f, 70f, 540f)
        };
        var heights = new float[resolution, resolution];
        for (var z = 0; z < resolution; z++)
        for (var x = 0; x < resolution; x++)
        {
            var worldX = x / (resolution - 1f) * 540f - 270f;
            var worldZ = z / (resolution - 1f) * 540f - 105f;
            var north = Mathf.InverseLerp(-75f, 390f, worldZ);
            var broad = Mathf.PerlinNoise(x * 0.035f + 2.1f, z * 0.035f + 7.3f);
            var fine = Mathf.PerlinNoise(x * 0.12f + 11f, z * 0.12f + 3f);
            var ridges = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.055f, z * 0.055f) * 2f - 1f);
            var height = 0.025f + north * north * 0.34f + broad * 0.08f + fine * 0.018f + ridges * north * 0.07f;
            height = Flatten(height, worldX, worldZ, 0f, -4f, 24f, 0.035f);
            height = Flatten(height, worldX, worldZ, 102f, 186f, 25f, 0.39f);
            heights[z, x] = Mathf.Clamp01(height);
        }
        data.SetHeights(0, 0, heights);

        var snow = new TerrainLayer();
        snow.diffuseTexture = NoiseTexture(new Color(0.78f, 0.83f, 0.86f), 0.08f);
        snow.tileSize = new Vector2(14f, 14f);
        var rock = new TerrainLayer();
        rock.diffuseTexture = NoiseTexture(new Color(0.22f, 0.25f, 0.27f), 0.16f);
        rock.tileSize = new Vector2(11f, 11f);
        data.terrainLayers = new[] { snow, rock };
        var blend = new float[64, 64, 2];
        for (var z = 0; z < 64; z++)
        for (var x = 0; x < 64; x++)
        {
            var steepness = data.GetSteepness(x / 63f, z / 63f);
            var exposedRock = Mathf.InverseLerp(28f, 55f, steepness);
            blend[z, x, 0] = 1f - exposedRock;
            blend[z, x, 1] = exposedRock;
        }
        data.SetAlphamaps(0, 0, blend);
        AddAlpineGrass(data);

        var terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "ProceduralSnowMountain";
        terrainObject.transform.position = new Vector3(-270f, 0f, -105f);
        var terrain = terrainObject.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.detailObjectDistance = 45f;
        terrain.heightmapPixelError = 8f;
        terrain.basemapDistance = 120f;
        return terrain;
    }

    private static float Flatten(float value, float x, float z, float centerX, float centerZ, float radius, float target)
    {
        var blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Vector2.Distance(new Vector2(x, z), new Vector2(centerX, centerZ)) / radius));
        return Mathf.Lerp(value, target, blend);
    }

    private static void AddAlpineGrass(TerrainData data)
    {
        data.SetDetailResolution(128, 16);
        data.detailPrototypes = new[]
        {
            new DetailPrototype
            {
                prototypeTexture = GrassTexture(),
                renderMode = DetailRenderMode.GrassBillboard,
                minWidth = 0.25f,
                maxWidth = 0.5f,
                minHeight = 0.35f,
                maxHeight = 0.75f,
                noiseSpread = 0.35f,
                healthyColor = new Color(0.22f, 0.31f, 0.18f),
                dryColor = new Color(0.46f, 0.42f, 0.3f)
            }
        };
        var grass = new int[128, 128];
        for (var z = 0; z < 128; z++)
        for (var x = 0; x < 128; x++)
        {
            var height = data.GetInterpolatedHeight(x / 127f, z / 127f);
            var slope = data.GetSteepness(x / 127f, z / 127f);
            grass[z, x] = height < 9f && slope < 28f && Mathf.PerlinNoise(x * 0.23f, z * 0.23f) > 0.54f ? 2 : 0;
        }
        data.SetDetailLayer(0, 0, 0, grass);
    }

    private static Texture2D GrassTexture()
    {
        var texture = new Texture2D(16, 32, TextureFormat.RGBA32, false);
        var pixels = new Color[16 * 32];
        for (var y = 0; y < 32; y++)
        for (var x = 0; x < 16; x++)
        {
            var blade = Mathf.Abs(x - 7.5f) < Mathf.Lerp(4f, 0.7f, y / 31f);
            pixels[y * 16 + x] = blade ? new Color(0.28f, 0.38f, 0.2f, 1f) : Color.clear;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D NoiseTexture(Color color, float variation)
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[64 * 64];
        var random = new System.Random(color.GetHashCode());
        for (var i = 0; i < pixels.Length; i++)
        {
            var value = ((float)random.NextDouble() - 0.5f) * variation;
            pixels[i] = new Color(color.r + value, color.g + value, color.b + value);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Vector3 OnTerrain(Terrain terrain, Vector3 position)
    {
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    private static Camera CreateViewer(Vector3 origin)
    {
        var viewer = new GameObject("QuestCamera");
        viewer.tag = "MainCamera";
        viewer.transform.position = origin;
        var camera = viewer.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.backgroundColor = new Color(0.43f, 0.5f, 0.56f);
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 720f;
        viewer.AddComponent<AudioListener>();
        var pose = viewer.AddComponent<QuestPoseDriver>();
        pose.trackingOrigin = origin;
        return camera;
    }

    private static void CreateLighting()
    {
        var lightObject = new GameObject("WinterSun");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(0.78f, 0.86f, 1f);
        light.shadows = LightShadows.Hard;
        lightObject.transform.rotation = Quaternion.Euler(28f, -38f, 0f);
        var skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            var sky = new Material(skyShader);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.65f);
            sky.SetColor("_SkyTint", new Color(0.42f, 0.55f, 0.72f));
            sky.SetColor("_GroundColor", new Color(0.24f, 0.28f, 0.32f));
            RenderSettings.skybox = sky;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.62f, 0.69f, 0.75f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.55f, 0.65f);
        RenderSettings.ambientEquatorColor = new Color(0.31f, 0.35f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.14f, 0.16f, 0.18f);
    }

    private void CreateAlpineEnvironment(Terrain terrain, Vector3 home, Vector3[] sites)
    {
        var random = new System.Random(2187);
        for (var i = 0; i < 34; i++)
        {
            var position = new Vector3((float)random.NextDouble() * 450f - 225f, 0f, (float)random.NextDouble() * 390f - 75f);
            if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(home.x, home.z)) < 12f ||
                Vector2.Distance(new Vector2(position.x, position.z), new Vector2(sites[1].x, sites[1].z)) < 12f) continue;
            position = OnTerrain(terrain, position);
            if (position.y < 14f) CreateFir(position, 0.8f + (float)random.NextDouble() * 0.8f);
        }
        for (var i = 0; i < 20; i++)
        {
            var position = OnTerrain(terrain, new Vector3((float)random.NextDouble() * 450f - 225f, 0f, (float)random.NextDouble() * 400f - 70f));
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "GraniteOutcrop";
            rock.transform.SetParent(staticScenery, false);
            rock.transform.position = position - Vector3.up * 0.2f;
            rock.transform.rotation = RandomRotation(random);
            rock.transform.localScale = new Vector3(1.4f, 0.8f, 1.1f) * (0.7f + (float)random.NextDouble() * 1.5f);
            rock.GetComponent<Renderer>().sharedMaterial = SurfaceMaterial(new Color(0.25f, 0.28f, 0.3f), 0.12f);
            Object.Destroy(rock.GetComponent<Collider>());
        }

        CreateBox("BaseHangar", home + new Vector3(-7f, 2.2f, 2f), new Vector3(8f, 4.4f, 7f), new Color(0.18f, 0.22f, 0.26f));
        CreateBox("RescueCabin", sites[1] + new Vector3(4.5f, 1.6f, 4f), new Vector3(6f, 3.2f, 5f), new Color(0.28f, 0.18f, 0.12f), Quaternion.Euler(0f, -18f, 0f));
        CreateBox("SnowRoof", sites[1] + new Vector3(4.5f, 3.3f, 4f), new Vector3(6.5f, 0.35f, 5.5f), new Color(0.86f, 0.89f, 0.91f), Quaternion.Euler(0f, -18f, 5f));
        var antenna = CreateBox("EmergencyAntenna", sites[2] + Vector3.up * 3.5f, new Vector3(0.12f, 7f, 0.12f), new Color(0.25f, 0.28f, 0.3f));
        Object.Destroy(antenna.GetComponent<Collider>());
        CreateBox("OldMineEntrance", sites[4] + new Vector3(0f, 1.7f, 2f), new Vector3(5f, 3.4f, 1f), new Color(0.08f, 0.09f, 0.1f));
        CreateBox("WeatherStation", sites[5] + new Vector3(3f, 1.3f, 2f), new Vector3(4f, 2.6f, 4f), new Color(0.72f, 0.76f, 0.78f));
        CreateBox("ForestShelter", sites[7] + new Vector3(-3f, 1.2f, 2f), new Vector3(4f, 2.4f, 3.5f), new Color(0.24f, 0.16f, 0.1f));
        var lake = CreateBox("FrozenLake", sites[3] + Vector3.down * 0.03f, new Vector3(15f, 0.08f, 12f), new Color(0.32f, 0.57f, 0.72f));
        Object.Destroy(lake.GetComponent<Collider>());
        var flare = new GameObject("EmergencyFlare");
        flare.transform.position = sites[1] + Vector3.up * 2f;
        var light = flare.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Orange;
        light.range = 18f;
        light.intensity = 5f;
    }

    private static Quaternion RandomRotation(System.Random random) => Quaternion.Euler((float)random.NextDouble() * 25f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 25f);

    private void CreateFir(Vector3 position, float scale)
    {
        var root = new GameObject("SnowFir");
        root.transform.SetParent(staticScenery, false);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * scale;
        var trunk = CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.22f, 1.4f, 0.22f), new Color(0.2f, 0.12f, 0.07f));
        for (var i = 0; i < 3; i++)
        {
            var crown = CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 2.2f + i * 0.85f, 0f), new Vector3(1.65f - i * 0.35f, 0.65f, 1.65f - i * 0.35f), new Color(0.08f, 0.19f, 0.15f));
            var snow = CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(-0.15f, 2.45f + i * 0.85f, 0f), new Vector3(1.45f - i * 0.32f, 0.22f, 1.45f - i * 0.32f), new Color(0.83f, 0.87f, 0.9f));
            Object.Destroy(crown.GetComponent<Collider>());
            Object.Destroy(snow.GetComponent<Collider>());
        }
        Object.Destroy(trunk.GetComponent<Collider>());
    }

    private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Color color, Quaternion rotation = default)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(staticScenery, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.transform.rotation = rotation == default ? Quaternion.identity : rotation;
        box.GetComponent<Renderer>().sharedMaterial = SurfaceMaterial(color, 0.06f);
        return box;
    }

    private Material SurfaceMaterial(Color color, float variation)
    {
        if (surfaceMaterials.TryGetValue((color, variation), out var shared)) return shared;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.mainTexture = NoiseTexture(color, variation);
        material.mainTextureScale = new Vector2(4f, 4f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
        surfaceMaterials.Add((color, variation), material);
        return material;
    }

    private GameObject CreateLandingLight(string name, Vector3 position, float diameter, Color color)
    {
        var zone = new GameObject(name);
        zone.transform.SetParent(staticScenery, false);
        zone.transform.position = position;
        if (!glowMaterials.TryGetValue(color, out var material))
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            glowMaterials.Add(color, material);
        }
        for (var i = 0; i < 16; i++)
        {
            var angle = i / 16f * Mathf.PI * 2f;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "LandingGlow";
            marker.transform.SetParent(zone.transform, false);
            marker.transform.localPosition = new Vector3(Mathf.Cos(angle) * diameter * 0.5f, 0.09f, Mathf.Sin(angle) * diameter * 0.5f);
            marker.transform.localScale = Vector3.one * 0.14f;
            marker.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(marker.GetComponent<Collider>());
        }
        var lightObject = new GameObject("LandingLight");
        lightObject.transform.SetParent(zone.transform, false);
        lightObject.transform.localPosition = Vector3.up * 5f;
        lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = 18f;
        light.range = 10f;
        light.spotAngle = 36f;
        light.shadows = LightShadows.None;
        return zone;
    }

    private DroneController CreateDrone(Vector3 position)
    {
        var droneObject = new GameObject("Drone");
        droneObject.transform.position = position;
        droneObject.AddComponent<Rigidbody>().mass = 0.65f;
        var controller = droneObject.AddComponent<DroneController>();
        var collider = droneObject.GetComponent<BoxCollider>();
        collider.size = new Vector3(0.82f, 0.36f, 0.82f);
        collider.center = new Vector3(0f, -0.07f, 0f);

        var carbon = new Color(0.025f, 0.04f, 0.055f);
        var metal = new Color(0.22f, 0.25f, 0.27f);
        CreateDronePart(PrimitiveType.Sphere, "CarbonBody", droneObject.transform, Vector3.zero, new Vector3(0.68f, 0.22f, 0.54f), carbon);
        CreateDronePart(PrimitiveType.Cube, "Battery", droneObject.transform, new Vector3(0f, 0.14f, -0.04f), new Vector3(0.34f, 0.12f, 0.34f), new Color(0.11f, 0.13f, 0.14f));
        CreateDronePart(PrimitiveType.Sphere, "Canopy", droneObject.transform, new Vector3(0f, 0.1f, 0.1f), new Vector3(0.4f, 0.17f, 0.34f), new Color(0.07f, 0.24f, 0.34f));
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
            for (var blade = 0; blade < 2; blade++)
                CreateDronePart(PrimitiveType.Capsule, "Propeller", rotor.transform, Vector3.zero, new Vector3(0.035f, 0.34f, 0.012f), new Color(0.08f, 0.09f, 0.1f), Quaternion.Euler(90f, blade * 90f, 0f));
            rotors.Add(rotor.transform);
        }

        CreateDronePart(PrimitiveType.Cube, "GimbalCamera", droneObject.transform, new Vector3(0f, -0.19f, 0.31f), new Vector3(0.22f, 0.16f, 0.18f), new Color(0.055f, 0.065f, 0.075f));
        CreateDronePart(PrimitiveType.Cylinder, "CameraLens", droneObject.transform, new Vector3(0f, -0.19f, 0.41f), new Vector3(0.065f, 0.04f, 0.065f), new Color(0.03f, 0.16f, 0.24f), Quaternion.Euler(90f, 0f, 0f));
        var navigationLight = new GameObject("NavigationLight").AddComponent<Light>();
        navigationLight.transform.SetParent(droneObject.transform, false);
        navigationLight.type = LightType.Point;
        navigationLight.range = 2.5f;
        navigationLight.intensity = 1.2f;
        navigationLight.color = Color.red;

        var presentation = droneObject.AddComponent<DronePresentation>();
        presentation.rotors = rotors.ToArray();
        presentation.navigationLight = navigationLight;
        return controller;
    }

    private void CreateDroneArm(Transform parent, float yaw)
    {
        CreateDronePart(PrimitiveType.Cube, "CarbonArm", parent, Vector3.zero, new Vector3(1.15f, 0.055f, 0.07f), new Color(0.04f, 0.055f, 0.065f), Quaternion.Euler(0f, yaw, 0f));
    }

    private void CreateDronePart(PrimitiveType primitive, string name, Transform parent, Vector3 position, Vector3 scale, Color color, Quaternion rotation = default)
    {
        var part = CreatePart(primitive, parent, position, scale, color, rotation);
        part.name = name;
        Object.Destroy(part.GetComponent<Collider>());
    }

    private GameObject CreatePart(PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Color color, Quaternion rotation = default)
    {
        var part = GameObject.CreatePrimitive(primitive);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<Renderer>();
        var key = (renderer.sharedMaterial, color);
        if (!partMaterials.TryGetValue(key, out var material))
        {
            material = new Material(renderer.sharedMaterial) { color = color };
            partMaterials.Add(key, material);
        }
        renderer.sharedMaterial = material;
        return part;
    }

    private GameObject CreatePackage(Transform parent)
    {
        var package = CreatePart(PrimitiveType.Cube, parent, new Vector3(0f, -0.28f, 0f), new Vector3(0.28f, 0.2f, 0.28f), new Color(0.92f, 0.94f, 0.9f));
        package.name = "EmergencyMedicalPackage";
        Object.Destroy(package.GetComponent<Collider>());
        return package;
    }

    private GameObject CreateRescueTarget(Vector3 position)
    {
        var person = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        person.name = "RescueLeader";
        person.transform.position = position;
        person.transform.localScale = new Vector3(0.42f, 0.72f, 0.42f);
        person.GetComponent<Renderer>().material.color = Orange;
        var vest = CreatePart(PrimitiveType.Cube, person.transform, new Vector3(0f, 0.25f, 0.4f), new Vector3(0.8f, 0.45f, 0.08f), new Color(1f, 0.9f, 0.12f));
        Object.Destroy(vest.GetComponent<Collider>());
        return person;
    }

    private static ParticleSystem CreateBlizzard(Transform viewer)
    {
        var stormObject = new GameObject("ProceduralBlizzard");
        stormObject.transform.SetParent(viewer, false);
        stormObject.transform.localPosition = new Vector3(0f, 8f, 14f);
        var storm = stormObject.AddComponent<ParticleSystem>();
        var main = storm.main;
        main.maxParticles = 900;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
        main.startColor = new Color(0.9f, 0.94f, 1f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = storm.emission;
        emission.rateOverTime = 75f;
        var shape = storm.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(45f, 18f, 45f);
        shape.rotation = new Vector3(18f, 90f, 0f);
        var velocity = storm.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-9f, -6f);
        velocity.y = new ParticleSystem.MinMaxCurve(-5f, -2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = storm.noise;
        noise.enabled = true;
        noise.strength = 1.5f;
        noise.frequency = 0.35f;
        var renderer = storm.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5f;
        renderer.velocityScale = 0.12f;
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
        return storm;
    }

    private static void CreateHud(Camera camera, out Text status, out Text telemetry, out RectTransform horizon)
    {
        var canvasObject = new GameObject("FpvHud");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 0.45f;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var statusPanel = UiImage(canvas.transform, "OperationsMenu", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -38f), new Vector2(700f, 350f), new Color(0.015f, 0.025f, 0.04f, 0.8f), new Vector2(0f, 1f));
        status = UiText(statusPanel.transform, "StoryStatus", 31, Color.white, TextAnchor.UpperLeft);
        status.rectTransform.offsetMin = new Vector2(26f, 22f);
        status.rectTransform.offsetMax = new Vector2(-26f, -22f);
        var accent = UiImage(statusPanel.transform, "Accent", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(7f, 0f), Cyan, new Vector2(0f, 0.5f));
        accent.rectTransform.sizeDelta = new Vector2(7f, 0f);

        var telemetryPanel = UiImage(canvas.transform, "TelemetryBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1390f, 100f), new Color(0.01f, 0.02f, 0.03f, 0.78f), new Vector2(0.5f, 0f));
        telemetry = UiText(telemetryPanel.transform, "Telemetry", 26, Cyan, TextAnchor.MiddleCenter);
        telemetry.rectTransform.offsetMin = new Vector2(18f, 8f);
        telemetry.rectTransform.offsetMax = new Vector2(-18f, -8f);

        var horizonObject = new GameObject("ArtificialHorizon");
        horizonObject.transform.SetParent(canvas.transform, false);
        horizon = horizonObject.AddComponent<RectTransform>();
        horizon.anchorMin = horizon.anchorMax = new Vector2(0.5f, 0.5f);
        horizon.sizeDelta = new Vector2(440f, 220f);
        Line(horizon, new Vector2(-125f, 0f), new Vector2(95f, 3f), Cyan);
        Line(horizon, new Vector2(125f, 0f), new Vector2(95f, 3f), Cyan);
        Line(horizon, new Vector2(0f, 0f), new Vector2(35f, 3f), Orange);
        Line(canvas.transform, new Vector2(0f, 0f), new Vector2(4f, 42f), Orange);
        Line(canvas.transform, new Vector2(0f, 0f), new Vector2(42f, 4f), Orange);
        Line(canvas.transform, new Vector2(-290f, 0f), new Vector2(70f, 3f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
        Line(canvas.transform, new Vector2(290f, 0f), new Vector2(70f, 3f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
        foreach (var child in canvasObject.GetComponentsInChildren<Transform>())
            child.gameObject.layer = LayerMask.NameToLayer("UI");
    }

    private static Image UiImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color, Vector2 pivot)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = anchorMin;
        image.rectTransform.anchorMax = anchorMax;
        image.rectTransform.pivot = pivot;
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    private static Text UiText(Transform parent, string name, int size, Color color, TextAnchor alignment)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<Text>();
        text.font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK JP", "Noto Sans JP", "Arial" }, size) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        return text;
    }

    private static void Line(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        UiImage(parent, "HudLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, color, new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        foreach (var material in partMaterials.Values) Object.Destroy(material);
        foreach (var material in glowMaterials.Values) Object.Destroy(material);
        foreach (var material in surfaceMaterials.Values)
        {
            Object.Destroy(material.mainTexture);
            Object.Destroy(material);
        }
    }
}
