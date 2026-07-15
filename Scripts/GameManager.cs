using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public Transform player;

    [Header("Death Settings")]
    public float deathFadeDuration = 2f;
    public float respawnDelay = 1.5f;

    private bool isRespawning;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Auto-find player if not assigned
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    public void OnPlayerDeath()
    {
        if (isRespawning) return;
        StartCoroutine(DeathAndRespawn());
    }

    IEnumerator DeathAndRespawn()
    {
        isRespawning = true;

        // 1. Disable player control
        if (player != null)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;

            var interaction = player.GetComponent<PlayerInteraction>();
            if (interaction != null) interaction.enabled = false;
        }

        // 2. Fade to black
        yield return StartCoroutine(FadeScreen(true, deathFadeDuration));

        yield return new WaitForSeconds(respawnDelay);

        // 3. Respawn at checkpoint
        if (CheckpointManager.StaticHasSave())
        {
            EnsurePlayerFound();
            if (player != null && CheckpointManager.Instance != null)
            {
                CheckpointManager.Instance.RespawnPlayer(player);
                RestoreGameState();
            }
        }

        // 4. Re-enable player control
        if (player != null)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = true;

            var interaction = player.GetComponent<PlayerInteraction>();
            if (interaction != null) interaction.enabled = true;
        }

        // 5. Fade back in
        yield return StartCoroutine(FadeScreen(false, deathFadeDuration));

        isRespawning = false;
    }

    void RestoreGameState()
    {
        if (CheckpointManager.Instance == null) return;

        // Restore key state
        EnsurePlayerFound();
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        if (pc != null)
            pc.HasKey = CheckpointManager.Instance.GetSavedHasKey();

        // Reset checkpoint triggers so they can fire again
        var triggers = FindObjectsOfType<CheckpointTrigger>();
        foreach (var t in triggers)
            t.ResetTrigger();
    }

    IEnumerator FadeScreen(bool fadeToBlack, float duration)
    {
        float startFogEnd = RenderSettings.fogEndDistance;
        float startFogStart = RenderSettings.fogStartDistance;
        float startAmbient = RenderSettings.ambientIntensity;

        float targetFogEnd = fadeToBlack ? 0.1f : 12f;
        float targetFogStart = fadeToBlack ? 0f : 1f;
        float targetAmbient = fadeToBlack ? 0f : 0.5f;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            RenderSettings.fog = true;
            RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, targetFogEnd, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, targetFogStart, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(startAmbient, targetAmbient, t);

            if (fadeToBlack)
                RenderSettings.ambientLight = Color.Lerp(
                    new Color(0.05f, 0.005f, 0.005f, 1f), Color.black, t);

            yield return null;
        }
    }

    public void RestartGame()
    {
        CheckpointManager.StaticClearSave();
        TVInteract.submittedTapes = 0;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void ContinueGame()
    {
        if (!CheckpointManager.StaticHasSave())
            return;

        EnsurePlayerFound();

        if (CheckpointManager.Instance == null)
        {
            var go = new GameObject("CheckpointManager");
            go.AddComponent<CheckpointManager>();
        }

        if (player != null && CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.RespawnPlayer(player);
            RestoreGameState();
        }
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    private void EnsurePlayerFound()
    {
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }
}
