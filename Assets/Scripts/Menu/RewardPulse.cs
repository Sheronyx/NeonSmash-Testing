using UnityEngine;

// Laesst ein UI-Element gleichmaessig pulsieren und leicht wackeln, solange die Komponente
// aktiv ist. Wird im Quests-Fenster eingeschaltet, sobald eine Aufgabe erfuellt und die
// Belohnung abholbar ist. Beim Ausschalten wird die Ruhelage exakt wiederhergestellt.
public class RewardPulse : MonoBehaviour
{
    [Header("Pulsieren")]
    [Tooltip("Groessenausschlag nach oben, z. B. 0.08 = bis 108 %.")]
    [SerializeField] private float pulseAmount = 0.22f;
    [SerializeField] private float pulseSpeed  = 3.6f;

    [Header("Wackeln")]
    [Tooltip("Ausschlag der Drehung in Grad, nach beiden Seiten.")]
    [SerializeField] private float wobbleDegrees = 7f;
    [Tooltip("Bewusst nicht dasselbe Tempo wie das Pulsieren -- sonst wirkt es mechanisch.")]
    [SerializeField] private float wobbleSpeed   = 2.6f;

    private Vector3    _ruheSkalierung = Vector3.one;
    private Quaternion _ruheDrehung    = Quaternion.identity;
    private bool       _gemerkt;
    private float      _zeit;

    private void Awake() => Merken();

    private void OnEnable()
    {
        Merken();
        _zeit = 0f;
    }

    private void OnDisable()
    {
        if (!_gemerkt) return;
        transform.localScale    = _ruheSkalierung;
        transform.localRotation = _ruheDrehung;
    }

    private void Merken()
    {
        if (_gemerkt) return;
        _ruheSkalierung = transform.localScale;
        _ruheDrehung    = transform.localRotation;
        _gemerkt = true;
    }

    private void Update()
    {
        _zeit += Time.unscaledDeltaTime;   // laeuft auch, wenn das Spiel pausiert ist
        float puls = 1f + Mathf.Sin(_zeit * pulseSpeed) * pulseAmount;
        transform.localScale    = _ruheSkalierung * puls;
        transform.localRotation = _ruheDrehung * Quaternion.Euler(0f, 0f, Mathf.Sin(_zeit * wobbleSpeed) * wobbleDegrees);
    }
}
