using UnityEngine;
using UnityEngine.UI;

public sealed class MissionController : MonoBehaviour
{
    public DroneController drone;
    public FpvMonitor fpv;
    public ViewController views;
    public Transform homePad;
    public Transform mountainPass;
    public Transform deliveryZone;
    public Transform package;
    public Text statusText;
    public Text telemetryText;
    public RectTransform artificialHorizon;
    public ParticleSystem blizzard;

    public MissionState State { get; private set; } = MissionState.FreeFlight;
    public StoryStage Stage { get; private set; } = StoryStage.MountainPass;

    private FlightMode selectedMode;
    private Rigidbody body;
    private float missionStart;
    private int jobPenalty;
    private MissionResult lastResult;

    private void Start()
    {
        selectedMode = (FlightMode)Mathf.Clamp(PlayerPrefs.GetInt("flightMode", 0), 0, 1);
        body = drone.GetComponent<Rigidbody>();
        drone.flightMode = selectedMode;
        drone.BeginMission(homePad.position);
        package.gameObject.SetActive(false);
        SetStorm(false);
        UpdateDisplay();
    }

    private void Update()
    {
        var input = QuestInput.Current;
        if (State == MissionState.FreeFlight)
        {
            if (input.leftSecondaryDown)
            {
                selectedMode = selectedMode == FlightMode.Assist ? FlightMode.Manual : FlightMode.Assist;
                drone.flightMode = selectedMode;
                PlayerPrefs.SetInt("flightMode", (int)selectedMode);
            }
            if (input.rightPrimaryDown) StartStory();
        }
        else if (State == MissionState.Active)
        {
            if (drone.Crashed) State = MissionState.Failed;
            else UpdateStory(input);
        }
        else if (input.rightPrimaryDown)
        {
            StartStory();
        }
        else if (input.leftPrimaryDown)
        {
            EnterFreeFlight();
        }

        UpdateDisplay();
    }

    private void StartStory()
    {
        State = MissionState.Active;
        Stage = StoryStage.MountainPass;
        missionStart = Time.time;
        jobPenalty = 0;
        drone.flightMode = selectedMode;
        drone.BeginMission(homePad.position);
        drone.SetWind(new Vector3(0.85f, 0f, -0.3f));
        fpv.SetThermalAvailable(false);
        package.SetParent(drone.transform, false);
        package.localPosition = new Vector3(0f, -0.28f, 0f);
        package.gameObject.SetActive(true);
        SetStorm(true);
        QuestInput.PulseRight(0.25f, 0.12f);
    }

    private void UpdateStory(QuestInputState input)
    {
        if (Stage == StoryStage.MountainPass)
        {
            if (Vector3.Distance(drone.transform.position, mountainPass.position) > 8f) return;
            Stage = StoryStage.DeliverSupplies;
            QuestInput.PulseRight(0.3f, 0.15f);
            return;
        }

        if (Stage == StoryStage.DeliverSupplies)
        {
            if (!input.rightTriggerDown) return;
            if (Vector3.Distance(drone.transform.position, deliveryZone.position) <= 4f)
            {
                package.SetParent(null, true);
                package.position = deliveryZone.position + Vector3.up * 0.3f;
                Stage = StoryStage.ReturnHome;
                QuestInput.PulseRight(0.55f, 0.22f);
            }
            else
            {
                jobPenalty += 100;
            }
            return;
        }

        if (drone.IsLanded && Vector3.Distance(drone.transform.position, homePad.position) < 2f)
            CompleteStory();
    }

    private void EnterFreeFlight()
    {
        State = MissionState.FreeFlight;
        drone.BeginMission(homePad.position);
        drone.SetWind(Vector3.zero);
        package.gameObject.SetActive(false);
        SetStorm(false);
    }

    private void CompleteStory()
    {
        State = MissionState.Completed;
        var elapsed = Time.time - missionStart;
        var landingDistance = Vector3.Distance(drone.transform.position, homePad.position);
        var score = Mathf.Max(0, Mathf.RoundToInt(1000f - elapsed * 2f - drone.CollisionCount * 75f - jobPenalty + drone.BatteryPercent));
        lastResult = new MissionResult
        {
            elapsedSeconds = elapsed,
            collisions = drone.CollisionCount,
            batteryPercent = drone.BatteryPercent,
            jobPenalty = jobPenalty,
            landingDistance = landingDistance,
            score = score,
            grade = Grade(score)
        };
        if (score > PlayerPrefs.GetInt("best_Whiteout", 0)) PlayerPrefs.SetInt("best_Whiteout", score);
        PlayerPrefs.Save();
        SetStorm(false);
    }

    private void SetStorm(bool strong)
    {
        if (blizzard != null)
        {
            var emission = blizzard.emission;
            emission.rateOverTime = strong ? 420f : 75f;
        }
        RenderSettings.fogStartDistance = strong ? 22f : 50f;
        RenderSettings.fogEndDistance = strong ? 95f : 180f;
    }

    private void UpdateDisplay()
    {
        if (statusText == null || drone == null) return;

        var objective = Stage == StoryStage.MountainPass ? mountainPass : Stage == StoryStage.DeliverSupplies ? deliveryZone : homePad;
        var distance = Vector3.Distance(drone.transform.position, objective.position);
        var elapsed = State == MissionState.Active ? Time.time - missionStart : 0f;
        var warning = drone.GeofenceWarning ? "  GEOFENCE / RTH" : drone.BatteryPercent < 20f ? "  LOW BATTERY" : "";
        var heading = Mathf.Repeat(drone.transform.eulerAngles.y, 360f);
        var horizontalSpeed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        var altitude = Mathf.Max(0f, drone.transform.position.y - homePad.position.y);
        telemetryText.text = $"REC  {heading:000} DEG   SPD {horizontalSpeed:0.0}m/s   ALT {altitude:0.0}m   VSI {body.linearVelocity.y:+0.0;-0.0;0.0}   BAT {drone.BatteryPercent:0}%   LINK HD{warning}\n" +
                             $"{selectedMode.ToString().ToUpper()}   VIEW {views.CurrentMode.ToString().ToUpper()}   WIND {drone.Wind.magnitude:0.0}m/s   OBJ {distance:000}m";

        var roll = Mathf.DeltaAngle(0f, drone.transform.eulerAngles.z);
        var pitch = Mathf.DeltaAngle(0f, drone.transform.eulerAngles.x);
        artificialHorizon.localRotation = Quaternion.Euler(0f, 0f, roll);
        artificialHorizon.anchoredPosition = new Vector2(0f, Mathf.Clamp(pitch * 3f, -90f, 90f));

        if (State == MissionState.FreeFlight)
        {
            statusText.text = "FREE FLIGHT  /  OPERATIONS MENU\n\n" +
                              "A / 1   STORY START: WHITEOUT\n" +
                              "大雪で孤立した山岳救護所へ\n緊急医療物資を届ける\n\n" +
                              $"Y / M   操作モード: {selectedMode}\nGRIP / C   視点切替";
            return;
        }

        if (State == MissionState.Active)
        {
            if (Stage == StoryStage.MountainPass)
                statusText.text = elapsed < 8f
                    ? "MISSION 01  /  WHITEOUT\n\n救護所の暖房が停止。残された親子と\n救助隊へ、血液と保温剤を輸送せよ。\n道路は雪崩で閉鎖されている。"
                    : $"01  雪崩監視所を通過\n\n青い航法ビーコンへ向かえ\nDISTANCE  {distance:0} m";
            else if (Stage == StoryStage.DeliverSupplies)
                statusText.text = $"02  最後の届け物\n\n尾根の赤い救難信号へ接近\n4m以内で TRIGGER / SPACE 投下\nDISTANCE  {distance:0} m";
            else
                statusText.text = $"03  嵐から帰還\n\n物資を受領。親子の搬送が始まった。\n基地へ戻り着陸せよ。 B / R で自動帰還\nHOME  {distance:0} m";
            return;
        }

        statusText.text = State == MissionState.Completed
            ? $"MISSION COMPLETE  /  WHITEOUT\n\n救護所の全員が夜明け前に救出された。\n{lastResult.score} PTS   GRADE {lastResult.grade}   {lastResult.elapsedSeconds:0}s\n\nA / 1  REPLAY    X / 2  FREE FLIGHT"
            : "SIGNAL LOST  /  AIRFRAME DOWN\n\n医療物資は届かなかった。\n嵐が強まる前に再出動せよ。\n\nA / 1  RETRY    X / 2  FREE FLIGHT";
    }

    private static string Grade(int score) => score >= 900 ? "S" : score >= 750 ? "A" : score >= 600 ? "B" : score >= 450 ? "C" : "D";
}
