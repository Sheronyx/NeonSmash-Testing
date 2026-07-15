using UnityEngine;

// Aktiviert eine Aura-Partikel-Kind-Objekt (z.B. "Star aura") auf der Fairy, sobald diese Farbe
// einen Diamant-Bonus trägt (PhaseManager.OnDiamondBonusEarned), und deaktiviert sie wieder, sobald
// der eigene Special Mode dieser Farbe endet (SpecialModeManager.OnModeEnded) — der Bonus bleibt
// bestehen, auch wenn zwischendurch ANDERE Special Modes starten, genau wie bei DiamondBonusIndicatorUI.
public class FairyBonusAura : MonoBehaviour
{
    [Tooltip("Welche Farbe diese Fairy repräsentiert.")]
    [SerializeField] private PointColor myColor;
    [Tooltip("Die Aura-Partikel (z.B. \"Star aura\"-Kind-Objekt), die bei aktivem Bonus läuft.")]
    [SerializeField] private GameObject auraObject;

    private void Awake()
    {
        PhaseManager.OnDiamondBonusEarned += HandleBonusEarned;
        SpecialModeManager.OnModeEnded += HandleModeEnded;
        if (auraObject != null) auraObject.SetActive(false);
    }

    private void OnDestroy()
    {
        PhaseManager.OnDiamondBonusEarned -= HandleBonusEarned;
        SpecialModeManager.OnModeEnded -= HandleModeEnded;
    }

    private void HandleBonusEarned(PointColor color)
    {
        if (color != myColor) return;
        if (auraObject != null) auraObject.SetActive(true);
    }

    private void HandleModeEnded(SpecialMode mode)
    {
        if (mode != PhaseManager.SpecialModeForColor(myColor)) return;
        if (auraObject != null) auraObject.SetActive(false);
    }
}
