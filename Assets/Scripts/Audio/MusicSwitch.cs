using UnityEngine;
using UnityEngine.Audio;

public class AudioSwitch : MonoBehaviour
{
    public static AudioSwitch Instance { get; private set; }

    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private string[] volumeParams = { "MasterVolume" }; // alle zu steuernden Params

    [Header("dB-Werte")]
    [SerializeField] private float onDb = -10f;  // angenehme Lautstärke
    [SerializeField] private float offDb = -80f; // stumm

    private const string PrefKey = "audio_enabled";
    public bool AudioEnabled { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        AudioEnabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
        Apply(AudioEnabled);
    }

    public void SetEnabled(bool enabled)
    {
        AudioEnabled = enabled;
        PlayerPrefs.SetInt(PrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Apply(enabled);
    }

    private void Apply(bool enabled)
    {
        float target = enabled ? onDb : offDb;
        foreach (var p in volumeParams)
            if (!string.IsNullOrEmpty(p)) masterMixer.SetFloat(p, target);
    }
}
