using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class DroneController : MonoBehaviour
{
    public FlightMode flightMode = FlightMode.Assist;
    public float verticalSpeed = 2.5f;
    public float altitudeGain = 18f;
    public float attitudeTorque = 10f;
    public float manualTorque = 4f;
    public float yawTorque = 1.8f;
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
    public bool GeofenceWarning { get; private set; }
    public Vector3 Wind { get; private set; }
    public bool IsLanded => transform.position.y <= homePosition.y + 0.5f && body.linearVelocity.sqrMagnitude < 0.35f;

    private Rigidbody body;
    private float initialBatterySeconds;
    private float targetAltitude;
    private Vector3 homePosition;
    private float returnAltitude;
    private float targetYaw;
    private float manualLift = 1f;
    private float batteryDrainMultiplier = 1f;
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
        targetAltitude = transform.position.y;
        targetYaw = transform.eulerAngles.y;
    }

    public void ResetToIdle(Vector3 home)
    {
        homePosition = home;
        body.position = home + Vector3.up * 0.42f;
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        batterySeconds = initialBatterySeconds;
        targetAltitude = home.y + 0.8f;
        CollisionCount = 0;
        Crashed = false;
        MotorsArmed = false;
        ReturningHome = false;
        returnAltitude = home.y + 45f;
        targetYaw = 0f;
        manualLift = 1f;
        GeofenceWarning = false;
    }

    public void BeginMission(Vector3 home)
    {
        ResetToIdle(home);
        MotorsArmed = true;
    }

    public void SetFlightMode(FlightMode mode)
    {
        flightMode = mode;
        if (mode != FlightMode.Assist) return;
        targetAltitude = transform.position.y;
        targetYaw = transform.eulerAngles.y;
    }

    public void SetWind(Vector3 wind) => Wind = wind;

    public void SetBatteryDrain(float multiplier) => batteryDrainMultiplier = Mathf.Max(0.1f, multiplier);

    private void Update()
    {
        if (Crashed || !QuestInput.Current.rightSecondaryDown) return;
        if (MotorsArmed)
        {
            returnAltitude = Mathf.Max(transform.position.y, homePosition.y + 45f);
            ReturningHome = true;
        }
        else
        {
            MotorsArmed = true;
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
        var homeDistance = Vector3.Distance(transform.position, homePosition);
        GeofenceWarning = homeDistance > geofenceWarningDistance;
        if (homeDistance > geofenceReturnDistance && !ReturningHome)
        {
            returnAltitude = Mathf.Max(transform.position.y, homePosition.y + 45f);
            ReturningHome = true;
        }

        batterySeconds = Mathf.Max(0f, batterySeconds - dt * BatteryLoad(throttle, horizontal.magnitude, Wind.magnitude) * batteryDrainMultiplier);
        if (batterySeconds <= 0f)
        {
            Crashed = true;
            MotorsArmed = false;
            QuestInput.PulseRight(0.5f, 0.2f);
            return;
        }

        if (ReturningHome) ApplyReturnHome(dt);
        else ApplyFlight(left, right, dt);
        if (!MotorsArmed) return;
        var gust = 1f + Mathf.Sin(Time.time * 0.73f + transform.position.z * 0.04f) * 0.25f;
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
            body.AddForce(transform.up * Physics.gravity.magnitude * manualLift, ForceMode.Acceleration);
            body.AddRelativeTorque(new Vector3(-right.y * manualTorque, left.x * yawTorque, -right.x * manualTorque), ForceMode.Acceleration);
        }
    }

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
        var delta = homePosition - transform.position;
        var planar = new Vector3(delta.x, 0f, delta.z);
        var target = planar.magnitude > 20f
            ? returnAltitude
            : Mathf.Lerp(homePosition.y + 0.2f, returnAltitude, planar.magnitude / 20f);
        if (planar.sqrMagnitude > 0.01f)
            targetYaw = Mathf.MoveTowardsAngle(targetYaw, Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg, 55f * dt);
        var desiredVelocity = planar.normalized * Mathf.Min(maxHorizontalSpeed, planar.magnitude * 0.35f);
        ApplyAssistedFlight(desiredVelocity, target, targetYaw);
        if (planar.magnitude >= 0.8f || !IsLanded) return;
        ReturningHome = false;
        MotorsArmed = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        QuestInput.PulseRight(0.2f, 0.1f);
    }

    private void ApplyAssistedFlight(Vector3 desiredVelocity, float desiredAltitude, float yaw)
    {
        var planarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        var maxAcceleration = Mathf.Tan(maxTiltDegrees * Mathf.Deg2Rad) * Physics.gravity.magnitude;
        var desiredAcceleration = Vector3.ClampMagnitude((desiredVelocity - planarVelocity) * 2.2f, maxAcceleration);
        var desiredUp = (Vector3.up + desiredAcceleration / Physics.gravity.magnitude).normalized;
        var desiredForward = Vector3.ProjectOnPlane(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, desiredUp).normalized;
        ApplyAttitude(Quaternion.LookRotation(desiredForward, desiredUp));

        var verticalAcceleration = Mathf.Clamp((desiredAltitude - transform.position.y) * altitudeGain - body.linearVelocity.y * 4f, -7f, 7f);
        var tiltCompensation = Mathf.Max(0.35f, Vector3.Dot(transform.up, Vector3.up));
        body.AddForce(transform.up * Mathf.Max(0f, Physics.gravity.magnitude + verticalAcceleration) / tiltCompensation, ForceMode.Acceleration);
    }

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
        QuestInput.PulseRight(0.8f, 0.3f);
    }
}
