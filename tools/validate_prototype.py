"""Small dependency-free smoke check for the generated Unity prototype."""

import json
import math
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

manifest = json.loads((ROOT / "Packages/manifest.json").read_text())
dependencies = manifest["dependencies"]
assert dependencies["com.unity.inputsystem"] == "1.20.0"
assert dependencies["com.unity.xr.openxr"] == "1.18.0-pre.2"
assert dependencies["com.unity.render-pipelines.universal"] == "17.6.0"
assert dependencies["com.unity.modules.terrain"] == "1.0.0"
assert dependencies["com.unity.modules.particlesystem"] == "1.0.0"
assert "com.unity.xr.interaction.toolkit" not in dependencies
assert (ROOT / "Assets/Scenes/Prototype.unity").is_file()

build_settings = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text()
assert "path: Assets/Scenes/Prototype.unity" in build_settings

scene = (ROOT / "Assets/Scenes/Prototype.unity").read_text()
assert "--- !u!1660057539 &9223372036854775807\nSceneRoots:" in scene
assert "GameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}" in scene
assert "  serializedVersion: 6\n  m_Component:" in scene
assert "Transform:" in scene and "  serializedVersion: 2\n" in scene
bootstrap_meta = (ROOT / "Assets/Scripts/PrototypeBootstrap.cs.meta").read_text()
guid = next(line.split(":", 1)[1].strip() for line in bootstrap_meta.splitlines() if line.startswith("guid:"))
assert f"guid: {guid}" in scene

player_settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text()
assert "  productName: WHITEOUT\n" in player_settings
assert "  activeInputHandler: 1\n" in player_settings
assert "  defaultScreenWidth: 1920\n" in player_settings
assert "  defaultScreenHeight: 1080\n" in player_settings
assert "  AndroidMinSdkVersion: 29\n" in player_settings
assert "  AndroidTargetArchitectures: 2\n" in player_settings
assert "  scriptingBackend:\n    Android: 1\n" in player_settings

xr_management = (ROOT / "Assets/XR/XRGeneralSettingsPerBuildTarget.asset").read_text()
assert "m_Name: Android Providers" in xr_management
assert "m_AutomaticLoading: 1" in xr_management
assert "guid: 67e84d3d777d151ac9dfec97d4aa0b29" in xr_management

openxr_settings = (ROOT / "Assets/XR/Settings/OpenXR Package Settings.asset").read_text()
for name in ("MetaQuestFeature Android", "OculusTouchControllerProfile Android", "MetaQuestTouchPlusControllerProfile Android"):
    section = openxr_settings.split(f"m_Name: {name}", 1)[1].split("---", 1)[0]
    assert "m_enabled: 1" in section
assert "visibleName: Quest 3S\n    manifestName: quest3s\n    enabled: 1" in openxr_settings

for script in (ROOT / "Assets/Scripts").glob("*.cs"):
    source = script.read_text()
    assert source.count("{") == source.count("}"), script

input_source = (ROOT / "Assets/Scripts/QuestInput.cs").read_text()
for token in ("digit1Key", "digit2Key", "spaceKey", "GetDeviceAtXRNode", "rightSecondaryDown", "PulseRight"):
    assert token in input_source

types_source = (ROOT / "Assets/Scripts/MissionTypes.cs").read_text()
for token in ("FreeFlight", "StoryStage", "Route", "Objective", "ReturnHome", "Operator", "Fpv", "Chase", "pilotStyle"):
    assert token in types_source

catalog_source = (ROOT / "Assets/Scripts/StoryCatalog.cs").read_text()
story_codes = re.findall(r'S\("(\d{2})",', catalog_source)
assert len(story_codes) == 24
assert len(set(story_codes)) == len(story_codes)
assert story_codes == [f"{number:02}" for number in range(1, 25)]
story_texts = re.findall(r'S\("\d{2}", "[^"]+", "([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)"', catalog_source)
assert len(story_texts) == len(story_codes)
for briefing, route_one, route_two, _, success in story_texts:
    assert "\\n" in briefing
    assert "。" in route_one and "。" in route_two and "。" in success
assert catalog_source.count("：「") >= 5
for token in ("StoryAction.Deliver", "StoryAction.Scan", "StoryAction.Relay", "StoryAction.Mark", "holdSeconds"):
    assert token in catalog_source
sites = [(24, 90), (102, 186), (-102, 204), (150, 60), (-156, 120), (-15, 258), (162, 225), (-90, 54)]
routes = [tuple(map(int, route)) for route in re.findall(r"StoryAction\.\w+, (\d), (\d), (\d),", catalog_source)]
assert len(routes) == len(story_codes)
for story_index, route in enumerate(routes):
    distress_site = (route[2] + story_index + 1) % len(sites)
    while distress_site in route:
        distress_site = (distress_site + 1) % len(sites)
    assert distress_site not in route
assert {story_index % 4 for story_index in range(len(story_codes))} == {0, 1, 2, 3}
mission_speed = 3.6
estimated_durations = []
for route in routes:
    points = [(0, -4), *(sites[index] for index in route), (0, -4)]
    estimated_seconds = sum(math.dist(start, end) for start, end in zip(points, points[1:])) / mission_speed + 20
    estimated_durations.append(estimated_seconds)
    assert 165 <= estimated_seconds <= 280, (route, estimated_seconds)

bootstrap_source = (ROOT / "Assets/Scripts/PrototypeBootstrap.cs").read_text()
for token in ("CreateMountainTerrain", "TerrainData", "windSnow", "windPack", "AddAlpineGrass", "CreateAlpineEnvironment", "CreateBlizzard", "ParticleSystemRenderMode.Billboard", "ArtificialHorizon", "Gauge(canvas.transform", "DirectionIndicator", 'name + "Fill"', 'name + "Needle"', "missionSites", "FrozenLake", "IceCrack", "OldMineEntrance", "MinePost", "WeatherStation", "WeatherDish", "ForestShelter", "ShelterWindow", "CabinWindow", "CabinDoor", "GlowMaterial", "CreateFieldMarker", "FieldBeacon", "DronePresentation", "AudioListener", "ScreenSpaceCamera", "CanvasScaler", "CreateLandingLight", "LandingGlow", "BrushlessMotor", "GimbalCamera"):
    assert token in bootstrap_source
assert "XRSettings.eyeTextureResolutionScale = 1f;" in bootstrap_source
assert "package.AddComponent<Rigidbody>()" in bootstrap_source
assert "texture.Apply(false, false);" in bootstrap_source

mission_source = (ROOT / "Assets/Scripts/MissionController.cs").read_text()
for token in ("STORY FLIGHT", "StoryCatalog.All", "SelectStory", "BestKey", "missionCruiseSpeed", "StoryStage.Route", "StoryStage.Objective", "StoryStage.ReturnHome", "MOTORS OFF  /  B ARM", "AGL\\n{groundClearance", "NAV\\n{distance", "speedFill.rectTransform.localScale", "navigationNeedle.localRotation", "telemetryText.transform.parent.gameObject.SetActive", "abortConfirmationUntil", "もう一度で中止", "Y / M サーマル", "MISSION COMPLETE", "NEXT STORY"):
    assert token in mission_source
for token in ("RefreshCampaignProgress", "FindUnlockedStory", "unlockedStory", "救助ネットワーク", "networkReadiness", "completedStories"):
    assert token in mission_source
assert "SelectedStory + direction + unlockedStory + 1" in mission_source
for token in ("DistressSiteFor", "DistressHoldSeconds", "DistressHoldReady", "HasFreshSuccess", "distressOfferUntil", "OPTIONAL RESCUE", "UpdateHazards", "CROSSWIND", "ROTOR", "UpdatePackageSway", "DeliveryReady", "ApplyWorldProgress", "UpdateObjectiveLights", "restoredSites", "PILOT STYLE"):
    assert token in mission_source
assert "actionProgress >= DistressHoldSeconds" in mission_source and "body.linearVelocity.magnitude" in mission_source
assert "UpdateObjectiveLights(navigationAvailable ? objective : null);" in mission_source
assert "flow.x - 0.35f" in mission_source and "Mathf.Lerp(35f, 240f, strength)" in mission_source
assert "lastResult.collisions" in mission_source and "lastResult.jobPenalty" in mission_source
assert "windZone != previousWindZone" in mission_source and "turbulencePulseAfter" in mission_source
assert "clearance < 3f" in mission_source and "terrainWarningAfter" in mission_source and '"PULL UP"' in mission_source
assert "Story.action == StoryAction.Deliver ? DeliveryReady() : stable" in mission_source
assert "fieldMarker.position = markerPosition" in mission_source and "fieldMarker.gameObject.SetActive(true)" in mission_source
assert "!lowBatteryWarned && drone.BatteryPercent < 20f" in mission_source
for token in ("CLOSING WEATHER", "WEAK BATTERY", "NAV INTERFERENCE", "UNSTABLE AIR"):
    assert token in mission_source
assert mission_source.count("drone.BeginMission(homePad.position);") == 3
active_flow = mission_source.split("else if (State == MissionState.Active)", 1)[1].split("else if (input.rightPrimaryDown)", 1)[0]
assert "input.leftPrimaryDown" in active_flow and "Time.unscaledTime < abortConfirmationUntil" in active_flow and "EnterFreeFlight();" in active_flow
start_story = mission_source.split("private void StartStory()", 1)[1].split("private void UpdateStory", 1)[0]
enter_idle = mission_source.split("private void EnterFreeFlight()", 1)[1].split("private void CompleteStory", 1)[0]
assert "drone.BeginMission(homePad.position);" in start_story
assert "drone.BeginMission(homePad.position);" in enter_idle

fpv_source = (ROOT / "Assets/Scripts/FpvMonitor.cs").read_text()
for token in ("gimbalPositionResponse", "gimbalRotationResponse", "ProjectOnPlane", "thermalTarget", "UpdateThermalTarget", "thermalTargets[i].enabled = thermal && i == thermalTarget"):
    assert token in fpv_source
assert "SetThermalAvailable(Story.action == StoryAction.Scan, Story.targetSite)" in mission_source

drone_source = (ROOT / "Assets/Scripts/DroneController.cs").read_text()
for token in ("MotorsArmed", "ResetToIdle", "SetFlightMode", "ApplyReturnHome", "ApplyAssistedFlight", "ApplyAttitude", "maxTiltDegrees", "homePosition.y + 0.5f", "returnAltitude", "planar.magnitude > 20f"):
    assert token in drone_source
for token in ("stickDeadzone", "stickExpo", "motorResponse", "ShapeStick", "manualLift", "BatteryLoad", "horizontal.magnitude", "Wind.magnitude"):
    assert token in drone_source
assert "Mathf.Clamp01(impact / CrashSpeed) * 0.45f" in drone_source
assert "SetBatteryDrain" in drone_source
assert "public bool MotorsArmed { get; private set; }\n" in drone_source
assert drone_source.count("MotorsArmed = true;") == 2
assert "body.linearVelocity = new Vector3(horizontal.x" not in drone_source

presentation_source = (ROOT / "Assets/Scripts/DronePresentation.cs").read_text()
for token in ("CreateWindLoop", "ProceduralWind", "drone.Wind.magnitude", "wind.volume", "CreateCue", "RadioWarning", "RadioSuccess", "PlayWarning", "PlaySuccess"):
    assert token in presentation_source
assert mission_source.count("presentation.PlayWarning();") == 4
assert mission_source.count("presentation.PlaySuccess();") == 3

assert 'camera.clearFlags = CameraClearFlags.Skybox;' in bootstrap_source
assert 'Shader.Find("Skybox/Procedural")' in bootstrap_source
assert "Mathf.Pow(1f - Mathf.Abs" in bootstrap_source
assert "AlpinePeak" in bootstrap_source
assert "CreateThermalTarget" in bootstrap_source
assert "texture.Apply(true, true);" in bootstrap_source
for value in ("snow.smoothness = 0.18f", "windSnow.smoothness = 0.3f", "rock.smoothness = 0.04f"):
    assert value in bootstrap_source

for token in ("ACT I  WHITEOUT", "ACT II  VOICES", "ACT III  BREAKING POINT", "ACT IV  FIRST LIGHT", "distressMissed", "DistressOutcome", "sun.color"):
    assert token in mission_source

configurator_source = (ROOT / "Assets/Editor/ProjectConfigurator.cs").read_text()
assert '"Game preview rendered black"' in configurator_source
for shader in ("Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit", "Skybox/Procedural"):
    assert shader in configurator_source

view_source = (ROOT / "Assets/Scripts/ViewController.cs").read_text()
assert "Application.isEditor ? ViewMode.Operator" in view_source

print(f"prototype smoke check: {len(story_codes)} stories, {min(estimated_durations) / 60:.1f}-{max(estimated_durations) / 60:.1f} min, ok")
