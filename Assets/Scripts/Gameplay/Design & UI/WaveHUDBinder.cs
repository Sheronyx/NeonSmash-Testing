using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Bindet Korb-Füllstand ("23/50") + 60s-Countdown (Text + optionaler Fill-Balken) an
// WaveBasketController. Blendet sich für Multiplayer (kein Korb/Timer dort) komplett aus.
public class WaveHUDBinder : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI basketText;
    [SerializeField] private TextMeshProUGUI timerText;
    [Tooltip("Optional: Image (Image Type = Filled), dessen Fill Amount synchron mit der verbleibenden Zeit runterfährt.")]
    [SerializeField] private Image timerFillImage;
    [Range(0.5f, 1f)]
    [Tooltip("Ab diesem Fill Amount sieht der Balken durchs Sprite-Padding/die abgerundete Ecke schon optisch 'voll' aus (z.B. 1.0↔0.9 sieht identisch aus). Die Restzeit wird auf [0, dieser Wert] gemappt, damit der Balken über die GESAMTE Spieldauer sichtbar schrumpft statt am Anfang eine Weile nichts zu tun.")]
    [SerializeField] private float timerFillVisualMax = 0.9f;
    [SerializeField] private WaveBasketController controller;

    void Awake()
    {
        if (GlobalGameManager.Instance == null ||
            GlobalGameManager.Instance.SelectedMode != GameMode.Infinity)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!controller)
            controller = FindFirstObjectByType<WaveBasketController>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        if (controller == null) return;
        controller.OnBasketChanged += HandleBasketChanged;
        controller.OnTimerChanged += HandleTimerChanged;
    }

    void OnDisable()
    {
        if (controller == null) return;
        controller.OnBasketChanged -= HandleBasketChanged;
        controller.OnTimerChanged -= HandleTimerChanged;
    }

    private void HandleBasketChanged(int current, int cap)
    {
        if (basketText) basketText.text = $"{current}/{cap}";
    }

    private void HandleTimerChanged(float remaining)
    {
        if (timerText) timerText.text = Mathf.CeilToInt(remaining).ToString();

        if (timerFillImage && controller != null && controller.MatchDuration > 0f)
            timerFillImage.fillAmount = timerFillVisualMax * Mathf.Clamp01(remaining / controller.MatchDuration);
    }
}
