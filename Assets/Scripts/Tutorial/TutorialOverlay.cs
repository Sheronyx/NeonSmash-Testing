using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;

public class TutorialOverlay : MonoBehaviour
{
    // ── Haupt-Overlay (Text + Finger) ─────────────────────────────────────────
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI label;

    // ── Dunkle Hintergrund-Schicht (SEPARAT vom Haupt-Overlay) ────────────────
    [Header("Dim Background (optional, separat zuweisen)")]
    [Tooltip("Separate CanvasGroup für den dunklen Screen-Overlay. " +
             "Muss auf einem Canvas mit 'Screen Space – Camera' liegen, " +
             "damit Spielobjekte mit höherem Sorting-Layer darüber erscheinen.")]
    [SerializeField] private CanvasGroup dimBackground;
    [SerializeField] [Range(0f, 1f)] private float dimTargetAlpha = 0.55f;

    // ── Spotlight-Glow am Element-Ort ─────────────────────────────────────────
    [Header("Spotlight am Element")]
    [Tooltip("UI-Image (Glow/Ring) das an der Canvas-Position des aktiven Elements " +
             "positioniert wird, damit es trotz Dimming hell hervorsticht.")]
    [SerializeField] private RectTransform spotlightGlow;

    // ── Finger ────────────────────────────────────────────────────────────────
    [Header("Finger")]
    [SerializeField] private RectTransform finger;
    [SerializeField] private CanvasGroup fingerAlpha;

    // ── Optionale Effekte ─────────────────────────────────────────────────────
    [Header("Optionale Effekte")]
    [SerializeField] private RectTransform tapRipple;
    [SerializeField] private RectTransform swipeTrail;
    [SerializeField] private Image swipeTrailImage;

    // ── End Screen ────────────────────────────────────────────────────────────
    [Header("End Screen")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TextMeshProUGUI endLabel;

    // ── Offsets ───────────────────────────────────────────────────────────────
    [Header("Label-Offset vom Punkt (Canvas-Pixel)")]
    [SerializeField] private Vector2 labelOffset = new Vector2(0f, 120f);

    [Header("Finger-Offset vom Punkt (Canvas-Pixel)")]
    [SerializeField] private Vector2 fingerTapOffset   = new Vector2(0f, -60f);
    [SerializeField] private Vector2 fingerSwipeOffset = new Vector2(0f,   0f);

    // ── Fade ──────────────────────────────────────────────────────────────────
    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.25f;

    // ── Tap Animation ─────────────────────────────────────────────────────────
    [Header("Tap Animation")]
    [SerializeField] private float tapDropDistance  = 40f;
    [SerializeField] private float tapPressScale    = 0.85f;
    [SerializeField] private float tapDownDuration  = 0.12f;
    [SerializeField] private float tapHoldDuration  = 0.08f;
    [SerializeField] private float tapUpDuration    = 0.18f;
    [SerializeField] private float tapPauseDuration = 0.5f;
    [SerializeField] private float rippleMaxScale   = 2.2f;
    [SerializeField] private float rippleDuration   = 0.3f;

    // ── Swipe Animation ───────────────────────────────────────────────────────
    [Header("Swipe Animation")]
    [SerializeField] private float swipeRange          = 130f;
    [Tooltip("Vertikales Verhältnis zur horizontalen Range (0 = horizontal, 1 = 45°)")]
    [SerializeField] private float swipeDiagonalRatio  = 0.55f;
    [SerializeField] private float swipeDuration       = 0.55f;
    [SerializeField] private float swipePauseDuration  = 0.45f;
    [SerializeField] private float swipeFadeInEnd      = 0.15f;
    [SerializeField] private float swipeFadeOutStart   = 0.75f;

    private Coroutine animCoroutine;
    private Coroutine fadeCoroutine;

    private Canvas rootCanvas;
    private Camera cam;

    private Vector2 anchorCanvasPos;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        cam = Camera.main;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (dimBackground != null)
        {
            dimBackground.alpha = 0f;
            dimBackground.interactable = false;
            dimBackground.blocksRaycasts = false;
        }

        if (finger        != null) finger.gameObject.SetActive(false);
        if (tapRipple     != null) tapRipple.gameObject.SetActive(false);
        if (swipeTrail    != null) swipeTrail.gameObject.SetActive(false);
        if (spotlightGlow != null) spotlightGlow.gameObject.SetActive(false);
        if (endPanel      != null) endPanel.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(TutorialStepData data, Vector3 worldPos)
    {
        anchorCanvasPos = WorldToCanvasPos(worldPos);

        // Label positionieren
        if (label != null)
        {
            label.rectTransform.anchoredPosition = anchorCanvasPos + labelOffset;
            label.text = data.text;
        }

        // Spotlight-Glow am Element positionieren
        if (spotlightGlow != null)
        {
            spotlightGlow.anchoredPosition = anchorCanvasPos;
            spotlightGlow.gameObject.SetActive(true);
        }

        // Finger positionieren
        bool isTap = data.animType == TutorialAnimType.Tap;
        if (finger != null)
        {
            Vector2 offset = isTap ? fingerTapOffset : fingerSwipeOffset;
            finger.anchoredPosition = anchorCanvasPos + offset;
            finger.gameObject.SetActive(true);
        }

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(isTap ? AnimateTap() : AnimateSwipe());

        // Dim-Hintergrund einblenden (separat, kein BlocksRaycasts nötig)
        if (dimBackground != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            StartCoroutine(FadeUnscaled(dimBackground, dimBackground.alpha, dimTargetAlpha, fadeDuration, null));
        }

        // Haupt-Overlay einblenden
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeUnscaled(canvasGroup, canvasGroup.alpha, 1f, fadeDuration, null));
    }

    /// <summary>
    /// Zeigt nur den Text an einer festen Position – keine Finger-Animation, kein Spotlight.
    /// Blendet nach <paramref name="duration"/> Sekunden automatisch aus und ruft onDone auf.
    /// </summary>
    public void ShowHint(string text, Vector3 worldPos, float duration, Action onDone = null)
    {
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        if (fadeCoroutine  != null) { StopCoroutine(fadeCoroutine);  fadeCoroutine  = null; }

        Vector2 canvasPos = WorldToCanvasPos(worldPos);

        if (label != null)
        {
            // Kein labelOffset – Hinweis wird exakt an der angegebenen Position platziert
            label.rectTransform.anchoredPosition = canvasPos;
            label.text = text;
        }

        // Kein Finger, kein Spotlight
        if (finger        != null) finger.gameObject.SetActive(false);
        if (spotlightGlow != null) spotlightGlow.gameObject.SetActive(false);

        // Dunkler Hintergrund einblenden
        if (dimBackground != null)
            StartCoroutine(FadeUnscaled(dimBackground, dimBackground.alpha, dimTargetAlpha, fadeDuration, null));

        fadeCoroutine = StartCoroutine(HintRoutine(duration, onDone));
    }

    private IEnumerator HintRoutine(float duration, Action onDone)
    {
        // Einblenden
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Anzeigedauer + Puls-Animation auf dem Label
        RectTransform labelRT = label?.rectTransform;
        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (labelRT != null)
            {
                // Sanftes Atmen: 0.95 ↔ 1.05, Periode ~1.2 s
                float pulse = 1f + 0.05f * Mathf.Sin(t * Mathf.PI * 1.67f);
                labelRT.localScale = Vector3.one * pulse;
            }
            yield return null;
        }
        if (labelRT != null) labelRT.localScale = Vector3.one;

        // Ausblenden (Text + Dim gleichzeitig)
        if (dimBackground != null)
            StartCoroutine(FadeUnscaled(dimBackground, dimBackground.alpha, 0f, fadeDuration, null));

        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        onDone?.Invoke();
    }

    public void Hide(Action onComplete)
    {
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        if (tapRipple     != null) tapRipple.gameObject.SetActive(false);
        if (swipeTrail    != null) swipeTrail.gameObject.SetActive(false);
        if (spotlightGlow != null) spotlightGlow.gameObject.SetActive(false);

        // Dim-Hintergrund ausblenden
        if (dimBackground != null)
            StartCoroutine(FadeUnscaled(dimBackground, dimBackground.alpha, 0f, fadeDuration, null));

        // Haupt-Overlay ausblenden
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeUnscaled(canvasGroup, canvasGroup.alpha, 0f, fadeDuration, () =>
        {
            if (finger != null) finger.gameObject.SetActive(false);
            onComplete?.Invoke();
        }));
    }

    // ── End Screen ────────────────────────────────────────────────────────────

    [Header("End Screen Timing")]
    [SerializeField] private float endDisplayDuration = 2f;
    [SerializeField] private float endPopInDuration   = 0.35f;
    [SerializeField] private float endPopOutDuration  = 0.25f;

    [Header("End Screen Firework")]
    [Tooltip("ParticleSystem mit NeonTap-Sprite – wird nach dem Pop-in gespielt")]
    [SerializeField] private ParticleSystem endFirework;

    private Coroutine endCoroutine;

    /// <summary>Zeigt den End-Screen mit Popup-Animation, blendet nach 2 s automatisch aus.</summary>
    public void ShowEndScreen(string message, Action onComplete)
    {
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        if (fadeCoroutine  != null) { StopCoroutine(fadeCoroutine);  fadeCoroutine  = null; }
        if (endCoroutine   != null) { StopCoroutine(endCoroutine);   endCoroutine   = null; }

        if (finger        != null) finger.gameObject.SetActive(false);
        if (tapRipple     != null) tapRipple.gameObject.SetActive(false);
        if (swipeTrail    != null) swipeTrail.gameObject.SetActive(false);
        if (spotlightGlow != null) spotlightGlow.gameObject.SetActive(false);

        if (dimBackground != null)
            StartCoroutine(FadeUnscaled(dimBackground, dimBackground.alpha, dimTargetAlpha, fadeDuration, null));

        if (endPanel == null) { onComplete?.Invoke(); return; }

        if (endLabel != null) endLabel.text = message;

        endPanel.SetActive(true);
        endCoroutine = StartCoroutine(EndScreenRoutine(onComplete));
    }

    private IEnumerator EndScreenRoutine(Action onComplete)
    {
        // Popup-Panel hat eine eigene RectTransform – animieren wir die Scale
        RectTransform rt = endPanel.GetComponent<RectTransform>();

        // ── Pop-in: 0 → 1.12 → 1.0 ──────────────────────────────────────────
        if (rt != null)
        {
            rt.localScale = Vector3.zero;
            float t = 0f;
            while (t < endPopInDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / endPopInDuration);
                // Overshoot-Kurve: sinusoid mit leichtem Überschwingen
                float scale = Mathf.Sin(p * Mathf.PI * 0.5f);
                scale = 1f + (scale - 1f) * 0.12f + scale * 0.12f; // leichter Overshoot
                // Einfacher: SmoothStep mit Overshoot via Punch
                float punch = Mathf.Sin(p * Mathf.PI) * 0.12f;
                rt.localScale = Vector3.one * (Mathf.SmoothStep(0f, 1f, p) + punch);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ── Feuerwerk starten (nach Pop-in) ──────────────────────────────────
        if (endFirework != null)
        {
            endFirework.gameObject.SetActive(true);
            endFirework.Play();
        }

        // ── 2 Sekunden anzeigen ───────────────────────────────────────────────
        yield return WaitUnscaled(endDisplayDuration);

        // ── Feuerwerk stoppen ─────────────────────────────────────────────────
        if (endFirework != null)
        {
            endFirework.Stop();
            endFirework.gameObject.SetActive(false);
        }

        // ── Pop-out: 1.0 → 0 + Dim ausblenden ────────────────────────────────
        if (dimBackground != null)
            StartCoroutine(FadeUnscaled(dimBackground, dimBackground.alpha, 0f, endPopOutDuration, null));

        if (rt != null)
        {
            float t = 0f;
            while (t < endPopOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / endPopOutDuration);
                rt.localScale = Vector3.one * Mathf.SmoothStep(1f, 0f, p);
                yield return null;
            }
            rt.localScale = Vector3.zero;
        }

        endPanel.SetActive(false);
        onComplete?.Invoke();
    }

    public void HideEndScreen()
    {
        if (endCoroutine != null) { StopCoroutine(endCoroutine); endCoroutine = null; }
        if (endPanel     != null) endPanel.SetActive(false);
        if (dimBackground != null)
            StartCoroutine(FadeUnscaled(dimBackground, dimBackground.alpha, 0f, fadeDuration, null));
    }

    // ── Tap Animation ─────────────────────────────────────────────────────────

    private IEnumerator AnimateTap()
    {
        if (finger == null) yield break;

        Vector2 origin = finger.anchoredPosition;
        finger.localScale = Vector3.one;
        SetFingerAlpha(1f);

        while (true)
        {
            yield return MoveFingerUnscaled(
                origin,
                origin + Vector2.down * tapDropDistance,
                Vector3.one,
                new Vector3(tapPressScale, tapPressScale, 1f),
                tapDownDuration
            );

            if (tapRipple != null) StartCoroutine(PlayRipple());

            yield return WaitUnscaled(tapHoldDuration);

            yield return MoveFingerUnscaled(
                origin + Vector2.down * tapDropDistance,
                origin,
                new Vector3(tapPressScale, tapPressScale, 1f),
                Vector3.one,
                tapUpDuration
            );

            yield return WaitUnscaled(tapPauseDuration);
        }
    }

    private IEnumerator PlayRipple()
    {
        if (tapRipple == null) yield break;

        tapRipple.anchoredPosition = finger.anchoredPosition;
        tapRipple.gameObject.SetActive(true);
        tapRipple.localScale = Vector3.one * 0.3f;

        var img = tapRipple.GetComponent<Image>();
        float t = 0f;
        while (t < rippleDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / rippleDuration;
            tapRipple.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.one * rippleMaxScale, p);
            if (img != null)
            {
                var c = img.color;
                c.a = Mathf.Lerp(0.6f, 0f, p);
                img.color = c;
            }
            yield return null;
        }
        tapRipple.gameObject.SetActive(false);
    }

    private IEnumerator MoveFingerUnscaled(Vector2 fromPos, Vector2 toPos,
                                            Vector3 fromScale, Vector3 toScale, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            finger.anchoredPosition = Vector2.Lerp(fromPos, toPos, p);
            finger.localScale = Vector3.Lerp(fromScale, toScale, p);
            yield return null;
        }
        finger.anchoredPosition = toPos;
        finger.localScale = toScale;
    }

    // ── Swipe Animation (diagonal: links-unten → rechts-oben) ─────────────────

    private IEnumerator AnimateSwipe()
    {
        if (finger == null) yield break;

        Vector2 center = finger.anchoredPosition;   // = Element-Mittelpunkt

        float halfW = swipeRange * 0.5f;
        float halfH = swipeRange * swipeDiagonalRatio * 0.5f;

        Vector2 startPos = center + new Vector2(-halfW, -halfH);
        Vector2 endPos   = center + new Vector2( halfW,  halfH);

        if (swipeTrail != null) swipeTrail.gameObject.SetActive(false);

        while (true)
        {
            finger.anchoredPosition = startPos;
            SetFingerAlpha(0f);

            float t = 0f;
            while (t < swipeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p     = Mathf.Clamp01(t / swipeDuration);
                float eased = Mathf.SmoothStep(0f, 1f, p);

                finger.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);

                float alpha;
                if      (p < swipeFadeInEnd)    alpha = Mathf.Lerp(0f, 1f, p / swipeFadeInEnd);
                else if (p > swipeFadeOutStart) alpha = Mathf.Lerp(1f, 0f, (p - swipeFadeOutStart) / (1f - swipeFadeOutStart));
                else                             alpha = 1f;

                SetFingerAlpha(alpha);
                yield return null;
            }

            SetFingerAlpha(0f);
            yield return WaitUnscaled(swipePauseDuration);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector2 WorldToCanvasPos(Vector3 worldPos)
    {
        if (cam == null) cam = Camera.main;
        if (rootCanvas == null) return Vector2.zero;

        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,
            screenPos,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out Vector2 canvasPos
        );

        return canvasPos;
    }

    private void SetFingerAlpha(float a)
    {
        if (fingerAlpha != null) fingerAlpha.alpha = a;
    }

    private IEnumerator FadeUnscaled(CanvasGroup cg, float from, float to, float duration, Action onDone)
    {
        cg.interactable   = to >= 0.99f;
        cg.blocksRaycasts = to >= 0.99f;

        float t = 0f;
        cg.alpha = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
        onDone?.Invoke();
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }
}
