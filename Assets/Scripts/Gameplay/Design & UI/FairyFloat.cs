using UnityEngine;

// Unabhängiger, sanfter Schwebeeffekt: driftet zufällig in wechselnde Richtungen um die
// Ausgangsposition. Jede Instanz startet mit eigenem Zufalls-Timing/-Ziel, damit mehrere Fairies
// nicht synchron im selben Takt schweben.
public class FairyFloat : MonoBehaviour
{
    [SerializeField] private float moveRadius   = 0.15f;
    [SerializeField] private float minInterval  = 1.2f;
    [SerializeField] private float maxInterval  = 2.6f;
    [SerializeField] private float moveDuration = 1.4f;

    [Header("Flügel-synchronisierter Auftrieb (optional)")]
    [Tooltip("Wenn gesetzt, hebt sich die Fairy beim Runterflappen der Flügel leicht an/senkt sich beim Aufschlag.")]
    [SerializeField] private FairyWingFlap wingFlap;
    [SerializeField] private float liftAmount = 0.05f;

    private Vector3 basePos;
    private Vector3 fromPos;
    private Vector3 toPos;
    private float timer;
    private float currentInterval;

    private void Start()
    {
        basePos = transform.position;
        fromPos = basePos;
        toPos   = RandomNearbyPoint();

        // Zufälliger Start-Fortschritt/Intervall, damit mehrere Fairies nicht im Gleichtakt starten.
        timer           = Random.Range(0f, moveDuration);
        currentInterval = Random.Range(minInterval, maxInterval);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveDuration));
        Vector3 wanderPos = Vector3.Lerp(fromPos, toPos, k);

        float lift = wingFlap != null ? wingFlap.LiftPhase * liftAmount : 0f;
        transform.position = wanderPos + Vector3.up * lift;

        if (timer >= currentInterval)
        {
            timer           = 0f;
            currentInterval = Random.Range(minInterval, maxInterval);
            // wanderPos (OHNE Lift-Offset) als neue Basis nehmen — sonst würde sich der
            // Flügel-Auftrieb über die Zeit dauerhaft in die Wander-Position einbrennen.
            fromPos         = wanderPos;
            toPos           = RandomNearbyPoint();
        }
    }

    private Vector3 RandomNearbyPoint()
    {
        Vector2 dir  = Random.insideUnitCircle.normalized;
        float   dist = Random.Range(moveRadius * 0.4f, moveRadius);
        return basePos + new Vector3(dir.x, dir.y, 0f) * dist;
    }
}
