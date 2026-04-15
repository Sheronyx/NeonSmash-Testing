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
