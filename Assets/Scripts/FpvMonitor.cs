using UnityEngine;

public sealed class FpvMonitor : MonoBehaviour
{
    public DroneController drone;
    public Renderer[] thermalTargets;
    public int textureSize = 512;
    public float renderInterval = 1f / 30f;
    public float gimbalPositionResponse = 10f;
    public float gimbalRotationResponse = 6f;

    private Camera fpvCamera;
    private RenderTexture feed;
    private GameObject panel;
    private Color[] normalColors;
    private float nextRender;
    private bool thermal;
    private bool thermalAvailable;

    public bool Thermal => thermal;
    public Transform Gimbal => fpvCamera.transform;

    public void Build(Camera viewer)
    {
        normalColors = new Color[thermalTargets.Length];
        for (var i = 0; i < thermalTargets.Length; i++)
            normalColors[i] = thermalTargets[i] != null ? thermalTargets[i].material.color : Color.white;

        var cameraObject = new GameObject("FPVCamera");
        cameraObject.transform.SetParent(transform, false);
        fpvCamera = cameraObject.AddComponent<Camera>();
        fpvCamera.fieldOfView = 84f;
        fpvCamera.nearClipPlane = 0.08f;
        fpvCamera.enabled = false;
        feed = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        feed.name = "FPVFeed";
        feed.antiAliasing = 2;
        feed.useMipMap = false;
        feed.Create();
        fpvCamera.targetTexture = feed;
        UpdateGimbal(true);

        panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "FPVMonitor";
        panel.transform.SetParent(viewer.transform, false);
        panel.transform.localPosition = new Vector3(0.3f, -0.18f, 0.82f);
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
        Object.Destroy(panel.GetComponent<Collider>());
        var material = new Material(Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        material.mainTexture = feed;
        panel.GetComponent<Renderer>().material = material;
    }

    private void Update()
    {
        UpdateGimbal(false);
        if (fpvCamera != null && panel.activeSelf && Time.unscaledTime >= nextRender)
        {
            nextRender = Time.unscaledTime + renderInterval;
            fpvCamera.Render();
        }

        if (!thermalAvailable || !QuestInput.Current.leftSecondaryDown) return;
        thermal = !thermal;
        for (var i = 0; i < thermalTargets.Length; i++)
        {
            if (thermalTargets[i] != null) thermalTargets[i].material.color = thermal ? new Color(1f, 0.18f, 0.02f) : normalColors[i];
        }
    }

    private void UpdateGimbal(bool snap)
    {
        var forward = Vector3.ProjectOnPlane(drone.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(fpvCamera.transform.forward, Vector3.up);
        forward.Normalize();
        var targetPosition = drone.transform.position + forward * 0.9f + Vector3.up * 0.16f;
        var targetRotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
        snap |= Vector3.SqrMagnitude(fpvCamera.transform.position - targetPosition) > 25f;
        fpvCamera.transform.position = Vector3.Lerp(fpvCamera.transform.position, targetPosition,
            snap ? 1f : 1f - Mathf.Exp(-gimbalPositionResponse * Time.unscaledDeltaTime));
        fpvCamera.transform.rotation = Quaternion.Slerp(fpvCamera.transform.rotation, targetRotation,
            snap ? 1f : 1f - Mathf.Exp(-gimbalRotationResponse * Time.unscaledDeltaTime));
    }

    public void SetPanelVisible(bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }

    public void SetThermalAvailable(bool available)
    {
        thermalAvailable = available;
        if (available || !thermal) return;
        thermal = false;
        for (var i = 0; i < thermalTargets.Length; i++)
        {
            if (thermalTargets[i] != null) thermalTargets[i].material.color = normalColors[i];
        }
    }

    private void OnDestroy()
    {
        if (feed != null) feed.Release();
    }
}
