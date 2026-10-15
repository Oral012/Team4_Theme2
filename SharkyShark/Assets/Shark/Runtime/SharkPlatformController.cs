using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class SharkPlayerEvent : UnityEvent<GameObject>
{
}

public sealed class SharkPlatformController : MonoBehaviour
{
    private const string BiteTrigger = "Bite";

    [SerializeField] private Animator sharkAnimator;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private BoxCollider platformCollider;
    [SerializeField, Min(0.1f)] private float biteDelay = 2f;
    [SerializeField, Range(30f, 90f)] private float bitePitchDegrees = 60f;
    [SerializeField, Min(0.05f)] private float pitchDuration = 0.4f;
    [SerializeField, Min(0f)] private float biteImpactDelay = 0.4f;
    [SerializeField, Min(0f)] private float biteHoldDuration = 0.8f;
    [SerializeField, Min(0f)] private float biteResetDelay = 0.5f;
    [SerializeField, Min(0f)] private float swimRange = 1.5f;
    [SerializeField, Min(0f)] private float swimSpeed = 1f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private SharkPlayerEvent onBite = new SharkPlayerEvent();

    private SharkBiteReceiver target;
    private float elapsed;
    [SerializeField] private float swimPhase;
    private Vector3 swimOrigin;
    private float swimStartTime;
    private float facingYaw;
    private bool hasBitten;
    private bool swimsSideToSide;

    public float BiteDelay
    {
        get { return biteDelay; }
        set { biteDelay = Mathf.Max(0.1f, value); }
    }

    public SharkPlayerEvent OnBite
    {
        get { return onBite; }
    }

    public bool HasTarget
    {
        get { return target != null; }
    }

    public bool IsAttacking
    {
        get { return hasBitten; }
    }

    public float FacingYaw
    {
        get { return facingYaw; }
    }

    public void ConfigureSwimming(float range, float speed, float phase)
    {
        swimRange = Mathf.Max(0f, range);
        swimSpeed = Mathf.Max(0f, speed);
        swimPhase = phase;
        swimsSideToSide = swimRange > 0f && swimSpeed > 0f;
        swimOrigin = transform.position;
    }

    public void ResetForCourse()
    {
        StopAllCoroutines();
        target = null;
        elapsed = 0f;
        hasBitten = false;

        if (visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
        }

        if (swimsSideToSide)
        {
            swimStartTime = Time.time;
            transform.position = swimOrigin;
        }

        if (sharkAnimator != null)
        {
            sharkAnimator.ResetTrigger(BiteTrigger);
            sharkAnimator.Rebind();
            sharkAnimator.Update(0f);
        }
    }

    private void Awake()
    {
        if (sharkAnimator == null)
        {
            sharkAnimator = GetComponentInChildren<Animator>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform.Find("Shark Visual");
        }

        if (sharkAnimator != null && !HasBiteTrigger(sharkAnimator))
        {
            Debug.LogError("The shark Animator Controller needs a trigger parameter named 'Bite'.", sharkAnimator);
        }

        swimOrigin = transform.position;
        swimStartTime = Time.time;
        swimsSideToSide = swimRange > 0f && swimSpeed > 0f;
    }

    private void Update()
    {
        MoveWhileSwimming();

        if (target == null || hasBitten)
        {
            return;
        }

        if (!target.IsAlive || !IsStandingOnTop(target, null))
        {
            target = null;
            elapsed = 0f;
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < biteDelay)
        {
            return;
        }

        hasBitten = true;
        if (sharkAnimator != null)
        {
            sharkAnimator.SetTrigger(BiteTrigger);
        }

        Debug.Log("The shark began its bite after the countdown.", this);
        StartCoroutine(PerformBite(target));
    }

    private void MoveWhileSwimming()
    {
        if (!swimsSideToSide || hasBitten)
        {
            return;
        }

        bool playerWasStanding = target != null
            && target.IsAlive
            && IsStandingOnTop(target, null);
        float wavePosition = (Time.time - swimStartTime) * swimSpeed + swimPhase;
        float direction = Mathf.Cos(wavePosition);
        if (direction > 0.05f)
        {
            facingYaw = 90f;
        }
        else if (direction < -0.05f)
        {
            facingYaw = -90f;
        }

        if (!hasBitten && visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
        }

        Vector3 destination = swimOrigin
            + Vector3.right * (
                Mathf.Sin(wavePosition)
                * swimRange
                * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Time.time - swimStartTime)));
        Vector3 displacement = destination - transform.position;
        transform.position = destination;

        if (!playerWasStanding || displacement.sqrMagnitude <= 0f)
        {
            return;
        }

        CharacterController characterController = target.GetComponent<CharacterController>();
        if (characterController != null && characterController.enabled)
        {
            characterController.Move(displacement);
        }
        else
        {
            target.transform.position += displacement;
        }
    }

    private IEnumerator PerformBite(SharkBiteReceiver bittenTarget)
    {
        Quaternion restingRotation = Quaternion.Euler(0f, facingYaw, 0f);
        Quaternion attackRotation = restingRotation * Quaternion.Euler(-bitePitchDegrees, 0f, 0f);

        yield return RotateVisual(restingRotation, attackRotation, pitchDuration);
        if (biteImpactDelay > pitchDuration)
        {
            yield return new WaitForSeconds(biteImpactDelay - pitchDuration);
        }

        if (bittenTarget != null && bittenTarget.IsAlive && IsStandingOnTop(bittenTarget, null))
        {
            GameObject bittenPlayer = bittenTarget.gameObject;
            bittenTarget.OnSharkBitten(gameObject);
            onBite.Invoke(bittenPlayer);
        }
        else if (bittenTarget != null && bittenTarget.IsAlive)
        {
            Debug.Log("The player stepped off the shark before the bite landed and was not harmed.", bittenTarget);
        }

        if (biteHoldDuration > 0f)
        {
            yield return new WaitForSeconds(biteHoldDuration);
        }

        yield return RotateVisual(attackRotation, restingRotation, pitchDuration);
        if (biteResetDelay > 0f)
        {
            yield return new WaitForSeconds(biteResetDelay);
        }

        target = null;
        elapsed = 0f;
        hasBitten = false;
    }

    private IEnumerator RotateVisual(Quaternion from, Quaternion to, float duration)
    {
        if (visualRoot == null)
        {
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            visualRoot.localRotation = Quaternion.Slerp(from, to, smoothProgress);
            yield return null;
        }

        visualRoot.localRotation = to;
    }

    private void OnTriggerStay(Collider other)
    {
        if (target != null || platformCollider == null)
        {
            return;
        }

        SharkBiteReceiver receiver = other.GetComponentInParent<SharkBiteReceiver>();
        RegisterPlayerStanding(receiver, other);
    }

    public void RegisterPlayerStanding(SharkBiteReceiver receiver, Collider contactCollider = null)
    {
        if (target != null || platformCollider == null
            || receiver == null || !receiver.IsAlive || !receiver.CompareTag(playerTag))
        {
            return;
        }

        if (!IsStandingOnTop(receiver, contactCollider))
        {
            return;
        }

        target = receiver;
        elapsed = 0f;
        Debug.Log("Player boarded the shark. Bite countdown started.", this);
    }

    private bool IsStandingOnTop(SharkBiteReceiver receiver, Collider fallbackCollider)
    {
        if (platformCollider == null || receiver == null)
        {
            return false;
        }

        CharacterController characterController = receiver.GetComponent<CharacterController>();
        Collider playerCollider = characterController != null
            ? characterController
            : receiver.GetComponent<Collider>();
        if (playerCollider == null)
        {
            playerCollider = fallbackCollider;
        }

        if (playerCollider == null)
        {
            return false;
        }

        Bounds surfaceBounds = platformCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        float platformTop = surfaceBounds.max.y;
        float footHeightDifference = playerBounds.min.y - platformTop;
        return footHeightDifference >= -0.3f
            && footHeightDifference <= 0.3f
            && playerBounds.center.x >= surfaceBounds.min.x
            && playerBounds.center.x <= surfaceBounds.max.x
            && playerBounds.center.z >= surfaceBounds.min.z
            && playerBounds.center.z <= surfaceBounds.max.z;
    }

    private static bool HasBiteTrigger(Animator animator)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == BiteTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }
}
