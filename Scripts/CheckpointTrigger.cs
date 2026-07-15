using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField] private string checkpointName = "Checkpoint";

    private bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;
        if (CheckpointManager.Instance == null) return;

        Transform player = other.transform.root;
        CheckpointManager.Instance.SaveCheckpoint(transform.position, player.eulerAngles.y);

        // Save game state
        int tvTapes = TVInteract.submittedTapes;
        bool hasKey = false;
        var pc = player.GetComponent<PlayerController>();
        if (pc != null) hasKey = pc.HasKey;

        bool monstersActive = false;
        var spawner = FindObjectOfType<MonsterSpawner>();
        if (spawner != null)
        {
            // Check if monsters have been activated by looking at ExitTrigger
            var exitTrigger = FindObjectOfType<ExitTrigger>();
            monstersActive = exitTrigger != null && exitTrigger.HasTriggered;
        }

        CheckpointManager.Instance.SaveGameState(tvTapes, hasKey, monstersActive);
        triggered = true;

        Debug.Log($"[Checkpoint] Saved: {checkpointName} at {transform.position}");
    }

    // Allow re-triggering after respawn
    public void ResetTrigger()
    {
        triggered = false;
    }
}
