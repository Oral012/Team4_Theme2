using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class SharkFinishLine : MonoBehaviour
{
    private SharkCourseManager courseManager;

    public void Configure(SharkCourseManager manager)
    {
        courseManager = manager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (courseManager == null)
        {
            courseManager = GetComponentInParent<SharkCourseManager>();
        }

        SharkBiteReceiver receiver = other.GetComponentInParent<SharkBiteReceiver>();
        if (courseManager != null && receiver != null)
        {
            courseManager.CompleteCourse(receiver);
        }
    }
}
