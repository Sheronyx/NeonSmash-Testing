using UnityEngine;
using UnityEngine.VFX;

// Auf das Portal-GameObject legen (gleiches Objekt wie ArcanePortalFlash +
// VisualEffect).
//
// Schaltet je nach aktivem Skin zwischen dem VFX-Portal und einer bereits in
// der Szene platzierten Plattform um. Das Portal-GameObject selbst bleibt IMMER
// aktiv (Saug-Anker + ArcanePortalFlash) — nur das VFX-Visual wird ausgeblendet
// und die passende Plattform eingeschaltet.
//
// Setup: Plattform(en) als Kind des Portals platzieren, korrekt positionieren,
// standardmäßig deaktivieren und hier mit ihrem Theme eintragen.
[RequireComponent(typeof(ArcanePortalFlash))]
public class PortalSkinApplier : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public SkinTheme  theme;
        public GameObject platform;
    }

    [Tooltip("Das VFX-Portal-Visual. Leer = VisualEffect auf diesem Objekt.")]
    [SerializeField] private VisualEffect defaultVfx;

    [Tooltip("Pro Skin: Theme + zugehörige Plattform (Kind-Objekt, korrekt platziert).")]
    [SerializeField] private Entry[] platforms;

    void Start()
    {
        var active = SkinManager.Instance != null ? SkinManager.Instance.ActiveTheme : null;
        GameObject chosen = null;

        foreach (var e in platforms)
        {
            bool match = active != null && e.theme == active;
            if (e.platform != null) e.platform.SetActive(match);
            if (match) chosen = e.platform;
        }

        // VFX-Portal nur zeigen, wenn keine Plattform aktiv ist
        var vfx = defaultVfx != null ? defaultVfx : GetComponent<VisualEffect>();
        if (vfx != null) vfx.enabled = (chosen == null);
    }
}
