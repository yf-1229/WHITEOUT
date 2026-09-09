using UnityEngine;
using UnityEngine.UI;

public sealed class MissionController : MonoBehaviour
{
    public DroneController drone;
    public FpvMonitor fpv;
    public ViewController views;
    public Transform homePad;
    public Transform[] missionSites;
    public Transform package;
    public Text statusText;
    public Text telemetryText;
    public RectTransform artificialHorizon;
    public ParticleSystem blizzard;
    public float missionCruiseSpeed = 2.7f;

    public MissionState State { get; private set; } = MissionState.FreeFlight;
    public StoryStage Stage { get; private set; } = StoryStage.Route;
    public int SelectedStory { get; private set; }

    private StoryDefinition Story => StoryCatalog.All[SelectedStory];
    private string BestKey => $"best_story_{Story.code}";
    private FlightMode selectedMode;
    private Rigidbody body;
    private float freeFlightSpeed;
    private float missionStart;
    private float actionProgress;
    private float abortConfirmationUntil;
    private int routeStep;
    private int jobPenalty;
    private MissionResult lastResult;

    private void Start()
    {
        selectedMode = (FlightMode)Mathf.Clamp(PlayerPrefs.GetInt("flightMode", 0), 0, 1);
        SelectedStory = Mathf.Clamp(PlayerPrefs.GetInt("storyIndex", 0), 0, StoryCatalog.All.Length - 1);
        body = drone.GetComponent<Rigidbody>();
        freeFlightSpeed = drone.maxHorizontalSpeed;
        drone.flightMode = selectedMode;
        drone.BeginMission(homePad.position);
        package.gameObject.SetActive(false);
        SetStorm(0f);
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
            if (input.leftPrimaryDown) SelectStory(1);
            if (input.rightStickClickDown) SelectStory(-1);
            if (input.rightPrimaryDown) StartStory();
        }
        else if (State == MissionState.Active)
        {
            if (input.leftPrimaryDown)
            {
                if (Time.unscaledTime < abortConfirmationUntil) EnterFreeFlight();
                else abortConfirmationUntil = Time.unscaledTime + 2f;
            }
            else if (drone.Crashed) State = MissionState.Failed;
            else UpdateStory(input);
        }
        else if (input.rightPrimaryDown)
        {
            StartStory();
        }
        else if (input.leftPrimaryDown)
        {
            if (State == MissionState.Completed) SelectStory(1);
            EnterFreeFlight();
        }

        UpdateDisplay();
    }

    private void SelectStory(int direction)
    {
        SelectedStory = (SelectedStory + direction + StoryCatalog.All.Length) % StoryCatalog.All.Length;
        PlayerPrefs.SetInt("storyIndex", SelectedStory);
        QuestInput.PulseRight(0.12f, 0.05f);
    }

    private void StartStory()
    {
        State = MissionState.Active;
        Stage = StoryStage.Route;
        routeStep = 0;
        actionProgress = 0f;
        abortConfirmationUntil = 0f;
        missionStart = Time.time;
        jobPenalty = 0;
        drone.flightMode = selectedMode;
        drone.maxHorizontalSpeed = missionCruiseSpeed;
        drone.BeginMission(homePad.position);
        drone.SetWind(new Vector3(Story.wind, 0f, -Story.wind * 0.35f));
        fpv.SetThermalAvailable(Story.action == StoryAction.Scan);
        package.SetParent(drone.transform, false);
        package.localPosition = new Vector3(0f, -0.28f, 0f);
        package.gameObject.SetActive(Story.action == StoryAction.Deliver);
        SetStorm(Story.storm);
        QuestInput.PulseRight(0.25f, 0.12f);
    }

    private void UpdateStory(QuestInputState input)
    {
        if (Stage == StoryStage.Route)
        {
            if (Vector3.Distance(drone.transform.position, RouteTarget.position) > 10f) return;
            routeStep++;
            if (routeStep == 2) Stage = StoryStage.Objective;
            QuestInput.PulseRight(0.3f, 0.15f);
            return;
        }

        if (Stage == StoryStage.Objective)
        {
            var close = Vector3.Distance(drone.transform.position, ObjectiveTarget.position) <= 5f;
            var stable = body.linearVelocity.magnitude < 1.6f;
            actionProgress = Story.holdSeconds <= 0f ? 0f : close && stable ? actionProgress + Time.deltaTime : 0f;
            var held = actionProgress >= Story.holdSeconds;
            if (Story.action == StoryAction.Relay)
            {
                if (held) CompleteObjective();
            }
            else if (input.rightTriggerDown)
            {
                if (close && held) CompleteObjective();
                else jobPenalty += 100;
            }
            return;
        }

        if (drone.IsLanded && Vector3.Distance(drone.transform.position, homePad.position) < 2f)
            CompleteStory();
    }

    private void CompleteObjective()
    {
        if (package.gameObject.activeSelf)
        {
            package.SetParent(null, true);
            package.position = ObjectiveTarget.position + Vector3.up * 0.3f;
        }
        Stage = StoryStage.ReturnHome;
        QuestInput.PulseRight(0.55f, 0.22f);
    }

    private Transform RouteTarget => missionSites[routeStep == 0 ? Story.firstSite : Story.secondSite];
    private Transform ObjectiveTarget => missionSites[Story.targetSite];

    private void EnterFreeFlight()
    {
        State = MissionState.FreeFlight;
        drone.maxHorizontalSpeed = freeFlightSpeed;
        drone.BeginMission(homePad.position);
        drone.SetWind(Vector3.zero);
        fpv.SetThermalAvailable(false);
        package.gameObject.SetActive(false);
        SetStorm(0f);
        abortConfirmationUntil = 0f;
    }

    private void CompleteStory()
    {
        State = MissionState.Completed;
        var elapsed = Time.time - missionStart;
        var landingDistance = Vector3.Distance(drone.transform.position, homePad.position);
        var score = Mathf.Max(0, Mathf.RoundToInt(1100f - elapsed * 1.2f - drone.CollisionCount * 75f - jobPenalty + drone.BatteryPercent));
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
        if (score > PlayerPrefs.GetInt(BestKey, 0)) PlayerPrefs.SetInt(BestKey, score);
        PlayerPrefs.Save();
        SetStorm(0f);
    }

    private void SetStorm(float strength)
    {
        if (blizzard != null)
        {
            var emission = blizzard.emission;
            emission.rateOverTime = Mathf.Lerp(65f, 480f, strength);
        }
        RenderSettings.fogStartDistance = Mathf.Lerp(80f, 25f, strength);
        RenderSettings.fogEndDistance = Mathf.Lerp(420f, 100f, strength);
    }

    private void UpdateDisplay()
    {
        if (statusText == null || drone == null) return;

        var objective = State != MissionState.Active ? homePad : Stage == StoryStage.Route ? RouteTarget : Stage == StoryStage.Objective ? ObjectiveTarget : homePad;
        var distance = Vector3.Distance(drone.transform.position, objective.position);
        var elapsed = State == MissionState.Active ? Time.time - missionStart : 0f;
        var warning = !drone.MotorsArmed ? "  MOTORS OFF / B ARM" : drone.GeofenceWarning ? "  GEOFENCE / RTH" : drone.BatteryPercent < 20f ? "  LOW BATTERY" : "";
        var heading = Mathf.Repeat(drone.transform.eulerAngles.y, 360f);
        var objectiveDirection = objective.position - drone.transform.position;
        var objectiveBearing = Mathf.Repeat(Mathf.Atan2(objectiveDirection.x, objectiveDirection.z) * Mathf.Rad2Deg, 360f);
        var turn = Mathf.DeltaAngle(heading, objectiveBearing);
        var navigation = Mathf.Abs(turn) < 5f ? "AHEAD" : $"{(turn < 0f ? "L" : "R")} {Mathf.Abs(turn):000}";
        var horizontalSpeed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        var altitude = Mathf.Max(0f, drone.transform.position.y - homePad.position.y);
        telemetryText.text = $"REC  {heading:000} DEG   SPD {horizontalSpeed:0.0}m/s   ALT {altitude:0.0}m   VSI {body.linearVelocity.y:+0.0;-0.0;0.0}   BAT {drone.BatteryPercent:0}%   LINK HD{warning}\n" +
                             $"{selectedMode.ToString().ToUpper()}   VIEW {views.CurrentMode.ToString().ToUpper()}   WIND {drone.Wind.magnitude:0.0}m/s   OBJ {distance:000}m   NAV {navigation}";

        var roll = Mathf.DeltaAngle(0f, drone.transform.eulerAngles.z);
        var pitch = Mathf.DeltaAngle(0f, drone.transform.eulerAngles.x);
        artificialHorizon.localRotation = Quaternion.Euler(0f, 0f, roll);
        artificialHorizon.anchoredPosition = new Vector2(0f, Mathf.Clamp(pitch * 3f, -90f, 90f));

        if (State == MissionState.FreeFlight)
        {
            var best = PlayerPrefs.GetInt(BestKey, 0);
            var record = best > 0 ? $"CLEARED  /  BEST {best}" : "NEW MISSION";
            statusText.text = $"STORY FLIGHT  {SelectedStory + 1:00} / {StoryCatalog.All.Length:00}   ~5 MIN\n\n" +
                              $"A / 1   START: {Story.title}\n{record}  /  {Story.briefing}\n\n" +
                              $"X / 2  NEXT   R-STICK / 3  PREV\nY / M  操作: {selectedMode}";
            return;
        }

        if (State == MissionState.Active)
        {
            if (elapsed < 7f)
                statusText.text = $"MISSION {Story.code}  /  {Story.title}\n\n{Story.briefing}\n\n出動せよ。  {AbortHint()}";
            else if (Stage == StoryStage.Route)
                statusText.text = $"MISSION {Story.code}  /  LEG {routeStep + 1}/2\n\n{(routeStep == 0 ? Story.routeOne : Story.routeTwo)}\nDISTANCE  {distance:0} m   {AbortHint()}";
            else if (Stage == StoryStage.Objective)
                statusText.text = $"MISSION {Story.code}  /  FIELD TASK\n\n{Story.objective}\n{ActionPrompt()}\nDISTANCE  {distance:0} m   {AbortHint()}";
            else
                statusText.text = $"MISSION {Story.code}  /  RETURN\n\n{Story.success}\n基地へ帰還・着陸せよ。 B / R 自動帰還\nHOME  {distance:0} m   {AbortHint()}";
            return;
        }

        statusText.text = State == MissionState.Completed
            ? $"MISSION COMPLETE  /  {Story.title}\n\n{Story.success}\n{lastResult.score} PTS   GRADE {lastResult.grade}   {lastResult.elapsedSeconds:0}s\n\nA / 1  REPLAY    X / 2  NEXT STORY"
            : $"SIGNAL LOST  /  {Story.title}\n\n任務は完了しなかった。\n嵐が強まる前に再出動せよ。\n\nA / 1  RETRY    X / 2  MENU";
    }

    private string ActionPrompt()
    {
        if (Story.action == StoryAction.Relay)
            return $"5m以内で静止維持  {actionProgress:0}/{Story.holdSeconds:0}s";
        if (Story.action == StoryAction.Scan)
            return $"Y / M サーマル  静止 {actionProgress:0}/{Story.holdSeconds:0}s → TRIGGER";
        return Story.action == StoryAction.Deliver ? "5m以内で TRIGGER / SPACE 投下" : "5m以内で TRIGGER / SPACE 設置";
    }

    private string AbortHint() => Time.unscaledTime < abortConfirmationUntil ? "X / 2 もう一度で中止" : "X / 2 中止";

    private static string Grade(int score) => score >= 900 ? "S" : score >= 750 ? "A" : score >= 600 ? "B" : score >= 450 ? "C" : "D";
}
