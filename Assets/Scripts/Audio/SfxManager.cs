using UnityEngine;

public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Header("Audio Source (2D, One-Shots)")]
    [Tooltip("Beliebiger AudioSource auf diesem GameObject. playOnAwake=false, loop=false.")]
    public AudioSource sfxSource;

    [Header("Clips")]
    [Tooltip("SFX für GameOver im InfinityMode.")]
    public AudioClip infinityGameOverClip;

    [Header("Lautstärke")]
    [Range(0f, 1f)] public float defaultVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;        // 2D
        sfxSource.ignoreListenerPause = true; // spielt auch, wenn global pausiert
    }

    public void PlayInfinityGameOver(float? volume = null)
    {
        if (infinityGameOverClip == null) return;
        sfxSource.PlayOneShot(infinityGameOverClip, Mathf.Clamp01(volume ?? defaultVolume));
    }

    public void PlayOneShot(AudioClip clip, float? volume = null)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume ?? defaultVolume));
    }
}
