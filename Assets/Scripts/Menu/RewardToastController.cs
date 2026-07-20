using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// Attach to a Toast Panel GameObject in MainMenuScene.
// Drains RewardNotificationQueue and shows one toast at a time.
public class RewardToastController : MonoBehaviour
{
    [Header("Toast Panel")]
    [SerializeField] private CanvasGroup  toastPanel;
    [FormerlySerializedAs("coinsRow")]
    [SerializeField] private RectTransform dreamEnergyRow;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [FormerlySerializedAs("coinsText")]
    [SerializeField] private TextMeshProUGUI dreamEnergyText;

    [Header("Timing")]
    [SerializeField] private float slideInDuration  = 0.25f;
    [SerializeField] private float holdDuration     = 2.5f;
    [SerializeField] private float slideOutDuration = 0.2f;
    [SerializeField] private float delayBetween     = 0.3f;

    [Header("Slide")]
    [SerializeField] private float slideOffsetY = 120f; // pixels to slide from

    RectTransform _rt;
    bool          _isShowing;
    Vector2       _shownPos;

    void Awake()
    {
        _rt = toastPanel != null ? toastPanel.GetComponent<RectTransform>() : null;
        if (toastPanel != null) toastPanel.gameObject.SetActive(false);
    }

    void Start()
    {
        if (_rt != null) _shownPos = _rt.anchoredPosition;

        // Drain any notifications queued before this scene loaded (e.g. from gameplay)
        if (RewardNotificationQueue.Count > 0)
            StartCoroutine(Co_DrainQueue());
    }

    // Call this from MainMenuController if you want to force a drain at a specific moment
    public void DrainQueue()
    {
        if (!_isShowing && RewardNotificationQueue.Count > 0)
            StartCoroutine(Co_DrainQueue());
    }

    IEnumerator Co_DrainQueue()
    {
        // Wait for scene fade to finish first
        if (SceneFader.Instance != null)
            yield return new WaitUntil(() => SceneFader.Instance == null || !SceneFader.Instance.IsFading);

        yield return new WaitForSecondsRealtime(0.5f);

        while (RewardNotificationQueue.TryDequeue(out var notification))
        {
            yield return Co_ShowToast(notification);
            yield return new WaitForSecondsRealtime(delayBetween);
        }

        _isShowing = false;
    }

    IEnumerator Co_ShowToast(RewardNotification n)
    {
        _isShowing = true;

        if (titleText       != null) titleText.text       = n.Title;
        if (subtitleText    != null) subtitleText.text    = n.Subtitle;
        if (dreamEnergyText != null) dreamEnergyText.text = $"+{n.Amount}";

        toastPanel.gameObject.SetActive(true);
        toastPanel.alpha = 0f;

        Vector2 hiddenPos = _shownPos + new Vector2(0f, slideOffsetY);
        if (_rt != null) _rt.anchoredPosition = hiddenPos;

        // Slide in
        float t = 0f;
        while (t < slideInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideInDuration));
            toastPanel.alpha = p;
            if (_rt != null) _rt.anchoredPosition = Vector2.Lerp(hiddenPos, _shownPos, p);
            yield return null;
        }
        toastPanel.alpha = 1f;
        if (_rt != null) _rt.anchoredPosition = _shownPos;

        // Dream Energy fliegt von der Reihe im Toast zur Header-Anzeige
        Vector3 source = dreamEnergyRow != null ? dreamEnergyRow.position : toastPanel.transform.position;
        DreamEnergyDisplayUI.Instance?.FlyDreamEnergyFrom(n.Amount, source);

        yield return new WaitForSecondsRealtime(holdDuration);

        // Slide out
        t = 0f;
        while (t < slideOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideOutDuration));
            toastPanel.alpha = 1f - p;
            if (_rt != null) _rt.anchoredPosition = Vector2.Lerp(_shownPos, hiddenPos, p);
            yield return null;
        }

        toastPanel.gameObject.SetActive(false);
    }
}
