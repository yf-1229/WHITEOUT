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

scene = (ROOT / "Assets/Scenes/Prototype.unity").read_text()
assert "--- !u!1660057539 &9223372036854775807\nSceneRoots:" in scene
assert "GameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}" in scene
assert "  serializedVersion: 6\n  m_Component:" in scene
assert "Transform:" in scene and "  serializedVersion: 2\n" in scene
bootstrap_meta = (ROOT / "Assets/Scripts/PrototypeBootstrap.cs.meta").read_text()
guid = next(line.split(":", 1)[1].strip() for line in bootstrap_meta.splitlines() if line.startswith("guid:"))
assert f"guid: {guid}" in scene

player_settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text()
assert "  activeInputHandler: 1\n" in player_settings
assert "  defaultScreenWidth: 1920\n" in player_settings
assert "  defaultScreenHeight: 1080\n" in player_settings

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
estimated_durations = []
for route in routes:
    points = [(0, -4), *(sites[index] for index in route), (0, -4)]
    estimated_seconds = sum(math.dist(start, end) for start, end in zip(points, points[1:])) / 2.7 + 20
    estimated_durations.append(estimated_seconds)
    assert 210 <= estimated_seconds <= 370, (route, estimated_seconds)

bootstrap_source = (ROOT / "Assets/Scripts/PrototypeBootstrap.cs").read_text()
for token in ("CreateMountainTerrain", "TerrainData", "AddAlpineGrass", "CreateAlpineEnvironment", "CreateBlizzard", "ArtificialHorizon", "missionSites", "FrozenLake", "OldMineEntrance", "WeatherStation", "ForestShelter", "DronePresentation", "AudioListener", "ScreenSpaceCamera", "CanvasScaler", "CreateLandingLight", "LandingGlow", "BrushlessMotor", "GimbalCamera"):
    assert token in bootstrap_source
assert "XRSettings.eyeTextureResolutionScale = 1f;" in bootstrap_source
assert "package.AddComponent<Rigidbody>()" in bootstrap_source

mission_source = (ROOT / "Assets/Scripts/MissionController.cs").read_text()
for token in ("STORY FLIGHT", "StoryCatalog.All", "SelectStory", "BestKey", "missionCruiseSpeed", "StoryStage.Route", "StoryStage.Objective", "StoryStage.ReturnHome", "MOTORS OFF / B ARM", "NAV {navigation}", "abortConfirmationUntil", "もう一度で中止", "Y / M サーマル", "MISSION COMPLETE", "NEXT STORY"):
    assert token in mission_source
for token in ("RefreshCampaignProgress", "救助ネットワーク", "networkReadiness", "completedStories"):
    assert token in mission_source
for token in ("DistressSiteFor", "distressOfferUntil", "OPTIONAL RESCUE", "UpdateHazards", "CROSSWIND", "ROTOR", "UpdatePackageSway", "DeliveryReady", "ApplyWorldProgress", "PILOT STYLE"):
    assert token in mission_source
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
for token in ("gimbalPositionResponse", "gimbalRotationResponse", "ProjectOnPlane"):
    assert token in fpv_source

drone_source = (ROOT / "Assets/Scripts/DroneController.cs").read_text()
for token in ("MotorsArmed", "ResetToIdle", "SetFlightMode", "ApplyReturnHome", "ApplyAssistedFlight", "ApplyAttitude", "maxTiltDegrees", "homePosition.y + 0.5f", "returnAltitude", "planar.magnitude > 20f"):
    assert token in drone_source
assert "SetBatteryDrain" in drone_source
assert "public bool MotorsArmed { get; private set; }\n" in drone_source
assert drone_source.count("MotorsArmed = true;") == 2
assert "body.linearVelocity = new Vector3(horizontal.x" not in drone_source

assert 'camera.clearFlags = CameraClearFlags.Skybox;' in bootstrap_source
assert 'Shader.Find("Skybox/Procedural")' in bootstrap_source

print(f"prototype smoke check: {len(story_codes)} stories, {min(estimated_durations) / 60:.1f}-{max(estimated_durations) / 60:.1f} min, ok")
