using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Vollbild-Quests-Fenster im Startmenue. Zeigt die 3 Weeklies der laufenden Woche, den Fortschritt
// und den Abholen-Knopf. Struktur/Optik/Animation bewusst wie SettingsController und ShopController:
// CanvasGroup-Panel, Pop-In/Pop-Out per Coroutine, DimOverlay dahinter.
public class WeeklyQuestsController : MonoBehaviour
{
    public static WeeklyQuestsController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private Button closeButton;

    [Header("Inhalt")]
    [Tooltip("Genau 3 Zeilen, von oben nach unten.")]
    [SerializeField] private WeeklyQuestRowUI[] questRows;
    [Tooltip("Zeigt die verbleibende Zeit bis Sonntag 23:59:59, z. B. \"Noch 3 Tage 4 Std.\".")]
    [SerializeField] private TextMeshProUGUI resetLabel;

    [Header("Animation")]
    [SerializeField] private float popInDuration  = 0.28f;
    [SerializeField] private float popOutDuration = 0.2f;

    [Header("Kachel-Einzug bei neuen Aufgaben")]
    [Tooltip("Bei einer noch ungesehenen Woche poppen die Kacheln nacheinander herein.")]
    [SerializeField] private float rowPopDuration = 0.34f;
    [Tooltip("Versatz zwischen zwei Kacheln.")]
    [SerializeField] private float rowPopStagger  = 0.12f;
    [Tooltip("Wartet, bis das Fenster selbst da ist, bevor die erste Kachel kommt.")]
    [SerializeField] private float rowPopInitialDelay = 0.12f;
    [SerializeField] private float rowPopOvershoot = 1.6f;

    private bool _open;
    private Coroutine _kachelEinzug;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        if (_open) return;
        _open = true;

        // VOR dem Markieren abfragen -- danach gilt die Woche als gesehen.
        bool neueAufgaben = WeeklyQuestManager.IsWeekUnseen();

        Refresh();
        DimOverlay.Instance?.Show();
        StartCoroutine(Co_Open());

        if (neueAufgaben)
        {
            if (_kachelEinzug != null) StopCoroutine(_kachelEinzug);
            _kachelEinzug = StartCoroutine(Co_KachelnEinziehen());
        }
        WeeklyQuestManager.MarkWeekSeen();
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        DimOverlay.Instance?.Hide();
        // Einen laufenden Einzug abbrechen und die Kacheln auf Normalgroesse setzen, sonst
        // stehen sie beim naechsten Oeffnen zusammengeschrumpft da.
        if (_kachelEinzug != null) { StopCoroutine(_kachelEinzug); _kachelEinzug = null; }
        KachelnAufNormal();
        StartCoroutine(Co_Close());
    }

    /// <summary>Baut die Zeilen neu auf. Wird beim Oeffnen und nach jedem Abholen gerufen.</summary>
    public void Refresh()
    {
        var quests = WeeklyQuestManager.GetQuests();
        if (questRows != null)
            for (int i = 0; i < questRows.Length; i++)
            {
                if (questRows[i] == null) continue;
                questRows[i].Bind(i < quests.Length ? quests[i] : null, Claim);
            }

        if (resetLabel != null) resetLabel.text = RestzeitText();
    }

    private void Claim(WeeklyQuestData quest)
    {
        if (!WeeklyQuestManager.TryClaim(quest)) return;
        Refresh();
    }

    private static string RestzeitText()
    {
        var rest = WeeklyQuestManager.TimeUntilReset();
        int tage = rest.Days, stunden = rest.Hours;
        if (tage > 0)    return $"{tage}d {stunden}h left";
        if (stunden > 0) return $"{stunden}h {rest.Minutes}m left";
        return $"{rest.Minutes}m left";
    }

    // Laeuft nur solange das Fenster offen ist -- haelt die Restzeit frisch, ohne jede Sekunde
    // die ganze Liste neu aufzubauen.
    private void Update()
    {
        if (!_open || resetLabel == null) return;
        if (Time.frameCount % 60 != 0) return;
        resetLabel.text = RestzeitText();
    }

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

    // Die drei Kacheln kommen nacheinander herein -- nur bei einer neuen, noch nicht
    // angesehenen Woche. Beim normalen Oeffnen stehen sie sofort da.
    private IEnumerator Co_KachelnEinziehen()
    {
        if (questRows == null) yield break;

        foreach (var zeile in questRows)
            if (zeile != null) zeile.transform.localScale = Vector3.zero;

        if (rowPopInitialDelay > 0f) yield return new WaitForSecondsRealtime(rowPopInitialDelay);

        for (int i = 0; i < questRows.Length; i++)
        {
            if (questRows[i] == null) continue;
            StartCoroutine(Co_KachelPoppt(questRows[i].transform));
            if (rowPopStagger > 0f) yield return new WaitForSecondsRealtime(rowPopStagger);
        }

        yield return new WaitForSecondsRealtime(rowPopDuration);
        KachelnAufNormal();
        _kachelEinzug = null;
    }

    private IEnumerator Co_KachelPoppt(Transform kachel)
    {
        float t = 0f;
        while (t < rowPopDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / rowPopDuration);
            float q = p - 1f;
            // Ease-Out-Back: ueberschwingt einmal und rastet dann auf 1 ein.
            float wert = 1f + (rowPopOvershoot + 1f) * q * q * q + rowPopOvershoot * q * q;
            kachel.localScale = Vector3.one * wert;
            yield return null;
        }
        kachel.localScale = Vector3.one;
    }

    private void KachelnAufNormal()
    {
        if (questRows == null) return;
        foreach (var zeile in questRows)
            if (zeile != null) zeile.transform.localScale = Vector3.one;
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
