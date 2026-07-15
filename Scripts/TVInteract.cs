using UnityEngine;

public class TVInteract : MonoBehaviour, IInteractable
{
    // Kept for the later door/progression mechanism.
    public static int submittedTapes = 0;

    private GUIStyle resultStyle;
    private AudioSource audioSrc;
    private AudioClip insertClip;

    private float displayTimer;
    private bool displaying;
    private const float HoldTime = 0.5f;
    private const float FadeTime = 0.5f;
    private float displayAlpha;

    public string InteractionPrompt => TapeInteract.tapesCollected > 0 ? "提交磁带" : "查看磁带进度";
    public bool CanInteract => true;

    private void Start()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0.8f;
        audioSrc.minDistance = 1f;
        audioSrc.maxDistance = 15f;
        insertClip = GenerateInsertClip();
    }

    private void Update()
    {
        if (!displaying)
            return;

        displayTimer += Time.deltaTime;
        if (displayTimer < HoldTime)
            displayAlpha = 1f;
        else if (displayTimer < HoldTime + FadeTime)
            displayAlpha = 1f - (displayTimer - HoldTime) / FadeTime;
        else
        {
            displaying = false;
            displayAlpha = 0f;
        }
    }

    public void Interact(PlayerInteraction interactor)
    {
        if (TapeInteract.tapesCollected > 0)
        {
            submittedTapes += TapeInteract.tapesCollected;
            TapeInteract.tapesCollected = 0;

            if (insertClip != null)
                audioSrc.PlayOneShot(insertClip, 1f);
        }

        displaying = true;
        displayTimer = 0f;
        displayAlpha = 1f;
    }

    private void OnGUI()
    {
        if (!displaying)
            return;

        if (resultStyle == null)
        {
            resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                alignment = TextAnchor.MiddleCenter
            };
        }

        resultStyle.normal.textColor = new Color(0.4f, 1f, 0.4f, displayAlpha);
        Rect rect = new Rect(Screen.width / 2f - 100f, Screen.height / 2f - 20f, 200f, 50f);
        GUI.Label(rect, $"{submittedTapes}/20", resultStyle);
    }

    private AudioClip GenerateInsertClip()
    {
        int sampleRate = 44100;
        float duration = 0.4f;
        int totalSamples = Mathf.RoundToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("TapeInsert", totalSamples, 1, sampleRate, false);
        float[] data = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            if (t < 0.05f)
                sample += (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.5f;
            if (t > 0.12f && t < 0.17f)
                sample += (Random.value * 2f - 1f) * Mathf.Exp(-(t - 0.12f) * 60f) * 0.4f;

            sample += Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t * 8f) * 0.3f;
            data[i] = Mathf.Clamp(sample, -1f, 1f);
        }

        clip.SetData(data, 0);
        return clip;
    }
}
