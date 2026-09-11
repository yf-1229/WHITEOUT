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
        UpdateGimbal(true);

        panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "FPVMonitor";
        panel.layer = LayerMask.NameToLayer("UI");
        panel.transform.SetParent(viewer.transform, false);
        panel.transform.localPosition = new Vector3(0.38f, -0.2f, 0.95f);
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = new Vector3(0.52f, 0.52f * 9f / 16f, 0.52f);
        Object.Destroy(panel.GetComponent<Collider>());
        var material = new Material(Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        material.mainTexture = feed;
        panel.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void Update()
    {
        UpdateGimbal(false);
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
