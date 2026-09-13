using UnityEngine;

public sealed class FpvMonitor : MonoBehaviour
{
    public DroneController drone;
    public Renderer[] thermalTargets;
    public int textureSize = 512;
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
        fpvCamera.fieldOfView = 84f;
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
