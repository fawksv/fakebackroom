using UnityEngine;

/// <summary>
/// 单例：跟踪检查点位置，处理死亡后复活逻辑。
/// 挂在 Player 上，订阅 DeathSequence.OnRespawnRequested。
/// 兼容旧的 SaveCheckpoint / SaveGameState / StaticHasSave 等 API。
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    private const string PosXKey = "Checkpoint.PosX";
    private const string PosYKey = "Checkpoint.PosY";
    private const string PosZKey = "Checkpoint.PosZ";
    private const string RotYKey = "Checkpoint.RotY";
    private const string HasSaveKey = "Checkpoint.HasSave";
    private const string TapesKey = "Checkpoint.Tapes";
    private const string HasKeyKey = "Checkpoint.HasKey";
    private const string MonstersKey = "Checkpoint.MonstersActive";

    private Vector3 checkpointPos;
    private Quaternion checkpointRot;
    private bool hasCheckpoint;

    private int savedTapes;
    private bool savedHasKey;
    private bool savedMonstersActive;

    public bool HasCheckpoint => hasCheckpoint;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        var deathSeq = GetComponent<DeathSequence>();
        if (deathSeq != null)
            deathSeq.OnRespawnRequested += RespawnAtCheckpoint;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── 新 API（EscapeZoneTrigger / DeathSequence 使用） ──────────

    public void SetCheckpoint(Vector3 pos, Quaternion rot)
    {
        checkpointPos = pos;
        checkpointRot = rot;
        hasCheckpoint = true;
    }

    // ── 旧 API 兼容 ──────────────────────────────────────────────

    public bool HasSave()
    {
        return hasCheckpoint || PlayerPrefs.GetInt(HasSaveKey, 0) == 1;
    }

    public static bool StaticHasSave()
    {
        if (Instance != null) return Instance.HasSave();
        return PlayerPrefs.GetInt(HasSaveKey, 0) == 1;
    }

    public static void StaticClearSave()
    {
        PlayerPrefs.DeleteKey(HasSaveKey);
        PlayerPrefs.DeleteKey(PosXKey);
        PlayerPrefs.DeleteKey(PosYKey);
        PlayerPrefs.DeleteKey(PosZKey);
        PlayerPrefs.DeleteKey(RotYKey);
        PlayerPrefs.DeleteKey(TapesKey);
        PlayerPrefs.DeleteKey(HasKeyKey);
        PlayerPrefs.DeleteKey(MonstersKey);
        if (Instance != null) Instance.hasCheckpoint = false;
    }

    public void SaveCheckpoint(Vector3 pos, float rotY)
    {
        checkpointPos = pos;
        checkpointRot = Quaternion.Euler(0, rotY, 0);
        hasCheckpoint = true;

        PlayerPrefs.SetFloat(PosXKey, pos.x);
        PlayerPrefs.SetFloat(PosYKey, pos.y);
        PlayerPrefs.SetFloat(PosZKey, pos.z);
        PlayerPrefs.SetFloat(RotYKey, rotY);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.Save();
    }

    public void SaveGameState(int tvTapes, bool hasKey, bool monstersActive)
    {
        savedTapes = tvTapes;
        savedHasKey = hasKey;
        savedMonstersActive = monstersActive;

        PlayerPrefs.SetInt(TapesKey, tvTapes);
        PlayerPrefs.SetInt(HasKeyKey, hasKey ? 1 : 0);
        PlayerPrefs.SetInt(MonstersKey, monstersActive ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool GetSavedHasKey()
    {
        return savedHasKey || PlayerPrefs.GetInt(HasKeyKey, 0) == 1;
    }

    public void RespawnPlayer(Transform playerTransform)
    {
        Vector3 pos = checkpointPos;
        Quaternion rot = checkpointRot;

        if (!hasCheckpoint && PlayerPrefs.GetInt(HasSaveKey, 0) == 1)
        {
            pos = new Vector3(
                PlayerPrefs.GetFloat(PosXKey),
                PlayerPrefs.GetFloat(PosYKey),
                PlayerPrefs.GetFloat(PosZKey));
            rot = Quaternion.Euler(0, PlayerPrefs.GetFloat(RotYKey), 0);
        }

        var cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerTransform.position = pos;
        playerTransform.rotation = rot;

        if (cc != null) cc.enabled = true;

        var cam = playerTransform.GetComponentInChildren<Camera>();
        if (cam != null)
            cam.transform.localRotation = Quaternion.identity;

        var pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null)
            pc.enabled = true;

        // Restore game state
        TVInteract.submittedTapes = PlayerPrefs.GetInt(TapesKey, 0);

        var ds = playerTransform.GetComponent<DeathSequence>();
        if (ds != null)
            ds.ResetSequence();

        Debug.Log("[CheckpointManager] 玩家已在检查点复活。");
    }

    // ── 死亡演出复活（DeathSequence 回调） ───────────────────────

    private void RespawnAtCheckpoint()
    {
        if (!HasSave())
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            return;
        }

        RespawnPlayer(transform);

        // 重置所有怪物
        var monsters = FindObjectsByType<MonsterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in monsters)
        {
            if (m != null)
                m.ResetAfterPlayerDeath();
        }
    }
}
