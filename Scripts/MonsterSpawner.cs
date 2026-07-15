using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [Header("生成设置")]
    [Tooltip("怪物生成点列表（若留空，自动查找子物体 SpawnPoint_Left / SpawnPoint_Right）")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("怪物预制体路径（Resources 目录下，不含扩展名）")]
    [SerializeField] private string monsterPrefabPath = "Monster";

    [Tooltip("每个生成点生成的怪物数量")]
    [SerializeField] private int monsterCount = 3;

    [Tooltip("怪物在生成点附近的随机散布范围")]
    [SerializeField] private float scatterRadius = 2f;

    private readonly List<MonsterController> spawnedMonsters = new List<MonsterController>();
    private bool hasSpawned = false;

    private void Start()
    {
        // 若 Inspector 未指定生成点，在子物体中查找
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            var list = new List<Transform>();
            var left = transform.Find("SpawnPoint_Left");
            if (left == null) left = GameObject.Find("SpawnPoint_Left")?.transform;
            var right = transform.Find("SpawnPoint_Right");
            if (right == null) right = GameObject.Find("SpawnPoint_Right")?.transform;
            if (left != null) list.Add(left);
            if (right != null) list.Add(right);
            spawnPoints = list.ToArray();
        }
    }

    private GameObject LoadMonsterTemplate()
    {
        // 优先从 Resources 加载
        GameObject template = Resources.Load<GameObject>(monsterPrefabPath);
        if (template != null)
        {
            Debug.Log("MonsterSpawner: 通过 Resources.Load 加载怪物模板。", this);
            return template;
        }

        // 回退：查找场景中名为 Monster 的对象
        var allObjs = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjs)
        {
            if (obj.name == "Monster" && obj.transform.parent == null)
            {
                obj.SetActive(false);
                Debug.Log("MonsterSpawner: 使用场景中的 Monster 对象作为模板。", this);
                return obj;
            }
        }

        Debug.LogWarning("MonsterSpawner: 无法加载怪物模板。请将 Monster.prefab 放到 Assets/Resources/ 目录下。", this);
        return null;
    }

    /// <summary>
    /// 生成所有怪物（初始未激活），仅执行一次
    /// </summary>
    public void SpawnAllMonsters()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        GameObject template = LoadMonsterTemplate();
        if (template == null) return;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("MonsterSpawner: 未找到生成点，请创建 SpawnPoint_Left / SpawnPoint_Right。", this);
            return;
        }

        foreach (var point in spawnPoints)
        {
            if (point == null) continue;

            for (int i = 0; i < monsterCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * scatterRadius;
                Vector3 pos = point.position + new Vector3(offset.x, 0f, offset.y);

                var monster = Instantiate(template, pos, point.rotation);
                monster.SetActive(true);

                var controller = monster.GetComponent<MonsterController>();
                if (controller == null)
                    controller = monster.AddComponent<MonsterController>();

                controller.isActive = false;
                spawnedMonsters.Add(controller);
            }
        }

        Debug.Log($"MonsterSpawner: 已生成 {spawnedMonsters.Count} 只怪物（未激活状态）。", this);
    }

    /// <summary>
    /// 生成（若尚未生成）并激活所有怪物，使其开始追踪玩家
    /// </summary>
    public void ActivateMonsters()
    {
        if (!hasSpawned)
            SpawnAllMonsters();

        foreach (var monster in spawnedMonsters)
        {
            if (monster != null)
            {
                monster.isActive = true;
                monster.hasBeenActivated = true;
                monster.corridorChase = true;
            }
        }

        Debug.Log($"MonsterSpawner: 已激活 {spawnedMonsters.Count} 只怪物，开始追踪玩家。", this);
    }
}
