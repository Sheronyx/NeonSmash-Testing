using System.Collections;
using UnityEngine;

// Zeigt an, dass der Diamant-Bonus (5+ in der aktuellen Diamant-Phase gesammelt) einer zufällig
// gelosten Farbe zugeteilt wurde. Es gibt DREI Instanzen dieses Scripts in der Szene (je eine über
// jedem der drei ColorProgressUI-Anzeigen "X/20"), jede mit ihrer eigenen myColor. Nur die Instanz,
// deren Farbe der gelosten Bonus-Farbe entspricht, poppt auf — die anderen beiden ignorieren das Event.
// Erscheint mit Pop-In sobald der Bonus verdient ist (PhaseManager.OnDiamondBonusEarned).
// Startet ein ANDERER Special Mode zuerst, ist der Bonus sofort verfallen → Icon verschwindet direkt.
// Startet GENAU der zur eigenen Farbe passende Special Mode, bleibt das Icon sichtbar/pulsierend,
// bis dieser Mode wieder endet (SpecialModeManager.OnModeEnded) — der Bonus ist ja während des ganzen
// Special Mode aktiv, nicht nur im Moment des Auslösens.
public class DiamondBonusIndicatorUI : MonoBehaviour
{
    [Header("Zuordnung")]
    [Tooltip("Über welcher der drei Farb-Anzeigen (ColorProgressUI) dieses Icon sitzt.")]
    [SerializeField] private PointColor myColor;

    [Header("Pop-In")]
    [SerializeField] private float popInDuration  = 0.25f;
    [SerializeField] private float popInOvershoot = 1.3f;

    private Vector3 targetScale;
    private PointPulse pulse;
    private Coroutine activeRoutine;

    // Subscription bewusst in Awake/OnDestroy statt OnEnable/OnDisable: wir deaktivieren das
    // GameObject unten selbst (SetActive(false)) direkt in Awake, damit es initial unsichtbar
    // startet. Würde die Subscription in OnEnable stehen, würde sie NIE laufen — wenn ein Objekt
    // sich innerhalb seines eigenen Awake() selbst deaktiviert, überspringt Unity dessen OnEnable
    // für diesen ersten Durchlauf, das Script würde also nie auf OnDiamondBonusEarned hören.
    private void Awake()
    {
        targetScale = transform.localScale;
        pulse = GetComponent<PointPulse>();
        PhaseManager.OnDiamondBonusEarned += HandleBonusEarned;
        SpecialModeManager.OnModeStarted += HandleModeStarted;
        SpecialModeManager.OnModeEnded += HandleModeEnded;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        PhaseManager.OnDiamondBonusEarned -= HandleBonusEarned;
        SpecialModeManager.OnModeStarted -= HandleModeStarted;
        SpecialModeManager.OnModeEnded -= HandleModeEnded;
    }

    private void HandleBonusEarned(PointColor color)
    {
        if (color != myColor) return;
        Show();
    }

    private void HandleModeStarted(SpecialMode mode)
    {
        // Startet der zur eigenen Farbe passende Mode, bleibt der Bonus für dessen Dauer aktiv —
        // Icon bleibt sichtbar, bis HandleModeEnded für denselben Mode feuert. Jeder ANDERE Mode
        // bedeutet, der Bonus ist an eine andere Farbe verschwendet worden → sofort ausblenden.
        if (mode == PhaseManager.SpecialModeForColor(myColor)) return;
        Hide();
    }

    private void HandleModeEnded(SpecialMode mode)
    {
        if (mode != PhaseManager.SpecialModeForColor(myColor)) return;
        Hide();
    }

    private void Show()
    {
        pulse?.StopPulsing();
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(Co_PopInThenPulse());
    }

    private void Hide()
    {
        if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
        pulse?.StopPulsing();
        gameObject.SetActive(false);
    }

    private IEnumerator Co_PopInThenPulse()
    {
        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popInDuration);
            float s = k < 0.7f
                ? Mathf.Lerp(0f, popInOvershoot, k / 0.7f)
                : Mathf.Lerp(popInOvershoot, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = targetScale * s;
            yield return null;
        }
        transform.localScale = targetScale;

        // Explizit statt uns auf PointPulse.Awake() zu verlassen: dessen Awake lief evtl. noch nie
        // (Objekt startet deaktiviert, s. Awake() oben), würde also JETZT erst beim ersten
        // SetActive(true) nachgeholt — und hätte dann fälschlich localScale=0 als baseScale erfasst.
        pulse?.SetBaseScale(targetScale);
        pulse?.StartPulsing();
        activeRoutine = null;
    }
}
