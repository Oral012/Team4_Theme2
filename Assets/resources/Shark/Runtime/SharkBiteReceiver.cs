using System.Collections;
using UnityEngine;

public sealed class SharkBiteReceiver : MonoBehaviour
{
    [SerializeField] private SharkPlayerEvent onBitten = new SharkPlayerEvent();
    [SerializeField] private Behaviour movementBehaviour;
    [SerializeField, Min(0f)] private float deathAnimationDuration = 0.8f;

    public bool IsAlive { get; private set; } = true;
    public float DeathAnimationDuration
    {
        get { return deathAnimationDuration; }
    }

    private Animator playerAnimator;
    private Coroutine deathCoroutine;
    private int deathSequence;

    public SharkPlayerEvent OnBitten
    {
        get { return onBitten; }
    }

    public void OnSharkBitten(GameObject shark)
    {
        if (!IsAlive)
        {
            return;
        }

        IsAlive = false;
        playerAnimator = GetComponentInChildren<Animator>();
        Debug.Log("The player was bitten.", this);

        if (movementBehaviour != null)
        {
            movementBehaviour.enabled = false;
        }

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (playerAnimator != null && HasPoseParameter(playerAnimator))
        {
            playerAnimator.SetInteger("Pose", 5);
            deathCoroutine = StartCoroutine(HideAfterDeathAnimation(++deathSequence));
        }
        else
        {
            HidePlayer();
        }

        onBitten.Invoke(shark);
    }

    public void Respawn()
    {
        deathSequence++;
        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }

        IsAlive = true;
        if (movementBehaviour != null)
        {
            movementBehaviour.enabled = true;
        }

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        foreach (Renderer bodyPart in GetComponentsInChildren<Renderer>(true))
        {
            bodyPart.enabled = true;
        }

        playerAnimator = GetComponentInChildren<Animator>();
        if (playerAnimator != null && HasPoseParameter(playerAnimator))
        {
            playerAnimator.SetInteger("Pose", 0);
        }
    }

    private IEnumerator HideAfterDeathAnimation(int sequence)
    {
        yield return new WaitForSeconds(deathAnimationDuration);
        if (sequence == deathSequence && !IsAlive)
        {
            HidePlayer();
        }

        deathCoroutine = null;
    }

    private void HidePlayer()
    {
        foreach (Renderer bodyPart in GetComponentsInChildren<Renderer>())
        {
            bodyPart.enabled = false;
        }
    }

    private static bool HasPoseParameter(Animator animator)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == "Pose" && parameter.type == AnimatorControllerParameterType.Int)
            {
                return true;
            }
        }

        return false;
    }
}
