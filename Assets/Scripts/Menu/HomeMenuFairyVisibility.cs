using UnityEngine;

// Blendet die 3 Feen-Charaktere im Startmenü aus, solange irgendeins der beobachteten Fenster
// (Shop, Freunde, Reward/Character-Progress, Sticker-Detail, Impressum, Cookie-Consent, ...) offen ist
// -- die Feen sitzen bei Z=-5 relativ zur Kamera und würden sonst über den (weiter entfernten, aber
// eigentlich vorne liegen sollenden) Fenster-Canvases rendern. Bewusst per Sichtbarkeits-Umschalten statt
// über Canvas-Tiefe/Plane-Distance gelöst, weil einzelne Fenster (z.B. Canvas Reward Window) beim direkten
// Ändern ihrer Plane Distance kaputtgehen.
//
// NUR im Startmenü (MainMenuScene) aktiv -- betrifft nicht die Gameplay-Feen in den Game-Szenen.
public class HomeMenuFairyVisibility : MonoBehaviour
{
    [Tooltip("Die sichtbaren Renderer der 3 Feen-Charaktere im Startmenü (z.B. die SkinnedMeshRenderer " +
             "von FairyIceV3_Character/FairyCrystal_Character/FairyForest2_Character). Renderer statt " +
             "GameObjects, damit es unabhängig von der genauen Eltern-Kind-Struktur funktioniert.")]
    [SerializeField] private Renderer[] fairyRenderers;

    [Tooltip("Alle Fenster/Popups, bei deren Offen-Sein die Feen ausgeblendet werden sollen (Root-" +
             "GameObject des jeweiligen Canvas bzw. Popups).")]
    [SerializeField] private GameObject[] watchedWindows;

    private bool _fairiesHidden;

    private void Update()
    {
        bool anyWindowOpen = false;
        foreach (var w in watchedWindows)
        {
            if (w != null && w.activeInHierarchy) { anyWindowOpen = true; break; }
        }

        if (anyWindowOpen == _fairiesHidden) return; // kein Wechsel nötig
        _fairiesHidden = anyWindowOpen;

        foreach (var r in fairyRenderers)
            if (r != null) r.enabled = !anyWindowOpen;
    }
}
