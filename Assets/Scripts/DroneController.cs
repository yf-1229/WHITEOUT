using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class DroneController : MonoBehaviour
{
    public FlightMode flightMode = FlightMode.Assist;
    public float verticalSpeed = 2.5f;
    public float altitudeGain = 18f;
    public float attitudeTorque = 10f;
    public float assistTiltRecovery = 150f;
    public float assistTiltDamping = 28f;
    public float manualRateDegrees = 200f;
    public float manualYawRateDegrees = 150f;
    public float manualRateGain = 7f;
    public float maxTiltDegrees = 28f;
    public float maxHorizontalSpeed = 8f;
    [Range(0f, 0.3f)] public float stickDeadzone = 0.08f;
    [Range(0f, 1f)] public float stickExpo = 0.35f;
    public float motorResponse = 5f;
    public float batterySeconds = 360f;
    public float geofenceWarningDistance = 330f;
    public float geofenceReturnDistance = 390f;

    public float BatteryPercent => batterySeconds / initialBatterySeconds * 100f;
    public int CollisionCount { get; private set; }
    public bool Crashed { get; private set; }
    public bool MotorsArmed { get; private set; }
    public bool ReturningHome { get; private set; }
    public bool IsStandby => standby;
    public bool GeofenceWarning { get; private set; }
    public Vector3 Wind { get; private set; }
    public float MotorLoad { get; private set; }
    public string AssistProfileName => assistProfile == 0 ? "SPORT" : assistProfile == 1 ? "CRUISE" : "CINEMA";
    public bool IsLanded => body.position.y <= homePosition.y + 0.5f && body.linearVelocity.sqrMagnitude < 0.35f;

    private Rigidbody body;
    private float initialBatterySeconds;
    private float targetAltitude;
    private Vector3 homePosition;
    private float returnAltitude;
    private float targetYaw;
    private float manualLift = 1f;
    private float batteryDrainMultiplier = 1f;
    private int assistProfile = 1;
    private bool standby;
    private const float CrashSpeed = 6f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.mass = 0.65f;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearDamping = 0.15f;
        body.angularDamping = 0.6f;
        initialBatterySeconds = batterySeconds;
        homePosition = transform.position;
        returnAltitude = homePosition.y + 45f;
        targetAltitude = body.position.y;
        targetYaw = transform.eulerAngles.y;
    }

    public void ResetToIdle(Vector3 home)
    {
        homePosition = home;
        var spawnPosition = home + Vector3.up * 0.65f;
        body.interpolation = RigidbodyInterpolation.None;
        body.position = spawnPosition;
        body.rotation = Quaternion.identity;
        transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        batterySeconds = initialBatterySeconds;
        targetAltitude = home.y + 0.8f;
        CollisionCount = 0;
        Crashed = false;
        MotorsArmed = false;
        MotorLoad = 0f;
        ReturningHome = false;
        returnAltitude = home.y + 45f;
        targetYaw = 0f;
        manualLift = 1f;
        GeofenceWarning = false;
    }

    public void BeginMission(Vector3 home)
    {
        ResetToIdle(home);
        body.isKinematic = false;
        body.useGravity = true;
        standby = false;
        MotorsArmed = true;
        MotorLoad = 1f;
        body.WakeUp();
    }

    public void SetStandby(Vector3 home)
    {
        ResetToIdle(home);
        var restingPosition = home + Vector3.up * 0.33f;
        body.position = restingPosition;
        transform.position = restingPosition;
        body.isKinematic = true;
        standby = true;
    }

    public void SetFlightMode(FlightMode mode)
    {
        flightMode = mode;
        if (mode != FlightMode.Assist) return;
        targetAltitude = transform.position.y;
        targetYaw = transform.eulerAngles.y;
    }

    public void CycleAssistProfile() => assistProfile = (assistProfile + 1) % 3;

    public void SetAssistProfile(int profile) => assistProfile = Mathf.Clamp(profile, 0, 2);

    public void SetWind(Vector3 wind) => Wind = wind;

    public void SetBatteryDrain(float multiplier) => batteryDrainMultiplier = Mathf.Max(0.1f, multiplier);

    private void Update()
    {
        if (standby || Crashed || !QuestInput.Current.rightSecondaryDown) return;
        if (MotorsArmed)
        {
            returnAltitude = Mathf.Max(body.position.y, homePosition.y + 45f);
            ReturningHome = true;
        }
        else
        {
            MotorsArmed = true;
            MotorLoad = 1f;
            ReturningHome = false;
            targetAltitude = homePosition.y + 1.2f;
            body.WakeUp();
            QuestInput.PulseRight(0.35f, 0.12f);
        }
    }

    private void FixedUpdate()
    {
        if (Crashed) return;
        if (!MotorsArmed) return;
        var input = QuestInput.Current;
        var dt = Time.fixedDeltaTime;
        var left = ShapeStick(input.leftStick);
        var right = ShapeStick(input.rightStick);
        var throttle = Mathf.Abs(left.y);
        var horizontal = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        var homeDistance = Vector3.Distance(body.position, homePosition);
        GeofenceWarning = homeDistance > geofenceWarningDistance;
        if (homeDistance > geofenceReturnDistance && !ReturningHome)
        {
            returnAltitude = Mathf.Max(body.position.y, homePosition.y + 45f);
            ReturningHome = true;
        }

        batterySeconds = Mathf.Max(0f, batterySeconds - dt * BatteryLoad(throttle, horizontal.magnitude, Wind.magnitude) * batteryDrainMultiplier);
        if (batterySeconds <= 0f)
        {
            Crashed = true;
            MotorsArmed = false;
            MotorLoad = 0f;
            QuestInput.PulseRight(0.5f, 0.2f);
            return;
        }

        if (ReturningHome) ApplyReturnHome(dt);
        else ApplyFlight(left, right, dt);
        if (!MotorsArmed) return;
        var gust = 1f + Mathf.Sin(Time.time * 0.73f + body.position.z * 0.04f) * 0.25f;
        body.AddForce((Wind * gust - horizontal) * 0.22f, ForceMode.Acceleration);

        if (horizontal.magnitude > maxHorizontalSpeed)
            body.AddForce(-horizontal.normalized * (horizontal.magnitude - maxHorizontalSpeed) * 3f, ForceMode.Acceleration);
    }

    private void ApplyFlight(Vector2 left, Vector2 right, float dt)
    {
        if (flightMode == FlightMode.Assist)
        {
            if (Mathf.Abs(left.y) > 0.05f)
                targetAltitude = Mathf.Clamp(targetAltitude + left.y * verticalSpeed * dt, homePosition.y + 0.25f, homePosition.y + 75f);
            targetYaw = Mathf.Repeat(targetYaw + left.x * 75f * dt, 360f);
            var heading = Quaternion.Euler(0f, targetYaw, 0f);
            ApplyAssistedFlight(heading * new Vector3(right.x, 0f, right.y) * maxHorizontalSpeed, targetAltitude, targetYaw);
        }
        else
        {
            manualLift = Mathf.Lerp(manualLift, 1f + left.y * 0.8f, 1f - Mathf.Exp(-motorResponse * dt));
            MotorLoad = manualLift;
            body.AddForce(body.rotation * Vector3.up * Physics.gravity.magnitude * manualLift, ForceMode.Acceleration);
            var localRate = Quaternion.Inverse(body.rotation) * body.angularVelocity;
            body.AddRelativeTorque((ManualRateTarget(left, right) - localRate) * manualRateGain, ForceMode.Acceleration);
        }
    }

    private Vector3 ManualRateTarget(Vector2 left, Vector2 right) => new Vector3(
        -right.y * manualRateDegrees,
        left.x * manualYawRateDegrees,
        -right.x * manualRateDegrees) * Mathf.Deg2Rad;

    private Vector2 ShapeStick(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);
        var magnitude = input.magnitude;
        if (magnitude <= stickDeadzone) return Vector2.zero;
        magnitude = Mathf.InverseLerp(stickDeadzone, 1f, magnitude);
        return input.normalized * Mathf.Lerp(magnitude, magnitude * magnitude * magnitude, stickExpo);
    }

    private static float BatteryLoad(float throttle, float speed, float wind)
        => 0.65f + Mathf.Clamp01(throttle) * 0.55f + Mathf.Clamp01(speed / 8f) * 0.35f + Mathf.Clamp01(wind / 6f) * 0.15f;

    private void ApplyReturnHome(float dt)
    {
        var delta = homePosition - body.position;
        var planar = new Vector3(delta.x, 0f, delta.z);
        var safeReturnAltitude = SafeReturnAltitude(Terrain.activeTerrain, body.position, planar.normalized, returnAltitude);
        var target = planar.magnitude > 20f
            ? safeReturnAltitude
            : Mathf.Lerp(homePosition.y + 0.2f, safeReturnAltitude, planar.magnitude / 20f);
        if (planar.sqrMagnitude > 0.01f)
            targetYaw = Mathf.MoveTowardsAngle(targetYaw, Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg, 55f * dt);
        var desiredVelocity = planar.normalized * Mathf.Min(maxHorizontalSpeed, planar.magnitude * 0.35f);
        ApplyAssistedFlight(desiredVelocity, target, targetYaw);
        if (planar.magnitude >= 0.8f || !IsLanded) return;
        ReturningHome = false;
        MotorsArmed = false;
        MotorLoad = 0f;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        QuestInput.PulseRight(0.2f, 0.1f);
    }

    private static float SafeReturnAltitude(Terrain terrain, Vector3 position, Vector3 direction, float minimum)
    {
        if (terrain == null || direction.sqrMagnitude < 0.5f) return minimum;
        for (var distance = 8f; distance <= 24f; distance += 8f)
            minimum = Mathf.Max(minimum, terrain.SampleHeight(position + direction * distance) + terrain.transform.position.y + 12f);
        return minimum;
    }

    private void ApplyAssistedFlight(Vector3 desiredVelocity, float desiredAltitude, float yaw)
    {
        var planarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        var maxAcceleration = Mathf.Tan(AssistTiltLimit * Mathf.Deg2Rad) * Physics.gravity.magnitude;
        var desiredAcceleration = Vector3.ClampMagnitude((desiredVelocity - planarVelocity) * 2.2f, maxAcceleration);
        var desiredUp = (Vector3.up + desiredAcceleration / Physics.gravity.magnitude).normalized;
        var desiredForward = Vector3.ProjectOnPlane(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, desiredUp).normalized;
        ApplyAttitude(Quaternion.LookRotation(desiredForward, desiredUp));
        ApplyAssistStabilization(desiredUp);

        var verticalAcceleration = Mathf.Clamp((desiredAltitude - body.position.y) * altitudeGain - body.linearVelocity.y * 4f, -7f, 7f);
        var bodyUp = body.rotation * Vector3.up;
        var tiltCompensation = Mathf.Max(0.35f, Vector3.Dot(bodyUp, Vector3.up));
        MotorLoad = AssistedMotorLoad(verticalAcceleration, tiltCompensation);
        body.AddForce(bodyUp * Physics.gravity.magnitude * MotorLoad, ForceMode.Acceleration);
    }

    private static float AssistedMotorLoad(float verticalAcceleration, float tiltCompensation)
        => Mathf.Clamp((Physics.gravity.magnitude + verticalAcceleration) / (Physics.gravity.magnitude * Mathf.Max(0.35f, tiltCompensation)), 0f, 2f);

    private void ApplyAssistStabilization(Vector3 desiredUp)
    {
        var bodyUp = body.rotation * Vector3.up;
        var tiltRate = body.angularVelocity - Vector3.Project(body.angularVelocity, bodyUp);
        if (Vector3.Dot(bodyUp, Vector3.up) < 0.3f)
            body.angularVelocity = Vector3.Project(body.angularVelocity, bodyUp);
        body.AddTorque(Vector3.Cross(bodyUp, desiredUp) * assistTiltRecovery * AssistRecovery - tiltRate * assistTiltDamping * AssistRecovery, ForceMode.Acceleration);
    }

    private float AssistTiltLimit => assistProfile == 0 ? maxTiltDegrees : assistProfile == 1 ? 23f : 18f;
    private float AssistRecovery => assistProfile == 0 ? 1f : assistProfile == 1 ? 1.2f : 1.45f;

    private void ApplyAttitude(Quaternion desiredRotation)
    {
        var error = desiredRotation * Quaternion.Inverse(body.rotation);
        error.ToAngleAxis(out var angle, out var axis);
        if (angle > 180f) angle -= 360f;
        body.AddTorque(axis * (angle * Mathf.Deg2Rad * attitudeTorque) - body.angularVelocity * 2.8f, ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Crashed || collision.relativeVelocity.magnitude < 0.4f) return;
        var impact = collision.relativeVelocity.magnitude;
        CollisionCount++;
        QuestInput.PulseRight(Mathf.Clamp01(impact / CrashSpeed) * 0.45f, 0.08f);
        if (impact < CrashSpeed) return;
        Crashed = true;
        MotorsArmed = false;
        MotorLoad = 0f;
        QuestInput.PulseRight(0.8f, 0.3f);
    }
}
