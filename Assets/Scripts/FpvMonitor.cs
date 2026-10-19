using UnityEngine;

public sealed class FpvMonitor : MonoBehaviour
{
    public DroneController drone;
    public Renderer[] thermalTargets;
    // ponytail: operator-view HUD renders into this feed, so 512 read as mush; 1280 keeps ~16px/deg on the panel
    // under 72fps. Drop to 1024 if Quest 2 frame time slips, raise to 1920 for 1:1 canvas units.
    public int textureSize = 1280;
    public float renderInterval = 1f / 30f;
    public Vector3 cameraOffset = new(0f, -0.16f, 0.48f);
    public float cameraTiltDegrees = 8f;

    private Camera fpvCamera;
    private RenderTexture feed;
    private GameObject panel;
    private float nextRender;
    private bool thermal;
    private bool thermalAvailable;
    private int thermalTarget = -1;

    public bool Thermal => thermal;
    public Transform Gimbal => fpvCamera.transform;

    public void Build(Camera viewer)
    {
        foreach (var target in thermalTargets)
            if (target != null) target.enabled = false;

        var cameraObject = new GameObject("FPVCamera");
        cameraObject.transform.SetParent(transform, false);
        fpvCamera = cameraObject.AddComponent<Camera>();
        fpvCamera.cullingMask |= LayerMask.GetMask("UI");
        fpvCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
        fpvCamera.fieldOfView = 96f;
        fpvCamera.nearClipPlane = 0.08f;
        fpvCamera.enabled = false;
        feed = new RenderTexture(textureSize, textureSize * 9 / 16, 16, RenderTextureFormat.ARGB32);
        feed.name = "FPVFeed";
        feed.antiAliasing = 2;
        feed.useMipMap = false;
        feed.Create();
        fpvCamera.targetTexture = feed;
        UpdateGimbal();

        panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "FPVMonitor";
        panel.layer = LayerMask.NameToLayer("UI");
        panel.transform.SetParent(viewer.transform, false);
        panel.transform.localPosition = new Vector3(0.3f, -0.18f, 0.9f);
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = new Vector3(0.68f, 0.68f * 9f / 16f, 0.68f);
        Object.Destroy(panel.GetComponent<Collider>());
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"));
        material.mainTexture = feed;
        panel.GetComponent<Renderer>().sharedMaterial = material;

        // A thin bezel so the feed reads as a physical monitor, not a floating rectangle.
        var bezel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bezel.name = "FPVMonitorBezel";
        bezel.transform.SetParent(panel.transform, false);
        bezel.transform.localPosition = new Vector3(0f, 0f, -0.018f);
        bezel.transform.localScale = new Vector3(1.06f, 1.1f, 1f);
        Object.Destroy(bezel.GetComponent<Collider>());
        bezel.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var bezelMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        bezelMaterial.color = new Color(0.018f, 0.022f, 0.028f);
        bezel.GetComponent<Renderer>().sharedMaterial = bezelMaterial;
    }

    private void Update()
    {
        UpdateGimbal();
        if (!Application.isBatchMode && fpvCamera != null && panel.activeSelf && Time.unscaledTime >= nextRender)
        {
            nextRender = Time.unscaledTime + renderInterval;
            panel.SetActive(false);
            fpvCamera.Render();
            panel.SetActive(true);
        }

        if (!thermalAvailable || !QuestInput.Current.leftSecondaryDown) return;
        thermal = !thermal;
        UpdateThermalTarget();
        QuestInput.PulseRight(0.12f, 0.06f);
    }

    private void UpdateGimbal()
    {
        var targetPosition = drone.transform.TransformPoint(cameraOffset);
        var targetRotation = drone.transform.rotation * Quaternion.Euler(cameraTiltDegrees, 0f, 0f);
        fpvCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    public void SetPanelVisible(bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }

    public void SetThermalAvailable(bool available, int target = -1)
    {
        thermalAvailable = available;
        thermalTarget = available ? Mathf.Clamp(target, 0, thermalTargets.Length - 1) : -1;
        if (!available) thermal = false;
        UpdateThermalTarget();
    }

    private void UpdateThermalTarget()
    {
        for (var i = 0; i < thermalTargets.Length; i++)
            if (thermalTargets[i] != null) thermalTargets[i].enabled = thermal && i == thermalTarget;
    }

    private void OnDestroy()
    {
        if (feed != null) feed.Release();
    }
}
