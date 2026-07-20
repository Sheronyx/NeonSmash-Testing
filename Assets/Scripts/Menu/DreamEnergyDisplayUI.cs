using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DreamEnergyDisplayUI : MonoBehaviour
{
    public static DreamEnergyDisplayUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI amountText;
    [FormerlySerializedAs("coinIconTarget")]
    [SerializeField] private RectTransform   dreamEnergyIconTarget; // icon in the header bar — orbs fly TO here
    [FormerlySerializedAs("coinSprite")]
    [SerializeField] private Sprite          dreamEnergySprite;     // same sprite as the header icon
    [SerializeField] private Canvas          flyCanvas;             // high sort-order canvas to spawn flying icons in

    [Header("Fly Animation")]
    [SerializeField] private int   flyCount     = 7;
    [SerializeField] private float flyDuration  = 0.6f;
    [SerializeField] private float staggerDelay = 0.07f;
    [SerializeField] private float coinSize     = 96f;
    [SerializeField] private float arcHeight    = 80f;

    int _displayedBalance;

    void Awake() => Instance = this;

    void OnEnable()
    {
        // Start from the pre-reward balance so each FlyDreamEnergyFrom animation counts up correctly.
        // Dream Energy for queued notifications is already in DreamEnergyManager.Balance — subtract it
        // so the display shows what the player had before rewards, then animates up to the final value.
        _displayedBalance = DreamEnergyManager.Balance - RewardNotificationQueue.PendingAmount;
        Refresh();
        // Do NOT subscribe to OnDreamEnergyChanged — display only updates via FlyDreamEnergyFrom
    }

    // Called by toast and popup after showing a reward
    public void FlyDreamEnergyFrom(int amount, Vector3 worldSourcePos)
    {
        if (dreamEnergyIconTarget == null) { Debug.LogWarning("[DreamEnergyDisplayUI] dreamEnergyIconTarget not assigned — nothing will fly!"); return; }
        if (dreamEnergySprite     == null) { Debug.LogWarning("[DreamEnergyDisplayUI] dreamEnergySprite not assigned — flying icon would be invisible!"); }
        StartCoroutine(Co_FlyAndCount(amount, worldSourcePos));
    }

    IEnumerator Co_FlyAndCount(int amount, Vector3 worldSourcePos)
    {
        int startBalance = _displayedBalance;
        int endBalance   = startBalance + amount;

        Canvas canvas  = flyCanvas != null ? flyCanvas : GetComponentInParent<Canvas>().rootCanvas;
        var canvasRect = canvas.GetComponent<RectTransform>();
        Camera cam     = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        // dreamEnergyIconTarget may be on a different canvas with a different render mode — resolve its camera separately.
        Camera targetCam = CanvasCamOf(dreamEnergyIconTarget);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, RectTransformUtility.WorldToScreenPoint(cam, worldSourcePos),
            cam, out Vector2 sourceLocal);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, RectTransformUtility.WorldToScreenPoint(targetCam, dreamEnergyIconTarget.position),
            cam, out Vector2 targetLocal);

        int arrived = 0;

        for (int i = 0; i < flyCount; i++)
        {
            Vector2 scatter = new Vector2(Random.Range(-50f, 50f), Random.Range(-40f, 40f));
            StartCoroutine(Co_OneIcon(canvas, sourceLocal + scatter, targetLocal, i * staggerDelay, () =>
            {
                arrived++;
                float progress    = (float)arrived / flyCount;
                _displayedBalance = startBalance + Mathf.RoundToInt(amount * progress);
                Refresh();
            }));
        }

        yield return new WaitUntil(() => arrived >= flyCount);
        _displayedBalance = endBalance;
        Refresh();
    }

    IEnumerator Co_OneIcon(Canvas canvas, Vector2 from, Vector2 to, float delay, System.Action onArrive)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        var go  = new GameObject("FlyingDreamEnergy", typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.GetComponent<Image>().sprite        = dreamEnergySprite;
        go.GetComponent<Image>().raycastTarget = false;
        var rt  = go.GetComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(coinSize, coinSize);
        rt.anchoredPosition = from;

        float t = 0f;
        while (t < flyDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / flyDuration);
            rt.anchoredPosition = Vector2.Lerp(from, to, p) + new Vector2(0f, Mathf.Sin(p * Mathf.PI) * arcHeight);
            rt.localScale       = Vector3.Lerp(Vector3.one, Vector3.one * 0.2f, p);
            yield return null;
        }

        Destroy(go);
        onArrive?.Invoke();
    }

    void Refresh() => amountText.text = _displayedBalance.ToString("N0");

    static Camera CanvasCamOf(RectTransform rt)
    {
        var c = rt.GetComponentInParent<Canvas>();
        if (c == null) return null;
        c = c.rootCanvas;
        return c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
    }
}
