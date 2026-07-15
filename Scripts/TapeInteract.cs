using UnityEngine;

public class TapeInteract : MonoBehaviour, IInteractable
{
    // Kept for the later door/progression mechanism.
    public static int tapesCollected = 0;

    private static AudioClip sharedZipperClip;

    public string InteractionPrompt => "拾取磁带";
    public bool CanInteract => true;

    private void OnEnable()
    {
        if (sharedZipperClip == null)
            sharedZipperClip = GenerateZipperClip();
    }

    public void Interact(PlayerInteraction interactor)
    {
        tapesCollected++;

        // Play at the tape position through a temporary source so it survives Destroy.
        if (sharedZipperClip != null)
            AudioSource.PlayClipAtPoint(sharedZipperClip, transform.position, 1f);

        Destroy(gameObject);
    }

    private AudioClip GenerateZipperClip()
    {
        int sampleRate = 44100;
        float duration = 0.5f;
        int totalSamples = Mathf.RoundToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("Zipper", totalSamples, 1, sampleRate, false);
        float[] data = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env;
            if (t < 0.02f)
                env = t / 0.02f;
            else if (t < 0.4f)
                env = 1f;
            else
                env = 1f - (t - 0.4f) / 0.1f;

            float sweepFreq = 800f + 2400f * (t / duration);
            float noise = Mathf.Sin(t * sweepFreq * 2f * Mathf.PI) * 0.3f;
            noise += (Random.value * 2f - 1f) * 0.5f;

            float clickRate = 30f;
            float click = Mathf.Abs(Mathf.Sin(t * clickRate * 2f * Mathf.PI)) > 0.5f ? 1f : 0.3f;
            data[i] = Mathf.Clamp(noise * env * click * 0.6f, -1f, 1f);
        }

        clip.SetData(data, 0);
        return clip;
    }
}
