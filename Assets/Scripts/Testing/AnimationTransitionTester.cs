using System.Collections;
using UnityEngine;

// Testhilfe fuer den Uebergang Idle -> GetEnergy -> Idle.
//
// Wie gut der Einstieg aussieht, haengt davon ab, WANN im Idle-Zyklus der Trigger faellt:
// bei Phase 0 steht die Figur genau in der Startpose des GetEnergy-Clips, bei Phase 0.5 dagegen
// am hoechsten Punkt des Schwebens und mit vertauschten Beinen. Zufaelliges Ausprobieren im
// Spiel trifft den unguenstigsten Moment womoeglich nie -- deshalb faehrt dieses Skript die
// Phasen der Reihe nach ab und loggt jedes Mal, bei welcher es ausgeloest hat.
//
// Nur fuer die Testszene gedacht, gehoert in keine Spielszene.
public class AnimationTransitionTester : MonoBehaviour
{
    [Tooltip("Leer lassen = wird in den Kindern gesucht.")]
    [SerializeField] private Animator animator;

    [SerializeField] private string idleStateName = "Idle_Fly";
    [SerializeField] private string triggerName   = "GetEnergy";

    [Tooltip("Stellen im Idle-Zyklus, an denen ausgeloest wird (0 = Zyklusanfang, 0.5 = Mitte). " +
             "0.5 ist der unguenstigste Fall: Huefte oben, Beine vertauscht.")]
    [SerializeField] private float[] phasen = { 0f, 0.2f, 0.4f, 0.6f, 0.8f };

    [Tooltip("Sekunden Ruhe zwischen zwei Durchlaeufen, damit der Idle wieder sauber laeuft.")]
    [SerializeField] private float pauseDazwischen = 2f;

    [SerializeField] private bool automatischDurchlaufen = true;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (automatischDurchlaufen) StartCoroutine(Co_Durchlauf());
    }

    [ContextMenu("Jetzt ausloesen")]
    public void JetztAusloesen()
    {
        if (animator == null) { Debug.LogWarning("[Tester] Kein Animator.", this); return; }
        animator.SetTrigger(triggerName);
        Debug.Log($"[Tester] Manuell ausgeloest bei Idle-Phase {AktuellePhase():0.00}", this);
    }

    private float AktuellePhase()
    {
        var st = animator.GetCurrentAnimatorStateInfo(0);
        return st.IsName(idleStateName) ? st.normalizedTime % 1f : -1f;
    }

    private IEnumerator Co_Durchlauf()
    {
        if (animator == null) yield break;
        yield return new WaitForSeconds(0.5f);

        int i = 0;
        while (true)
        {
            float ziel = phasen.Length > 0 ? phasen[i % phasen.Length] : 0f;
            i++;

            // Warten, bis der Idle laeuft UND die gewuenschte Phase gerade erreicht wird.
            // Auf den Phasensprung achten statt auf ein Zeitfenster -- sonst trifft man bei
            // kurzen Clips die Stelle nie genau.
            float vorher = -1f;
            while (true)
            {
                float jetzt = AktuellePhase();
                if (jetzt >= 0f)
                {
                    bool erreicht = vorher >= 0f &&
                                    (vorher < ziel && jetzt >= ziel || vorher > jetzt && ziel <= jetzt);
                    if (erreicht || (ziel <= 0.001f && vorher > jetzt)) break;
                    vorher = jetzt;
                }
                yield return null;
            }

            animator.SetTrigger(triggerName);
            Debug.Log($"[Tester] Ausgeloest bei Idle-Phase {ziel:0.00}", this);

            // Abwarten, bis GetEnergy durch ist und der Idle wieder laeuft
            float t = 0f;
            while (t < 5f && AktuellePhase() < 0f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(pauseDazwischen);
        }
    }
}
