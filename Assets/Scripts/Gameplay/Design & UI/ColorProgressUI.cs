using UnityEngine;
using UnityEngine.UI;

// Zeigt den Fortschritt der 3 Farb-Zähler als Füllbalken an (0 = leer, Schwellenwert = voll),
// die den jeweiligen Special Mode auslösen.
// Rein anzeigend — die eigentliche Logik/der Zählerstand liegt im PhaseManager.
public class ColorProgressUI : MonoBehaviour
{
    [Header("Fill Bars (Image Type: Filled, Fill Method: Horizontal)")]
    [SerializeField] private Image pinkFill;
    [SerializeField] private Image greenFill;
    [SerializeField] private Image blueFill;

    [Header("Optional: Glow-Marker am Füllstand-Ende")]
    [SerializeField] private RectTransform pinkGlow;
    [SerializeField] private RectTransform greenGlow;
    [SerializeField] private RectTransform blueGlow;

    // Reward-Preview (siehe docs/RESEARCH.md "UI-Pattern für Progress-/Meta-Progression-Bars"):
    // deutet knapp vor Balken-voll bereits an, dass gleich der Special Mode ausgelöst wird, statt
    // nur reinen Fortschritt zu zeigen. Optional/nullable, damit dieses Feature ohne Scene-Änderung
    // vorbereitet werden kann — Felder bleiben leer, bis in der Szene ein Image mit
    // "Reward Preview Sparkle.png" zugewiesen wird (siehe Assets/001 Fairy World/Elements/Fairy
    // Progress Icons/), Verhalten ist bis dahin identisch zu vorher (reine Glow-Balken).
    [Header("Optional: Reward-Preview (aktiviert kurz vor Balken-voll)")]
    [SerializeField, Range(0.5f, 0.99f)] private float rewardPreviewThreshold = 0.75f;
    [SerializeField] private GameObject pinkRewardPreview;
    [SerializeField] private GameObject greenRewardPreview;
    [SerializeField] private GameObject blueRewardPreview;

    // Countdown-Glow waehrend des Special Modes: die Fill-Bilder tragen ein TextMeshPro-basiertes
    // "CountdownGlowMat" (_FaceColor als HDR-Farbe). Normalzustand = 0.6 Intensitaet, sobald eine
    // Farbe voll ist (Special Mode wird ausgeloest) springt sie auf 1.4 UND der Balken zaehlt
    // waehrend des Special Modes von 100% auf 0% runter (statt sofort zu leeren), bis der Special
    // Mode fertig ist -- dann erst Reset auf 0.6 und Balken leer.
    //
    // WICHTIG: Diese beiden Farbwerte sind die EXAKTEN, direkt aus dem Material-Asset ausgelesenen
    // RGB-Werte bei Intensitaet 0.6 bzw. 1.4 (im Inspector eingestellt und abgelesen) -- bewusst KEINE
    // Formel (Unitys HDR-"Intensity"-Slider im Color Picker folgt einer anderen, nicht dokumentierten
    // Kurve als der einfache log2/exp2-Zusammenhang, den man erwarten wuerde; mehrere Versuche, das
    // nachzurechnen, ergaben falsche Werte). Falls die gewuenschten Intensitaeten sich mal aendern
    // sollen: im Material-Inspector den Regler auf den neuen Zielwert stellen, den resultierenden
    // _FaceColor-RGB-Wert ablesen und hier eintragen.
    private static readonly Color NormalGlowColor = new Color(1.135302f, 1.135302f, 1.135302f, 1f); // Intensitaet 0.6
    private static readonly Color FullGlowColor   = new Color(1.976675f, 1.976675f, 1.976675f, 1f); // Intensitaet 1.4

    [Tooltip("Wie schnell der Balken waehrend des Special Modes Richtung Zielwert gleitet (Fuellstand pro Sekunde, 1 = einmal komplett voll->leer pro Sekunde). Hoeher = spuerbarer Nachzieheffekt bei jedem Treffer, statt sprunghaftem Sprung.")]
    [SerializeField] private float countdownSmoothSpeed = 1.5f;

    private bool _pinkCountingDown, _greenCountingDown, _blueCountingDown;

    private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");

    private void OnEnable()
    {
        PhaseManager.OnColorProgressChanged += HandleProgressChanged;
        PhaseManager.OnRunReset += HandleRunReset;
        GravityModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
        FountainModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
        VortexModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;

        EnsurePrivateMaterialInstance(pinkFill);
        EnsurePrivateMaterialInstance(greenFill);
        EnsurePrivateMaterialInstance(blueFill);

        // Falls dieses UI erst nach Rundenstart aktiviert wird: aktuellen Stand sofort nachziehen.
        if (PhaseManager.Instance != null)
        {
            int threshold = PhaseManager.Instance.ColorTriggerThreshold;
            UpdateFill(pinkFill,  pinkGlow,  pinkRewardPreview,  PhaseManager.Instance.GetColorCount(PointColor.Pink),  threshold);
            UpdateFill(greenFill, greenGlow, greenRewardPreview, PhaseManager.Instance.GetColorCount(PointColor.Green), threshold);
            UpdateFill(blueFill,  blueGlow,  blueRewardPreview,  PhaseManager.Instance.GetColorCount(PointColor.Blue),  threshold);
        }
    }

    private void OnDisable()
    {
        PhaseManager.OnColorProgressChanged -= HandleProgressChanged;
        PhaseManager.OnRunReset -= HandleRunReset;
        GravityModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
        FountainModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
        VortexModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
    }

    // Bei jedem Rundenstart (auch "Play Again") hart zuruecksetzen -- unabhaengig davon, ob die
    // vorherige Runde ihren Special Mode sauber zu Ende gespielt hat (siehe PhaseManager.OnRunReset).
    private void HandleRunReset()
    {
        _pinkCountingDown = false;
        _greenCountingDown = false;
        _blueCountingDown = false;

        if (pinkFill  != null) { pinkFill.fillAmount  = 0f; SetGlowColor(pinkFill,  NormalGlowColor); }
        if (greenFill != null) { greenFill.fillAmount = 0f; SetGlowColor(greenFill, NormalGlowColor); }
        if (blueFill  != null) { blueFill.fillAmount  = 0f; SetGlowColor(blueFill,  NormalGlowColor); }
    }

    // WICHTIG: Image.material klont NICHT automatisch -- ohne dieses manuelle Instanziieren wuerden
    // SetGlowColor()-Aufrufe direkt das GETEILTE Material-Asset veraendern (persistiert dann
    // projektweit ueber Szenen-Reloads hinweg, bis Play Mode komplett gestoppt wird -- das war die
    // Ursache fuer die haengenbleibende Intensitaet nach "Try Again"). Einmalig in OnEnable aufgerufen.
    private void EnsurePrivateMaterialInstance(Image fill)
    {
        if (fill == null || fill.material == null) return;
        fill.material = new Material(fill.material);
    }

    private void SetGlowColor(Image fill, Color target)
    {
        if (fill == null) return;
        var mat = fill.material;
        if (mat == null || !mat.HasProperty(FaceColorId)) return;
        mat.SetColor(FaceColorId, target);
    }

    private void HandleProgressChanged(PointColor color, int current, int threshold)
    {
        // Balken gerade voll geworden -> Special Mode wird ausgeloest: Glow hochfahren und ab jetzt
        // den Fuellstand waehrend des Special Modes selbst steuern (siehe Update()).
        if (threshold > 0 && current >= threshold)
        {
            switch (color)
            {
                case PointColor.Pink:  _pinkCountingDown  = true; SetGlowColor(pinkFill,  FullGlowColor); break;
                case PointColor.Green: _greenCountingDown = true; SetGlowColor(greenFill, FullGlowColor); break;
                case PointColor.Blue:  _blueCountingDown  = true; SetGlowColor(blueFill,  FullGlowColor); break;
            }
            return; // Fuellstand bleibt auf "voll", Update() uebernimmt ab jetzt den Countdown.
        }

        // Solange eine Farbe gerade im Special-Mode-Countdown steckt: den sofortigen Reset-auf-0-
        // Event, den PhaseManager direkt nach dem Trigger noch sendet, NICHT anwenden -- sonst
        // wuerde der Balken vor Start des Countdowns schon leerspringen.
        switch (color)
        {
            case PointColor.Pink:  if (!_pinkCountingDown)  UpdateFill(pinkFill,  pinkGlow,  pinkRewardPreview,  current, threshold); break;
            case PointColor.Green: if (!_greenCountingDown) UpdateFill(greenFill, greenGlow, greenRewardPreview, current, threshold); break;
            case PointColor.Blue:  if (!_blueCountingDown)  UpdateFill(blueFill,  blueGlow,  blueRewardPreview,  current, threshold); break;
        }
    }

    private void HandleSpecialPhaseComplete()
    {
        // Kommt undifferenziert (kein Farb-/Mode-Argument) von allen 3 Systemen -- da immer nur EIN
        // Special Mode gleichzeitig laeuft, reicht es, die aktuell zaehlende(n) Farbe(n) zu beenden.
        if (_pinkCountingDown)  { _pinkCountingDown  = false; pinkFill.fillAmount  = 0f; SetGlowColor(pinkFill,  NormalGlowColor); }
        if (_greenCountingDown) { _greenCountingDown = false; greenFill.fillAmount = 0f; SetGlowColor(greenFill, NormalGlowColor); }
        if (_blueCountingDown)  { _blueCountingDown  = false; blueFill.fillAmount  = 0f; SetGlowColor(blueFill,  NormalGlowColor); }
    }

    private void Update()
    {
        if (_pinkCountingDown)  ApplyCountdown(pinkFill,  PointColor.Pink);
        if (_greenCountingDown) ApplyCountdown(greenFill, PointColor.Green);
        if (_blueCountingDown)  ApplyCountdown(blueFill,  PointColor.Blue);
    }

    // Welcher Special Mode zu welcher Farbe gehoert: siehe PhaseManager.SpecialModeForColor
    // (Pink->Gravity, Green->Vortex, Blue->Fountain) -- hier dieselbe Zuordnung genutzt, um an das
    // richtige System's Progress01 heranzukommen.
    //
    // WICHTIG: Progress01 wird erst beim tatsaechlichen Start des Spawn-Loops (Co_GravityMode/
    // Co_VortexMode/SpawnRoutine) auf 0 zurueckgesetzt -- NICHT schon in dem Moment, in dem der
    // Balken voll wird und dieser Countdown hier beginnt. Dazwischen liegt noch die Portal-Orb-
    // Fluganimation (SpawnActivationOrb -> WaitUntil IsModeActive), teils mehrere Sekunden. Ohne
    // den IsActive-Guard wuerde in dieser Luecke noch der ALTE Progress01-Wert vom vorherigen Lauf
    // gelesen (z.B. 1.0, wenn die letzte Phase komplett durchgelaufen ist) -> Balken wirkt faelschlich
    // leer, bis der Spawn-Loop endlich startet und Progress01 zurueckspringt.
    private void ApplyCountdown(Image fill, PointColor color)
    {
        if (fill == null) return;

        bool systemActive;
        float progress;
        switch (color)
        {
            case PointColor.Pink:
                systemActive = GravityModeSystem.Instance != null && GravityModeSystem.Instance.IsActive;
                progress = systemActive ? GravityModeSystem.Instance.Progress01 : 0f;
                break;
            case PointColor.Green:
                systemActive = VortexModeSystem.Instance != null && VortexModeSystem.Instance.IsActive;
                progress = systemActive ? VortexModeSystem.Instance.Progress01 : 0f;
                break;
            default:
                systemActive = FountainModeSystem.Instance != null && FountainModeSystem.Instance.IsActive;
                progress = systemActive ? FountainModeSystem.Instance.Progress01 : 0f;
                break;
        }

        // Solange der Spawn-Loop noch nicht aktiv ist (Orb fliegt noch): Balken bleibt voll (glueht
        // bereits), statt einen veralteten Progress01-Wert vom vorherigen Special Mode zu zeigen.
        float target = systemActive ? (1f - progress) : 1f;

        // Progress01 springt pro gespawntem Element sprunghaft (Stufen von 1/totalCount) -- weich
        // dorthin gleiten statt ruckartig springen, wirkt wie ein fluessig runterlaufender Balken.
        fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, target, countdownSmoothSpeed * Time.unscaledDeltaTime);
    }

    private void UpdateFill(Image fill, RectTransform glow, GameObject rewardPreview, int current, int threshold)
    {
        if (fill == null) return;
        float amount = threshold > 0 ? Mathf.Clamp01((float)current / threshold) : 0f;
        fill.fillAmount = amount;

        if (glow != null)
        {
            // Glow-Marker entlang der Fülllänge des Balkens positionieren (links -> rechts, horizontaler Fill).
            float width = fill.rectTransform.rect.width;
            glow.anchoredPosition = new Vector2(-width * 0.5f + width * amount, glow.anchoredPosition.y);
            glow.gameObject.SetActive(current > 0);
        }

        if (rewardPreview != null)
            rewardPreview.SetActive(amount >= rewardPreviewThreshold);
    }
}
