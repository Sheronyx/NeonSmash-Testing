using UnityEngine;

// Steuert live NUR EINEN Keyframe der Width-Kurve eines LineRenderers je nach übriger Reaktionszeit der
// aktuellen Elementreihe (PhaseManager.CurrentRowProgress01) — gleiches Prinzip wie
// EyeRaysIntensity/FairyPlasmaLink. Standardmäßig automatisch der MITTLERE Key (z.B. für die
// Fee-zu-Fee-Linien mit einem "Bauch" in der Mitte), per keyIndexOverride aber auch explizit ein anderer
// Key wählbar (z.B. Index 0 = der ERSTE Key, für die Fee-zu-Orb-Linien, deren Breite am Fee-Ende
// schrumpfen soll). Die restlichen Keys (i.d.R. auf 0, damit die Linie spitz endet) bleiben unangetastet
// — nur der gewählte Key pulsiert. Läuft unabhängig neben TetherLineFX auf demselben Objekt —
// TetherLineFX kümmert sich nur um Position/Zittern.
[RequireComponent(typeof(LineRenderer))]
public class TetherReactionWidth : MonoBehaviour
{
    [SerializeField] private LineRenderer line;

    [Header("Ziel-Key")]
    [Tooltip("-1 = automatisch der MITTLERE Key der Width-Kurve (Standard, z.B. Fee-zu-Fee-Linien mit " +
             "Bauch in der Mitte). Explizit 0 = der ERSTE Key (z.B. Fee-zu-Orb-Linien, Breite schrumpft " +
             "am Fee-Ende). Jeder andere gültige Index geht ebenfalls.")]
    [SerializeField] private int keyIndexOverride = -1;

    [Header("Breite (nur der gewählte Key der Width-Kurve)")]
    [Tooltip("Wert des gewählten Keyframes bei der LEICHTESTEN Phase (maxReactionTime) UND vollem " +
             "Zeitguthaben der aktuellen Reihe (Reihe frisch gespawnt) — das absolute Maximum.")]
    [SerializeField] private float widthAtRowStart = 0.16f;
    [Tooltip("Wert des gewählten Keyframes kurz bevor die Reaktionszeit dieser Reihe abläuft.")]
    [SerializeField] private float widthAtRowTimeout = 0f;

    [Header("Globale Reaktionszeit-Spanne")]
    [Tooltip("Reaktionszeit der leichtesten Phase im Spiel — bei dieser CurrentReactionTime erreicht die " +
             "Breite am Reihen-Start ihr volles Maximum (widthAtRowStart).")]
    [SerializeField] private float maxReactionTime = 2.2f;
    [Tooltip("Reaktionszeit der schwersten Phase im Spiel — bei dieser CurrentReactionTime ist die " +
             "Breite selbst am Reihen-Start schon bei 0.")]
    [SerializeField] private float minReactionTime = 0.5f;

    [Header("Glättung")]
    [Tooltip("Wie viele Sekunden die Breite braucht, um komplett vom Start- zum Zielwert zu wechseln — " +
             "wichtig vor allem für den Reset nach einem Treffer, damit sie nicht hart zurückspringt.")]
    [SerializeField] private float transitionDuration = 0.3f;

    [Header("Ausblenden der äußeren Keys")]
    [Tooltip("Sobald der mittlere Key auf/unter diesen ANTEIL der aktuellen Reihen-Maximalbreite fällt, " +
             "werden auch die ÄUSSEREN Keys proportional mit auf 0 heruntergefahren — sonst bleibt eine " +
             "dünne Restlinie sichtbar, obwohl die Reaktionszeit eigentlich komplett abgelaufen ist. " +
             "Relativ statt absolut, weil die Reihen-Maximalbreite je nach Phase stark variiert (siehe " +
             "Globale Reaktionszeit-Spanne oben) — ein fixer absoluter Wert würde bei schwereren Phasen " +
             "(kleinere Maximalbreite) viel zu früh greifen.")]
    [Range(0f, 1f)]
    [SerializeField] private float outerKeysFadeOutThresholdRatio = 0.125f;

    [Header("Debug")]
    [Tooltip("Loggt pro Frame progress/effectiveWidthAtRowStart/currentWidth in die Console — zum " +
             "Aufspüren, ob die Linie rechnerisch zu früh (progress < 1) auf 0 landet, oder ob sie " +
             "'nur' optisch schon vorher unsichtbar dünn wirkt.")]
    [SerializeField] private bool logDebug = false;

    // Ursprüngliche Kurve (Zeiten/Tangenten aller Keys) einmalig gecacht — pro Frame wird davon nur der
    // Wert des gewählten Keys überschrieben, damit die im Inspector gebaute Form sonst unverändert bleibt.
    private Keyframe[] _baseKeys;
    private int _targetKeyIndex;
    private float _currentWidth;

    private void Awake()
    {
        if (line == null) line = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        _baseKeys = line.widthCurve.keys;
        _targetKeyIndex = keyIndexOverride >= 0 && keyIndexOverride < _baseKeys.Length
            ? keyIndexOverride
            : _baseKeys.Length / 2; // z.B. 3 Keys (0, Mitte, 1) -> Index 1
        _currentWidth = widthAtRowStart;
        ApplyMidWidth(_currentWidth, widthAtRowStart);
    }

    private void Update()
    {
        if (line == null || PhaseManager.Instance == null || _baseKeys == null || _baseKeys.Length == 0) return;

        // Reihen-Start-Breite ist nicht fix, sondern selbst proportional dazu, wo die CurrentReactionTime
        // der AKTUELLEN PHASE zwischen dem globalen Minimum (schwerste Phase) und Maximum (leichteste
        // Phase) liegt — je schwerer die Phase, desto kleiner schon der Ausgangswert am Reihen-Start.
        float reactionRatio = Mathf.InverseLerp(minReactionTime, maxReactionTime, PhaseManager.Instance.CurrentReactionTime);
        float effectiveWidthAtRowStart = widthAtRowStart * reactionRatio;

        float progress = PhaseManager.Instance.CurrentRowProgress01; // 0 = frisch gespawnt, 1 = abgelaufen
        float targetWidth = Mathf.Lerp(effectiveWidthAtRowStart, widthAtRowTimeout, progress);

        float dt = Mathf.Max(0.0001f, transitionDuration);
        float maxDelta = Mathf.Abs(widthAtRowTimeout - effectiveWidthAtRowStart) * Time.deltaTime / dt;
        _currentWidth = Mathf.MoveTowards(_currentWidth, targetWidth, maxDelta);

        if (logDebug)
        {
            Debug.Log($"[TetherReactionWidth] {name}: progress={progress:F3} rt={PhaseManager.Instance.CurrentReactionTime:F3} " +
                      $"reactionRatio={reactionRatio:F3} effMax={effectiveWidthAtRowStart:F4} target={targetWidth:F4} current={_currentWidth:F4}");
        }

        ApplyMidWidth(_currentWidth, effectiveWidthAtRowStart);
    }

    private void ApplyMidWidth(float midValue, float currentRowMaxWidth)
    {
        // 1 = äußere Keys behalten ihren ursprünglichen Wert, 0 = äußere Keys komplett ausgeblendet —
        // fährt linear runter, sobald midValue die (an die AKTUELLE Reihen-Maximalbreite gekoppelte)
        // Schwelle unterschreitet, und erreicht 0 genau dann, wenn auch midValue 0 erreicht.
        float fadeThresholdValue = currentRowMaxWidth * outerKeysFadeOutThresholdRatio;
        float outerMultiplier = fadeThresholdValue > 0.0001f
            ? Mathf.Clamp01(midValue / fadeThresholdValue)
            : 1f;

        Keyframe[] keys = (Keyframe[])_baseKeys.Clone();
        for (int i = 0; i < keys.Length; i++)
        {
            if (i == _targetKeyIndex)
            {
                keys[i].value = midValue;
            }
            else
            {
                keys[i].value = _baseKeys[i].value * outerMultiplier;
            }
        }
        line.widthCurve = new AnimationCurve(keys);
    }
}
