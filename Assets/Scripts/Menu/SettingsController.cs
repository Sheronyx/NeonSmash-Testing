using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Vollbild-Settings-Fenster im Startmenü — Struktur/Optik/Animation an ShopController/
// RewardWindowController angelehnt (CanvasGroup-Panel, Pop-In/Pop-Out per Coroutine, DimOverlay).
public class SettingsController : MonoBehaviour
{
    public static SettingsController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private CanvasGroup panel;

    [Header("Sound")]
    [SerializeField] private Toggle soundToggle;
    [Tooltip("Icon-Bild auf dem Sound-Toggle -- wechselt je nach An/Aus zwischen soundIconOn/soundIconOff.")]
    [SerializeField] private Image soundIcon;
    [SerializeField] private Sprite soundIconOn;
    [SerializeField] private Sprite soundIconOff;

    // ConsentManager lebt als persistentes Objekt in der BootstrapScene (DontDestroyOnLoad) -- eine
    // szenenübergreifende Referenz lässt sich nicht im Inspector serialisieren, deshalb zur Laufzeit
    // suchen statt als [SerializeField].
    private ConsentManager _consentManager;
    private ConsentManager ConsentManagerInstance =>
        _consentManager != null ? _consentManager : (_consentManager = FindFirstObjectByType<ConsentManager>());

    [Header("Animation")]
    [SerializeField] private float popInDuration  = 0.28f;
    [SerializeField] private float popOutDuration = 0.2f;

    private bool _open;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.gameObject.SetActive(false);

        if (soundToggle != null)
            soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
    }

    public void Open()
    {
        if (_open) return;
        _open = true;

        if (soundToggle != null && AudioSwitch.Instance != null)
        {
            bool enabled = AudioSwitch.Instance.AudioEnabled;
            soundToggle.SetIsOnWithoutNotify(enabled);
            UpdateSoundIcon(enabled);
        }

        DimOverlay.Instance?.Show();
        StartCoroutine(Co_Open());
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        DimOverlay.Instance?.Hide();
        StartCoroutine(Co_Close());
    }

    private void OnSoundToggleChanged(bool isOn)
    {
        AudioSwitch.Instance?.SetEnabled(isOn);
        UpdateSoundIcon(isOn);
    }

    private void UpdateSoundIcon(bool isOn)
    {
        if (soundIcon == null) return;
        soundIcon.sprite = isOn ? soundIconOn : soundIconOff;
    }

    // ── Datenschutz / Impressum (leiten nur an ConsentManager weiter) ─────────

    public void OnManageConsent()   => ConsentManagerInstance?.OnManageConsent();
    public void OnOpenImpressum()   => ConsentManagerInstance?.OnImpressumCanvas();
    public void OnOpenPrivacyWeb()  => ConsentManagerInstance?.OpenPrivacyWebsite();

    // ── Animation ────────────────────────────────────────────────────────────

    private IEnumerator Co_Open()
    {
        if (panel != null) { panel.gameObject.SetActive(true); panel.alpha = 0f; }
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (rt != null) rt.localScale = Vector3.one * 0.85f;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popInDuration));
            if (panel != null) panel.alpha   = p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, p);
            yield return null;
        }
        if (panel != null) panel.alpha   = 1f;
        if (rt    != null) rt.localScale = Vector3.one;
    }

    private IEnumerator Co_Close()
    {
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;

        float t = 0f;
        while (t < popOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popOutDuration));
            if (panel != null) panel.alpha   = 1f - p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.85f, p);
            yield return null;
        }
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
