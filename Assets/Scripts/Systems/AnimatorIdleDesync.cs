using UnityEngine;

// Startet den Animator einmalig an einem ZUFÄLLIGEN Zeitpunkt innerhalb seines aktuellen (Idle-)
// States, statt immer bei 0 — sonst laufen mehrere Objekte mit demselben Controller/Clip (z.B. die
// drei Feen im Idle-Flug) exakt synchron, egal in welcher Szene. Funktioniert generisch mit
// beliebigem Default-State/Layer, kein State-Name nötig.
public class AnimatorIdleDesync : MonoBehaviour
{
    [Tooltip("Leer lassen = wird automatisch in den Kindern gesucht.")]
    [SerializeField] private Animator animator;
    [SerializeField] private int layer = 0;

    private void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        var state = animator.GetCurrentAnimatorStateInfo(layer);
        animator.Play(state.fullPathHash, layer, Random.value);
    }
}
