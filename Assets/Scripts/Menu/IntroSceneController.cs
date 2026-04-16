using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class IntroSceneController : MonoBehaviour
{
    public RawImage logo;
    public float displayTime = 1f;
    public string nextScene = "MainMenuScene";

    public float durationPulse = 1.0f;
    public float shrinkAmount = 0.9f;
    public float growAmount = 1.15f;

    public float baseScale = 0.25f; // 👈 Deine gewünschte Grundgröße

    private void Start()
    {
        // Logo zu Beginn auf Basisgröße setzen
        logo.transform.localScale = Vector3.one * baseScale;

        StartCoroutine(PlayIntro());
    }

    private IEnumerator PlayIntro()
    {
        // Warten bis VFX-Warmup abgeschlossen ist (läuft während Bildschirm schwarz ist)
        var warmup = FindFirstObjectByType<VFXWarmup>();
        if (warmup != null)
            yield return new WaitUntil(() => warmup.IsComplete);

        // Nächste Szene im Hintergrund vorladen während die Animation läuft
        AsyncOperation preload = SceneManager.LoadSceneAsync(nextScene);
        preload.allowSceneActivation = false;

        if (SceneFader.Instance != null)
            yield return SceneFader.Instance.FadeFromBlack();

        // Heartbeat-Animation (Puls + kurzes Absinken)
        yield return ScaleOverTime(logo.transform, Vector3.one * baseScale * shrinkAmount, durationPulse * 0.25f);
        yield return ScaleOverTime(logo.transform, Vector3.one * baseScale * growAmount,   durationPulse * 0.35f);
        yield return ScaleOverTime(logo.transform, Vector3.one * baseScale,                durationPulse * 0.4f);
        yield return MoveOverTime(logo.transform, Vector3.down * 400f, 0.3f, relative: true);

        // Sicherstellen dass Vorladen bei 90% ist (sollte längst fertig sein)
        while (preload.progress < 0.9f) yield return null;

        // FadeToBlack startet gleichzeitig mit dem Logo-Abschuss (beide 0.4s)
        if (SceneFader.Instance != null)
            SceneFader.Instance.StartFadeToBlack(0.4f);
        yield return MoveUpAndOut(logo.transform, 2800f, 0.4f);

        // Screen ist jetzt schwarz, Szene sofort aktivieren → kurzes FadeFromBlack im Menü
        if (SceneFader.Instance != null)
            SceneFader.Instance.ActivatePreloaded(preload, 0.3f);
        else
            preload.allowSceneActivation = true;
    }

private IEnumerator ScaleOverTime(Transform target, Vector3 targetScale, float duration)
    {
        Vector3 startScale = target.localScale;
        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;

            // Smooth-Animation (Herzschlag-Style)
            target.localScale = Vector3.Lerp(startScale, targetScale, Mathf.SmoothStep(0, 1, progress));

            yield return null; // 1 Frame warten
        }

        target.localScale = targetScale;
    }
    
    private IEnumerator MoveOverTime(Transform target, Vector3 offset, float duration, bool relative = false)
{
    Vector3 startPos = target.localPosition;
    Vector3 endPos = relative ? startPos + offset : offset;
    float t = 0f;

    while (t < duration)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / duration);
        target.localPosition = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, p));
        yield return null;
    }

    target.localPosition = endPos;
}


private IEnumerator MoveUpAndOut(Transform target, float distance, float duration)
{
    Vector3 startPos = target.localPosition;
    Vector3 endPos = startPos + new Vector3(0, distance, 0);
    Vector3 startScale = target.localScale;
    Vector3 endScale = startScale * 0.3f;

    float t = 0f;
    float fadeDuration = 0.4f; // Alpha-Fade-Zeit
    Image img = target.GetComponent<Image>();

    while (t < duration)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / duration);

        // Position hoch
        target.localPosition = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, p));

        // Gleichzeitig kleiner werden
        target.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, p));

        // Alpha nur während der ersten 0.4 Sekunden verringern
        if (img != null)
        {
            float fadeProgress = Mathf.Clamp01(t / fadeDuration);
            Color c = img.color;
            c.a = Mathf.Lerp(1f, 0f, fadeProgress);
            img.color = c;
        }

        yield return null;
    }

    target.localPosition = endPos;
    target.localScale = endScale;
}



}
