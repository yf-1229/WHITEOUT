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
    private readonly List<Mesh> generatedMeshes = new();
    private Transform staticScenery;
    private Mesh firMesh;

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
        var camera = CreateViewer(OnTerrain(terrain, homePosition + new Vector3(0f, 0f, -6f)) + Vector3.up * 1.55f);

        var sun = CreateLighting();
        staticScenery = new GameObject("StaticScenery").transform;
        CreateAlpineEnvironment(terrain, homePosition, groundSites);
        CreateDistantMountains();
        CreateRescueTrail(terrain, homePosition, groundSites[1]);
        var homePad = CreateLandingLight("HomeBase", homePosition, 2.2f, Cyan, 0f, TerrainNormal(terrain, homePosition));
        var sitePositions = (Vector3[])groundSites.Clone();
        sitePositions[0] += Vector3.up * 5f;
        sitePositions[2] += Vector3.up * 4f;
        sitePositions[6] += Vector3.up * 2f;
        var siteNames = new[] { "MountainPass", "RescueCamp", "RadioRidge", "FrozenLake", "OldMine", "WeatherStation", "AvalancheShelf", "ForestShelter" };
        var siteColors = new[] { Cyan, Orange, Cyan, new Color(0.3f, 0.65f, 1f), new Color(1f, 0.7f, 0.15f), Color.green, Orange, new Color(0.7f, 0.4f, 1f) };
        var missionSites = new Transform[sitePositions.Length];
        for (var i = 0; i < missionSites.Length; i++)
            missionSites[i] = CreateLandingLight(siteNames[i], sitePositions[i], 2.8f, siteColors[i], groundSites[i].y - sitePositions[i].y, TerrainNormal(terrain, groundSites[i])).transform;
        // Keep each renderer's culling bounds, including FPV views and off-screen shadow casters.
        // ponytail: static batching copies geometry; use instancing if scenery grows enough to pressure Quest memory.
        StaticBatchingUtility.Combine(staticScenery.gameObject);
        var rescueTarget = CreateRescueTarget(groundSites[1] + new Vector3(2.4f, 0f, 1.5f));
        var thermalTargets = new Renderer[groundSites.Length];
        for (var i = 0; i < thermalTargets.Length; i++)
            thermalTargets[i] = CreateThermalTarget(groundSites[i] + Vector3.up * 0.7f);

        var drone = CreateDrone(homePosition + Vector3.up * 0.42f);
        var package = CreatePackage(drone.transform);
        var fieldMarker = CreateFieldMarker();
        var monitor = gameObject.AddComponent<FpvMonitor>();
        monitor.drone = drone;
        monitor.thermalTargets = thermalTargets;
        monitor.Build(camera);
        var views = gameObject.AddComponent<ViewController>();
        views.drone = drone;
        views.pose = camera.GetComponent<QuestPoseDriver>();
        views.monitor = monitor;
        CreateHud(camera, out var status, out var telemetry, out var radio, out var horizon, out var menuButtons, out var menuButtonTexts);
        views.hud = telemetry.canvas;
        views.missionHud = status.canvas;
        views.Build(camera);
        var missions = gameObject.AddComponent<MissionController>();
        missions.drone = drone;
        missions.fpv = monitor;
        missions.views = views;
        missions.homePad = homePad.transform;
        missions.missionSites = missionSites;
        missions.package = package.transform;
        missions.fieldMarker = fieldMarker.transform;
        missions.rescueTarget = rescueTarget.transform;
        missions.recoveryWindows = new[]
        {
            GameObject.Find("CabinWindow").GetComponent<Renderer>(),
            GameObject.Find("ShelterWindow").GetComponent<Renderer>()
        };
        missions.statusText = status;
        missions.telemetryText = telemetry;
        missions.radioText = radio;
        missions.menuButtons = menuButtons;
        missions.menuButtonTexts = menuButtonTexts;
        missions.artificialHorizon = horizon;
        missions.blizzard = CreateBlizzard(monitor.Gimbal);
        missions.sun = sun;
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
            var ridges = Mathf.Pow(1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.055f, z * 0.055f) * 2f - 1f), 2f);
            var summits = Mathf.Max(AlpinePeak(worldX, worldZ, -170f, 355f, 105f),
                Mathf.Max(AlpinePeak(worldX, worldZ, 5f, 370f, 82f), AlpinePeak(worldX, worldZ, 175f, 345f, 110f)));
            var height = 0.025f + north * north * 0.42f + broad * 0.09f + fine * 0.018f + ridges * north * 0.11f + summits * north * 0.22f;
            height = Flatten(height, worldX, worldZ, 0f, -4f, 24f, 0.035f);
            height = Flatten(height, worldX, worldZ, 102f, 186f, 25f, 0.39f);
            heights[z, x] = Mathf.Clamp01(height);
        }
        data.SetHeights(0, 0, heights);

        var snow = new TerrainLayer();
        snow.diffuseTexture = NoiseTexture(new Color(0.78f, 0.83f, 0.86f), 0.08f);
        snow.tileSize = new Vector2(14f, 14f);
        snow.smoothness = 0.18f;
        var windSnow = new TerrainLayer();
        windSnow.diffuseTexture = NoiseTexture(new Color(0.58f, 0.72f, 0.82f), 0.045f);
        windSnow.tileSize = new Vector2(22f, 10f);
        windSnow.smoothness = 0.3f;
        var rock = new TerrainLayer();
        rock.diffuseTexture = NoiseTexture(new Color(0.22f, 0.25f, 0.27f), 0.16f);
        rock.tileSize = new Vector2(11f, 11f);
        rock.smoothness = 0.04f;
        data.terrainLayers = new[] { snow, windSnow, rock };
        var blend = new float[64, 64, 3];
        for (var z = 0; z < 64; z++)
        for (var x = 0; x < 64; x++)
        {
            var steepness = data.GetSteepness(x / 63f, z / 63f);
            var exposedRock = Mathf.InverseLerp(28f, 55f, steepness);
            var windPack = Mathf.PerlinNoise(x * 0.1f + 4f, z * 0.025f + 8f) * 0.32f * (1f - exposedRock);
            blend[z, x, 0] = 1f - exposedRock - windPack;
            blend[z, x, 1] = windPack;
            blend[z, x, 2] = exposedRock;
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

    private static float AlpinePeak(float x, float z, float centerX, float centerZ, float width)
        => Mathf.Exp(-((x - centerX) * (x - centerX) + (z - centerZ) * (z - centerZ)) / (width * width));

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
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D NoiseTexture(Color color, float variation)
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[64 * 64];
        var random = new System.Random(color.GetHashCode());
        var offsetX = (float)random.NextDouble() * 100f;
        var offsetY = (float)random.NextDouble() * 100f;
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++)
        {
            var broad = Mathf.PerlinNoise(offsetX + x * 0.08f, offsetY + y * 0.08f) - 0.5f;
            var fine = Mathf.PerlinNoise(offsetX + x * 0.28f, offsetY + y * 0.28f) - 0.5f;
            var value = (broad * 0.75f + fine * 0.25f) * variation * 2f;
            pixels[y * 64 + x] = new Color(color.r + value, color.g + value, color.b + value);
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private static Vector3 OnTerrain(Terrain terrain, Vector3 position)
    {
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    private static Vector3 TerrainNormal(Terrain terrain, Vector3 position)
    {
        var local = position - terrain.transform.position;
        return terrain.terrainData.GetInterpolatedNormal(local.x / terrain.terrainData.size.x, local.z / terrain.terrainData.size.z);
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

    private static Light CreateLighting()
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
        return light;
    }

    private void CreateAlpineEnvironment(Terrain terrain, Vector3 home, Vector3[] sites)
    {
        var random = new System.Random(2187);
        for (var i = 0; i < 60; i++)
        {
            var position = new Vector3((float)random.NextDouble() * 450f - 225f, 0f, (float)random.NextDouble() * 390f - 75f);
            if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(home.x, home.z)) < 12f ||
                Vector2.Distance(new Vector2(position.x, position.z), new Vector2(sites[1].x, sites[1].z)) < 12f) continue;
            position = OnTerrain(terrain, position);
            if (position.y < 18f) CreateFir(position, 0.8f + (float)random.NextDouble() * 0.8f);
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

        var hangar = home + new Vector3(-10f, 2.2f, -6f);
        CreateBox("BaseHangar", hangar, new Vector3(8f, 4.4f, 7f), new Color(0.18f, 0.22f, 0.26f));
        CreateBox("HangarDoor", hangar + new Vector3(4.04f, -0.5f, 0f), new Vector3(0.08f, 3.2f, 5.4f), new Color(0.055f, 0.065f, 0.075f));

        var cabinRotation = Quaternion.Euler(0f, -18f, 0f);
        var cabin = sites[1] + new Vector3(4.5f, 1.6f, 4f);
        CreateBox("RescueCabin", cabin, new Vector3(6f, 3.2f, 5f), new Color(0.28f, 0.18f, 0.12f), cabinRotation);
        CreateBox("SnowRoof", cabin + cabinRotation * new Vector3(-1.4f, 1.9f, 0f), new Vector3(3.5f, 0.3f, 5.6f), new Color(0.86f, 0.89f, 0.91f), cabinRotation * Quaternion.Euler(0f, 0f, 25f));
        CreateBox("SnowRoof", cabin + cabinRotation * new Vector3(1.4f, 1.9f, 0f), new Vector3(3.5f, 0.3f, 5.6f), new Color(0.86f, 0.89f, 0.91f), cabinRotation * Quaternion.Euler(0f, 0f, -25f));
        var cabinWindow = CreateBox("CabinWindow", cabin + cabinRotation * new Vector3(-1.25f, 0.2f, -2.53f), new Vector3(1.35f, 0.8f, 0.08f), new Color(1f, 0.72f, 0.38f), cabinRotation, true);
        Object.Destroy(cabinWindow.GetComponent<Collider>());
        var cabinDoor = CreateBox("CabinDoor", cabin + cabinRotation * new Vector3(1.55f, -0.45f, -2.54f), new Vector3(1.2f, 2.2f, 0.08f), new Color(0.08f, 0.09f, 0.1f), cabinRotation);
        Object.Destroy(cabinDoor.GetComponent<Collider>());
        var antenna = CreateBox("EmergencyAntenna", sites[2] + Vector3.up * 3.5f, new Vector3(0.12f, 7f, 0.12f), new Color(0.25f, 0.28f, 0.3f));
        Object.Destroy(antenna.GetComponent<Collider>());

        var mine = sites[4] + new Vector3(0f, 1.7f, 2f);
        CreateBox("OldMineEntrance", mine, new Vector3(5f, 3.4f, 1f), new Color(0.08f, 0.09f, 0.1f));
        CreateBox("MinePost", mine + new Vector3(-2.35f, 0f, -0.55f), new Vector3(0.35f, 3.8f, 0.35f), new Color(0.28f, 0.18f, 0.12f));
        CreateBox("MinePost", mine + new Vector3(2.35f, 0f, -0.55f), new Vector3(0.35f, 3.8f, 0.35f), new Color(0.28f, 0.18f, 0.12f));
        CreateBox("MineBeam", mine + new Vector3(0f, 1.75f, -0.55f), new Vector3(5.1f, 0.35f, 0.35f), new Color(0.28f, 0.18f, 0.12f));

        var station = sites[5] + new Vector3(3f, 1.3f, 2f);
        CreateBox("WeatherStation", station, new Vector3(4f, 2.6f, 4f), new Color(0.72f, 0.76f, 0.78f));
        var dish = CreatePart(PrimitiveType.Sphere, staticScenery, station + new Vector3(0f, 2.25f, 0f), new Vector3(1.2f, 0.18f, 1.2f), new Color(0.72f, 0.76f, 0.78f), Quaternion.Euler(55f, 0f, 0f));
        dish.name = "WeatherDish";
        Object.Destroy(dish.GetComponent<Collider>());

        var shelter = sites[7] + new Vector3(-3f, 1.2f, 2f);
        CreateBox("ForestShelter", shelter, new Vector3(4f, 2.4f, 3.5f), new Color(0.24f, 0.16f, 0.1f));
        CreateBox("ShelterRoof", shelter + Vector3.up * 1.35f, new Vector3(4.5f, 0.3f, 4f), new Color(0.86f, 0.89f, 0.91f), Quaternion.Euler(0f, 0f, -6f));
        var shelterWindow = CreateBox("ShelterWindow", shelter + new Vector3(0f, 0.15f, -1.78f), new Vector3(1.1f, 0.65f, 0.08f), new Color(1f, 0.65f, 0.25f), Quaternion.identity, true);
        Object.Destroy(shelterWindow.GetComponent<Collider>());

        var lake = CreateBox("FrozenLake", sites[3] + Vector3.down * 0.03f, new Vector3(15f, 0.08f, 12f), new Color(0.32f, 0.57f, 0.72f));
        Object.Destroy(lake.GetComponent<Collider>());
        if (lake.GetComponent<Renderer>().sharedMaterial.HasProperty("_Smoothness")) lake.GetComponent<Renderer>().sharedMaterial.SetFloat("_Smoothness", 0.72f);
        for (var crack = 0; crack < 3; crack++)
        {
            var iceCrack = CreateBox("IceCrack", sites[3] + new Vector3(crack * 2.2f - 2.2f, 0.02f, crack - 1f), new Vector3(5.5f, 0.025f, 0.08f), new Color(0.22f, 0.25f, 0.27f), Quaternion.Euler(0f, -28f + crack * 31f, 0f));
            Object.Destroy(iceCrack.GetComponent<Collider>());
        }
        var flare = new GameObject("EmergencyFlare");
        flare.transform.position = sites[1] + Vector3.up * 2f;
        var light = flare.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Orange;
        light.range = 18f;
        light.intensity = 5f;
    }

    private void CreateDistantMountains()
    {
        const int columns = 33;
        const int rows = 17;
        var points = new Vector3[columns * rows];
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var x = Mathf.Lerp(-480f, 480f, column / (columns - 1f));
            var z = Mathf.Lerp(435f, 735f, row / (rows - 1f));
            var north = row / (rows - 1f);
            var rise = Mathf.SmoothStep(0f, 1f, north * 1.45f);
            var broad = Mathf.PerlinNoise(x * 0.004f + 2.1f, z * 0.004f + 7.3f);
            var ridge = Mathf.Pow(1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.007f, z * 0.006f) * 2f - 1f), 2f);
            var summits = Mathf.Max(AlpinePeak(x, z, -240f, 625f, 145f),
                Mathf.Max(AlpinePeak(x, z, 15f, 650f, 120f), AlpinePeak(x, z, 245f, 610f, 150f)));
            points[row * columns + column] = new Vector3(x, 35f + broad * 18f + rise * (north * 24f + ridge * 36f + summits * 92f), z);
        }

        var vertices = new Vector3[(columns - 1) * (rows - 1) * 6];
        var uv = new Vector2[vertices.Length];
        var faces = new[] { new List<int>(), new List<int>(), new List<int>() };
        var vertex = 0;
        for (var row = 0; row < rows - 1; row++)
        for (var column = 0; column < columns - 1; column++)
        {
            var bottomLeft = points[row * columns + column];
            var bottomRight = points[row * columns + column + 1];
            var topLeft = points[(row + 1) * columns + column];
            var topRight = points[(row + 1) * columns + column + 1];
            var diagonal = (row + column & 1) == 0
                ? new[] { bottomLeft, topLeft, bottomRight, bottomRight, topLeft, topRight }
                : new[] { bottomLeft, topLeft, topRight, bottomLeft, topRight, bottomRight };
            var first = vertex;
            for (var i = 0; i < 6; i++)
            {
                vertices[vertex] = diagonal[i];
                uv[vertex] = new Vector2((diagonal[i].x + 480f) / 960f, (diagonal[i].z - 435f) / 300f);
                vertex++;
            }
            for (var triangle = 0; triangle < 2; triangle++)
            {
                var index = first + triangle * 3;
                var normal = Vector3.Cross(vertices[index + 1] - vertices[index], vertices[index + 2] - vertices[index]).normalized;
                var material = normal.y < 0.78f ? 2 : normal.x < -0.08f ? 1 : 0;
                faces[material].AddRange(new[] { index, index + 1, index + 2 });
            }
        }
        var mesh = new Mesh { name = "DistantMountainMesh", vertices = vertices, uv = uv, subMeshCount = 3 };
        for (var face = 0; face < faces.Length; face++) mesh.SetTriangles(faces[face], face);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        var mountains = new GameObject("DistantMountain");
        mountains.transform.SetParent(new GameObject("DistantScenery").transform, false);
        mountains.AddComponent<MeshFilter>().sharedMesh = mesh;
        mountains.AddComponent<MeshRenderer>().sharedMaterials = new[]
        {
            SurfaceMaterial(new Color(0.68f, 0.76f, 0.82f), 0.035f),
            SurfaceMaterial(new Color(0.38f, 0.49f, 0.61f), 0.045f),
            SurfaceMaterial(new Color(0.25f, 0.31f, 0.37f), 0.06f)
        };
    }

    private void CreateRescueTrail(Terrain terrain, Vector3 home, Vector3 destination)
    {
        const int points = 96;
        var vertices = new List<Vector3>(points * 4);
        var triangles = new List<int>((points - 1) * 12);
        var uv = new List<Vector2>(points * 4);
        for (var track = -1; track <= 1; track += 2)
        {
            var start = vertices.Count;
            for (var point = 0; point < points; point++)
            {
                var t = point / (points - 1f);
                var center = Vector3.Lerp(home + new Vector3(2.5f, 0f, 4f), destination, t);
                center.x -= Mathf.Sin(t * Mathf.PI) * 18f;
                var tangent = Vector3.ProjectOnPlane(destination - home + new Vector3(-Mathf.Cos(t * Mathf.PI) * 18f * Mathf.PI, 0f, 0f), Vector3.up).normalized;
                var side = new Vector3(-tangent.z, 0f, tangent.x);
                center += side * track * 0.55f;
                center.y = terrain.SampleHeight(center) + terrain.transform.position.y + 0.035f;
                vertices.Add(center - side * 0.08f);
                vertices.Add(center + side * 0.08f);
                uv.Add(new Vector2(0f, t * 12f));
                uv.Add(new Vector2(1f, t * 12f));
            }
            for (var point = 0; point < points - 1; point++)
            {
                var index = start + point * 2;
                triangles.AddRange(new[] { index, index + 1, index + 2, index + 1, index + 3, index + 2 });
            }
        }
        var mesh = new Mesh { name = "RescueTrailMesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        var trail = new GameObject("RescueTrail");
        trail.transform.SetParent(transform, false);
        trail.AddComponent<MeshFilter>().sharedMesh = mesh;
        trail.AddComponent<MeshRenderer>().sharedMaterial = SurfaceMaterial(new Color(0.58f, 0.64f, 0.68f), 0.025f);
    }

    private static Quaternion RandomRotation(System.Random random) => Quaternion.Euler((float)random.NextDouble() * 25f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 25f);

    private void CreateFir(Vector3 position, float scale)
    {
        firMesh ??= CreateFirMesh();
        var tree = new GameObject("SnowFir");
        tree.transform.SetParent(staticScenery, false);
        tree.transform.position = position;
        tree.transform.localScale = Vector3.one * scale;
        tree.AddComponent<MeshFilter>().sharedMesh = firMesh;
        tree.AddComponent<MeshRenderer>().sharedMaterials = new[]
        {
            SurfaceMaterial(new Color(0.2f, 0.12f, 0.07f), 0.04f),
            SurfaceMaterial(new Color(0.055f, 0.16f, 0.12f), 0.045f),
            SurfaceMaterial(new Color(0.82f, 0.87f, 0.9f), 0.025f)
        };
        var collider = tree.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 2.1f, 0f);
        collider.radius = 0.75f;
        collider.height = 4.2f;
    }

    private Mesh CreateFirMesh()
    {
        var vertices = new List<Vector3>();
        var trunk = new List<int>();
        var needles = new List<int>();
        var snow = new List<int>();
        AddFirLayer(vertices, trunk, 0f, 2.5f, 0.14f, 0.09f, 6);
        AddFirLayer(vertices, needles, 1.05f, 2.1f, 1.35f, 0f, 7);
        AddFirLayer(vertices, needles, 1.9f, 1.9f, 1.08f, 0f, 7);
        AddFirLayer(vertices, needles, 2.7f, 1.65f, 0.78f, 0f, 7);
        AddFirLayer(vertices, snow, 2.38f, 0.82f, 0.5f, 0f, 7);
        AddFirLayer(vertices, snow, 3.08f, 0.77f, 0.42f, 0f, 7);
        AddFirLayer(vertices, snow, 3.68f, 0.72f, 0.32f, 0f, 7);
        var mesh = new Mesh { name = "SnowFirMesh", subMeshCount = 3 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(trunk, 0);
        mesh.SetTriangles(needles, 1);
        mesh.SetTriangles(snow, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        return mesh;
    }

    private static void AddFirLayer(List<Vector3> vertices, List<int> triangles, float y, float height, float bottomRadius, float topRadius, int sides)
    {
        for (var side = 0; side < sides; side++)
        {
            var angle = side * Mathf.PI * 2f / sides;
            var nextAngle = (side + 1) * Mathf.PI * 2f / sides;
            var bottomLeft = new Vector3(Mathf.Sin(angle) * bottomRadius, y, Mathf.Cos(angle) * bottomRadius);
            var bottomRight = new Vector3(Mathf.Sin(nextAngle) * bottomRadius, y, Mathf.Cos(nextAngle) * bottomRadius);
            var topLeft = new Vector3(Mathf.Sin(angle) * topRadius, y + height, Mathf.Cos(angle) * topRadius);
            var topRight = new Vector3(Mathf.Sin(nextAngle) * topRadius, y + height, Mathf.Cos(nextAngle) * topRadius);
            var first = vertices.Count;
            vertices.AddRange(new[] { bottomLeft, topLeft, bottomRight, bottomRight, topLeft, topRight });
            for (var vertex = 0; vertex < 6; vertex++) triangles.Add(first + vertex);
        }
    }

    private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Color color, Quaternion rotation = default, bool glowing = false)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(staticScenery, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.transform.rotation = rotation == default ? Quaternion.identity : rotation;
        box.GetComponent<Renderer>().sharedMaterial = glowing ? GlowMaterial(color) : SurfaceMaterial(color, 0.06f);
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

    private Material GlowMaterial(Color color)
    {
        if (glowMaterials.TryGetValue(color, out var shared)) return shared;
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        glowMaterials.Add(color, material);
        return material;
    }

    private GameObject CreateLandingLight(string name, Vector3 position, float diameter, Color color, float surfaceOffset = 0f, Vector3 surfaceNormal = default)
    {
        var zone = new GameObject(name);
        zone.transform.SetParent(staticScenery, false);
        zone.transform.position = position;
        var surface = new GameObject("LandingSurface").transform;
        surface.SetParent(zone.transform, false);
        surface.localPosition = Vector3.up * surfaceOffset;
        surface.localRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal.sqrMagnitude > 0.5f ? surfaceNormal : Vector3.up);
        var padColor = new Color(0.12f, 0.15f, 0.16f);
        var pad = CreatePart(PrimitiveType.Cylinder, surface, Vector3.up * 0.02f, new Vector3(diameter * 0.9f, 0.035f, diameter * 0.9f), padColor);
        pad.name = "LandingPad";
        Object.Destroy(pad.GetComponent<Collider>());
        var marking = new Color(0.74f, 0.78f, 0.8f);
        var marker = CreatePart(PrimitiveType.Cylinder, surface, Vector3.up * 0.06f, new Vector3(diameter * 0.78f, 0.008f, diameter * 0.78f), marking);
        marker.name = "LandingMark";
        Object.Destroy(marker.GetComponent<Collider>());
        var center = CreatePart(PrimitiveType.Cylinder, surface, Vector3.up * 0.071f, new Vector3(diameter * 0.6f, 0.009f, diameter * 0.6f), padColor);
        center.name = "LandingPadCenter";
        Object.Destroy(center.GetComponent<Collider>());
        if (name == "HomeBase")
        {
            foreach (var mark in new[]
                     {
                         CreatePart(PrimitiveType.Cube, surface, new Vector3(-0.18f, 0.075f, 0f), new Vector3(0.09f, 0.025f, 0.65f), marking),
                         CreatePart(PrimitiveType.Cube, surface, new Vector3(0.18f, 0.075f, 0f), new Vector3(0.09f, 0.025f, 0.65f), marking),
                         CreatePart(PrimitiveType.Cube, surface, new Vector3(0f, 0.075f, 0f), new Vector3(0.42f, 0.025f, 0.09f), marking)
                     })
            {
                mark.name = "HomeMark";
                Object.Destroy(mark.GetComponent<Collider>());
            }
        }
        var lightObject = new GameObject("LandingLight");
        lightObject.transform.SetParent(zone.transform, false);
        lightObject.transform.localPosition = Vector3.up * (surfaceOffset + 3f);
        lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = 0f;
        light.range = 14f;
        light.spotAngle = 44f;
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
        navigationLight.range = 0.8f;
        navigationLight.intensity = 0.15f;
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
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color };
            partMaterials.Add(key, material);
        }
        renderer.sharedMaterial = material;
        return part;
    }

    private GameObject CreatePackage(Transform parent)
    {
        var package = CreatePart(PrimitiveType.Cube, parent, new Vector3(0f, -0.28f, 0f), new Vector3(0.28f, 0.2f, 0.28f), new Color(0.92f, 0.94f, 0.9f));
        package.name = "EmergencyMedicalPackage";
        var body = package.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        package.GetComponent<Collider>().enabled = false;
        return package;
    }

    private GameObject CreateFieldMarker()
    {
        var marker = new GameObject("FieldMarker");
        marker.transform.SetParent(transform, false);
        var pole = CreatePart(PrimitiveType.Cylinder, marker.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.07f, 0.55f, 0.07f), new Color(0.12f, 0.14f, 0.16f));
        pole.name = "FieldMarkerPole";
        Object.Destroy(pole.GetComponent<Collider>());
        var beacon = CreatePart(PrimitiveType.Sphere, marker.transform, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.18f, Orange);
        beacon.name = "FieldBeacon";
        beacon.GetComponent<Renderer>().sharedMaterial = GlowMaterial(Orange);
        Object.Destroy(beacon.GetComponent<Collider>());
        var light = new GameObject("FieldMarkerLight").AddComponent<Light>();
        light.transform.SetParent(marker.transform, false);
        light.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        light.type = LightType.Point;
        light.color = Orange;
        light.range = 10f;
        light.intensity = 8f;
        marker.SetActive(false);
        return marker;
    }

    private GameObject CreateRescueTarget(Vector3 position)
    {
        var signal = new GameObject("RescueSignal");
        signal.transform.SetParent(transform, false);
        signal.transform.position = position;
        var person = CreatePart(PrimitiveType.Capsule, signal.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.58f, 0.42f), Orange, Quaternion.Euler(0f, 0f, 12f));
        person.name = "RescueLeader";
        var hood = CreatePart(PrimitiveType.Sphere, signal.transform, new Vector3(-0.13f, 1.28f, 0f), Vector3.one * 0.46f, new Color(0.17f, 0.22f, 0.25f));
        hood.name = "RescueHood";
        var vest = CreatePart(PrimitiveType.Cube, signal.transform, new Vector3(0.08f, 0.7f, 0.39f), new Vector3(0.62f, 0.34f, 0.07f), new Color(1f, 0.82f, 0.12f), Quaternion.Euler(0f, 0f, 12f));
        vest.name = "RescueVest";
        var blanket = CreatePart(PrimitiveType.Cube, signal.transform, new Vector3(0.75f, 0.08f, 0.25f), new Vector3(1.4f, 0.06f, 1f), new Color(0.82f, 0.16f, 0.05f), Quaternion.Euler(0f, -18f, 0f));
        blanket.name = "EmergencyBlanket";
        foreach (var collider in signal.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
        signal.SetActive(false);
        return signal;
    }

    private Renderer CreateThermalTarget(Vector3 position)
    {
        var target = CreatePart(PrimitiveType.Capsule, transform, position, new Vector3(0.35f, 0.65f, 0.35f), Orange);
        target.name = "ThermalTrace";
        Object.Destroy(target.GetComponent<Collider>());
        var renderer = target.GetComponent<Renderer>();
        renderer.sharedMaterial = GlowMaterial(Orange);
        renderer.enabled = false;
        return renderer;
    }

    private static ParticleSystem CreateBlizzard(Transform viewer)
    {
        var stormObject = new GameObject("ProceduralBlizzard");
        stormObject.transform.SetParent(viewer, false);
        stormObject.transform.localPosition = new Vector3(0f, 8f, 14f);
        var storm = stormObject.AddComponent<ParticleSystem>();
        var main = storm.main;
        main.maxParticles = 520;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
        main.startColor = new Color(0.9f, 0.94f, 1f, 0.72f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = storm.emission;
        emission.rateOverTime = 35f;
        var shape = storm.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 14f, 30f);
        var velocity = storm.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        velocity.y = new ParticleSystem.MinMaxCurve(-1.8f, -0.7f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        var noise = storm.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.55f;
        var renderer = storm.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
        return storm;
    }

    private static void CreateHud(Camera camera, out Text status, out Text telemetry, out Text radio, out RectTransform horizon, out Image[] menuButtons, out Text[] menuButtonTexts)
    {
        var canvas = UiCanvas("FpvHud", camera);
        var missionCanvas = UiCanvas("MissionHud", camera);
        var thermalOverlay = UiImage(canvas.transform, "ThermalOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.18f, 0.28f, 0.16f), new Vector2(0.5f, 0.5f));
        thermalOverlay.enabled = false;

        var statusPanel = UiImage(missionCanvas.transform, "OperationsMenu", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -38f), new Vector2(790f, 340f), new Color(0.012f, 0.018f, 0.024f, 0.86f), new Vector2(0f, 1f));
        UiImage(statusPanel.transform, "BriefingHeader", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 6f), Orange, new Vector2(0.5f, 1f));
        var accent = UiImage(statusPanel.transform, "BriefingRail", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f), Cyan, new Vector2(0f, 0.5f));
        accent.rectTransform.sizeDelta = new Vector2(5f, 0f);
        var uplink = UiText(statusPanel.transform, "BriefingUplink", 16, new Color(0.48f, 0.58f, 0.64f), TextAnchor.UpperRight);
        uplink.text = "SAR COMMAND  //  ENCRYPTED UPLINK";
        uplink.rectTransform.offsetMin = new Vector2(380f, 0f);
        uplink.rectTransform.offsetMax = new Vector2(-22f, -18f);
        var index = UiText(statusPanel.transform, "BriefingIndex", 96, new Color(0.55f, 0.67f, 0.72f, 0.07f), TextAnchor.UpperRight);
        index.text = "01";
        index.rectTransform.offsetMin = new Vector2(520f, 40f);
        index.rectTransform.offsetMax = new Vector2(-24f, -58f);
        UiImage(statusPanel.transform, "BriefingFooter", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f), new Vector2(0.5f, 0f));
        status = UiText(statusPanel.transform, "StoryStatus", 24, Color.white, TextAnchor.UpperLeft);
        status.rectTransform.offsetMin = new Vector2(28f, 22f);
        status.rectTransform.offsetMax = new Vector2(-28f, -42f);
        menuButtons = new Image[4];
        menuButtonTexts = new Text[4];
        for (var i = 0; i < menuButtons.Length; i++)
        {
            var button = UiImage(statusPanel.transform, $"MenuButton{i + 1}", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -52f - i * 66f), new Vector2(278f, 52f), new Color(0.08f, 0.13f, 0.17f, 0.94f), new Vector2(1f, 1f));
            UiImage(button.transform, "ButtonRail", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f), Cyan, new Vector2(0f, 0.5f));
            var label = UiText(button.transform, $"MenuButtonLabel{i + 1}", 18, Color.white, TextAnchor.MiddleLeft);
            label.rectTransform.offsetMin = new Vector2(18f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            menuButtons[i] = button;
            menuButtonTexts[i] = label;
        }

        var alertPanel = UiImage(canvas.transform, "AlertBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(560f, 52f), new Color(0.14f, 0.025f, 0.015f, 0.88f), new Vector2(0.5f, 1f));
        telemetry = UiText(alertPanel.transform, "Telemetry", 28, Orange, TextAnchor.MiddleCenter);
        telemetry.rectTransform.offsetMin = new Vector2(12f, 5f);
        telemetry.rectTransform.offsetMax = new Vector2(-12f, -5f);

        var radioPanel = UiImage(canvas.transform, "RadioCaption", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 146f), new Vector2(900f, 88f), new Color(0.005f, 0.008f, 0.012f, 0.52f), new Vector2(0.5f, 0f));
        UiImage(radioPanel.transform, "RadioRail", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f), Orange, new Vector2(0f, 0.5f));
        radio = UiText(radioPanel.transform, "RadioSubtitle", 28, Color.white, TextAnchor.MiddleLeft);
        radio.rectTransform.offsetMin = new Vector2(18f, 8f);
        radio.rectTransform.offsetMax = new Vector2(-18f, -8f);
        TextShadow(radio);
        radioPanel.gameObject.SetActive(false);

        Gauge(canvas.transform, "Speed", "H.S", Vector2.zero, new Vector2(48f, 36f), Vector2.zero, Color.white);
        Gauge(canvas.transform, "Altitude", "ALT", new Vector2(1f, 0f), new Vector2(-48f, 36f), new Vector2(1f, 0f), Color.white);
        Gauge(canvas.transform, "Battery", "BAT", Vector2.one, new Vector2(-48f, -36f), Vector2.one, Color.white);
        DirectionIndicator(canvas.transform, "Wind", "WIND", new Vector2(0f, 1f), new Vector2(48f, -36f), new Vector2(0f, 1f), Color.white);
        DirectionIndicator(canvas.transform, "Navigation", "DST", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(0.5f, 0f), Orange);

        var heading = UiText(canvas.transform, "HeadingValue", 28, Color.white, TextAnchor.UpperCenter);
        heading.text = "000°";
        heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        heading.rectTransform.pivot = new Vector2(0.5f, 1f);
        heading.rectTransform.anchoredPosition = new Vector2(0f, -28f);
        heading.rectTransform.sizeDelta = new Vector2(140f, 36f);
        TextShadow(heading);
        UiImage(canvas.transform, "HeadingTick", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(2f, 10f), new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 1f));

        var horizonObject = new GameObject("ArtificialHorizon");
        horizonObject.transform.SetParent(canvas.transform, false);
        horizon = horizonObject.AddComponent<RectTransform>();
        horizon.anchorMin = horizon.anchorMax = new Vector2(0.5f, 0.5f);
        horizon.sizeDelta = new Vector2(440f, 220f);
        Line(horizon, new Vector2(-125f, 0f), new Vector2(95f, 2f), new Color(1f, 1f, 1f, 0.82f));
        Line(horizon, new Vector2(125f, 0f), new Vector2(95f, 2f), new Color(1f, 1f, 1f, 0.82f));
        var direction = UiText(canvas.transform, "FlightDirection", 56, Color.white, TextAnchor.MiddleCenter);
        direction.text = "○";
        direction.rectTransform.anchorMin = direction.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        direction.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        direction.rectTransform.sizeDelta = new Vector2(72f, 72f);
        TextShadow(direction);
        Line(canvas.transform, Vector2.zero, new Vector2(5f, 5f), Orange);

        var action = UiImage(canvas.transform, "ActionIndicator", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(220f, 38f), Color.clear, new Vector2(0.5f, 0.5f));
        var actionLabel = UiText(action.transform, "ActionLabel", 16, new Color(0.86f, 0.89f, 0.91f), TextAnchor.UpperCenter);
        actionLabel.rectTransform.offsetMin = new Vector2(0f, 13f);
        actionLabel.rectTransform.offsetMax = Vector2.zero;
        TextShadow(actionLabel);
        var actionTrack = UiImage(action.transform, "ActionTrack", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(200f, 2f), new Color(1f, 1f, 1f, 0.24f), new Vector2(0.5f, 0f));
        UiImage(actionTrack.transform, "ActionFill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200f, 2f), Orange, new Vector2(0f, 0.5f));
        foreach (var root in new[] { canvas.transform, missionCanvas.transform })
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");
    }

    private static Canvas UiCanvas(string name, Camera camera)
    {
        var obj = new GameObject(name);
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 0.45f;
        var scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
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

    private static void Gauge(Transform parent, string name, string labelText, Vector2 anchor, Vector2 position, Vector2 pivot, Color color)
    {
        var panel = UiImage(parent, name + "Gauge", anchor, anchor, position, new Vector2(230f, 82f), Color.clear, pivot);
        var label = UiText(panel.transform, name + "Label", 18, new Color(0.82f, 0.85f, 0.87f), TextAnchor.UpperLeft);
        label.text = labelText;
        label.rectTransform.offsetMin = new Vector2(0f, 54f);
        label.rectTransform.offsetMax = new Vector2(0f, -6f);
        var value = UiText(panel.transform, name + "Value", 40, Color.white, TextAnchor.LowerLeft);
        value.rectTransform.offsetMin = new Vector2(0f, 12f);
        value.rectTransform.offsetMax = new Vector2(0f, -25f);
        TextShadow(label);
        TextShadow(value);
        var track = UiImage(panel.transform, name + "Track", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 7f), new Vector2(190f, 2f), new Color(1f, 1f, 1f, 0.24f), new Vector2(0f, 0f));
        UiImage(track.transform, name + "Fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(190f, 2f), color, new Vector2(0f, 0.5f));
    }

    private static void DirectionIndicator(Transform parent, string name, string labelText, Vector2 anchor, Vector2 position, Vector2 pivot, Color color)
    {
        var panel = UiImage(parent, name + "Gauge", anchor, anchor, position, new Vector2(230f, 82f), Color.clear, pivot);
        var label = UiText(panel.transform, name + "Label", 18, new Color(0.82f, 0.85f, 0.87f), TextAnchor.UpperLeft);
        label.text = labelText;
        label.rectTransform.offsetMin = new Vector2(0f, 54f);
        label.rectTransform.offsetMax = new Vector2(0f, -6f);
        var value = UiText(panel.transform, name + "Value", 40, Color.white, TextAnchor.LowerLeft);
        value.rectTransform.offsetMin = new Vector2(0f, 12f);
        value.rectTransform.offsetMax = new Vector2(-48f, -25f);
        TextShadow(label);
        TextShadow(value);
        UiImage(panel.transform, name + "Track", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 7f), new Vector2(190f, 2f), new Color(color.r, color.g, color.b, 0.35f), new Vector2(0f, 0f));
        var needle = UiText(panel.transform, name + "Needle", 28, color, TextAnchor.MiddleCenter);
        needle.text = "▲";
        needle.rectTransform.anchorMin = needle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        needle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        needle.rectTransform.anchoredPosition = new Vector2(205f, -2f);
        needle.rectTransform.sizeDelta = new Vector2(32f, 32f);
    }

    private static void TextShadow(Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private static void Line(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        UiImage(parent, "HudLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, color, new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        foreach (var mesh in generatedMeshes) Object.Destroy(mesh);
        foreach (var material in partMaterials.Values) Object.Destroy(material);
        foreach (var material in glowMaterials.Values) Object.Destroy(material);
        foreach (var material in surfaceMaterials.Values)
        {
            Object.Destroy(material.mainTexture);
            Object.Destroy(material);
        }
    }
}
