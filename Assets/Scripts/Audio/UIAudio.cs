using UnityEngine;
using UnityEngine.Audio;

public class UIAudio : MonoBehaviour
{
    public static UIAudio Instance { get; private set; }

    private AudioSource src;

    [Header("Standard-Klaenge")]
    [Tooltip("Fuer Knoepfe, die ein Fenster OEFFNEN oder den Tab WECHSELN.")]
    [SerializeField] private AudioClip openSwitchClip;
    [Tooltip("Fuer Knoepfe, die ein Fenster SCHLIESSEN.")]
    [SerializeField] private AudioClip closeClip;
    [Range(0f, 1f)] [SerializeField] private float uiVolume = 1f;

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

    /// <summary>Oeffnen eines Fensters oder Wechseln des Tabs.</summary>
    public void PlayOpenSwitch() => PlayOneShot(openSwitchClip, uiVolume);

    /// <summary>Schliessen eines Fensters.</summary>
    public void PlayClose() => PlayOneShot(closeClip, uiVolume);

    public bool IsPlaying => src != null && src.isPlaying;
}
