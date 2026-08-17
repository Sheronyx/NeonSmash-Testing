using UnityEngine;

// Zieht die drei Feen Richtung ihrer gemeinsamen Mitte, je weniger Reaktionszeit der aktuellen
// Elementreihe übrig ist (PhaseManager.CurrentRowProgress01) — und wieder nach außen zur Ursprungs-
// position, sobald eine neue Reihe mit voller Zeit startet. Das Dreieck aus TetherLines/
// TetherTriangleFillMesh/TriangleFillMesh (inkl. der gefüllten Fläche in der Mitte) schrumpft dadurch
// automatisch mit, weil die alle live die Feen-Positionen auslesen.
//
// Läuft ADDITIV neben FairyFloat, nicht anstelle davon: FairyFloat wackelt in seinem eigenen Update()
// weiter lokal um seine gecachte basePos, dieses Script läuft in LateUpdate() (garantiert NACH allen
// Update()-Aufrufen) und addiert nur einen zusätzlichen Versatz Richtung Zentrum obendrauf, ohne
// FairyFloat selbst zu verändern.
public class FairyTriangleContract : MonoBehaviour
{
    [Header("Feen")]
    [SerializeField] private Transform fairyA;
    [SerializeField] private Transform fairyB;
    [SerializeField] private Transform fairyC;

    [Header("Ziehen zur Mitte (0 = Ursprungsposition, 1 = exakt im Zentrum)")]
    [Tooltip("Zug-Stärke Richtung Zentrum direkt nach dem Spawnen einer neuen Reihe, bei der LEICHTESTEN " +
             "Phase (maxReactionTime) — das absolute Minimum.")]
    [Range(0f, 1f)] [SerializeField] private float pullAtRowStart = 0f;
    [Tooltip("Zug-Stärke Richtung Zentrum direkt nach dem Spawnen einer neuen Reihe, bei der SCHWERSTEN " +
             "Phase (minReactionTime) — schon am Reihen-Start näher zusammen, nicht erst bei Ablauf.")]
    [Range(0f, 1f)] [SerializeField] private float pullAtRowStartHardestPhase = 0.3f;
    [Tooltip("Zug-Stärke Richtung Zentrum kurz bevor die Reaktionszeit dieser Reihe abläuft.")]
    [Range(0f, 1f)] [SerializeField] private float pullAtRowTimeout = 0.7f;

    [Header("Globale Reaktionszeit-Spanne (für Zug-Stärke am Reihen-Start)")]
    [SerializeField] private float maxReactionTime = 2.2f;
    [SerializeField] private float minReactionTime = 0.5f;

    [Header("Glättung (SmoothDamp -> natürliches Beschleunigen/Abbremsen statt konstanter Geschwindigkeit)")]
    [Tooltip("Ungefähre Zeit, die der Zug braucht, um sich dem Zielwert anzunähern — wichtig vor allem " +
             "für den Reset nach einem Treffer, damit die Feen nicht hart/linear zurückschnappen.")]
    [SerializeField] private float smoothTime = 0.3f;
    [Tooltip("Pro Fee eine leicht andere Glättungszeit (+/- dieser Wert, einmalig zufällig verteilt), " +
             "damit nicht alle drei exakt synchron/rototisch fliegen.")]
    [SerializeField] private float perFairySmoothTimeJitter = 0.08f;

    [Header("Bogen statt Gerader (Flugkurve)")]
    [Tooltip("Wie stark die Flugbahn seitlich ausbeult, als Anteil der Home->Zentrum-Distanz " +
             "(0 = stur gerade Linie zum Zentrum, wie zuvor). Jede Fee bekommt zufällig eine der beiden " +
             "Seiten zugelost, damit die drei Bögen nicht identisch aussehen.")]
    [SerializeField] private float arcHeight = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private Vector3 _homeA, _homeB, _homeC, _center;
    private Vector3 _controlA, _controlB, _controlC;
    private float _currentPullA, _currentPullB, _currentPullC;
    private float _velocityA, _velocityB, _velocityC;
    private float _smoothTimeA, _smoothTimeB, _smoothTimeC;
    private bool _homeCaptured;
    private FairyFloat _floatA, _floatB, _floatC;

    private void Start()
    {
        _floatA = fairyA != null ? fairyA.GetComponent<FairyFloat>() : null;
        _floatB = fairyB != null ? fairyB.GetComponent<FairyFloat>() : null;
        _floatC = fairyC != null ? fairyC.GetComponent<FairyFloat>() : null;

        _smoothTimeA = Mathf.Max(0.01f, smoothTime + Random.Range(-perFairySmoothTimeJitter, perFairySmoothTimeJitter));
        _smoothTimeB = Mathf.Max(0.01f, smoothTime + Random.Range(-perFairySmoothTimeJitter, perFairySmoothTimeJitter));
        _smoothTimeC = Mathf.Max(0.01f, smoothTime + Random.Range(-perFairySmoothTimeJitter, perFairySmoothTimeJitter));
    }

    private void LateUpdate()
    {
        if (fairyA == null || fairyB == null || fairyC == null || PhaseManager.Instance == null) return;

        // Home-Positionen NICHT sofort in Start() cachen -- da läuft noch die FairyArrivalSequence-
        // Flugbahn (Portal -> Ruheposition), Start() würde also die falsche (Portal-)Position erwischen.
        // FairyArrivalSequence aktiviert FairyFloat erst wieder, wenn die Fee wirklich angekommen ist --
        // genau dann (erstmalig) cachen wir die "voll auseinander"-Ursprungsposition.
        if (!_homeCaptured)
        {
            bool arrived = (_floatA == null || _floatA.enabled)
                         && (_floatB == null || _floatB.enabled)
                         && (_floatC == null || _floatC.enabled);
            if (!arrived)
            {
                if (logDebug) Debug.Log($"[FairyTriangleContract] wartet auf Ankunft: floatA.enabled={_floatA?.enabled} floatB.enabled={_floatB?.enabled} floatC.enabled={_floatC?.enabled}");
                return;
            }

            _homeA = fairyA.position;
            _homeB = fairyB.position;
            _homeC = fairyC.position;
            _center = (_homeA + _homeB + _homeC) / 3f;
            _currentPullA = _currentPullB = _currentPullC = pullAtRowStart;
            _controlA = BuildArcControlPoint(_homeA);
            _controlB = BuildArcControlPoint(_homeB);
            _controlC = BuildArcControlPoint(_homeC);
            _homeCaptured = true;

            if (logDebug) Debug.Log($"[FairyTriangleContract] Home-Positionen gecacht: A={_homeA} B={_homeB} C={_homeC} Center={_center} Abstand A-Center={Vector3.Distance(_homeA, _center):F3}");
        }

        // Reihen-Start-Zug ist nicht fix, sondern selbst proportional dazu, wo die CurrentReactionTime
        // der AKTUELLEN PHASE zwischen globalem Minimum (schwerste Phase) und Maximum (leichteste Phase)
        // liegt -- je schwerer die Phase, desto näher sind die Feen schon am Reihen-Start zusammen.
        float reactionRatio = Mathf.InverseLerp(minReactionTime, maxReactionTime, PhaseManager.Instance.CurrentReactionTime);
        float effectivePullAtRowStart = Mathf.Lerp(pullAtRowStartHardestPhase, pullAtRowStart, reactionRatio);

        float progress = PhaseManager.Instance.CurrentRowProgress01; // 0 = frisch gespawnt, 1 = abgelaufen
        float targetPull = Mathf.Lerp(effectivePullAtRowStart, pullAtRowTimeout, progress);

        _currentPullA = Mathf.SmoothDamp(_currentPullA, targetPull, ref _velocityA, _smoothTimeA);
        _currentPullB = Mathf.SmoothDamp(_currentPullB, targetPull, ref _velocityB, _smoothTimeB);
        _currentPullC = Mathf.SmoothDamp(_currentPullC, targetPull, ref _velocityC, _smoothTimeC);

        if (logDebug)
        {
            Debug.Log($"[FairyTriangleContract] progress={progress:F3} rt={PhaseManager.Instance.CurrentReactionTime:F3} " +
                      $"reactionRatio={reactionRatio:F3} effStart={effectivePullAtRowStart:F3} target={targetPull:F3} " +
                      $"currentA={_currentPullA:F3} |offsetA|={((_center - _homeA) * _currentPullA).magnitude:F4}");
        }

        // Versatz relativ zur jeweiligen URSPRUNGS-Position (nicht zur aktuellen!), sonst würde sich
        // der Effekt bei jedem Frame draufaddieren und immer weiter Richtung Zentrum "wegdriften". Statt
        // einer geraden Linie folgt der Weg einer quadratischen Bézier-Kurve über den vorberechneten
        // Bogen-Kontrollpunkt -- bei _currentPull=0 exakt Home, bei =1 exakt Zentrum, dazwischen ein Bogen.
        Vector3 offsetA = EvaluateArcOffset(_homeA, _controlA, _center, _currentPullA);
        Vector3 offsetB = EvaluateArcOffset(_homeB, _controlB, _center, _currentPullB);
        Vector3 offsetC = EvaluateArcOffset(_homeC, _controlC, _center, _currentPullC);

        // FairyFloat hat in seinem EIGENEN Update() bereits um _home* gewackelt -- wir addieren nur den
        // Zug-Versatz obendrauf, ohne das lokale Wackeln zu überschreiben.
        fairyA.position += offsetA;
        fairyB.position += offsetB;
        fairyC.position += offsetC;
    }

    // Kontrollpunkt auf halbem Weg zwischen Home und Zentrum, seitlich (senkrecht zur Home->Zentrum-
    // Linie) um arcHeight * Distanz verschoben -- Seite (links/rechts) wird pro Fee zufällig gewählt,
    // damit die drei Bögen nicht wie Kopien voneinander aussehen.
    private Vector3 BuildArcControlPoint(Vector3 home)
    {
        Vector3 toCenter = _center - home;
        Vector3 mid = Vector3.Lerp(home, _center, 0.5f);
        Vector3 perpendicular = new Vector3(-toCenter.y, toCenter.x, 0f).normalized;
        float side = Random.value < 0.5f ? -1f : 1f;
        return mid + perpendicular * (toCenter.magnitude * arcHeight * side);
    }

    // Quadratische Bézier zwischen home (t=0) und center (t=1) über control -- gibt den VERSATZ relativ
    // zu home zurück (nicht die absolute Position), da wir additiv auf FairyFloats Ergebnis draufaddieren.
    private Vector3 EvaluateArcOffset(Vector3 home, Vector3 control, Vector3 center, float t)
    {
        float tc = Mathf.Clamp01(t);
        float u = 1f - tc;
        Vector3 point = u * u * home + 2f * u * tc * control + tc * tc * center;
        return point - home;
    }
}
