using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Clips")]
    public AudioClip normalPointClip;
    public AudioClip swipePointClip;

    [Header("Tonhoehe")]
    [Tooltip("Tonhoehen, die beim Zerstoeren der Reihe nach durchlaufen werden. Der Zaehler ist " +
             "GEMEINSAM fuer Tap und Swipe -- es zaehlt also jedes zerstoerte Element, egal wie. " +
             "Nach der letzten Stufe geht es wieder bei der ersten los.")]
    [SerializeField] private float[] pitchStufen = { 0.8f, 0.9f, 1.0f, 1.1f, 1.2f };

    private int _pitchStufe;

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

    // Der streak-Parameter wird nicht mehr ausgewertet: die Tonhoehe laeuft jetzt als feste
    // Leiter durch pitchStufen, unabhaengig von der Kombo. Die Signatur bleibt, damit die
    // bestehenden Aufrufe (MixedPointSpawner, Gravity, Fountain, PeekElement) unveraendert bleiben.
    public void PlayNormalPoint(int streak = 0)
    {
        if (normalPointClip == null || sfxSource == null) return;
        sfxSource.pitch = NaechsteTonhoehe();
        sfxSource.PlayOneShot(normalPointClip);
    }

    public void PlaySwipePoint(int streak = 0)
    {
        if (swipePointClip == null || sfxSource == null) return;
        sfxSource.pitch = NaechsteTonhoehe();
        sfxSource.PlayOneShot(swipePointClip);
    }

    // Eine Stufe weiter und wieder von vorn, wenn die letzte erreicht ist. Bewusst EIN Zaehler
    // fuer beide Sounds -- die Leiter soll ueber alle zerstoerten Elemente hinweg steigen, nicht
    // je Element-Art getrennt.
    private float NaechsteTonhoehe()
    {
        if (pitchStufen == null || pitchStufen.Length == 0) return 1f;
        if (_pitchStufe >= pitchStufen.Length) _pitchStufe = 0;
        float h = pitchStufen[_pitchStufe];
        _pitchStufe = (_pitchStufe + 1) % pitchStufen.Length;
        return h;
    }

    /// <summary>Setzt die Tonhoehen-Leiter auf die erste Stufe zurueck. Fuer Rundenstart oder
    /// wenn sie irgendwann an den Kombo-Abbruch gekoppelt werden soll.</summary>
    public void ResetTonhoehe() => _pitchStufe = 0;
}
