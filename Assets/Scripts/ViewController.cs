using UnityEngine;

public sealed class ViewController : MonoBehaviour
{
    public DroneController drone;
    public QuestPoseDriver pose;
    public FpvMonitor monitor;
    public ViewMode CurrentMode { get; private set; } = ViewMode.Operator;

    private Vector3 operatorPosition;
    private Quaternion operatorRotation;
    private Vector3 chasePosition;
    private Quaternion chaseRotation;
    private GameObject comfortFrame;

    public void Build(Camera viewer)
    {
        operatorPosition = pose.trackingOrigin;
        operatorRotation = pose.trackingRotation;
        chasePosition = operatorPosition;
        chaseRotation = operatorRotation;
        comfortFrame = CreateComfortFrame(viewer.transform);
        SetMode(Application.isEditor ? ViewMode.Operator : (ViewMode)Mathf.Clamp(PlayerPrefs.GetInt("viewMode", 0), 0, 2));
    }

    private void Update()
    {
        if (QuestInput.Current.viewCycleDown)
            SetMode((ViewMode)(((int)CurrentMode + 1) % 3));

        if (CurrentMode == ViewMode.Operator)
        {
            pose.SetOrigin(operatorPosition, operatorRotation);
        }
        else if (CurrentMode == ViewMode.Fpv)
        {
            pose.SetOrigin(monitor.Gimbal.position, monitor.Gimbal.rotation);
        }
        else
        {
            var forward = Vector3.ProjectOnPlane(monitor.Gimbal.forward, Vector3.up).normalized;
            var desired = drone.transform.position - forward * 4.5f + Vector3.up * 2.2f;
            chasePosition = Vector3.Lerp(chasePosition, desired, 1f - Mathf.Exp(-4f * Time.deltaTime));
            var direction = drone.transform.position + forward * 1.5f - chasePosition;
            var desiredRotation = direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction, Vector3.up) : chaseRotation;
            chaseRotation = Quaternion.Slerp(chaseRotation, desiredRotation, 1f - Mathf.Exp(-5f * Time.deltaTime));
            pose.SetOrigin(chasePosition, chaseRotation);
        }
    }

    private void SetMode(ViewMode mode)
    {
        CurrentMode = mode;
        PlayerPrefs.SetInt("viewMode", (int)mode);
        monitor.SetPanelVisible(mode == ViewMode.Operator);
        if (comfortFrame != null) comfortFrame.SetActive(mode != ViewMode.Operator);
        if (mode == ViewMode.Chase)
        {
            var forward = Vector3.ProjectOnPlane(monitor.Gimbal.forward, Vector3.up).normalized;
            chasePosition = drone.transform.position - forward * 4.5f + Vector3.up * 2.2f;
            chaseRotation = Quaternion.LookRotation(drone.transform.position - chasePosition, Vector3.up);
        }
    }

    private static GameObject CreateComfortFrame(Transform viewer)
    {
        var frame = new GameObject("ComfortFrame");
        frame.transform.SetParent(viewer, false);
        var material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = new Color(0.015f, 0.02f, 0.025f);
        AddBar(frame.transform, material, new Vector3(0f, 0.43f, 0.85f), new Vector3(1.2f, 0.06f, 0.04f));
        AddBar(frame.transform, material, new Vector3(0f, -0.43f, 0.85f), new Vector3(1.2f, 0.06f, 0.04f));
        AddBar(frame.transform, material, new Vector3(-0.62f, 0f, 0.85f), new Vector3(0.06f, 0.86f, 0.04f));
        AddBar(frame.transform, material, new Vector3(0.62f, 0f, 0.85f), new Vector3(0.06f, 0.86f, 0.04f));
        foreach (var child in frame.GetComponentsInChildren<Transform>())
            child.gameObject.layer = LayerMask.NameToLayer("UI");
        return frame;
    }

    private static void AddBar(Transform parent, Material material, Vector3 position, Vector3 scale)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = position;
        bar.transform.localScale = scale;
        bar.GetComponent<Renderer>().sharedMaterial = material;
        Object.Destroy(bar.GetComponent<Collider>());
    }
}
