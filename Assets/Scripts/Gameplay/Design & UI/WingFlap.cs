using UnityEngine;

// Prozedurales Flügelschlagen für den Peek-a-boo-Papagei (Tropical Jungle).
// Body-Sprite mit zwei Flügel-Kind-Sprites; die Flügel rotieren symmetrisch um ihren
// Schulter-Pivot per Sinus → kein Sprite-Sheet nötig, praktisch gratis.
//
// Setup: Sprite-Pivot der Flügel ans Schulter-/Ansatz-Ende legen, dann schlagen sie
// realistisch von dort. Linker und rechter Flügel rotieren spiegelverkehrt.
public class WingFlap : MonoBehaviour
{
    [Header("Flügel")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;

    [Header("Schlag")]
    [Tooltip("Max. Ausschlag des Flügels (Grad).")]
    [SerializeField] private float flapAmplitude = 35f;
    [Tooltip("Normale Schläge pro Sekunde (Schweben in der Mitte).")]
    [SerializeField] private float flapFrequency = 2.5f;
    [Tooltip("Ruhewinkel des Flügels (Grad, vom Sprite-Default).")]
    [SerializeField] private float restAngle = 0f;

    [Header("Schlag-Boost beim Rein-/Rausfliegen")]
    [Tooltip("Höhere Schlagfrequenz während Rein-/Rausflug (kräftiger Flug).")]
    [SerializeField] private float flyInOutFrequency = 5f;
    [Tooltip("Wie weich zwischen Boost- und Normal-Frequenz übergeblendet wird (Sek). Größer = sanfter.")]
    [SerializeField] private float frequencySmoothTime = 0.5f;

    [Header("Flug-Auf/Ab (synchron zum Flügelschlag)")]
    [Tooltip("Vertikaler Hub pro Schlag in Local Units. Größer = mehr Flugbewegung. 0 = aus.")]
    [SerializeField] private float flightBobAmplitude = 0.35f;
    [Tooltip("Phasen-Verschiebung (0..1), damit der Vogel im richtigen Moment steigt. Bei Bedarf justieren.")]
    [Range(0f, 1f)] [SerializeField] private float flightBobPhase = 0.25f;
    [Tooltip("Was auf/ab bewegt wird. Leer = dieses Objekt. WICHTIG: NICHT das vom Peek-System bewegte Root nehmen → eigenes Child!")]
    [SerializeField] private Transform flightBobTarget;

    [Header("Optionen")]
    [SerializeField] private bool useUnscaledTime = false;

    private float leftBaseZ, rightBaseZ;
    private Vector3 bobBasePos;

    private float currentFrequency;
    private float targetFrequency;
    private float freqVel;
    private float phase;   // akkumulierte Phase → Frequenzwechsel ohne Sprung

    private void Awake()
    {
        if (leftWing  != null) leftBaseZ  = leftWing.localEulerAngles.z;
        if (rightWing != null) rightBaseZ = rightWing.localEulerAngles.z;

        if (flightBobTarget == null) flightBobTarget = transform;
        bobBasePos = flightBobTarget.localPosition;

        currentFrequency = targetFrequency = flapFrequency;
    }

    // Vom PeekABooSystem: true beim Rein-/Rausfliegen (schneller), false beim Schweben.
    // Der Übergang wird weich geblendet.
    public void SetBoost(bool active) => targetFrequency = active ? flyInOutFrequency : flapFrequency;

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Frequenz weich zur Ziel-Frequenz führen, Phase damit akkumulieren (kein Sprung)
        currentFrequency = Mathf.SmoothDamp(currentFrequency, targetFrequency, ref freqVel, frequencySmoothTime);
        phase += dt * currentFrequency * Mathf.PI * 2f;

        float wave = Mathf.Sin(phase);   // -1..1
        float flap = restAngle + wave * flapAmplitude;

        // Flügel spiegelverkehrt: links +, rechts -
        if (leftWing != null)
            leftWing.localRotation  = Quaternion.Euler(0f, 0f, leftBaseZ  + flap);
        if (rightWing != null)
            rightWing.localRotation = Quaternion.Euler(0f, 0f, rightBaseZ - flap);

        // Flug-Hub: Körper steigt/sinkt im Schlag-Takt (eigene Phase → Auftrieb beim Abschlag)
        if (flightBobAmplitude != 0f && flightBobTarget != null)
        {
            float bob = Mathf.Sin(phase + flightBobPhase * Mathf.PI * 2f) * flightBobAmplitude;
            flightBobTarget.localPosition = bobBasePos + new Vector3(0f, bob, 0f);
        }
    }
}
