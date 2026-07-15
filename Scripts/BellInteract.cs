using UnityEngine;

public class BellInteract : MonoBehaviour
{
    public Transform player;
    public float interactionRange = 2f;

    // 怪物监听：按下铃时写入位置，MonsterController 消费后置 null
    public static Vector3? PendingBellPosition;

    private bool playerNear;
    private bool bellRinging;
    private float ringTimer;
    private AudioSource audioSrc;
    private GUIStyle uiStyle;
    private AudioClip bellClip;

    private const float RingDuration = 10f;

    void Start()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0.8f;
        audioSrc.minDistance = 1f;
        audioSrc.maxDistance = 30f;
        bellClip = GenerateBellClip();
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        playerNear = dist < interactionRange;

        if (playerNear && Input.GetKeyDown(KeyCode.F) && !bellRinging)
        {
            bellRinging = true;
            ringTimer = 0f;
            if (bellClip != null)
                audioSrc.PlayOneShot(bellClip, 1f);

            // 通知怪物前往铃铛位置
            PendingBellPosition = transform.position;
        }

        if (bellRinging)
        {
            ringTimer += Time.deltaTime;
            if (ringTimer >= RingDuration)
                bellRinging = false;
        }
    }

    void OnGUI()
    {
        if (uiStyle == null)
        {
            uiStyle = new GUIStyle(GUI.skin.label);
            uiStyle.fontSize = 24;
            uiStyle.alignment = TextAnchor.MiddleCenter;
            uiStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
        }

        if (playerNear && !bellRinging)
        {
            Rect rect = new Rect(Screen.width / 2 - 100, Screen.height / 2 + 80, 200, 40);
            GUI.Label(rect, "【按铃】 按 F", uiStyle);
        }
    }

    AudioClip GenerateBellClip()
    {
        int sampleRate = 44100;
        float duration = RingDuration;
        int totalSamples = Mathf.RoundToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("Bell", totalSamples, 1, sampleRate, false);
        float[] data = new float[totalSamples];

        float freq1 = 440f;
        float freq2 = 880f;
        float freq3 = 1320f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 0.4f);
            float s = Mathf.Sin(2 * Mathf.PI * freq1 * t) * 0.4f;
            s += Mathf.Sin(2 * Mathf.PI * freq2 * t) * 0.2f;
            s += Mathf.Sin(2 * Mathf.PI * freq3 * t) * 0.1f;
            s *= env;
            if (t > 5f)
            {
                float env2 = Mathf.Exp(-(t - 5f) * 0.4f);
                s += (Mathf.Sin(2 * Mathf.PI * freq1 * t) * 0.3f + Mathf.Sin(2 * Mathf.PI * freq2 * t) * 0.15f) * env2;
            }
            data[i] = Mathf.Clamp(s * 0.5f, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }
}
