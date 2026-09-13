using UnityEngine;

public sealed class DronePresentation : MonoBehaviour
{
    public Transform[] rotors;
    public Light navigationLight;

    private Rigidbody body;
    private DroneController drone;
    private AudioSource motor;
    private AudioSource wind;
    private AudioSource radio;
    private AudioClip warningCue;
    private AudioClip successCue;
    private AudioClip uplinkCue;
    private float rotorSpeed;

    private void Start()
    {
        body = GetComponent<Rigidbody>();
        drone = GetComponent<DroneController>();
        motor = gameObject.AddComponent<AudioSource>();
        motor.clip = CreateMotorLoop();
        motor.loop = true;
        motor.spatialBlend = 0.9f;
        motor.volume = 0.18f;
        motor.Play();
        wind = gameObject.AddComponent<AudioSource>();
        wind.clip = CreateWindLoop();
        wind.loop = true;
        wind.spatialBlend = 0f;
        wind.volume = 0f;
        wind.Play();
        radio = gameObject.AddComponent<AudioSource>();
        radio.spatialBlend = 0f;
        radio.volume = 0.35f;
        warningCue = CreateCue("RadioWarning", 880f, 660f);
        successCue = CreateCue("RadioSuccess", 660f, 990f);
        uplinkCue = CreateCue("RadioUplink", 1120f, 920f);
    }

    private void Update()
    {
        var armed = drone.MotorsArmed && !drone.Crashed;
        var load = drone.MotorLoad;
        var targetSpeed = armed ? 650f + load * 520f + body.linearVelocity.magnitude * 30f : 0f;
        rotorSpeed = Mathf.MoveTowards(rotorSpeed, targetSpeed, 1800f * Time.deltaTime);
        foreach (var rotor in rotors) rotor.Rotate(0f, rotorSpeed * Time.deltaTime, 0f, Space.Self);
        motor.pitch = 0.72f + load * 0.35f + Mathf.Clamp01(body.linearVelocity.magnitude / 8f) * 0.18f;
        motor.volume = Mathf.MoveTowards(motor.volume, armed ? Mathf.Lerp(0.12f, 0.28f, Mathf.Clamp01(load / 1.8f)) : 0f, 0.4f * Time.deltaTime);
        var airspeed = body.linearVelocity.magnitude + drone.Wind.magnitude;
        wind.volume = Mathf.MoveTowards(wind.volume, Mathf.Clamp01(airspeed / 8f) * 0.22f, 0.25f * Time.deltaTime);
        wind.pitch = Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(airspeed / 10f));
        if (navigationLight != null) navigationLight.enabled = armed && Mathf.Repeat(Time.time, 1f) < 0.7f;
    }

    private static AudioClip CreateMotorLoop()
    {
        const int sampleRate = 22050;
        var samples = new float[sampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / sampleRate;
            samples[i] = (Mathf.Sin(t * 565f) + Mathf.Sin(t * 1130f) * 0.35f) * 0.08f;
        }
        var clip = AudioClip.Create("ProceduralMotor", sampleRate, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateWindLoop()
    {
        const int sampleRate = 22050;
        const int window = 96;
        var random = new System.Random(2187);
        var noise = new float[sampleRate * 2];
        var samples = new float[noise.Length];
        for (var i = 0; i < noise.Length; i++) noise[i] = (float)random.NextDouble() * 2f - 1f;
        var sum = 0f;
        for (var offset = -window; offset <= window; offset++) sum += noise[(offset + noise.Length) % noise.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = sum / (window * 2 + 1);
            sum += noise[(i + window + 1) % noise.Length] - noise[(i - window + noise.Length) % noise.Length];
        }
        var clip = AudioClip.Create("ProceduralWind", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateCue(string name, float firstFrequency, float secondFrequency)
    {
        const int sampleRate = 22050;
        var samples = new float[sampleRate / 2];
        var half = samples.Length / 2;
        for (var i = 0; i < samples.Length; i++)
        {
            var localSample = i % half;
            var frequency = i < half ? firstFrequency : secondFrequency;
            samples[i] = Mathf.Sin(localSample / (float)sampleRate * frequency * Mathf.PI * 2f) * Mathf.Sin(localSample / (float)half * Mathf.PI) * 0.15f;
        }
        var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayWarning() => radio.PlayOneShot(warningCue);

    public void PlaySuccess() => radio.PlayOneShot(successCue);

    public void PlayRadio() => radio.PlayOneShot(uplinkCue, 0.55f);

    private void OnDestroy()
    {
        Object.Destroy(motor.clip);
        Object.Destroy(wind.clip);
        Object.Destroy(warningCue);
        Object.Destroy(successCue);
        Object.Destroy(uplinkCue);
    }
}
