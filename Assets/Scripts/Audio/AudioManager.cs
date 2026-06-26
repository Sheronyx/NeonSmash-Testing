using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Clips")]
    public AudioClip normalPointClip;
    public AudioClip swipePointClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource != null) sfxSource.playOnAwake = false;
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    // streak = aktueller Kombo-Streak NACH dem Treffer (aus ComboManager.ComboCount).
    // Kein Argument nötig für Nicht-Farb-Treffer (PeekElement, Gravity, Fountain) → streak 0 → Originalpitch.
    public void PlayNormalPoint(int streak = 0)
    {
        if (normalPointClip == null || sfxSource == null) return;
        sfxSource.pitch = ComboPitch(streak);
        sfxSource.PlayOneShot(normalPointClip);
    }

    public void PlaySwipePoint(int streak = 0)
    {
        if (swipePointClip == null || sfxSource == null) return;
        sfxSource.pitch = ComboPitch(streak);
        sfxSource.PlayOneShot(swipePointClip);
    }

    private static float ComboPitch(int streak) => streak switch
    {
        <= 1 => 1.0f,
        2    => 1.1f,
        3    => 1.2f,
        4    => 1.3f,
        5    => 1.4f,
        _    => 0.7f   // streak >= 6, Kombomodus läuft schon
    };
}
