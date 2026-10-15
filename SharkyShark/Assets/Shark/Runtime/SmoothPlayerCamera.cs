using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SmoothPlayerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField, Min(0.1f)] private float distance = 7f;
    [SerializeField, Min(0.1f)] private float minimumDistance = 3f;
    [SerializeField, Min(0.1f)] private float maximumDistance = 12f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minimumPitch = -25f;
    [SerializeField] private float maximumPitch = 70f;
    [SerializeField, Min(0f)] private float pivotSharpness = 14f;
    [SerializeField, Min(0f)] private float zoomSharpness = 5f;
    [SerializeField, Min(0f)] private float collisionRadius = 0.3f;
    [SerializeField] private LayerMask collisionMask = 1;
    [SerializeField] private bool lockCursorOnStart = true;

    private Vector3 smoothPivot;
    private float currentDistance;
    private float yaw;
    private float pitch = 18f;
    private bool inputEnabled = true;

    public Transform Target
    {
        get { return target; }
        set
        {
            target = value;
            SnapBehindTarget();
        }
    }

    public bool InputEnabled
    {
        get { return inputEnabled; }
        set { inputEnabled = value; }
    }

    private void OnEnable()
    {
        if (target != null)
        {
            SnapBehindTarget();
        }

        currentDistance = distance;
        if (Application.isPlaying && lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void OnDisable()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            SetCursorLocked(false);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        HandleInput();

        Vector3 pivot = target.position + pivotOffset;
        float pivotBlend = 1f - Mathf.Exp(-pivotSharpness * deltaTime);
        smoothPivot = Vector3.Lerp(smoothPivot, pivot, pivotBlend);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 backward = rotation * Vector3.back;
        float wantedDistance = distance;
        if (Physics.SphereCast(
            smoothPivot,
            collisionRadius,
            backward,
            out RaycastHit hit,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            wantedDistance = Mathf.Max(hit.distance - 0.1f, 0.8f);
        }

        if (wantedDistance < currentDistance)
        {
            currentDistance = wantedDistance;
        }
        else
        {
            float distanceBlend = 1f - Mathf.Exp(-zoomSharpness * deltaTime);
            currentDistance = Mathf.Lerp(currentDistance, wantedDistance, distanceBlend);
        }

        transform.SetPositionAndRotation(smoothPivot + backward * currentDistance, rotation);
    }

    public void SnapBehindTarget()
    {
        if (target == null)
        {
            return;
        }

        yaw = target.eulerAngles.y;
        smoothPivot = target.position + pivotOffset;
        currentDistance = distance;
        Vector3 backward = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
        transform.SetPositionAndRotation(
            smoothPivot + backward * currentDistance,
            Quaternion.Euler(pitch, yaw, 0f));
    }

    public void SnapToTarget()
    {
        SnapBehindTarget();
    }

    private void HandleInput()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorLocked(false);
        }
        else if (mouse != null && Cursor.lockState != CursorLockMode.Locked
            && mouse.leftButton.wasPressedThisFrame)
        {
            SetCursorLocked(true);
        }

        if (!inputEnabled || mouse == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 mouseDelta = mouse.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minimumPitch, maximumPitch);

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(
                distance - Mathf.Sign(scroll) * 0.6f,
                minimumDistance,
                maximumDistance);
        }
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
