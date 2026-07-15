using UnityEngine;

public class ProgressDoor : MonoBehaviour
{
    private bool opened;
    private Vector3 closedPos;
    private Vector3 openPos;
    private float slideSpeed = 2f;

    void Start()
    {
        closedPos = transform.position;
        // Slide upward to open
        openPos = closedPos + Vector3.up * 4f;
    }

    void Update()
    {
        if (!opened && TVInteract.submittedTapes >= 20)
        {
            opened = true;
        }

        if (opened)
        {
            transform.position = Vector3.MoveTowards(transform.position, openPos, slideSpeed * Time.deltaTime);
        }
    }
}
