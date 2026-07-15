using UnityEngine;

public class BarrierDoor : MonoBehaviour
{
    [Header("Settings")]
    public Transform player;
    public float triggerDistance = 3f;
    public float deployAngle = 90f;
    public float deploySpeed = 200f;

    // hiddenRot = flush against wall; deployedRot = blocking corridor
    private Quaternion hiddenRot;
    private Quaternion deployedRot;
    private bool deploying;

    void Start()
    {
        hiddenRot = transform.rotation;
        deployedRot = transform.rotation * Quaternion.Euler(0, deployAngle, 0);
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist < triggerDistance)
            deploying = true;

        if (deploying)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, deployedRot, deploySpeed * Time.deltaTime);
        }
    }
}
