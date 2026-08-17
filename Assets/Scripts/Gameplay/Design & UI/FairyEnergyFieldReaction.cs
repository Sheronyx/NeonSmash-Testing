using UnityEngine;

// Alternative zum Tether-Line-Dreieck: ein "Energiefeld" aus zwei Partikel-Systemen (Orb + Glow, wie bei
// der bestehenden Fairy Energy Orb), das LOSE dem gemeinsamen Zentrum der drei Feen hinterherschwebt
// (kein starres Andocken, kein Dreieck-Mesh) -- und dabei kleiner und röter wird, je weniger
// Reaktionszeit der aktuellen Elementreihe übrig ist (PhaseManager.CurrentRowProgress01).
//
// Orb und Glow färben sich unterschiedlich ein: Orb nutzt einen Legacy-Partikel-Shader, dessen Farbe
// über die MATERIAL-Tint-Farbe läuft (_TintColor), Glow dagegen über die normale Start Color im
// Particle-System-Modul (wie bei EyeRaysIntensity). Größe läuft bei beiden über transform.localScale
// (nicht "Start Size"), da Start Size nur neu gespawnte Partikel beeinflusst, localScale dagegen sofort
// alle -- gleicher Grund wie bei EyeRaysIntensity.
public class FairyEnergyFieldReaction : MonoBehaviour
{
    [Header("Feen (für Zentrum)")]
    [SerializeField] private Transform fairyA;
    [SerializeField] private Transform fairyB;
    [SerializeField] private Transform fairyC;

    [Header("Folgen (lose, nicht starr)")]
    [Tooltip("Wie träge das Feld dem Zentrum der drei Feen hinterherschwebt (Sekunden, SmoothDamp) — " +
             "höher = loser/schwebender, niedriger = folgt enger.")]
    [SerializeField] private float followSmoothTime = 0.6f;

    [Header("Orb-Partikel-System (Farbe über Material-Tint)")]
    [SerializeField] private ParticleSystem orbParticleSystem;
    [Tooltip("Name der Farb-Property im Material — Legacy Shaders/Particles/Additive nutzt \"_TintColor\".")]
    [SerializeField] private string orbTintColorProperty = "_TintColor";

    [Header("Glow-Partikel-System (Farbe über Start Color)")]
    [SerializeField] private ParticleSystem glowParticleSystem;

    [Header("Größe (beide Systeme gemeinsam skaliert)")]
    [SerializeField] private float scaleAtRowStart = 1f;
    [SerializeField] private float scaleAtRowTimeout = 0.4f;

    [Header("Farbe (Ziel beim Ablaufen — Ausgangsfarbe wird live von Orb/Glow selbst übernommen)")]
    [Tooltip("Falls deaktiviert, bleibt die Original-Farbe von Orb/Glow immer unverändert — nur die " +
             "Größe reagiert dann noch auf die Reaktionszeit.")]
    [SerializeField] private bool changeColor = true;
    [SerializeField] private Color colorAtRowTimeout = new Color32(0xFF, 0x22, 0x22, 0xFF);

    [Header("Glättung (Größe/Farbe)")]
    [SerializeField] private float transitionDuration = 0.3f;

    private Vector3 _followVelocity;
    private Vector3 _currentPos;
    private float _currentScale;
    private Color _currentOrbColor, _currentGlowColor;
    private Color _orbOriginalColor, _glowOriginalColor;
    private Vector3 _orbBaseScale, _glowBaseScale;
    private Material _orbMaterialInstance;

    private void Start()
    {
        _currentPos = ComputeCenter();
        transform.position = _currentPos;
        _currentScale = scaleAtRowStart;

        _orbBaseScale = orbParticleSystem != null ? orbParticleSystem.transform.localScale : Vector3.one;
        _glowBaseScale = glowParticleSystem != null ? glowParticleSystem.transform.localScale : Vector3.one;

        // .material (statt .sharedMaterial) legt beim ersten Zugriff automatisch eine Instanz-Kopie an --
        // wir verändern also nur unsere eigene Kopie, nicht das geteilte Material-Asset (das evtl. auch
        // von der ursprünglichen Fairy Energy Orb genutzt wird).
        if (orbParticleSystem != null)
        {
            var orbRenderer = orbParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (orbRenderer != null)
            {
                _orbMaterialInstance = orbRenderer.material;
                _orbOriginalColor = _orbMaterialInstance.GetColor(orbTintColorProperty);
            }
        }
        _currentOrbColor = _orbOriginalColor;

        if (glowParticleSystem != null) _glowOriginalColor = glowParticleSystem.main.startColor.color;
        _currentGlowColor = _glowOriginalColor;
    }

    private Vector3 ComputeCenter()
    {
        if (fairyA == null || fairyB == null || fairyC == null) return transform.position;
        return (fairyA.position + fairyB.position + fairyC.position) / 3f;
    }

    private void Update()
    {
        // Position lose Richtung Zentrum nachziehen -- SmoothDamp statt direktem Setzen, damit es wie
        // ein träge schwebendes Feld wirkt statt starr an den Feen zu hängen. Funktioniert unabhängig
        // davon, ob die Feen gerade noch ihre Ankunfts-Flugbahn fliegen -- zieht einfach kontinuierlich
        // Richtung aktuellem Zentrum, kein einmalig gecachter Referenzpunkt nötig.
        Vector3 target = ComputeCenter();
        _currentPos = Vector3.SmoothDamp(_currentPos, target, ref _followVelocity, followSmoothTime);
        transform.position = _currentPos;

        if (PhaseManager.Instance == null) return;

        float progress = PhaseManager.Instance.CurrentRowProgress01; // 0 = frisch gespawnt, 1 = abgelaufen
        float targetScale = Mathf.Lerp(scaleAtRowStart, scaleAtRowTimeout, progress);

        float dt = Mathf.Max(0.0001f, transitionDuration);
        float scaleDelta = Mathf.Abs(scaleAtRowTimeout - scaleAtRowStart) * Time.deltaTime / dt;
        _currentScale = Mathf.MoveTowards(_currentScale, targetScale, scaleDelta);

        if (changeColor)
        {
            Color targetOrbColor = Color.Lerp(_orbOriginalColor, colorAtRowTimeout, progress);
            Color targetGlowColor = Color.Lerp(_glowOriginalColor, colorAtRowTimeout, progress);

            float colorDelta = Time.deltaTime / dt;
            _currentOrbColor = MoveTowardsColor(_currentOrbColor, targetOrbColor, colorDelta);
            _currentGlowColor = MoveTowardsColor(_currentGlowColor, targetGlowColor, colorDelta);
        }
        else
        {
            _currentOrbColor = _orbOriginalColor;
            _currentGlowColor = _glowOriginalColor;
        }

        if (orbParticleSystem != null)
        {
            orbParticleSystem.transform.localScale = _orbBaseScale * _currentScale;
            if (_orbMaterialInstance != null) _orbMaterialInstance.SetColor(orbTintColorProperty, _currentOrbColor);
        }

        if (glowParticleSystem != null)
        {
            glowParticleSystem.transform.localScale = _glowBaseScale * _currentScale;
            var main = glowParticleSystem.main;
            main.startColor = _currentGlowColor;
        }
    }

    private static Color MoveTowardsColor(Color current, Color target, float maxDelta)
    {
        current.r = Mathf.MoveTowards(current.r, target.r, maxDelta);
        current.g = Mathf.MoveTowards(current.g, target.g, maxDelta);
        current.b = Mathf.MoveTowards(current.b, target.b, maxDelta);
        current.a = Mathf.MoveTowards(current.a, target.a, maxDelta);
        return current;
    }
}
