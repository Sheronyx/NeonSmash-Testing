using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("Fade UI")]
    public Canvas fadeCanvas;
    public Image  fadeImage;
    public float  fadeDuration = 1f;

    /// <summary>True solange der Fade gerade läuft (Bild noch nicht vollständig transparent).</summary>
    public bool IsFading => fadeImage != null && fadeImage.color.a > 0.01f;

    private bool _isLoading = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetAlpha(1f); // Start: schwarz
        if (fadeCanvas != null) fadeCanvas.enabled = true;
    }

    void Start()
    {
        if (fadeImage != null && fadeImage.canvas != null)
            fadeImage.canvas.enabled = true;
    }

    // ===== ÖFFENTLICHE APIS =====

    // Variante A: Klick hier drin abspielen lassen (kein UIButtonSound nötig)
    public void LoadSceneWithClick(string sceneName, AudioClip click, float minDelay = 0.08f)
    {
        if (_isLoading) return;
        _isLoading = true;
        if (click != null && UIAudio.Instance != null)
            UIAudio.Instance.PlayOneShot(click);   // UIAudio ist DontDestroyOnLoad + ignoreListenerPause

        StartCoroutine(FadeAndSwitchAsync(sceneName, minDelay));
    }

    // Variante B: Du spielst den Klick bereits extern (z. B. per UIButtonSound)
    public void LoadSceneDelayed(string sceneName, float minDelay = 0.08f)
    {
        if (_isLoading) return;
        _isLoading = true;
        StartCoroutine(FadeAndSwitchAsync(sceneName, minDelay));
    }

    // Alte API, falls irgendwo noch verwendet
    public void LoadScene(string sceneName)
    {
        if (_isLoading) return;
        _isLoading = true;
        StartCoroutine(FadeAndSwitchAsync(sceneName, 0.0f));
    }

    /// <summary>Lädt eine Szene und lässt den Bildschirm schwarz – kein FadeFromBlack.
    /// Die Zielszene (z.B. IntroScene) übernimmt selbst die Kontrolle über den Fade.</summary>
    public void LoadSceneKeepBlack(string sceneName)
    {
        if (_isLoading) return;
        _isLoading = true;
        StartCoroutine(LoadAndKeepBlackAsync(sceneName));
    }

    private IEnumerator LoadAndKeepBlackAsync(string sceneName)
    {
        // Canvas schwarz halten (Awake hat alpha=1 gesetzt)
        if (fadeCanvas != null) fadeCanvas.enabled = true;
        SetAlpha(1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        yield return null;
        yield return null;

        _isLoading = false;
        // Kein FadeFromBlack – die Zielszene steuert selbst wann aufgeblendet wird
    }

    /// <summary>Startet FadeToBlack mit eigener Dauer asynchron (fire-and-forget, läuft auf SceneFader).</summary>
    public void StartFadeToBlack(float dur)
    {
        StartCoroutine(FadeToBlackWithDuration(dur));
    }

    private IEnumerator FadeToBlackWithDuration(float dur)
    {
        float saved = fadeDuration;
        fadeDuration = dur;
        if (fadeCanvas != null) fadeCanvas.enabled = true;
        yield return Fade(0f, 1f);
        fadeDuration = saved;
    }

    /// <summary>Aktiviert eine bereits per LoadSceneAsync vorgeladene Operation mit kurzem Fade.
    /// Coroutine läuft auf SceneFader (DontDestroyOnLoad) – überlebt den Szenen-Wechsel.</summary>
    public void ActivatePreloaded(AsyncOperation op, float fadeDur = 0.3f)
    {
        if (_isLoading) return;
        _isLoading = true;
        StartCoroutine(ActivatePreloadedAsync(op, fadeDur));
    }

    private IEnumerator ActivatePreloadedAsync(AsyncOperation op, float fadeDur)
    {
        float saved = fadeDuration;
        fadeDuration = fadeDur;

        // Sofort schwarz – kein FadeToBlack nötig, Animation ist bereits der Übergang
        if (fadeCanvas != null) fadeCanvas.enabled = true;
        SetAlpha(1f);

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        yield return null;
        yield return null;

        yield return FadeFromBlack();

        fadeDuration = saved;
        _isLoading = false;
    }

    // ===== KERN-LOGIK =====
    private IEnumerator FadeAndSwitchAsync(string sceneName, float minDelay)
{
    // 1️⃣ Bildschirm schwarz machen
    yield return FadeToBlack();


    // 2️⃣ Async Szene laden
    AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
    op.allowSceneActivation = false;

    float timer = 0f;

    // 3️⃣ Laden bis 90%
    while (op.progress < 0.9f)
    {
        timer += Time.unscaledDeltaTime;
        yield return null;
    }

    // Optionaler Delay (für Button Click Sound)
    while (timer < minDelay)
    {
        timer += Time.unscaledDeltaTime;
        yield return null;
    }

    // 4️⃣ Szene aktivieren
    op.allowSceneActivation = true;

    // 5️⃣ Warten bis Szene komplett geladen ist
    while (!op.isDone)
        yield return null;

    // 6️⃣ Stabilisierung Frames (sehr wichtig)
    yield return null;
    yield return null;

    // 7️⃣ Jetzt erst Fade öffnen
    yield return FadeFromBlack();
    _isLoading = false;
}

    // ===== FADES =====
    public IEnumerator FadeToBlack()
    {
        if (fadeCanvas != null) fadeCanvas.enabled = true;
        yield return Fade(0f, 1f);
    }

    public IEnumerator FadeFromBlack()
    {
        if (fadeCanvas != null) fadeCanvas.enabled = true;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(1f, 0f, t / fadeDuration));
            yield return null;
        }
        SetAlpha(0f);
        if (fadeCanvas != null) fadeCanvas.enabled = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        if (fadeImage == null) return;
        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
        fadeImage.raycastTarget = a > 0.01f; // blockt UI nur, wenn sichtbar
    }
}
