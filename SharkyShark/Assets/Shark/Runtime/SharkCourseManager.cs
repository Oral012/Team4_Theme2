using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SharkCourseManager : MonoBehaviour
{
    public const int CurrentCourseVersion = 4;

    [SerializeField] private GameObject player;
    [SerializeField] private SharkFinishLine finishLine;
    [SerializeField] private SharkPlatformController[] sharks;
    [SerializeField] private SmoothPlayerCamera playerCamera;
    [SerializeField] private Vector3 respawnPosition;
    [SerializeField] private Quaternion respawnRotation = Quaternion.identity;
    [SerializeField] private float groundHeight;
    [SerializeField] private int courseVersion;

    private SharkBiteReceiver biteReceiver;
    private SharkCoursePlayerController movement;
    private bool respawnScheduled;
    private bool finished;

    public float GroundHeight
    {
        get { return groundHeight; }
    }

    public int CourseVersion
    {
        get { return courseVersion; }
    }

    public GameObject Player
    {
        get { return player; }
    }

    public SharkPlatformController[] Sharks
    {
        get { return sharks; }
    }

    public void Configure(
        GameObject playerObject,
        Vector3 startPosition,
        Quaternion startRotation,
        SharkFinishLine finish,
        SharkPlatformController[] courseSharks,
        SmoothPlayerCamera cameraFollow,
        float courseGroundHeight)
    {
        player = playerObject;
        respawnPosition = startPosition;
        respawnRotation = startRotation;
        finishLine = finish;
        sharks = courseSharks;
        playerCamera = cameraFollow;
        groundHeight = courseGroundHeight;
        courseVersion = CurrentCourseVersion;
        biteReceiver = playerObject.GetComponent<SharkBiteReceiver>();
        movement = playerObject.GetComponent<SharkCoursePlayerController>();
    }

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError("The shark course manager needs a player reference.", this);
            enabled = false;
            return;
        }

        biteReceiver = player.GetComponent<SharkBiteReceiver>();
        movement = player.GetComponent<SharkCoursePlayerController>();
        if (biteReceiver == null || movement == null)
        {
            Debug.LogError("The course player must have a bite receiver and movement controller.", player);
            enabled = false;
            return;
        }

        if (finishLine != null)
        {
            finishLine.Configure(this);
        }
    }

    private void OnEnable()
    {
        if (biteReceiver != null)
        {
            biteReceiver.OnBitten.AddListener(HandlePlayerBitten);
        }
    }

    private void OnDisable()
    {
        if (biteReceiver != null)
        {
            biteReceiver.OnBitten.RemoveListener(HandlePlayerBitten);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            RestartCourse();
        }

        if (!finished && !respawnScheduled && biteReceiver != null
            && biteReceiver.IsAlive && player.transform.position.y < groundHeight - 3f)
        {
            respawnScheduled = true;
            StartCoroutine(RespawnAfterFall());
        }
    }

    public void CompleteCourse(SharkBiteReceiver receiver)
    {
        if (finished || receiver == null || receiver != biteReceiver || !receiver.IsAlive)
        {
            return;
        }

        finished = true;
        movement.enabled = false;
        if (playerCamera != null)
        {
            playerCamera.InputEnabled = false;
        }

        Debug.Log("Course complete! Press R to restart.", this);
    }

    public void RestartCourse()
    {
        StopAllCoroutines();
        respawnScheduled = false;
        finished = false;

        if (sharks != null)
        {
            foreach (SharkPlatformController shark in sharks)
            {
                if (shark != null)
                {
                    shark.ResetForCourse();
                }
            }
        }

        if (playerCamera != null)
        {
            playerCamera.InputEnabled = true;
        }

        RespawnPlayer();
    }

    private void HandlePlayerBitten(GameObject shark)
    {
        if (!finished && !respawnScheduled)
        {
            respawnScheduled = true;
            StartCoroutine(RespawnAfterBite());
        }
    }

    private IEnumerator RespawnAfterBite()
    {
        yield return new WaitForSeconds(biteReceiver.DeathAnimationDuration + 0.15f);
        RespawnPlayer();
        respawnScheduled = false;
    }

    private IEnumerator RespawnAfterFall()
    {
        yield return new WaitForSeconds(0.35f);
        if (biteReceiver != null && biteReceiver.IsAlive && !finished)
        {
            RespawnPlayer();
        }

        respawnScheduled = false;
    }

    private void RespawnPlayer()
    {
        biteReceiver.Respawn();
        movement.ResetMotion();
        player.transform.SetPositionAndRotation(respawnPosition, respawnRotation);
        if (playerCamera != null)
        {
            playerCamera.Target = player.transform;
            playerCamera.SnapToTarget();
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(16f, 16f, 520f, 28f), "WASD move   Space jump   Shift sprint   R restart");
        if (finished)
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 145f, Screen.height * 0.5f - 48f, 290f, 96f), "");
            GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.5f - 24f, 250f, 32f), "LEVEL COMPLETE!");
            GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.5f + 8f, 250f, 24f), "Press R to play again");
        }
    }
}
