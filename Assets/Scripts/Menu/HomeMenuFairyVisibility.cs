using System.Collections.Generic;
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

    [Tooltip("Weitere Renderer mit demselben Problem -- vor allem die Ambient-Glow-Partikel des " +
             "Startmenüs und der Portale. Sie liegen wie die Feen im Weltraum nahe der Kamera und " +
             "würden sonst durch das offene Fenster hindurchscheinen.")]
    [SerializeField] private Renderer[] additionalRenderers;

    [Tooltip("Eltern-Objekte der Feen (z.B. der 'Fairies'-Container). Alle Renderer darunter werden " +
             "mit ausgeblendet -- auch solche, die erst zur Laufzeit entstehen, wie das Accessoire " +
             "aus FairyAccessorySlot. Die feste Liste oben kann so etwas naturgemaess nicht erfassen.")]
    [SerializeField] private Transform[] fairyContainers;

    private bool _fairiesHidden;
    private bool[] _additionalWasEnabled;

    private readonly List<Renderer> _ausContainern = new List<Renderer>();
    private readonly List<bool>     _containerWarAn = new List<bool>();
    private readonly List<Renderer> _gefunden       = new List<Renderer>();

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

        // Renderer unterhalb der Container jedes Mal neu einsammeln: zur Laufzeit erzeugte
        // Accessoires (Sonnenbrille) gibt es beim Szenenstart noch gar nicht.
        if (anyWindowOpen)
        {
            _ausContainern.Clear();
            _containerWarAn.Clear();
            foreach (var c in fairyContainers)
            {
                if (c == null) continue;
                c.GetComponentsInChildren(true, _gefunden);
                foreach (var r in _gefunden)
                {
                    if (r == null || System.Array.IndexOf(fairyRenderers, r) >= 0) continue;
                    _ausContainern.Add(r);
                    _containerWarAn.Add(r.enabled);
                    r.enabled = false;
                }
            }
        }
        else
        {
            for (int i = 0; i < _ausContainern.Count && i < _containerWarAn.Count; i++)
                if (_ausContainern[i] != null) _ausContainern[i].enabled = _containerWarAn[i];
            _ausContainern.Clear();
            _containerWarAn.Clear();
        }

        // Anders als die Feen dürfen diese Renderer beim Schließen NICHT pauschal eingeschaltet
        // werden: Ein Teil davon ist bewusst deaktiviert (z.B. Hilfs-/Maskenobjekte der Portale und
        // die Effekte gerade nicht gewählter Portale). Deshalb den Ausgangszustand merken und exakt
        // wiederherstellen.
        if (anyWindowOpen)
        {
            if (_additionalWasEnabled == null || _additionalWasEnabled.Length != additionalRenderers.Length)
                _additionalWasEnabled = new bool[additionalRenderers.Length];

            for (int i = 0; i < additionalRenderers.Length; i++)
            {
                if (additionalRenderers[i] == null) continue;
                _additionalWasEnabled[i] = additionalRenderers[i].enabled;
                additionalRenderers[i].enabled = false;
            }
        }
        else if (_additionalWasEnabled != null)
        {
            for (int i = 0; i < additionalRenderers.Length && i < _additionalWasEnabled.Length; i++)
                if (additionalRenderers[i] != null) additionalRenderers[i].enabled = _additionalWasEnabled[i];
        }
    }
}
