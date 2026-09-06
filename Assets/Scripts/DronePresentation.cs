using UnityEngine;

public sealed class DronePresentation : MonoBehaviour
{
    public Transform[] rotors;
    public Light navigationLight;

    private Rigidbody body;
    private DroneController drone;
    private AudioSource motor;
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
    }

    private void Update()
    {
        var armed = drone.MotorsArmed && !drone.Crashed;
        var targetSpeed = armed ? 900f + body.linearVelocity.magnitude * 100f : 0f;
        rotorSpeed = Mathf.MoveTowards(rotorSpeed, targetSpeed, 1800f * Time.deltaTime);
        foreach (var rotor in rotors) rotor.Rotate(0f, rotorSpeed * Time.deltaTime, 0f, Space.Self);
        motor.pitch = 0.85f + Mathf.Clamp01(body.linearVelocity.magnitude / 8f) * 0.65f;
        motor.volume = Mathf.MoveTowards(motor.volume, armed ? 0.18f : 0f, 0.4f * Time.deltaTime);
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
}
