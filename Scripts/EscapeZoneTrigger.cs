using UnityEngine;

/// <summary>
/// 终点触发器：玩家进入后停用怪物并触发逃离演出。
/// 放置在 FinalRoom 出口处。
/// </summary>
[RequireComponent(typeof(Collider))]
public class EscapeZoneTrigger : MonoBehaviour
{
    private bool triggered = false;

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;

        // 停用所有怪物，防止干扰逃离演出
        var monsters = FindObjectsByType<MonsterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in monsters)
        {
            if (m != null)
                m.isActive = false;
        }

        var escapeSeq = other.GetComponent<EscapeSequence>();
        if (escapeSeq != null)
            escapeSeq.PlayEscapeSequence();
    }
}
