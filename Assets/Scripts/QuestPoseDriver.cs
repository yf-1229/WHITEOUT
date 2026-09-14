using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(1000)]
public sealed class QuestPoseDriver : MonoBehaviour
{
    public Vector3 trackingOrigin = new(0f, 1.6f, -8f);
    public Quaternion trackingRotation = Quaternion.identity;
    private Vector3 initialHeadPosition;
    private Quaternion initialHeadRotation;
    private InputDevice head;
    private bool calibrated;

    public void SetOrigin(Vector3 position, Quaternion rotation)
    {
        trackingOrigin = position;
        trackingRotation = rotation;
    }

    private void LateUpdate()
    {
        if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.TryGetFeatureValue(CommonUsages.devicePosition, out var headPosition) ||
            !head.TryGetFeatureValue(CommonUsages.deviceRotation, out var headRotation))
        {
            transform.SetPositionAndRotation(trackingOrigin, trackingRotation);
            return;
        }
        if (!calibrated)
        {
            initialHeadPosition = headPosition;
            initialHeadRotation = headRotation;
            calibrated = true;
        }
        transform.position = trackingOrigin + trackingRotation * (headPosition - initialHeadPosition);
        transform.rotation = trackingRotation * Quaternion.Inverse(initialHeadRotation) * headRotation;
    }
}
