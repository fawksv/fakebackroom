using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 让怪物脸部朝向移动方向
/// </summary>
public class Billboard : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform root;

    void Start()
    {
        root = transform.root;
        agent = root.GetComponent<NavMeshAgent>();
    }

    void LateUpdate()
    {
        if (agent == null)
        {
            agent = transform.root.GetComponent<NavMeshAgent>();
            if (agent == null) return;
        }

        Vector3 velocity = agent.velocity;
        if (velocity.sqrMagnitude > 0.1f)
        {
            Vector3 dir = new Vector3(velocity.x, 0, velocity.z).normalized;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
