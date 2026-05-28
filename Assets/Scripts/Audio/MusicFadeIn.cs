using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    // <-- von außen setzen (z. B. beim Restart-Button), damit die Game-Musik beim nächsten Game-Load garantiert neu startet
    public static bool ForceRestartGameMusicNextLoad = false;

    [Header("Audio Sources")]
    public AudioSource menuSource;   // Menü-Musik
    public AudioSource gameSource;   // Spiel-Musik

    [Header("Fades")]
    [Tooltip("Dauer für Crossfades zwischen Menü- und Spielmusik.")]
    public float fadeDuration = 1.5f;

    [Header("Game Music Speed-Up")]
    [Tooltip("Pitch-Zuwachs pro Level (z. B. 0.05 = +5%).")]
    public float pitchStepPerLevel = 0.05f;
    [Tooltip("Harte Obergrenze für den Pitch der Spielmusik.")]
    public float maxGamePitch = 2f;

    [Header("Pitch Ramp (Artefakt-Vermeidung)")]
    [Tooltip("Dauer der sanften Pitch-Änderung.")]
    public float pitchRampSeconds = 0.3f;
    [Tooltip("Beim Rampen kurz etwas leiser machen (0..1). 1 = kein Ducking.")]
    [Range(0f, 1f)] public float pitchRampVolumeDuck = 0.9f;
    [Tooltip("Dauer, um nach dem Rampen das Volume zurückzublenden.")]
    public float pitchUnduckSeconds = 0.15f;

    private static MusicManager instance;
    public static MusicManager Instance => instance;

    private Coroutine fadeRoutine;
    private Coroutine pitchRoutine;

    // Merker, ob wir uns logisch im Game befinden (für Transition-Logik)
    private bool inGameContext = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void Start()
    {
        // Aktive Szene prüfen (App kann im Menü ODER direkt im Game starten)
        var active = SceneManager.GetActiveScene();
        inGameContext = IsGameScene(active.name);

        // Grundsetup
        if (menuSource != null)
        {
            menuSource.loop = true;
            menuSource.playOnAwake = false;
            menuSource.volume = 0f;
        }
        if (gameSource != null)
        {
            gameSource.loop = true;
            gameSource.playOnAwake = false;
            gameSource.volume = 0f;
            gameSource.pitch = 1f;
        }

        // Falls wir direkt in einer Game-Scene sind UND das Flag gesetzt ist -> sofort harter Neustart des Game-Tracks
        if (inGameContext && ForceRestartGameMusicNextLoad)
        {
            ForceRestartGameMusicNextLoad = false;
            RestartGameMusicFromZeroNow();
            return;
        }

        // Startverhalten je nach aktueller Szene
        if (inGameContext)
        {
            PrepareAndPlayFromStart(gameSource); // Spiel läuft -> Game-Track ab 0:00
            if (gameSource != null) FadeTo(gameSource, 1f);
        }
        else
        {
            PrepareAndPlayFromStart(menuSource); // Menü -> Menü-Track ab 0:00
            if (menuSource != null) FadeTo(menuSource, 1f);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool wasInGame = inGameContext;
        bool nowInGame = IsGameScene(scene.name);
        inGameContext = nowInGame;

        // Harte Anweisung: Beim nächsten Game-Load MUSS neu gestartet werden
        if (nowInGame && ForceRestartGameMusicNextLoad)
        {
            ForceRestartGameMusicNextLoad = false;
            RestartGameMusicFromZeroNow();
            return; // nichts weiter tun
        }

        // Transition MENÜ -> GAME  (Spielstart)
        if (!wasInGame && nowInGame)
        {
            // Game-Track frisch von 0:00 starten
            PrepareAndPlayFromStart(gameSource);
            // Menü-Track weich ausblenden und ANSCHLIESSEND hart resetten
            Crossfade(menuSource, gameSource, resetFromAtEnd: true);

            ResetGameMusicSpeed(); // Pitch 1.0 zum Start
            return;
        }

        // Transition GAME -> MENÜ  (Spielende)
        if (wasInGame && !nowInGame)
        {
            // Menü-Track frisch von 0:00 starten
            PrepareAndPlayFromStart(menuSource);
            // Game-Track weich ausblenden und ANSCHLIESSEND hart resetten
            Crossfade(gameSource, menuSource, resetFromAtEnd: true);

            ResetGameMusicSpeed(); // Sicher normalisieren
            return;
        }

        // Gleicher Kontext wie vorher (Menü->Menü ODER Game->Game):
        // -> KEIN Reset/Restart, nur sicherstellen, dass der richtige Track spielt.
        if (nowInGame)
        {
            EnsurePlaying(gameSource);
            PauseSilently(menuSource);
        }
        else
        {
            EnsurePlaying(menuSource);
            PauseSilently(gameSource);
        }
    }

    private bool IsGameScene(string sceneName)
    {
        return sceneName == "GameScene_InfinityMode";
    }

    // ========= Utilities =========

    /// <summary>Stoppt Quelle, setzt Position auf 0, Pitch/Volume sinnvoll und spielt von Anfang.</summary>
    private void PrepareAndPlayFromStart(AudioSource src)
    {
        if (src == null) return;
        src.Stop();            // setzt auch timeSamples = 0
        src.time = 0f;
        src.timeSamples = 0;
        if (src == gameSource) src.pitch = 1f; // Sicherheit
        src.volume = 0f;       // wird rein-gefaded
        src.Play();
    }

    /// <summary>Stop & Reset: Stoppt Quelle und setzt sie auf Anfang, spielt aber NICHT.</summary>
    private void StopAndReset(AudioSource src)
    {
        if (src == null) return;
        src.Stop();
        src.time = 0f;
        src.timeSamples = 0;
        if (src == gameSource) src.pitch = 1f;
        src.volume = 0f;
    }

    private void EnsurePlaying(AudioSource src)
    {
        if (src == null) return;
        if (!src.isPlaying) src.Play();
    }

    private void PauseSilently(AudioSource src)
    {
        if (src == null) return;
        // Nicht stoppen/resetten, nur pausieren+Volume 0, damit kein Restart passiert
        src.volume = 0f;
        if (src.isPlaying) src.Pause();
    }

    // ========= Crossfades =========

    private void Crossfade(AudioSource from, AudioSource to, bool resetFromAtEnd)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTracks(from, to, resetFromAtEnd));
    }

    private IEnumerator FadeTracks(AudioSource from, AudioSource to, bool resetFromAtEnd)
    {
        float time = 0f;
        float fromStart = from != null ? from.volume : 0f;
        float toStart   = to   != null ? to.volume   : 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / fadeDuration;
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, t);
            if (to   != null) to.volume   = Mathf.Lerp(toStart,   1f, t);
            yield return null;
        }

        if (from != null)
        {
            if (resetFromAtEnd)
            {
                StopAndReset(from); // nach Fade-Out hart stoppen & auf Anfang setzen
            }
            else
            {
                from.volume = 0f;
                from.Pause(); // CPU sparen
            }
        }
        if (to != null) to.volume = 1f;
    }

    private void FadeTo(AudioSource source, float target)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeSingle(source, target));
    }

    private IEnumerator FadeSingle(AudioSource source, float target)
    {
        if (source == null) yield break;

        float start = source.volume;
        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, target, time / fadeDuration);
            yield return null;
        }
        source.volume = target;
    }

    // ========= Pitch-API (smooth) =========

private int levelCounter = 0; // 👈 neu: zählt Level-Aufrufe

public void IncreaseGameMusicSpeed(float? customStep = null)
{
    if (gameSource == null || !gameSource.isPlaying) return;

    levelCounter++;

    // 👇 Nur jedes 3. Level beschleunigen
    if (levelCounter % 3 != 0)
    {
        Debug.Log($"[MusicManager] Level {levelCounter}: kein Speed-Up diesmal.");
        return;
    }

    float step = customStep ?? pitchStepPerLevel;
    float target = Mathf.Min(gameSource.pitch + step, maxGamePitch);

    if (pitchRoutine != null) StopCoroutine(pitchRoutine);
    pitchRoutine = StartCoroutine(Co_SmoothPitchTo(target, pitchRampSeconds));

    Debug.Log($"[MusicManager] Level {levelCounter}: Musik-Speed erhöht auf {target:F2}");
}


    public void ResetGameMusicSpeed()
    {
        if (pitchRoutine != null) { StopCoroutine(pitchRoutine); pitchRoutine = null; }
        if (gameSource == null) return;
        gameSource.pitch = 1f;
    }

    public void ResetGameOnGameOver()
    {
        ResetGameMusicSpeed();
        StopAndReset(gameSource);
    }

    private IEnumerator Co_SmoothPitchTo(float target, float duration)
    {
        if (gameSource == null) yield break;

        float startPitch = gameSource.pitch;
        if (Mathf.Approximately(startPitch, target) || duration <= 0f)
        {
            gameSource.pitch = target;
            yield break;
        }

        float volStart = gameSource.volume;
        float duckVol  = Mathf.Clamp01(volStart * pitchRampVolumeDuck);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = t / duration;

            // Cosine-Ease (sanfter als linear)
            float e = 0.5f - 0.5f * Mathf.Cos(k * Mathf.PI);
            gameSource.pitch  = Mathf.Lerp(startPitch, target, e);

            // sanftes Ducking hin
            float duckK = Mathf.SmoothStep(0f, 1f, Mathf.Min(k * 2f, 1f));
            gameSource.volume = Mathf.Lerp(volStart, duckVol, duckK);

            yield return null;
        }
        gameSource.pitch = target;

        // Unduck zurück
        t = 0f;
        while (t < pitchUnduckSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = t / Mathf.Max(0.0001f, pitchUnduckSeconds);
            gameSource.volume = Mathf.Lerp(duckVol, volStart, k);
            yield return null;
        }
        gameSource.volume = volStart;

        pitchRoutine = null;
    }

    // ===== Öffentliche API: Shop-Preview =====
    public void PauseForPreview()
    {
        if (menuSource != null && menuSource.isPlaying) menuSource.Pause();
    }

    public void ResumeAfterPreview()
    {
        if (menuSource != null && !menuSource.isPlaying) menuSource.UnPause();
    }

    // ===== Öffentliche API: Sound-Theme wechseln =====
    public void ApplyMenuClip(AudioClip clip)
    {
        if (clip == null || menuSource == null) return;
        menuSource.clip = clip;
        if (!inGameContext) RestartMenuMusicFromZeroNow();
    }

    public void ApplyGameClip(AudioClip clip)
    {
        if (clip == null || gameSource == null) return;
        gameSource.clip = clip;
    }

    public void RestartMenuMusicFromZeroNow()
    {
        PrepareAndPlayFromStart(menuSource);
        FadeTo(menuSource, 1f);
        StopAndReset(gameSource);
    }

    // ===== Öffentliche API: Game-Musik jetzt sofort neu starten =====
    public void RestartGameMusicFromZeroNow()
    {
        // Startet Game-Track sauber neu und sorgt dafür, dass Menü-Track stumm/auf Anfang ist
        PrepareAndPlayFromStart(gameSource);
        FadeTo(gameSource, 1f);
        StopAndReset(menuSource);
    }
}
