using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoinDisplayUI : MonoBehaviour
{
    public static CoinDisplayUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private RectTransform   coinIconTarget; // coin icon in the header bar — coins fly TO here
    [SerializeField] private Sprite          coinSprite;     // same sprite as the coin icon
    [SerializeField] private Canvas          flyCanvas;      // high sort-order canvas to spawn flying coins in

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
        _displayedBalance = CoinManager.Balance;
        Refresh();
        // Do NOT subscribe to OnCoinsChanged — display only updates via FlyCoinsFrom
    }

    // Called by toast and popup after showing a reward
    public void FlyCoinsFrom(int amount, Vector3 worldSourcePos)
    {
        StartCoroutine(Co_FlyAndCount(amount, worldSourcePos));
    }

    IEnumerator Co_FlyAndCount(int amount, Vector3 worldSourcePos)
    {
        int startBalance = _displayedBalance;
        int endBalance   = startBalance + amount;

        Canvas canvas  = flyCanvas != null ? flyCanvas : GetComponentInParent<Canvas>().rootCanvas;
        var canvasRect = canvas.GetComponent<RectTransform>();
        Camera cam     = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, RectTransformUtility.WorldToScreenPoint(cam, worldSourcePos),
            cam, out Vector2 sourceLocal);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, RectTransformUtility.WorldToScreenPoint(cam, coinIconTarget.position),
            cam, out Vector2 targetLocal);

        int arrived = 0;

        for (int i = 0; i < flyCount; i++)
        {
            Vector2 scatter = new Vector2(Random.Range(-50f, 50f), Random.Range(-40f, 40f));
            StartCoroutine(Co_OneCoin(canvas, sourceLocal + scatter, targetLocal, i * staggerDelay, () =>
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

    IEnumerator Co_OneCoin(Canvas canvas, Vector2 from, Vector2 to, float delay, System.Action onArrive)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        var go  = new GameObject("FlyingCoin", typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.GetComponent<Image>().sprite        = coinSprite;
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
}
