using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class DroneController : MonoBehaviour
{
    public FlightMode flightMode = FlightMode.Assist;
    public float throttleForce = 12f;
    public float verticalSpeed = 2.5f;
    public float altitudeGain = 18f;
    public float attitudeTorque = 2.8f;
    public float yawTorque = 1.8f;
    public float maxHorizontalSpeed = 8f;
    public float batterySeconds = 360f;

    public float BatteryPercent => batterySeconds / initialBatterySeconds * 100f;
    public int CollisionCount { get; private set; }
    public bool Crashed { get; private set; }
    public bool MotorsArmed { get; private set; } = true;
    public bool ReturningHome { get; private set; }
    public bool GeofenceWarning { get; private set; }
    public Vector3 Wind { get; private set; }
    public bool IsLanded => transform.position.y <= homePosition.y + 0.5f && body.linearVelocity.sqrMagnitude < 0.35f;

    private Rigidbody body;
    private float initialBatterySeconds;
    private float targetAltitude;
    private bool takingOff;
    private Vector3 homePosition;
    private float returnAltitude;
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
        returnAltitude = homePosition.y + 6f;
        targetAltitude = transform.position.y;
    }

    public void BeginMission(Vector3 home)
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
        MotorsArmed = true;
        ReturningHome = false;
        returnAltitude = home.y + 6f;
        takingOff = false;
        GeofenceWarning = false;
    }

    public void SetWind(Vector3 wind) => Wind = wind;

    private void Update()
    {
        if (Crashed || !QuestInput.Current.rightSecondaryDown) return;
        if (MotorsArmed)
        {
            takingOff = false;
            returnAltitude = Mathf.Max(transform.position.y, homePosition.y + 6f);
            ReturningHome = true;
        }
        else
        {
            MotorsArmed = true;
            takingOff = true;
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
        var left = Vector2.ClampMagnitude(input.leftStick, 1f);
        var right = Vector2.ClampMagnitude(input.rightStick, 1f);
        var throttle = Mathf.Abs(left.y);
        var homeDistance = Vector3.Distance(transform.position, homePosition);
        GeofenceWarning = homeDistance > 80f;
        if (homeDistance > 100f && !ReturningHome)
        {
            returnAltitude = Mathf.Max(transform.position.y, homePosition.y + 6f);
            ReturningHome = true;
        }

        batterySeconds = Mathf.Max(0f, batterySeconds - dt * (0.55f + throttle * 0.9f));
        if (batterySeconds <= 0f)
        {
            Crashed = true;
            MotorsArmed = false;
            QuestInput.PulseRight(0.5f, 0.2f);
            return;
        }

        if (takingOff) ApplyTakeoff();
        else if (ReturningHome) ApplyReturnHome();
        else ApplyFlight(left, right, dt);
        if (!MotorsArmed) return;
        body.AddForce(Wind, ForceMode.Acceleration);

        var horizontal = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        if (horizontal.magnitude > maxHorizontalSpeed)
        {
            horizontal = horizontal.normalized * maxHorizontalSpeed;
            body.linearVelocity = new Vector3(horizontal.x, body.linearVelocity.y, horizontal.z);
        }
    }

    private void ApplyFlight(Vector2 left, Vector2 right, float dt)
    {
        if (flightMode == FlightMode.Assist)
        {
            if (Mathf.Abs(left.y) > 0.05f)
                targetAltitude = Mathf.Clamp(targetAltitude + left.y * verticalSpeed * dt, homePosition.y + 0.25f, homePosition.y + 24f);
            var verticalError = targetAltitude - transform.position.y;
            body.AddForce(Vector3.up * (verticalError * altitudeGain - body.linearVelocity.y * 4f), ForceMode.Acceleration);
            StabilizeAttitude();
        }
        else
        {
            body.AddForce(Vector3.up * left.y * throttleForce, ForceMode.Acceleration);
        }

        var planarInput = transform.forward * right.y + transform.right * right.x;
        var desiredPlanarVelocity = planarInput * maxHorizontalSpeed;
        var planarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        body.AddForce((desiredPlanarVelocity - planarVelocity) * (flightMode == FlightMode.Assist ? 3.5f : 1.8f), ForceMode.Acceleration);
        body.AddRelativeTorque(new Vector3(-right.y, left.x * yawTorque, -right.x) * attitudeTorque, ForceMode.Acceleration);
    }

    private void ApplyReturnHome()
    {
        var delta = homePosition - transform.position;
        var planar = new Vector3(delta.x, 0f, delta.z);
        var target = planar.magnitude > 8f
            ? returnAltitude
            : Mathf.Lerp(homePosition.y + 0.2f, returnAltitude, planar.magnitude / 8f);
        body.AddForce(planar * 1.8f - new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z) * 1.2f, ForceMode.Acceleration);
        var verticalError = target - transform.position.y;
        body.AddForce(Vector3.up * (verticalError * altitudeGain - body.linearVelocity.y * 4f), ForceMode.Acceleration);
        StabilizeAttitude();
        if (planar.magnitude >= 0.8f || !IsLanded) return;
        ReturningHome = false;
        MotorsArmed = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        QuestInput.PulseRight(0.2f, 0.1f);
    }

    private void ApplyTakeoff()
    {
        body.AddForce(Vector3.up * ((targetAltitude - transform.position.y) * altitudeGain - body.linearVelocity.y * 4f), ForceMode.Acceleration);
        StabilizeAttitude();
        if (transform.position.y >= 1.1f) takingOff = false;
    }

    private void StabilizeAttitude()
    {
        var tilt = Vector3.Cross(transform.up, Vector3.up);
        body.AddTorque(tilt * attitudeTorque, ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Crashed || collision.relativeVelocity.magnitude < 0.4f) return;
        CollisionCount++;
        if (collision.relativeVelocity.magnitude < CrashSpeed) return;
        Crashed = true;
        MotorsArmed = false;
        QuestInput.PulseRight(0.8f, 0.3f);
    }
}
