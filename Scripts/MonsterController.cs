using UnityEngine;
using UnityEngine.AI;

public class MonsterController : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("怪物追踪玩家的移动速度")]
    [SerializeField] private float moveSpeed = 1.95f;

    [Tooltip("灯灭时怪物的追逐速度")]
    [SerializeField] private float darkChaseSpeed = 3.5f;

    [Tooltip("转向玩家的速度")]
    [SerializeField] private float rotationSpeed = 5f;

    [Tooltip("怪物到达玩家的停止距离")]
    [SerializeField] private float stopDistance = 1f;

    [Tooltip("怪物碰到玩家的判定距离")]
    [SerializeField] private float killDistance = 1.5f;

    [Header("图书馆行为参数")]
    [Tooltip("灯亮时触发追逐的距离")]
    [SerializeField] private float lightOnChaseRange = 5f;

    [Tooltip("巡逻时选点半径")]
    [SerializeField] private float patrolRadius = 15f;

    [Tooltip("巡逻选点间隔")]
    [SerializeField] private float patrolWaitTime = 3f;

    [Tooltip("去铃路上截断追玩家的距离")]
    [SerializeField] private float bellInterceptRange = 5f;

    [Tooltip("截断追击后持续追逐到该距离才放弃")]
    [SerializeField] private float chaseAbandonRange = 10f;

    [Header("引用设置")]
    [Tooltip("玩家对象的标签（需在 Tag Manager 中设置）")]
    [SerializeField] private string playerTag = "Player";

    [Header("解体设置")]
    [Tooltip("玩家解体时的爆炸力")]
    [SerializeField] private float explosionForce = 500f;

    [Tooltip("爆炸影响半径")]
    [SerializeField] private float explosionRadius = 5f;

    [Tooltip("爆炸向上的力")]
    [SerializeField] private float explosionUpward = 1f;

    [Header("状态")]
    [Tooltip("为 true 时怪物开始追踪玩家")]
    public bool isActive = false;

    [Tooltip("记录怪物是否曾被 MonsterSpawner 激活过")]
    public bool hasBeenActivated = false;

    [Tooltip("走廊追击模式：玩家不在图书馆时，被激活后持续追击")]
    public bool corridorChase = false;

    [Header("黑雾粒子引用")]
    [SerializeField] private ParticleSystem mainFog;
    [SerializeField] private ParticleSystem trailSmoke;

    private Transform player;
    private NavMeshAgent agent;
    private Rigidbody rb;

    // ---- 状态机 ----
    private enum MonsterState { Patrol, Chase, GoToBell, ChaseFromBell, Stunned }
    private MonsterState state = MonsterState.Patrol;

    // ---- 铃铛 ----
    private bool bellActive;
    private Vector3 bellTarget;
    private float bellTimer;
    private const float BellDuration = 10f;

    // ---- 硬控 ----
    private float stunTimer;
    private const float StunDuration = 3f;

    // ---- 巡逻 ----
    private float patrolTimer;

    // ---- 击杀保护 ----
    private bool hasKilledPlayer = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed * 180f;
        agent.stoppingDistance = stopDistance;
        agent.radius = 0.4f;
        agent.height = 2f;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var col = GetComponent<Collider>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = false;

        if (mainFog == null)
            mainFog = FindChildPS("MainFog");
        if (trailSmoke == null)
            trailSmoke = FindChildPS("TrailSmoke");

        // 生成程序化噪声纹理，替换外部 smoke 贴图
        var noiseTex = BlackFogEntity.GenerateNoiseTexture(128);
        var smokeMat = new Material(Shader.Find("Particles/Standard Unlit"));
        smokeMat.SetTexture("_MainTex", noiseTex);
        smokeMat.SetColor("_Color", new Color(0.005f, 0.005f, 0.005f, 1f));

        if (mainFog != null)
        {
            var mr = mainFog.GetComponent<ParticleSystemRenderer>();
            if (mr != null) mr.material = smokeMat;
        }
        if (trailSmoke != null)
        {
            var mr = trailSmoke.GetComponent<ParticleSystemRenderer>();
            if (mr != null) mr.material = smokeMat;
        }
    }

    private ParticleSystem FindChildPS(string name)
    {
        var t = transform.Find(name);
        return t?.GetComponent<ParticleSystem>();
    }

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogWarning($"MonsterController: 未找到标签为 '{playerTag}' 的对象。", this);
    }

    private void Update()
    {
        if (!isActive || player == null || !agent.isOnNavMesh) return;

        // 检查是否有新铃铛被按下
        if (BellInteract.PendingBellPosition.HasValue)
        {
            bellTarget = BellInteract.PendingBellPosition.Value;
            bellTimer = BellDuration;
            bellActive = true;
            state = MonsterState.GoToBell;
            BellInteract.PendingBellPosition = null;
        }

        // 铃铛计时
        if (bellActive)
        {
            bellTimer -= Time.deltaTime;
            if (bellTimer <= 0f)
            {
                bellActive = false;
            }
        }

        float playerDist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(player.position.x, 0, player.position.z)
        );

        // ---- 状态转换（优先级：铃铛 > 截断追击 > 硬控 > 正常） ----

        if (state == MonsterState.Stunned)
        {
            // 硬控期间不可被打断
        }
        else if (bellActive)
        {
            // 铃铛响着：玩家近就追玩家，玩家远就去铃铛
            if (playerDist <= bellInterceptRange)
            {
                state = MonsterState.ChaseFromBell;
            }
            else if (state == MonsterState.ChaseFromBell && playerDist > chaseAbandonRange)
            {
                // 追到距离超过15，放弃追击，回去找铃铛
                state = MonsterState.GoToBell;
            }
            else if (state != MonsterState.ChaseFromBell)
            {
                state = MonsterState.GoToBell;
            }
        }
        else if (state == MonsterState.ChaseFromBell)
        {
            // 铃铛不响了但还在追击：追到距离超过15才放弃
            if (playerDist > chaseAbandonRange)
            {
                state = DetermineNormalState(playerDist);
            }
        }
        else
        {
            // 正常行为
            state = DetermineNormalState(playerDist);
        }

        // ---- 执行状态 ----

        bool lightsOn = RoomLightController.LightsOn;
        bool flashlightOnMonster = IsFlashlightHittingMonster();

        switch (state)
        {
            case MonsterState.Patrol:
                agent.speed = moveSpeed;
                PatrolBehavior();
                break;

            case MonsterState.Chase:
                agent.speed = (lightsOn || flashlightOnMonster) ? moveSpeed : darkChaseSpeed;
                agent.SetDestination(player.position);
                break;

            case MonsterState.GoToBell:
                agent.speed = (lightsOn || flashlightOnMonster) ? moveSpeed : darkChaseSpeed;
                float bellDist = Vector3.Distance(transform.position, bellTarget);
                if (bellDist > 1f)
                    agent.SetDestination(bellTarget);
                else
                {
                    if (agent.hasPath) agent.ResetPath();
                    state = MonsterState.Stunned;
                    stunTimer = StunDuration;
                }
                break;

            case MonsterState.Stunned:
                if (agent.hasPath) agent.ResetPath();
                stunTimer -= Time.deltaTime;
                if (stunTimer <= 0f)
                    state = DetermineNormalState(playerDist);
                break;

            case MonsterState.ChaseFromBell:
                agent.speed = flashlightOnMonster ? moveSpeed : darkChaseSpeed;
                agent.SetDestination(player.position);
                break;
        }

        // 击杀检测（所有状态都有效）
        if (playerDist <= killDistance)
        {
            KillPlayer(player.gameObject);
        }
    }

    /// <summary>
    /// 判断玩家手电筒光锥是否照到怪物身上。
    /// </summary>
    private bool IsFlashlightHittingMonster()
    {
        if (player == null) return false;
        var fc = player.GetComponentInChildren<FlashlightController>(true);
        if (fc == null || !fc.IsOn) return false;

        Vector3 toMonster = transform.position - fc.transform.position;
        float dist = toMonster.magnitude;
        if (dist > 30f) return false;

        Vector3 dir = toMonster.normalized;
        float dot = Vector3.Dot(fc.transform.forward, dir);
        // spotAngle=40 → 半角20° → cos(20°)≈0.94
        return dot >= 0.94f;
    }

    private MonsterState DetermineNormalState(float playerDist)
    {
        // 走廊追击模式：被激活后持续追击，不受灯亮/距离限制
        if (corridorChase)
            return MonsterState.Chase;

        // 图书馆内：灯亮时近距离追，远距离巡逻；灯灭直接追
        if (RoomLightController.LightsOn)
        {
            return playerDist <= lightOnChaseRange ? MonsterState.Chase : MonsterState.Patrol;
        }
        else
        {
            return MonsterState.Chase;
        }
    }

    private void PatrolBehavior()
    {
        if (agent.hasPath && agent.remainingDistance > 0.5f)
            return;

        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f)
        {
            Vector3 randomOffset = Random.insideUnitSphere * patrolRadius;
            randomOffset.y = 0f;
            Vector3 candidate = transform.position + randomOffset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            patrolTimer = patrolWaitTime;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            KillPlayer(collision.gameObject);
        }
    }

    /// <summary>
    /// 杀死玩家：优先触发 DeathSequence 死亡演出；如果没有 DeathSequence 则执行爆炸解体。
    /// </summary>
    private void KillPlayer(GameObject playerObj)
    {
        if (hasKilledPlayer) return;
        hasKilledPlayer = true;

        isActive = false;

        if (agent.isOnNavMesh)
            agent.isStopped = true;

        // 优先触发死亡演出
        var deathSeq = playerObj.GetComponent<DeathSequence>();
        if (deathSeq != null)
        {
            deathSeq.PlayDeathSequence();
            return;
        }

        // 回退：通知 GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDeath();
            return;
        }

        // 最终回退：爆炸解体
        var playerRoot = playerObj.transform;

        var renderers = playerRoot.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            foreach (var rend in renderers)
            {
                var part = rend.transform;
                part.SetParent(null);

                var partRb = part.GetComponent<Rigidbody>();
                if (partRb == null)
                    partRb = part.gameObject.AddComponent<Rigidbody>();

                if (part.GetComponent<Collider>() == null)
                    part.gameObject.AddComponent<BoxCollider>();

                partRb.AddExplosionForce(
                    explosionForce,
                    playerRoot.position,
                    explosionRadius,
                    explosionUpward
                );
            }
        }

        Destroy(playerObj);
    }

    /// <summary>
    /// 玩家死亡复活后重置怪物状态。
    /// </summary>
    public void ResetAfterPlayerDeath()
    {
        hasKilledPlayer = false;
        isActive = false;
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    /// <summary>
    /// 玩家复活后恢复怪物状态：如果怪物曾被激活过，重新激活追踪。
    /// </summary>
    public void ReviveAfterPlayerDeath()
    {
        hasKilledPlayer = false;
        if (hasBeenActivated)
        {
            isActive = true;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }
        }
    }
}
