using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class SharkCoursePlayerController : MonoBehaviour
{
    private const int IdlePose = 0;
    private const int WalkPose = 1;
    private const int RunPose = 2;
    private const int JumpPose = 3;
    private const int FallPose = 4;
    private const int DiePose = 5;

    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [SerializeField, Min(0f)] private float sprintSpeed = 6f;
    [SerializeField, Min(0f)] private float jumpSpeed = 9.2f;
    [SerializeField, Min(0f)] private float gravity = 21f;
    [SerializeField, Min(0f)] private float runSpeedThreshold = 4.5f;

    private CharacterController characterController;
    private Animator playerAnimator;
    private float verticalSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerAnimator = GetComponentInChildren<Animator>();
    }

    public void ResetMotion()
    {
        verticalSpeed = 0f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            UpdateAnimation(0f, characterController.isGrounded);
            return;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed) input.y += 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 move = forward * input.y + right * input.x;
        bool sprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        float currentMoveSpeed = sprinting ? sprintSpeed : moveSpeed;

        if (characterController.isGrounded)
        {
            verticalSpeed = -2f;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalSpeed = jumpSpeed;
            }
        }
        else
        {
            verticalSpeed -= gravity * Time.deltaTime;
        }

        characterController.Move((move * currentMoveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);

        if (move.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(move, Vector3.up);
        }

        UpdateAnimation(move.magnitude * currentMoveSpeed, characterController.isGrounded);
    }

    private void UpdateAnimation(float horizontalSpeed, bool grounded)
    {
        if (playerAnimator == null)
        {
            return;
        }

        int pose = !grounded
            ? (verticalSpeed > 0f ? JumpPose : FallPose)
            : horizontalSpeed > runSpeedThreshold
                ? RunPose
                : horizontalSpeed > 0.1f ? WalkPose : IdlePose;
        playerAnimator.SetInteger("Pose", pose);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        SharkPlatformController shark = hit.collider.GetComponentInParent<SharkPlatformController>();
        if (shark != null && Vector3.Dot(hit.normal, Vector3.up) > 0.5f)
        {
            shark.RegisterPlayerStanding(GetComponent<SharkBiteReceiver>(), hit.collider);
        }
    }
}
