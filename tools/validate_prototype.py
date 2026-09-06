"""Small dependency-free smoke check for the generated Unity prototype."""

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

manifest = json.loads((ROOT / "Packages/manifest.json").read_text())
dependencies = manifest["dependencies"]
assert dependencies["com.unity.inputsystem"] == "1.20.0"
assert dependencies["com.unity.xr.openxr"] == "1.18.0-pre.2"
assert dependencies["com.unity.render-pipelines.universal"] == "17.6.0"
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

for script in (ROOT / "Assets/Scripts").glob("*.cs"):
    source = script.read_text()
    assert source.count("{") == source.count("}"), script

input_source = (ROOT / "Assets/Scripts/QuestInput.cs").read_text()
for token in ("digit1Key", "digit2Key", "spaceKey", "GetDeviceAtXRNode", "rightSecondaryDown", "PulseRight"):
    assert token in input_source

types_source = (ROOT / "Assets/Scripts/MissionTypes.cs").read_text()
for token in ("Inspection", "Operator", "Fpv", "Chase"):
    assert token in types_source

bootstrap_source = (ROOT / "Assets/Scripts/PrototypeBootstrap.cs").read_text()
for token in ("CreateInspectionTower", "CreateEnvironment", "DronePresentation", "AudioListener", "ScreenSpaceCamera", "CanvasScaler", "CreateLandingLight", "LandingGlow", "BrushlessMotor", "GimbalCamera"):
    assert token in bootstrap_source

mission_source = (ROOT / "Assets/Scripts/MissionController.cs").read_text()
for token in ("GAME OVER", "やり直す", "リタイア"):
    assert token in mission_source

fpv_source = (ROOT / "Assets/Scripts/FpvMonitor.cs").read_text()
for token in ("gimbalPositionResponse", "gimbalRotationResponse", "ProjectOnPlane"):
    assert token in fpv_source

drone_source = (ROOT / "Assets/Scripts/DroneController.cs").read_text()
for token in ("MotorsArmed", "ApplyReturnHome", "ApplyTakeoff"):
    assert token in drone_source

print("prototype smoke check: ok")
