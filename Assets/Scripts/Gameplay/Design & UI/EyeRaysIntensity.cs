using UnityEngine;

// Steuert Farbe, Alpha UND Größe des "Eye Rays"-Partikelsystems live pro Elementreihe: alle drei
// wandern parallel von "ruhig" zu "intensiv", je mehr von der Reaktionszeit der GERADE aktuellen Reihe
// verstreicht (siehe PhaseManager.CurrentRowProgress01), und fallen beim nächsten Treffer sofort
// wieder ab — smooth statt hart, alle drei über dieselbe transitionDuration synchron geglättet.
//
// Größe läuft bewusst über transform.localScale (nicht "Start Size" im Particle-System-Modul): Start
// Size wirkt nur auf NEU gespawnte Partikel, bereits lebende behalten ihre alte Größe bis sie
// durchlaufen sind — dieselbe Verzögerung, die wir bei Farbe/Alpha schon hatten. Scale auf dem
// Transform wirkt dagegen sofort auf ALLE Partikel gleichzeitig, unabhängig von ihrem Alter.
public class EyeRaysIntensity : MonoBehaviour
{
    [SerializeField] private ParticleSystem eyeRays;

    [Header("Farbe (RGB)")]
    [Tooltip("Farbe direkt nach dem Spawnen einer neuen Reihe (volle Reaktionszeit übrig).")]
    [SerializeField] private Color colorAtRowStart = new Color32(0x44, 0x44, 0x44, 0xFF);
    [Tooltip("Farbe kurz bevor die Reaktionszeit dieser Reihe abläuft.")]
    [SerializeField] private Color colorAtRowTimeout = new Color32(0xFF, 0x00, 0x00, 0xFF);

    [Header("Alpha-Bereich")]
    [Tooltip("Alpha direkt nach dem Spawnen einer neuen Reihe (volle Reaktionszeit übrig).")]
    [Range(0, 255)] [SerializeField] private int alphaAtRowStart = 20;
    [Tooltip("Alpha kurz bevor die Reaktionszeit dieser Reihe abläuft.")]
    [Range(0, 255)] [SerializeField] private int alphaAtRowTimeout = 255;

    [Header("Größe (transform.localScale)")]
    [Tooltip("Scale direkt nach dem Spawnen einer neuen Reihe (volle Reaktionszeit übrig).")]
    [SerializeField] private float scaleAtRowStart = 1f;
    [Tooltip("Scale kurz bevor die Reaktionszeit dieser Reihe abläuft.")]
    [SerializeField] private float scaleAtRowTimeout = 1.3f;

    [Header("Glättung")]
    [Tooltip("Wie viele Sekunden Farbe, Alpha UND Größe jeweils brauchen, um komplett vom Start- zum " +
             "Zielwert zu wechseln — alle drei sind dadurch immer gleich schnell. Wichtig vor allem für " +
             "den Reset nach einem Treffer, damit nichts hart zurückspringt.")]
    [SerializeField] private float transitionDuration = 0.3f;

    [Header("Timeout-Burst")]
    [Tooltip("Einmaliger zusätzlicher Partikel-Schub genau in dem Moment, in dem die Reaktionszeit " +
             "einer Reihe wirklich abläuft (MixedPointSpawner.OnRowTimedOut) — on top vom laufenden " +
             "Ambient-Effekt.")]
    [SerializeField] private int timeoutBurstCount = 200;

    private Color _currentColor;
    private float _currentAlpha;
    private float _currentScale;
    private Vector3 _baseScale;

    private void Start()
    {
        _currentColor = colorAtRowStart;
        _currentAlpha = alphaAtRowStart;
        _currentScale = scaleAtRowStart;

        // Ursprüngliche (evtl. bewusst ungleichmäßige, z.B. für die Strahl-Form gestreckte) Skalierung
        // merken — unser Wert wird nur als Multiplikator DARAUF angewendet, nicht als Ersatz dafür.
        _baseScale = eyeRays != null ? eyeRays.transform.localScale : Vector3.one;
    }

    private void OnEnable()
    {
        MixedPointSpawner.OnRowTimedOut += HandleRowTimedOut;
    }

    private void OnDisable()
    {
        MixedPointSpawner.OnRowTimedOut -= HandleRowTimedOut;
    }

    private void HandleRowTimedOut()
    {
        if (eyeRays != null) eyeRays.Emit(timeoutBurstCount);
    }

    private void Update()
    {
        if (eyeRays == null || PhaseManager.Instance == null) return;

        float progress = PhaseManager.Instance.CurrentRowProgress01; // 0 = frisch gespawnt, 1 = abgelaufen
        Color targetColor = Color.Lerp(colorAtRowStart, colorAtRowTimeout, progress);
        float targetAlpha = Mathf.Lerp(alphaAtRowStart, alphaAtRowTimeout, progress);
        float targetScale = Mathf.Lerp(scaleAtRowStart, scaleAtRowTimeout, progress);

        // Delta pro Frame anhand der JEWEILS EIGENEN konfigurierten Start→Ziel-Spanne berechnet, nicht
        // anhand einer angenommenen Standard-Spanne — dadurch braucht jede der drei Eigenschaften exakt
        // transitionDuration Sekunden für ihren eigenen vollen Weg, unabhängig von ihrer Skala.
        float dt = Mathf.Max(0.0001f, transitionDuration);
        float colorDelta = Time.deltaTime / dt; // Farbkanäle sind 0-1, volle Spanne = 1
        float alphaDelta = Mathf.Abs(alphaAtRowTimeout - alphaAtRowStart) * Time.deltaTime / dt;
        float scaleDelta = Mathf.Abs(scaleAtRowTimeout - scaleAtRowStart) * Time.deltaTime / dt;

        _currentColor.r = Mathf.MoveTowards(_currentColor.r, targetColor.r, colorDelta);
        _currentColor.g = Mathf.MoveTowards(_currentColor.g, targetColor.g, colorDelta);
        _currentColor.b = Mathf.MoveTowards(_currentColor.b, targetColor.b, colorDelta);
        _currentAlpha   = Mathf.MoveTowards(_currentAlpha, targetAlpha, alphaDelta);
        _currentScale   = Mathf.MoveTowards(_currentScale, targetScale, scaleDelta);

        var main = eyeRays.main;
        Color c = _currentColor;
        c.a = _currentAlpha / 255f;
        main.startColor = c;

        eyeRays.transform.localScale = _baseScale * _currentScale;
    }
}
