using UnityEngine;
using UnityEngine.UI;

public sealed class MissionController : MonoBehaviour
{
    public DroneController drone;
    public FpvMonitor fpv;
    public ViewController views;
    public Transform homePad;
    public Transform deliveryZone;
    public Transform rescueTarget;
    public Transform[] inspectionPoints;
    public Transform package;
    public Text statusText;

    public MissionState State { get; private set; } = MissionState.Ready;
    public MissionType CurrentMission { get; private set; }

    private float missionStart;
    private float rescueStableSeconds;
    private bool objectiveComplete;
    private int jobPenalty;
    private FlightMode selectedMode;
    private bool[] inspected;
    private MissionResult lastResult;

    private void Start()
    {
        selectedMode = (FlightMode)Mathf.Clamp(PlayerPrefs.GetInt("flightMode", 0), 0, 1);
        inspected = new bool[inspectionPoints.Length];
        State = MissionState.Ready;
        UpdateDisplay();
    }

    private void Update()
    {
        var input = QuestInput.Current;
        if (State == MissionState.Ready)
        {
            if (input.leftSecondaryDown)
            {
                selectedMode = selectedMode == FlightMode.Assist ? FlightMode.Manual : FlightMode.Assist;
                PlayerPrefs.SetInt("flightMode", (int)selectedMode);
            }
            if (input.rightPrimaryDown) StartMission(MissionType.Delivery);
            else if (input.leftPrimaryDown) StartMission(MissionType.Rescue);
            else if (input.rightStickClickDown) StartMission(MissionType.Inspection);
        }
        else if (State == MissionState.Active)
        {
            if (drone.Crashed) State = MissionState.Failed;
            else
            {
                UpdateObjective(input);
                if (objectiveComplete && drone.IsLanded && Vector3.Distance(drone.transform.position, homePad.position) < 2f)
                    CompleteMission();
            }
        }
        else if (input.rightPrimaryDown)
        {
            StartMission(CurrentMission);
        }
        else if (input.leftPrimaryDown)
        {
            State = MissionState.Ready;
            drone.BeginMission(homePad.position);
            drone.SetWind(Vector3.zero);
            fpv.SetThermalAvailable(false);
            package.gameObject.SetActive(false);
        }

        UpdateDisplay();
    }

    private void StartMission(MissionType type)
    {
        CurrentMission = type;
        State = MissionState.Active;
        missionStart = Time.time;
        rescueStableSeconds = 0f;
        objectiveComplete = false;
        jobPenalty = 0;
        drone.flightMode = selectedMode;
        drone.BeginMission(homePad.position);
        drone.SetWind(type == MissionType.Delivery ? new Vector3(0.25f, 0f, 0.08f)
            : type == MissionType.Rescue ? new Vector3(0.65f, 0f, 0.18f)
            : new Vector3(0.95f, 0f, -0.28f));
        fpv.SetThermalAvailable(type == MissionType.Rescue);
        package.SetParent(type == MissionType.Delivery ? drone.transform : transform, false);
        package.gameObject.SetActive(type == MissionType.Delivery);
        package.localPosition = new Vector3(0f, -0.28f, 0f);
        for (var i = 0; i < inspected.Length; i++)
        {
            inspected[i] = false;
            inspectionPoints[i].GetComponent<Renderer>().material.color = new Color(1f, 0.75f, 0.12f);
        }
    }

    private void UpdateObjective(QuestInputState input)
    {
        if (CurrentMission == MissionType.Delivery)
        {
            if (!input.rightTriggerDown) return;
            if (Vector3.Distance(drone.transform.position, deliveryZone.position) <= 1.5f)
            {
                package.SetParent(null, true);
                package.position = deliveryZone.position + Vector3.up * 0.25f;
                objectiveComplete = true;
            }
            else
            {
                jobPenalty += 100;
                package.SetParent(drone.transform, false);
                package.localPosition = new Vector3(0f, -0.28f, 0f);
            }
        }
        else if (CurrentMission == MissionType.Rescue)
        {
            var closeEnough = Vector3.Distance(drone.transform.position, rescueTarget.position) <= 8f;
            var velocity = drone.GetComponent<Rigidbody>().linearVelocity.magnitude;
            rescueStableSeconds = closeEnough && velocity < 1.5f ? rescueStableSeconds + Time.deltaTime : 0f;
            if (!input.rightTriggerDown) return;
            if (closeEnough && rescueStableSeconds >= 3f) objectiveComplete = true;
            else jobPenalty += 100;
        }
        else
        {
            UpdateInspection(input);
        }
    }

    private void UpdateInspection(QuestInputState input)
    {
        if (!input.rightTriggerDown) return;
        var nearest = -1;
        var nearestDistance = 5f;
        for (var i = 0; i < inspectionPoints.Length; i++)
        {
            if (inspected[i]) continue;
            var distance = Vector3.Distance(drone.transform.position, inspectionPoints[i].position);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }

        if (nearest < 0)
        {
            jobPenalty += 100;
            return;
        }

        inspected[nearest] = true;
        inspectionPoints[nearest].GetComponent<Renderer>().material.color = new Color(0.2f, 0.9f, 0.35f);
        objectiveComplete = true;
        foreach (var complete in inspected) objectiveComplete &= complete;
    }

    private void CompleteMission()
    {
        State = MissionState.Completed;
        var elapsed = Time.time - missionStart;
        var timeBonus = Mathf.Max(0f, 250f - elapsed);
        var landingDistance = Vector3.Distance(drone.transform.position, homePad.position);
        var landingScore = Mathf.InverseLerp(2f, 0.5f, landingDistance) * 150f;
        var batteryScore = Mathf.Clamp01(drone.BatteryPercent / 25f) * 100f;
        var score = Mathf.Max(0, Mathf.RoundToInt(500f + timeBonus + landingScore + batteryScore - drone.CollisionCount * 50f - jobPenalty));
        var result = new MissionResult { elapsedSeconds = elapsed, collisions = drone.CollisionCount, batteryPercent = drone.BatteryPercent, jobPenalty = jobPenalty, landingDistance = landingDistance, score = score, grade = Grade(score) };
        lastResult = result;
        var key = $"best_{CurrentMission}_{selectedMode}";
        if (result.score > PlayerPrefs.GetInt(key, 0)) PlayerPrefs.SetInt(key, result.score);
        PlayerPrefs.Save();
    }

    private static string Grade(int score) => score >= 900 ? "S" : score >= 750 ? "A" : score >= 600 ? "B" : score >= 450 ? "C" : "D";

    private void UpdateDisplay()
    {
        if (statusText == null || drone == null) return;
        if (State == MissionState.Ready)
        {
            statusText.text = $"DRONE JOBS VR\n\nA / 1  配送\nX / 2  捜索救助\n右Stick押込 / 3  建物点検\nY / M  操作: {selectedMode}\nGrip / C  視点: {views.CurrentMode}\n\nTrigger / SPACE: 仕事   B / R: 帰還・離陸";
            return;
        }
        if (State == MissionState.Active)
        {
            string task;
            if (CurrentMission == MissionType.Delivery)
                task = objectiveComplete ? "荷物を届けた。発着地点へ帰還" : "黄色い光で荷物を投下";
            else if (CurrentMission == MissionType.Rescue)
                task = objectiveComplete ? "遭難者を報告済み。帰還" : $"遭難者付近で3秒静止 ({rescueStableSeconds:0.0}s)";
            else
                task = objectiveComplete ? "点検完了。発着地点へ帰還" : $"黄色の設備を点検 ({InspectionCount()}/{inspectionPoints.Length})";
            var warning = drone.GeofenceWarning ? "\n警告：飛行範囲外。自動帰還します" : "";
            var parked = drone.MotorsArmed ? "" : "\nモーター停止：B / Rで離陸";
            statusText.text = $"{CurrentMission}\n{task}\n\n電池 {drone.BatteryPercent:0}%  接触 {drone.CollisionCount}\n風 {drone.Wind.magnitude:0.0}  操作 {selectedMode}  視点 {views.CurrentMode}{warning}{parked}";
        }
        else
        {
            statusText.text = State == MissionState.Completed
                ? $"MISSION COMPLETE\n\n{lastResult.score}点  Grade {lastResult.grade}\n\nA / 1  やり直す\nX / 2  リタイア"
                : "GAME OVER\n\nドローンが墜落しました\n\nA / 1  やり直す\nX / 2  リタイア";
        }
    }

    private int InspectionCount()
    {
        var count = 0;
        foreach (var complete in inspected) if (complete) count++;
        return count;
    }
}
