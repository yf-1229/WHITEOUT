using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

public struct QuestInputState
{
    public Vector2 leftStick;
    public Vector2 rightStick;
    public bool leftPrimaryDown;
    public bool leftSecondaryDown;
    public bool rightPrimaryDown;
    public bool rightSecondaryDown;
    public bool rightTriggerDown;
    public bool rightStickClickDown;
    public bool viewCycleDown;
}

public static class QuestInput
{
    public static QuestInputState Current { get; private set; }

    private static UnityEngine.XR.InputDevice left;
    private static UnityEngine.XR.InputDevice right;
    private static bool previousLeftPrimary;
    private static bool previousLeftSecondary;
    private static bool previousRightPrimary;
    private static bool previousRightSecondary;
    private static bool previousTrigger;
    private static bool previousRightStickClick;
    private static bool previousRightGrip;

    public static void Poll()
    {
        FindDevices();
        var state = new QuestInputState();

        if (left.isValid)
        {
            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out state.leftStick);
            left.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);
            left.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary);
            state.leftPrimaryDown = primary && !previousLeftPrimary;
            state.leftSecondaryDown = secondary && !previousLeftSecondary;
            previousLeftPrimary = primary;
            previousLeftSecondary = secondary;
        }

        if (right.isValid)
        {
            right.TryGetFeatureValue(CommonUsages.primary2DAxis, out state.rightStick);
            right.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);
            right.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary);
            right.TryGetFeatureValue(CommonUsages.trigger, out var trigger);
            right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var stickClick);
            right.TryGetFeatureValue(CommonUsages.grip, out var grip);
            state.rightPrimaryDown = primary && !previousRightPrimary;
            state.rightSecondaryDown = secondary && !previousRightSecondary;
            state.rightTriggerDown = trigger > 0.8f && !previousTrigger;
            state.rightStickClickDown = stickClick && !previousRightStickClick;
            state.viewCycleDown = grip > 0.8f && !previousRightGrip;
            previousRightPrimary = primary;
            previousRightSecondary = secondary;
            previousTrigger = trigger > 0.8f;
            previousRightStickClick = stickClick;
            previousRightGrip = grip > 0.8f;
        }

        // Keyboard fallback keeps the prototype testable without a headset.
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            state.leftStick += new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f));
            state.rightStick += new Vector2(
                (keyboard.lKey.isPressed ? 1f : 0f) - (keyboard.jKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            state.leftPrimaryDown |= keyboard.digit2Key.wasPressedThisFrame;
            state.rightPrimaryDown |= keyboard.digit1Key.wasPressedThisFrame;
            state.rightStickClickDown |= keyboard.digit3Key.wasPressedThisFrame;
            state.viewCycleDown |= keyboard.cKey.wasPressedThisFrame;
            state.leftSecondaryDown |= keyboard.mKey.wasPressedThisFrame;
            state.rightTriggerDown |= keyboard.spaceKey.wasPressedThisFrame;
            state.rightSecondaryDown |= keyboard.rKey.wasPressedThisFrame;
        }
        Current = state;
    }

    public static void PulseRight(float amplitude, float duration)
    {
        if (!right.isValid) FindDevices();
        if (right.isValid) right.SendHapticImpulse(0, amplitude, duration);
    }

    private static void FindDevices()
    {
        if (!left.isValid) left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!right.isValid) right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }
}

[DefaultExecutionOrder(-1000)]
public sealed class QuestInputDriver : MonoBehaviour
{
    private void Update() => QuestInput.Poll();
}
