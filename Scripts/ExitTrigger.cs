using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitTrigger : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("怪物生成器（若留空，自动查找场景中的 MonsterSpawner）")]
    [SerializeField] private MonsterSpawner spawner;

    [Tooltip("玩家标签")]
    [SerializeField] private string playerTag = "Player";

    private bool triggered = false;

    public bool HasTriggered => triggered;

    void Start()
    {
        // 确保碰撞体是触发模式
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (spawner == null)
            spawner = FindObjectOfType<MonsterSpawner>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag(playerTag))
        {
            triggered = true;
            Debug.Log("ExitTrigger: 玩家进入走廊，激活怪物追击！", this);

            if (spawner != null)
                spawner.ActivateMonsters();
            else
                Debug.LogError("ExitTrigger: 未找到 MonsterSpawner，请确保场景中有 MonsterSpawner。", this);
        }
    }
}
