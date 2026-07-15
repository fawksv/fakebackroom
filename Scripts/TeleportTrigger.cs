using UnityEngine;

public class TeleportTrigger : MonoBehaviour
{
    private BackroomsMaze maze;

    void Start()
    {
        maze = GetComponentInParent<BackroomsMaze>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (maze == null) return;
        maze.HandleTeleport(other);
    }
}