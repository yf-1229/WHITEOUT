using UnityEngine;
using UnityEngine.UI;

public sealed class MissionController : MonoBehaviour
{
    private const float DistressHoldSeconds = 3f;
    public DroneController drone;
    public FpvMonitor fpv;
    public ViewController views;
    public Transform homePad;
    public Transform[] missionSites;
    public Transform package;
    public Transform fieldMarker;
    public Transform rescueTarget;
    public Renderer[] recoveryWindows;
    public Text statusText;
    public Text telemetryText;
    public Image[] menuButtons;
    public Text[] menuButtonTexts;
    public RectTransform artificialHorizon;
    public ParticleSystem blizzard;
    public Light sun;
    public float missionCruiseSpeed = 8f;

    public MissionState State { get; private set; } = MissionState.FreeFlight;
    public StoryStage Stage { get; private set; } = StoryStage.Route;
    public int SelectedStory { get; private set; }

    private StoryDefinition Story => StoryCatalog.All[SelectedStory];
    private string BestKey => $"best_story_{Story.code}";
    private FlightMode selectedMode;
    private Rigidbody body;
    private Rigidbody packageBody;
    private Collider packageCollider;
    private DronePresentation presentation;
    private Light homeLight;
    private Light[] siteLights;
    private float freeFlightSpeed;
    private float missionStart;
    private float actionProgress;
    private float abortConfirmationUntil;
    private float distressOfferUntil;
    private int routeStep;
    private int jobPenalty;
    private int fieldBonus;
    private int distressSite;
    private int completedStories;
    private int completedDistressCalls;
    private int networkReadiness;
    private int unlockedStory;
    private readonly bool[] siteRestored = new bool[8];
    private Color[] recoveryWindowColors;
    private bool distressOffered;
    private bool distressAccepted;
    private bool distressCompleted;
    private bool distressMissed;
    private bool objectiveComplete;
    private bool lowBatteryWarned;
    private string windZone = "CALM";
    private string previousWindZone = "CALM";
    private float turbulencePulseAfter;
    private float terrainWarningAfter;
    private bool terrainAlert;
    private MissionResult lastResult;
    private Image speedFill;
    private Image altitudeFill;
    private Image batteryFill;
    private Image actionFill;
    private Image thermalOverlay;
    private Text speedValue;
    private Text altitudeValue;
    private Text batteryValue;
    private Text windValue;
    private Text navigationValue;
    private Text headingValue;
    private Text actionLabel;
    private Text briefingIndex;
    private Text taskCue;
    private RectTransform windNeedle;
    private RectTransform navigationNeedle;
    private MenuPage menuPage;
    private int menuSelection;

    private enum MenuPage { Main, Missions, Practice, Settings }

    private int MissionModifier => SelectedStory % 4;
    private bool HasDistressCall => SelectedStory % 3 == 0;
    private bool NavigationAvailable => MissionModifier != 2 || Mathf.Repeat(Time.time - missionStart, 8f) < 5.5f;
    private string ActName => (SelectedStory / 6) switch
    {
        0 => "ACT I  WHITEOUT / 孤立",
        1 => "ACT II  VOICES / 連携",
        2 => "ACT III  BREAKING POINT / 決断",
        _ => "ACT IV  FIRST LIGHT / 希望"
    };

    private void Start()
    {
        selectedMode = (FlightMode)Mathf.Clamp(PlayerPrefs.GetInt("flightMode", (int)FlightMode.Assist), (int)FlightMode.Assist, (int)FlightMode.Manual);
        drone.SetAssistProfile(PlayerPrefs.GetInt("assistProfile", 1));
        unlockedStory = FindUnlockedStory();
        SelectedStory = Mathf.Clamp(PlayerPrefs.GetInt("storyIndex", 0), 0, unlockedStory);
        body = drone.GetComponent<Rigidbody>();
        presentation = drone.GetComponent<DronePresentation>();
        packageBody = package.GetComponent<Rigidbody>();
        packageCollider = package.GetComponent<Collider>();
        speedFill = Hud<Image>("SpeedFill");
        altitudeFill = Hud<Image>("AltitudeFill");
        batteryFill = Hud<Image>("BatteryFill");
        actionFill = Hud<Image>("ActionFill");
        thermalOverlay = Hud<Image>("ThermalOverlay");
        speedValue = Hud<Text>("SpeedValue");
        altitudeValue = Hud<Text>("AltitudeValue");
        batteryValue = Hud<Text>("BatteryValue");
        windValue = Hud<Text>("WindValue");
        navigationValue = Hud<Text>("NavigationValue");
        headingValue = Hud<Text>("HeadingValue");
        actionLabel = Hud<Text>("ActionLabel");
        briefingIndex = Hud<Text>("BriefingIndex");
        taskCue = Hud<Text>("TaskCue");
        windNeedle = Hud<RectTransform>("WindNeedle");
        navigationNeedle = Hud<RectTransform>("NavigationNeedle");
        homeLight = homePad.GetComponentInChildren<Light>();
        siteLights = new Light[missionSites.Length];
        for (var i = 0; i < missionSites.Length; i++) siteLights[i] = missionSites[i].GetComponentInChildren<Light>();
        recoveryWindowColors = new Color[recoveryWindows.Length];
        for (var i = 0; i < recoveryWindows.Length; i++)
            recoveryWindowColors[i] = recoveryWindows[i].sharedMaterial.HasProperty("_BaseColor") ? recoveryWindows[i].sharedMaterial.GetColor("_BaseColor") : recoveryWindows[i].sharedMaterial.color;
        freeFlightSpeed = drone.maxHorizontalSpeed;
        drone.SetFlightMode(selectedMode);
        drone.SetStandby(homePad.position);
        ResetPackage(false);
        fieldMarker.gameObject.SetActive(false);
        rescueTarget.gameObject.SetActive(false);
        SetStorm(0f);
        RefreshCampaignProgress();
        UpdateDisplay();
    }

    private void Update()
    {
        var input = QuestInput.Current;
        if (State == MissionState.FreeFlight)
        {
            if (input.leftPrimaryDown) MoveMenu(1);
            if (input.rightStickClickDown) MoveMenu(-1);
            if (input.rightPrimaryDown) ActivateMenuSelection();
        }
        else if (State == MissionState.Active)
        {
            if (input.leftPrimaryDown)
            {
                if (Time.unscaledTime < abortConfirmationUntil) EnterFreeFlight();
                else abortConfirmationUntil = Time.unscaledTime + 2f;
            }
            else if (drone.Crashed)
            {
                State = MissionState.Failed;
                SetStorm(0f);
                presentation.PlayWarning();
            }
            else
            {
                if (!lowBatteryWarned && drone.BatteryPercent < 20f)
                {
                    lowBatteryWarned = true;
                    presentation.PlayWarning();
                    QuestInput.PulseRight(0.35f, 0.25f);
                }
                UpdateHazards();
                UpdatePackageSway();
                UpdateStory(input);
            }
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

    private void MoveMenu(int direction)
    {
        var count = menuPage == MenuPage.Main ? 3 : menuPage == MenuPage.Missions ? 4 : menuPage == MenuPage.Practice ? 1 : 4;
        menuSelection = (menuSelection + direction + count) % count;
        QuestInput.PulseRight(0.08f, 0.04f);
    }

    private void ActivateMenuSelection()
    {
        if (menuPage == MenuPage.Main)
        {
            menuPage = menuSelection == 0 ? MenuPage.Missions : menuSelection == 1 ? MenuPage.Practice : MenuPage.Settings;
            menuSelection = 0;
            if (menuPage == MenuPage.Practice)
            {
                drone.BeginMission(homePad.position);
                drone.SetWind(Vector3.zero);
                SetStorm(0f);
            }
            return;
        }
        if (menuPage == MenuPage.Practice)
        {
            menuPage = MenuPage.Main;
            drone.SetStandby(homePad.position);
            return;
        }
        if (menuPage == MenuPage.Missions)
        {
            if (menuSelection == 0) { StartStory(); return; }
            if (menuSelection == 1) SelectStory(1);
            else if (menuSelection == 2) ToggleFlightMode();
            else { menuPage = MenuPage.Main; menuSelection = 0; }
            return;
        }
        if (menuSelection == 0) ToggleFlightMode();
        else if (menuSelection == 1)
        {
            drone.CycleAssistProfile();
            PlayerPrefs.SetInt("assistProfile", drone.AssistProfileName == "SPORT" ? 0 : drone.AssistProfileName == "CRUISE" ? 1 : 2);
            PlayerPrefs.Save();
        }
        else if (menuSelection == 2) { views.CycleMode(); views.missionHud.gameObject.SetActive(true); }
        else { menuPage = MenuPage.Main; menuSelection = 0; }
    }

    private void ToggleFlightMode()
    {
        selectedMode = selectedMode == FlightMode.Assist ? FlightMode.Manual : FlightMode.Assist;
        drone.SetFlightMode(selectedMode);
        PlayerPrefs.SetInt("flightMode", (int)selectedMode);
        PlayerPrefs.Save();
    }

    private void SelectStory(int direction)
    {
        SelectedStory = (SelectedStory + direction + unlockedStory + 1) % (unlockedStory + 1);
        PlayerPrefs.SetInt("storyIndex", SelectedStory);
        SetStorm(0f);
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
        fieldBonus = 0;
        distressSite = DistressSiteFor(SelectedStory);
        distressOffered = false;
        distressAccepted = false;
        distressCompleted = false;
        distressMissed = false;
        objectiveComplete = false;
        lowBatteryWarned = false;
        previousWindZone = "CALM";
        turbulencePulseAfter = 0f;
        terrainWarningAfter = 0f;
        terrainAlert = false;
        drone.SetFlightMode(selectedMode);
        drone.maxHorizontalSpeed = missionCruiseSpeed;
        drone.BeginMission(homePad.position);
        drone.SetBatteryDrain(MissionModifier == 1 ? 1.35f : 1f);
        drone.SetWind(new Vector3(Story.wind, 0f, -Story.wind * 0.35f));
        fpv.SetThermalAvailable(Story.action == StoryAction.Scan, Story.targetSite);
        ResetPackage(Story.action == StoryAction.Deliver);
        fieldMarker.gameObject.SetActive(false);
        rescueTarget.gameObject.SetActive(false);
        SetStorm(Story.storm);
        presentation.PlayRadio();
        QuestInput.PulseRight(0.25f, 0.12f);
    }

    private void UpdateStory(QuestInputState input)
    {
        if (Stage == StoryStage.Route)
        {
            if (distressOffered && !distressAccepted)
            {
                if (input.rightPrimaryDown)
                {
                    distressAccepted = true;
                    presentation.PlayRadio();
                    QuestInput.PulseRight(0.45f, 0.18f);
                }
                else if (Time.unscaledTime >= distressOfferUntil)
                {
                    distressOffered = false;
                    distressMissed = true;
                    rescueTarget.gameObject.SetActive(false);
                    jobPenalty += 75;
                    QuestInput.PulseRight(0.2f, 0.35f);
                }
                else return;
            }
            if (distressAccepted && !distressCompleted)
            {
                var distressDistance = Vector3.Distance(drone.transform.position, missionSites[distressSite].position);
                actionProgress = DistressHoldReady(distressDistance, body.linearVelocity.magnitude) ? actionProgress + Time.deltaTime : 0f;
                if (actionProgress >= DistressHoldSeconds)
                {
                    distressCompleted = true;
                    distressOffered = false;
                    actionProgress = 0f;
                    presentation.PlaySuccess();
                    QuestInput.PulseRight(0.65f, 0.25f);
                }
                return;
            }
            if (Vector3.Distance(drone.transform.position, RouteTarget.position) > 10f) return;
            routeStep++;
            if (routeStep == 1 && HasDistressCall)
            {
                distressOffered = true;
                distressOfferUntil = Time.unscaledTime + 8f;
                ShowRescueTarget(distressSite);
                presentation.PlayWarning();
            }
            else if (routeStep == 1) presentation.PlayRadio();
            if (routeStep == 2)
            {
                Stage = StoryStage.Objective;
                distressOffered = false;
                ShowRescueTarget(Story.targetSite);
                presentation.PlayRadio();
            }
            QuestInput.PulseRight(0.3f, 0.15f);
            return;
        }

        if (Stage == StoryStage.Objective)
        {
            var close = Vector3.Distance(drone.transform.position, ObjectiveTarget.position) <= 5f;
            var stable = body.linearVelocity.magnitude < 1.6f;
            var sensorReady = Story.action != StoryAction.Scan || fpv.Thermal;
            actionProgress = Story.holdSeconds <= 0f ? 0f : ActionHoldReady(close, stable, sensorReady) ? actionProgress + Time.deltaTime : 0f;
            var held = actionProgress >= Story.holdSeconds;
            if (Story.action == StoryAction.Relay)
            {
                if (held) CompleteObjective();
            }
            else if (input.rightTriggerDown)
            {
                if (held && sensorReady && ActionReady(Story.action, close, stable, DeliveryReady())) CompleteObjective();
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
            var distance = Vector3.Distance(drone.transform.position, ObjectiveTarget.position);
            fieldBonus = Mathf.Clamp(Mathf.RoundToInt(140f - distance * 14f - body.linearVelocity.magnitude * 32f), 0, 120);
            package.SetParent(null, true);
            packageBody.isKinematic = false;
            packageBody.useGravity = true;
            packageCollider.enabled = true;
            packageBody.linearVelocity = body.linearVelocity;
        }
        else fieldBonus = Story.action == StoryAction.Mark
            ? Mathf.Clamp(Mathf.RoundToInt(70f - Vector3.Distance(drone.transform.position, ObjectiveTarget.position) * 10f - body.linearVelocity.magnitude * 12f), 20, 70)
            : 60;
        if (Story.action == StoryAction.Mark)
        {
            var markerPosition = ObjectiveTarget.position;
            var terrain = Terrain.activeTerrain;
            if (terrain != null) markerPosition.y = terrain.SampleHeight(markerPosition) + terrain.transform.position.y;
            fieldMarker.position = markerPosition;
            fieldMarker.gameObject.SetActive(true);
        }
        Stage = StoryStage.ReturnHome;
        objectiveComplete = true;
        siteRestored[Story.targetSite] = true;
        UpdateRecoveryWindows();
        rescueTarget.GetComponent<RescueMotion>().SetRelieved(true);
        presentation.PlaySuccess();
        QuestInput.PulseRight(0.55f, 0.22f);
    }

    private Transform RouteTarget => missionSites[routeStep == 0 ? Story.firstSite : Story.secondSite];
    private Transform ObjectiveTarget => missionSites[Story.targetSite];

    private void EnterFreeFlight()
    {
        State = MissionState.FreeFlight;
        drone.maxHorizontalSpeed = freeFlightSpeed;
        drone.SetStandby(homePad.position);
        drone.SetBatteryDrain(1f);
        drone.SetWind(Vector3.zero);
        fpv.SetThermalAvailable(false);
        ResetPackage(false);
        fieldMarker.gameObject.SetActive(false);
        rescueTarget.gameObject.SetActive(false);
        SetStorm(0f);
        abortConfirmationUntil = 0f;
        objectiveComplete = false;
        RefreshCampaignProgress();
    }

    private void CompleteStory()
    {
        State = MissionState.Completed;
        var elapsed = Time.time - missionStart;
        var landingDistance = Vector3.Distance(drone.transform.position, homePad.position);
        var score = Mathf.Max(0, Mathf.RoundToInt(1100f - elapsed * 1.2f - drone.CollisionCount * 75f - jobPenalty + drone.BatteryPercent + fieldBonus + (distressCompleted ? 150 : 0)));
        var pilotStyle = PilotStyle(elapsed, landingDistance);
        lastResult = new MissionResult
        {
            elapsedSeconds = elapsed,
            collisions = drone.CollisionCount,
            batteryPercent = drone.BatteryPercent,
            jobPenalty = jobPenalty,
            landingDistance = landingDistance,
            score = score,
            grade = Grade(score),
            pilotStyle = pilotStyle
        };
        if (score > PlayerPrefs.GetInt(BestKey, 0))
        {
            PlayerPrefs.SetInt(BestKey, score);
            PlayerPrefs.SetString($"style_story_{Story.code}", pilotStyle);
        }
        if (distressCompleted) PlayerPrefs.SetInt($"distress_story_{Story.code}", 1);
        PlayerPrefs.Save();
        unlockedStory = FindUnlockedStory();
        RefreshCampaignProgress();
        presentation.PlaySuccess();
        drone.SetWind(Vector3.zero);
        drone.SetBatteryDrain(1f);
        SetStorm(0f);
        rescueTarget.gameObject.SetActive(false);
    }

    private void RefreshCampaignProgress()
    {
        completedStories = 0;
        completedDistressCalls = 0;
        var readiness = 0f;
        for (var site = 0; site < siteRestored.Length; site++) siteRestored[site] = false;
        foreach (var story in StoryCatalog.All)
        {
            var best = PlayerPrefs.GetInt($"best_story_{story.code}", 0);
            var distress = PlayerPrefs.GetInt($"distress_story_{story.code}", 0) != 0;
            if (best > 0)
            {
                completedStories++;
                siteRestored[story.targetSite] = true;
            }
            if (distress) completedDistressCalls++;
            readiness += Mathf.Clamp01(best / 900f + (distress ? 0.1f : 0f));
        }
        networkReadiness = Mathf.RoundToInt(readiness / StoryCatalog.All.Length * 100f);
        UpdateRecoveryWindows();
    }

    private void UpdateRecoveryWindows()
    {
        var sites = new[] { 1, 7 };
        for (var i = 0; i < recoveryWindows.Length; i++)
        {
            var material = recoveryWindows[i].sharedMaterial;
            var color = RecoveryColor(recoveryWindowColors[i], siteRestored[sites[i]]);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }
    }

    private static Color RecoveryColor(Color color, bool restored)
    {
        var brightness = restored ? 1f : 0.22f;
        return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
    }

    private static int FindUnlockedStory()
    {
        var story = 0;
        while (story < StoryCatalog.All.Length - 1 && PlayerPrefs.GetInt($"best_story_{StoryCatalog.All[story].code}", 0) > 0) story++;
        return story;
    }

    private void UpdateHazards()
    {
        var position = drone.transform.position;
        var wind = new Vector3(Story.wind, 0f, -Story.wind * 0.35f);
        var terrain = Terrain.activeTerrain;
        var clearance = terrain == null ? 15f : position.y - terrain.SampleHeight(position) - terrain.transform.position.y;
        terrainAlert = !drone.ReturningHome && clearance < 3f && body.linearVelocity.y < -1.2f;
        if (terrainAlert && Time.unscaledTime >= terrainWarningAfter)
        {
            presentation.PlayWarning();
            QuestInput.PulseRight(0.4f, 0.12f);
            terrainWarningAfter = Time.unscaledTime + 1.5f;
        }
        var multiplier = clearance < 10f ? 0.7f : clearance > 30f ? 1.25f : 1f;
        windZone = "OPEN";

        if (SiteDistance(1) < 32f || SiteDistance(4) < 32f || SiteDistance(7) < 32f)
        {
            multiplier *= 0.45f;
            windZone = "LEE";
        }
        else if (SiteDistance(0) < 48f || SiteDistance(2) < 48f || SiteDistance(6) < 48f)
        {
            multiplier *= 1.65f;
            wind.y -= Story.wind * 0.65f;
            windZone = "ROTOR";
        }
        else if (SiteDistance(3) < 52f)
        {
            multiplier *= 1.25f;
            wind += new Vector3(-Story.wind * 0.7f, 0f, Story.wind * 0.45f);
            windZone = "CROSSWIND";
        }

        var escalation = MissionModifier == 0 ? Mathf.Clamp01((Time.time - missionStart) / 180f) : 0f;
        multiplier *= Mathf.Lerp(1f, 1.55f, escalation);
        if (MissionModifier == 3)
        {
            var phase = Time.time * 1.7f + SelectedStory;
            wind += new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 0.7f) * 0.35f, Mathf.Cos(phase * 1.3f)) * Story.wind * 0.9f;
            windZone = "GUST";
        }

        if (windZone != previousWindZone && windZone != "OPEN" && Time.unscaledTime >= turbulencePulseAfter)
        {
            QuestInput.PulseRight(0.22f, 0.1f);
            turbulencePulseAfter = Time.unscaledTime + 1.5f;
        }
        previousWindZone = windZone;

        drone.SetWind(wind * multiplier);
        SetStorm(Mathf.Clamp01(Story.storm + escalation * 0.35f - (objectiveComplete ? 0.25f : 0f)));
    }

    private void UpdatePackageSway()
    {
        if (!package.gameObject.activeSelf || !packageBody.isKinematic) return;
        var localVelocity = drone.transform.InverseTransformDirection(body.linearVelocity);
        var target = Quaternion.Euler(Mathf.Clamp(localVelocity.z * 5f, -18f, 18f), 0f, Mathf.Clamp(-localVelocity.x * 5f, -18f, 18f));
        package.localRotation = Quaternion.Slerp(package.localRotation, target, 1f - Mathf.Exp(-5f * Time.deltaTime));
    }

    private void ResetPackage(bool visible)
    {
        if (!packageBody.isKinematic)
        {
            packageBody.linearVelocity = Vector3.zero;
            packageBody.angularVelocity = Vector3.zero;
        }
        packageBody.isKinematic = true;
        packageBody.useGravity = false;
        packageCollider.enabled = false;
        package.SetParent(drone.transform, false);
        package.localPosition = visible ? new Vector3(0f, -0.28f, 0f) : Vector3.zero;
        package.localRotation = Quaternion.identity;
        package.gameObject.SetActive(visible);
    }

    private bool DeliveryReady() => Vector3.Distance(drone.transform.position, ObjectiveTarget.position) <= 5f &&
                                    body.linearVelocity.magnitude < 1.6f &&
                                    Vector3.Dot(drone.transform.up, Vector3.up) > 0.94f;

    private float SiteDistance(int index)
    {
        var offset = drone.transform.position - missionSites[index].position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private static int DistressSiteFor(int storyIndex)
    {
        var story = StoryCatalog.All[storyIndex];
        var site = (story.targetSite + storyIndex + 1) % 8;
        while (site == story.firstSite || site == story.secondSite || site == story.targetSite)
            site = (site + 1) % 8;
        return site;
    }

    private static bool DistressHoldReady(float distance, float speed) => distance <= 8f && speed < 2f;

    private static bool ActionHoldReady(bool close, bool stable, bool sensorReady) => close && stable && sensorReady;

    private static bool ActionReady(StoryAction action, bool close, bool stable, bool deliveryReady)
        => action == StoryAction.Deliver ? deliveryReady : close && stable;

    private void ShowRescueTarget(int siteIndex)
    {
        var position = missionSites[siteIndex].position + new Vector3(2.4f, 0f, 1.5f);
        var terrain = Terrain.activeTerrain;
        if (terrain != null) position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        rescueTarget.position = position;
        rescueTarget.rotation = Quaternion.Euler(0f, siteIndex * 47f, 0f);
        rescueTarget.GetComponent<RescueMotion>().SetRelieved(false);
        rescueTarget.gameObject.SetActive(true);
    }

    private string ModifierName() => MissionModifier switch
    {
        0 => "CLOSING WEATHER / 嵐が時間とともに強まる",
        1 => "WEAK BATTERY / 電池消費 +35%",
        2 => "NAV INTERFERENCE / 航法信号が断続する",
        _ => "UNSTABLE AIR / 突風が頻発する"
    };

    private string PilotStyle(float elapsed, float landingDistance)
    {
        if (distressCompleted) return "RESCUE FIRST";
        if (Story.action == StoryAction.Deliver && fieldBonus >= 100) return "PRECISION DROP";
        if (drone.CollisionCount == 0 && landingDistance < 0.8f) return "STEADY HAND";
        if (elapsed < 240f) return "STORM RUNNER";
        if (drone.BatteryPercent >= 35f) return "ENERGY SAVER";
        return "MISSION FINISHER";
    }

    private Transform CurrentObjective => State != MissionState.Active ? homePad :
        Stage == StoryStage.Route ? distressAccepted && !distressCompleted ? missionSites[distressSite] : RouteTarget :
        Stage == StoryStage.Objective ? ObjectiveTarget : homePad;

    private string DistanceReadout(float distance) => NavigationAvailable ? $"{distance:0} m" : "NO FIX";

    private string DistressOffer()
    {
        if (!distressOffered || distressAccepted) return distressCompleted ? "\nSOS RESCUED / +150" : "";
        return $"\nMAYDAY  {missionSites[distressSite].name}  A / 1 救助へ  {Mathf.Max(0f, distressOfferUntil - Time.unscaledTime):0}s";
    }

    private void SetStorm(float strength)
    {
        var storyProgress = SelectedStory / (StoryCatalog.All.Length - 1f);
        var clearFog = Color.Lerp(new Color(0.46f, 0.55f, 0.65f), new Color(0.7f, 0.66f, 0.72f), storyProgress);
        var weatherFog = Color.Lerp(clearFog, new Color(0.72f, 0.76f, 0.8f), strength);
        if (blizzard != null)
        {
            var emission = blizzard.emission;
            emission.rateOverTime = Mathf.Lerp(8f, 90f, strength);
            var velocity = blizzard.velocityOverLifetime;
            var flow = drone == null ? Vector3.zero : drone.Wind;
            velocity.x = new ParticleSystem.MinMaxCurve(flow.x - 0.35f, flow.x + 0.35f);
            velocity.y = new ParticleSystem.MinMaxCurve(-1.8f, -0.7f);
            velocity.z = new ParticleSystem.MinMaxCurve(flow.z - 0.35f, flow.z + 0.35f);
        }
        if (sun != null)
        {
            sun.color = Color.Lerp(new Color(0.68f, 0.8f, 1f), new Color(1f, 0.86f, 0.72f), storyProgress);
            sun.intensity = Mathf.Lerp(1.05f, 1.35f, storyProgress) * Mathf.Lerp(1f, 0.62f, strength);
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(26f, 11f, storyProgress), Mathf.Lerp(-38f, -18f, storyProgress), 0f);
        }
        if (RenderSettings.skybox != null)
        {
            RenderSettings.skybox.SetColor("_SkyTint", Color.Lerp(new Color(0.34f, 0.48f, 0.68f), new Color(0.52f, 0.47f, 0.65f), storyProgress));
            RenderSettings.skybox.SetColor("_GroundColor", Color.Lerp(new Color(0.2f, 0.24f, 0.29f), new Color(0.32f, 0.28f, 0.34f), storyProgress));
        }
        RenderSettings.fogColor = weatherFog;
        var ambientDimmer = Mathf.Lerp(1f, 0.82f, strength);
        RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.48f, 0.58f, 0.72f), new Color(0.58f, 0.52f, 0.62f), storyProgress) * ambientDimmer;
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.38f, 0.45f, 0.54f), new Color(0.5f, 0.44f, 0.5f), storyProgress) * ambientDimmer;
        RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.32f, 0.4f, 0.5f), new Color(0.44f, 0.36f, 0.42f), storyProgress) * ambientDimmer;
        RenderSettings.fogStartDistance = Mathf.Lerp(90f, 25f, strength);
        RenderSettings.fogEndDistance = Mathf.Lerp(650f, 100f, strength);
    }

    private void UpdateDisplay()
    {
        if (statusText == null || drone == null) return;
        fpv.SetPanelVisible(State != MissionState.FreeFlight && views.CurrentMode == ViewMode.Operator);

        var objective = CurrentObjective;
        var distance = Vector3.Distance(drone.transform.position, objective.position);
        var elapsed = State == MissionState.Active ? Time.time - missionStart : 0f;
        var navigationAvailable = State != MissionState.Active || NavigationAvailable;
        briefingIndex.text = Story.code;
        UpdateObjectiveLights(navigationAvailable ? objective : null);
        var warning = drone.IsStandby ? "STANDBY  /  SELECT FLIGHT" : !drone.MotorsArmed ? "MOTORS OFF  /  B ARM" : drone.ReturningHome ? "RTH  /  AUTO LANDING" : drone.GeofenceWarning ? "GEOFENCE  /  RETURN HOME" : drone.BatteryPercent < 20f ? "LOW BATTERY" : State == MissionState.Active && terrainAlert ? "PULL UP" : !navigationAvailable ? "NAV SIGNAL LOST" : "";
        var heading = Mathf.Repeat(drone.transform.eulerAngles.y, 360f);
        headingValue.text = $"{Mathf.RoundToInt(heading) % 360:000}°";
        var objectiveDirection = objective.position - drone.transform.position;
        var objectiveBearing = Mathf.Repeat(Mathf.Atan2(objectiveDirection.x, objectiveDirection.z) * Mathf.Rad2Deg, 360f);
        var turn = Mathf.DeltaAngle(heading, objectiveBearing);
        var horizontalSpeed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        var altitude = Mathf.Max(0f, drone.transform.position.y - homePad.position.y);
        var windBearing = Mathf.Repeat(Mathf.Atan2(drone.Wind.x, drone.Wind.z) * Mathf.Rad2Deg, 360f);
        var terrain = Terrain.activeTerrain;
        var groundClearance = terrain == null ? altitude : Mathf.Max(0f, drone.transform.position.y - terrain.SampleHeight(drone.transform.position) - terrain.transform.position.y);
        var showTaskCue = State == MissionState.Active && views.CurrentMode != ViewMode.Operator;
        taskCue.transform.parent.gameObject.SetActive(showTaskCue);
        if (showTaskCue)
            taskCue.text = distressOffered && !distressAccepted
                ? $"MAYDAY  //  [A / 1] RESPOND  //  {Mathf.Max(0f, distressOfferUntil - Time.unscaledTime):0}s"
                : distressAccepted && !distressCompleted
                    ? $"RESCUE HOLD  //  STABILIZE  //  {actionProgress:0.0}/{DistressHoldSeconds:0.0}s"
                    : Stage == StoryStage.Route
                        ? $"ROUTE {routeStep + 1}/2  //  {RouteTarget.name.ToUpperInvariant()}  //  {DistanceReadout(distance)}"
                        : Stage == StoryStage.Objective
                            ? $"FIELD TASK  //  {FieldTaskLabel()}  //  {DistanceReadout(distance)}"
                            : $"EXTRACTION  //  HOME BASE  //  {DistanceReadout(distance)}";
        telemetryText.transform.parent.gameObject.SetActive(warning.Length > 0);
        telemetryText.text = warning;
        thermalOverlay.enabled = fpv.Thermal;
        var distressHold = State == MissionState.Active && Stage == StoryStage.Route && distressAccepted && !distressCompleted && distance <= 12f;
        var fieldHold = State == MissionState.Active && Stage == StoryStage.Objective && Story.holdSeconds > 0f && distance <= 8f;
        var fieldCue = State == MissionState.Active && Stage == StoryStage.Objective && Story.holdSeconds <= 0f && distance <= 10f;
        actionLabel.transform.parent.gameObject.SetActive(distressHold || fieldHold || fieldCue);
        if (distressHold || fieldHold || fieldCue)
        {
            var ready = ActionReady(Story.action, distance <= 5f, body.linearVelocity.magnitude < 1.6f, DeliveryReady());
            actionLabel.text = distressHold ? "SIGNAL LOCK" : fieldCue ? ready ? Story.action == StoryAction.Deliver ? "DROP READY" : "MARK READY" : "STABILIZE" : Story.action == StoryAction.Scan && !fpv.Thermal ? "THERMAL OFF" : Story.action == StoryAction.Scan ? "THERMAL SCAN" : "RELAY LINK";
            var progress = fieldCue ? ready ? 1f : 0f : actionProgress / (distressHold ? DistressHoldSeconds : Story.holdSeconds);
            actionFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(progress), 1f, 1f);
        }
        speedFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(horizontalSpeed / drone.maxHorizontalSpeed), 1f, 1f);
        altitudeFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(groundClearance / 30f), 1f, 1f);
        batteryFill.rectTransform.localScale = new Vector3(drone.BatteryPercent / 100f, 1f, 1f);
        speedValue.text = $"<b>{horizontalSpeed:0.0}</b> <size=15><color=#A5B5BC>m/s</color></size>";
        altitudeValue.text = $"<b>{groundClearance:0.0}</b> <size=15><color=#A5B5BC>m AGL</color></size>";
        batteryValue.text = $"<b>{drone.BatteryPercent:0}</b><size=15><color=#A5B5BC>%</color></size>";
        altitudeFill.color = terrainAlert ? new Color(1f, 0.34f, 0.08f) : Color.white;
        batteryFill.color = drone.BatteryPercent < 20f ? new Color(1f, 0.34f, 0.08f) : drone.BatteryPercent < 40f ? new Color(1f, 0.75f, 0.15f) : Color.white;
        windNeedle.gameObject.SetActive(drone.Wind.magnitude >= 0.05f);
        windNeedle.localRotation = Quaternion.Euler(0f, 0f, -Mathf.DeltaAngle(heading, windBearing));
        windValue.text = drone.Wind.magnitude < 0.05f ? "<b>CALM</b>" : $"<b>{drone.Wind.magnitude:0.0}</b> <size=15><color=#A5B5BC>m/s</color></size>";
        navigationNeedle.gameObject.SetActive(navigationAvailable);
        navigationNeedle.localRotation = Quaternion.Euler(0f, 0f, -turn);
        navigationValue.text = navigationAvailable ? $"<b>{distance:0}</b> <size=15><color=#A5B5BC>m</color></size>" : "<b>NO FIX</b>";

        var roll = Mathf.DeltaAngle(0f, drone.transform.eulerAngles.z);
        var pitch = Mathf.DeltaAngle(0f, drone.transform.eulerAngles.x);
        artificialHorizon.localRotation = Quaternion.Euler(0f, 0f, -roll);
        artificialHorizon.anchoredPosition = new Vector2(0f, Mathf.Clamp(pitch * 3f, -90f, 90f));

        if (State == MissionState.FreeFlight)
        {
            UpdateMenuButtons();
            if (menuPage == MenuPage.Main)
                statusText.text = MissionCard("WHITEOUT  //  MOUNTAIN RESCUE", "DRONE RESCUE", "雪山の救助ネットワークへ。\n状況を読み、FPVで現場を処理する。\n<color=#81909B>NETWORK {networkReadiness}%  //  CLEARED {completedStories}/{StoryCatalog.All.Length}</color>", "[A / 1] SELECT     [X / 2] NEXT     [R-STICK / 3] PREV");
            else if (menuPage == MenuPage.Missions)
            {
                var best = PlayerPrefs.GetInt(BestKey, 0);
                statusText.text = MissionCard($"{ActName}  //  FLIGHT {SelectedStory + 1:00}/{StoryCatalog.All.Length:00}", Story.title,
                    $"{Intel("STATUS", best > 0 ? $"CLEARED  //  BEST {best}" : "READY")}\n{Intel("FIELD TASK", FieldTaskLabel())}\n{Intel("WEATHER", $"WIND {Story.wind:0.0} m/s  //  VIS {(1f - Story.storm) * 100f:0}%")}", "[A / 1] SELECT     [X / 2] NEXT     [R-STICK / 3] PREV");
            }
            else if (menuPage == MenuPage.Practice)
                statusText.text = MissionCard("FLIGHT SCHOOL  //  PRACTICE RANGE", "FREE FLIGHT", "基地周辺で安全に操縦を練習する。\n左スティックで高度・ヨー、右スティックで前後左右。\n<color=#81909B>アシストは高度維持とSELF-LEVEL付き。B / Rで離陸・着陸。</color>", "[A / 1] SELECT     [X / 2] NEXT     [R-STICK / 3] PREV");
            else
                statusText.text = MissionCard("SYSTEM  //  PILOT SETTINGS", "SETTINGS", $"{Intel("FLIGHT MODE", selectedMode.ToString().ToUpper())}\n{Intel("STABILITY", selectedMode == FlightMode.Assist ? drone.AssistProfileName : "OFF / FPV RATE")}\n{Intel("CAMERA", views.CurrentMode.ToString().ToUpper())}\n視点はGRIP / Cでも切り替え可能。\n<color=#81909B>CRUISEとCINEMAは強い水平復帰と低いバンク角を使う。</color>", "[A / 1] SELECT     [X / 2] NEXT     [R-STICK / 3] PREV");
            return;
        }

        foreach (var button in menuButtons) button.gameObject.SetActive(false);
        statusText.rectTransform.offsetMax = new Vector2(-28f, -42f);

        if (State == MissionState.Active)
        {
            if (elapsed < 7f)
                statusText.text = MissionCard($"{ActName}  //  OPERATION {Story.code}", Story.title,
                    $"{Intel("PRIMARY", FieldTaskLabel())}\n{Intel("ROUTE", "TWO CHECKPOINTS") }\n{Intel("FLIGHT PROFILE", ModifierName())}", AbortHint());
            else if (Stage == StoryStage.Route)
                statusText.text = distressAccepted && !distressCompleted
                    ? MissionCard("PRIORITY SIGNAL  //  MAYDAY", "OPTIONAL RESCUE",
                        $"{Intel("ACTION", $"{missionSites[distressSite].name}の上空で低速ホバリング")}\nSIGNAL {actionProgress:0.0}/{DistressHoldSeconds:0.0}s  //  RANGE {DistanceReadout(distance)}", AbortHint())
                    : MissionCard($"OPERATION {Story.code}  //  ROUTE {routeStep + 1}/2", "ROUTE UPDATE",
                        $"{Intel("CHECKPOINT", RouteTarget.name)}\n{Intel("RANGE", DistanceReadout(distance))}{DistressOffer()}", AbortHint());
            else if (Stage == StoryStage.Objective)
                statusText.text = MissionCard($"OPERATION {Story.code}  //  ON STATION", "FIELD TASK",
                    $"{Intel("PRIMARY", Story.objective)}\n{ActionPrompt()}\n{Intel("RANGE", DistanceReadout(distance))}", AbortHint());
            else
                statusText.text = MissionCard($"OPERATION {Story.code}  //  RETURN", "EXTRACTION",
                    $"{Intel("STATUS", "FIELD TASK COMPLETE")}{DistressOutcome()}\n{Intel("HOME", $"基地へ帰還・着陸  //  {DistanceReadout(distance)}")}", $"[B / R] AUTO RETURN     {AbortHint()}");
            return;
        }

        statusText.text = State == MissionState.Completed
            ? MissionCard($"AFTER ACTION REPORT  //  OPERATION {Story.code}", "MISSION COMPLETE",
                $"{Intel("RESULT", $"{lastResult.score} PTS  //  GRADE {lastResult.grade}  //  {lastResult.elapsedSeconds:0}s")}\nBAT {lastResult.batteryPercent:0}%  //  IMPACT {lastResult.collisions}  //  PENALTY -{lastResult.jobPenalty}\nPILOT STYLE {lastResult.pilotStyle}  //  FIELD +{fieldBonus}{(distressCompleted ? "  //  SOS +150" : "")}\n<color=#81909B>NETWORK {networkReadiness}%  //  CLEARED {completedStories}/{StoryCatalog.All.Length}</color>",
                "[A / 1] REPLAY     [X / 2] NEXT STORY")
            : MissionCard($"UPLINK TERMINATED  //  OPERATION {Story.code}", "SIGNAL LOST",
                "任務は完了しなかった。\n嵐が強まる前に再出動せよ。", "[A / 1] RETRY     [X / 2] MENU");
    }

    private static T Hud<T>(string name) where T : Component => GameObject.Find(name).GetComponent<T>();

    private static string Intel(string label, string value) => $"<color=#38DCF2><b>{label}</b></color>  {value}";

    private void UpdateMenuButtons()
    {
        var labels = menuPage == MenuPage.Main
            ? new[] { "START FLIGHT", "PRACTICE RANGE", "SETTINGS" }
            : menuPage == MenuPage.Missions
                ? new[] { "DEPLOY", "NEXT MISSION", $"FLIGHT: {selectedMode.ToString().ToUpper()}", "BACK" }
                : menuPage == MenuPage.Practice
                    ? new[] { "BACK TO MENU" }
                : new[] { $"FLIGHT: {selectedMode.ToString().ToUpper()}", $"ASSIST: {drone.AssistProfileName}", $"CAMERA: {views.CurrentMode.ToString().ToUpper()}", "BACK" };
        statusText.rectTransform.offsetMax = new Vector2(-330f, -42f);
        for (var i = 0; i < menuButtons.Length; i++)
        {
            var active = i < labels.Length;
            menuButtons[i].gameObject.SetActive(active);
            if (!active) continue;
            var selected = i == menuSelection;
            menuButtons[i].color = selected ? new Color(1f, 0.3f, 0.08f, 0.94f) : new Color(0.08f, 0.13f, 0.17f, 0.94f);
            menuButtonTexts[i].text = selected ? $">  {labels[i]}" : labels[i];
            menuButtonTexts[i].color = selected ? Color.white : new Color(0.75f, 0.84f, 0.87f);
        }
    }

    private static string MissionCard(string channel, string title, string body, string footer)
        => $"<size=15><color=#81909B>{channel}</color></size>\n<size=32><b>{title}</b></size>\n<size=18>{body}</size>\n<size=14><color=#FF6A2A><b>{footer}</b></color></size>";

    private string ActionPrompt()
    {
        if (Story.action == StoryAction.Relay)
            return $"5m以内で静止維持  {actionProgress:0}/{Story.holdSeconds:0}s";
        if (Story.action == StoryAction.Scan)
            return $"Y / M サーマル  静止 {actionProgress:0}/{Story.holdSeconds:0}s → TRIGGER";
        if (Story.action == StoryAction.Deliver)
            return $"5m以内・低速・水平で投下  {(DeliveryReady() ? "DROP READY" : "STABILIZE")}";
        return "5m以内・低速で TRIGGER / SPACE 設置";
    }

    private string FieldTaskLabel() => Story.action switch
    {
        StoryAction.Deliver => "DROP SUPPLIES",
        StoryAction.Scan => "THERMAL SCAN",
        StoryAction.Relay => "UPLINK RELAY",
        _ => "PLACE MARKER"
    };

    private string AbortHint() => Time.unscaledTime < abortConfirmationUntil ? "X / 2 もう一度で中止" : "X / 2 中止";

    private string DistressOutcome() => distressCompleted ? $"\n{missionSites[distressSite].name}の救難信号を救助隊へ接続した。 +150" : distressMissed ? "\n救難信号には応答できなかった。 -75" : "";

    private void UpdateObjectiveLights(Transform objective)
    {
        for (var i = 0; i < siteLights.Length; i++)
        {
            var selected = State == MissionState.Active && objective == missionSites[i];
            siteLights[i].intensity = selected ? 1.5f : 0f;
            siteLights[i].range = selected ? 14f : 0f;
        }
        var homeSelected = State == MissionState.Active && objective == homePad;
        homeLight.intensity = homeSelected ? 1.2f : 0f;
        homeLight.range = homeSelected ? 12f : 0f;
    }

    private static string Grade(int score) => score >= 900 ? "S" : score >= 750 ? "A" : score >= 600 ? "B" : score >= 450 ? "C" : "D";
}
