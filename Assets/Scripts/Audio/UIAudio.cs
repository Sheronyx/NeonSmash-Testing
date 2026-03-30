using UnityEngine;
using UnityEngine.Audio;

public class UIAudio : MonoBehaviour
{
    public static UIAudio Instance { get; private set; }

    private AudioSource src;

    [Header("Audio Routing")]
    [SerializeField] private AudioMixerGroup uiGroup; // <-- MasterMixer/UI zuweisen

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        src = GetComponent<AudioSource>();
        if (src == null)
            src = gameObject.AddComponent<AudioSource>();

        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;

        if (uiGroup != null)
            src.outputAudioMixerGroup = uiGroup; // <-- WICHTIG: auf UI routen
    }

    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        src.PlayOneShot(clip, volume); // läuft nun durch die UI-Group
    }

    public bool IsPlaying => src != null && src.isPlaying;
}
