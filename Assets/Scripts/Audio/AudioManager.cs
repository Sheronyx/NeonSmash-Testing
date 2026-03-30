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

    // Hilfsfunktionen für Komfort
    public void PlayNormalPoint()
    {
        PlaySfx(normalPointClip);
    }

    public void PlaySwipePoint()
    {
        PlaySfx(swipePointClip);
    }
}
