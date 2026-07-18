using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Lässt das Hauptmenü-Portal horizontal durchwischen, um durch alle Skins zu blättern.
// Bereits besessene Skins werden beim Wischen sofort aktiv equipped (SkinManager + ShopInventory).
// Noch nicht gekaufte Skins werden nur als Vorschau gezeigt ("zum Anschauen") — das Portal
// wechselt visuell mit, ohne den Skin tatsächlich auszurüsten.
//
// Portal-Objekte pro Skin liegen bereits fertig in der Szene (analog zu SkinObjectSwap) und
// werden hier nur ein-/ausgeblendet, kein Instantiate nötig. Beim Wischen läuft zusätzlich ein
// Slide+Scale-Übergang, an die Wisch-Richtung gekoppelt (altes Portal raus, neues rein).
public class MenuPortalSwitcher : MonoBehaviour
{
    [System.Serializable]
    public struct PortalEntry
    {
        public SkinTheme  theme;
        public GameObject portalObject;
    }

    [Tooltip("Pro Skin: Theme + zugehöriges, bereits in der Szene platziertes Portal-Objekt.")]
    [SerializeField] private PortalEntry[] portalEntries;

    [Tooltip("Zusätzliches Objekt pro Skin (z.B. ein weiteres Partikelsystem), das synchron mit dem " +
             "Portal umgeschaltet wird — ohne Slide-Animation, einfach ein-/ausgeblendet.")]
    [SerializeField] private PortalEntry[] secondaryEffectEntries;

    [Header("Wisch-Erkennung")]
    [Tooltip("Ab welcher Distanz (Pixel) ein Wisch als Skin-Wechsel zählt.")]
    [SerializeField] private float swipeThresholdPixels = 100f;
    [Tooltip("Nur Wische zählen, die vertikal in der Nähe dieses Punkts starten (leer = dieses Objekt " +
             "selbst) — horizontal ist die gesamte Bildschirmbreite erlaubt (links/rechts vom Portal " +
             "geht also auch), nur oben/unten (Buttons/Shop/Freunde etc.) soll nicht mit auslösen.")]
    [SerializeField] private Transform touchZoneAnchor;
    [Tooltip("Erlaubte vertikale Distanz (Pixel) vom Touch Zone Anchor, in der ein Wisch noch zählt.")]
    [SerializeField] private float touchZoneRadius = 320f;
    [Tooltip("Nach dem letzten Skin wieder beim ersten weitermachen (und umgekehrt).")]
    [SerializeField] private bool wrapAround = true;

    [Header("Übergangs-Animation (beim Wischen)")]
    [SerializeField] private float transitionDuration = 0.35f;
    [Tooltip("Wie weit (Weltraum-Einheiten) altes/neues Portal seitlich rausgleiten/reinkommen.")]
    [SerializeField] private float slideDistance = 6f;
    [Tooltip("Überschwingen beim Reinwachsen des neuen Portals (Ease-Out-Back, 0 = kein Überschwingen).")]
    [SerializeField] private float popOvershoot = 1.7f;

    private ShopCatalogue _catalogue;
    private ShopItem[]    _skinItems;
    private int            _index;
    private bool           _dragging;
    private float          _dragStartX;
    private Coroutine      _transitionRoutine;
    private bool           _suppressExternalSync;

    // Ursprüngliche (im Editor eingestellte) Position/Skalierung pro Portal-Objekt — jedes Portal
    // darf individuell positioniert/skaliert sein, die Animation lerpt relativ dazu statt sie zu überschreiben.
    private readonly Dictionary<GameObject, (Vector3 pos, Vector3 scale)> _homeTransforms = new();

    private void OnEnable()  => ShopInventory.OnEquippedChanged += HandleEquippedChangedExternally;
    private void OnDisable() => ShopInventory.OnEquippedChanged -= HandleEquippedChangedExternally;

    private void Start()
    {
        _catalogue = Resources.Load<ShopCatalogue>("ShopCatalogue");
        if (_catalogue == null)
        {
            Debug.LogWarning("[MenuPortalSwitcher] ShopCatalogue nicht unter Resources/ShopCatalogue gefunden — Script deaktiviert.");
            enabled = false;
            return;
        }

        // Nur Bundle-Items: jeder Skin wird in diesem Shop als Bundle (Skin + Sound) verkauft/equippt
        // (siehe ShopController.OnEquipItem) — ein Portal pro Bundle, nicht pro einzelnem Skin-Item.
        var list = new List<ShopItem>();
        foreach (var item in _catalogue.allItems)
            if (item != null && item.type == ShopItemType.Bundle && item.skinTheme != null) list.Add(item);
        _skinItems = list.ToArray();

        if (_skinItems.Length == 0)
        {
            Debug.LogWarning("[MenuPortalSwitcher] Keine Bundle-Items mit Skin Theme in der ShopCatalogue gefunden.");
            enabled = false;
            return;
        }

        foreach (var entry in portalEntries)
            if (entry.portalObject != null && !_homeTransforms.ContainsKey(entry.portalObject))
                _homeTransforms[entry.portalObject] = (entry.portalObject.transform.localPosition, entry.portalObject.transform.localScale);

        SyncToEquippedState(forceShow: true);
    }

    // Liest den aktuell equippten Skin (egal ob per Shop oder per Portal-Swipe gesetzt) und
    // zeigt das passende Portal sofort (ohne Animation) — beim Start UND jedes Mal, wenn
    // ShopInventory.OnEquippedChanged von woanders (z.B. dem Shop) feuert.
    //
    // forceShow=false (Standard, für den externen Event-Handler): wenn der aufgelöste Index
    // bereits dem aktuell angezeigten entspricht, passiert NICHTS — das ist genau der Fall, wenn
    // die eigene Wisch-Animation gerade erst SetEquipped ausgelöst hat (das hier feuert dann als
    // Rückkopplung desselben Frames) und sie sonst sofort wieder abgebrochen/zurückgesetzt würde.
    // forceShow=true (nur beim Start): dort MUSS immer gezeigt werden, auch wenn Index 0 zufällig
    // schon dem Default-Wert von _index entspricht.
    private void SyncToEquippedState(bool forceShow = false)
    {
        // Über das Theme statt die Item-Id auflösen: ein equippter Bundle-Skin steht unter der
        // Bundle-Id im Skin-Slot, matcht also nicht zwangsläufig die Id des Eintrags, den wir
        // für dieses Theme in der Liste behalten haben.
        string equippedId = ShopInventory.GetEquipped(ShopItemType.Skin);
        SkinTheme equippedTheme = _catalogue.FindById(equippedId)?.skinTheme;
        int index = equippedTheme != null ? System.Array.FindIndex(_skinItems, i => i.skinTheme == equippedTheme) : -1;
        if (index < 0)
        {
            Debug.LogWarning($"[MenuPortalSwitcher] Equipped Skin-Id \"{equippedId}\" nicht auflösbar — falle auf Index 0 ({_skinItems[0].itemId}) zurück.");
            index = 0;
        }

        if (!forceShow && index == _index) return;

        _index = index;
        ShowPortal(_index);
    }

    // Equip-Änderung kann auch von außerhalb kommen (z.B. der Shop equippt ein Bundle) — dann
    // nur die Anzeige nachziehen, NICHT erneut equippen (sonst Endlosschleife/unnötige Writes).
    // Während der eigene Swipe gerade selbst equippt (_suppressExternalSync), wird das komplett
    // ignoriert — sonst würde der erste von drei SetEquipped-Aufrufen (Bundle vor Skin) hier kurz
    // noch den ALTEN Skin auslesen und die gerade gestartete Animation sofort wieder zurücksetzen.
    private void HandleEquippedChangedExternally()
    {
        if (_suppressExternalSync) return;
        if (_catalogue == null || _skinItems == null || _skinItems.Length == 0) return;
        SyncToEquippedState();
    }

    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            Vector2 screenPos = pointer.position.ReadValue();
            if (IsInsideTouchZone(screenPos))
            {
                _dragging   = true;
                _dragStartX = screenPos.x;
            }
        }
        else if (pointer.press.wasReleasedThisFrame && _dragging)
        {
            _dragging = false;
            float dx = pointer.position.ReadValue().x - _dragStartX;
            if (Mathf.Abs(dx) >= swipeThresholdPixels)
                Step(dx < 0f ? 1 : -1); // nach links wischen = nächster Skin, wie LeaderboardPager
        }
    }

    private bool IsInsideTouchZone(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return true;

        Transform anchor = touchZoneAnchor != null ? touchZoneAnchor : transform;
        Vector3 anchorScreen = cam.WorldToScreenPoint(anchor.position);
        // Nur vertikale Distanz prüfen — horizontal ist die volle Bildschirmbreite erlaubt, damit man
        // auch links/rechts neben dem Portal wischen kann, nicht nur direkt darauf.
        return Mathf.Abs(screenPos.y - anchorScreen.y) <= touchZoneRadius;
    }

    public void Next() => Step(1);
    public void Prev() => Step(-1);

    private void Step(int direction)
    {
        int newIndex = _index + direction;
        if (wrapAround)
            newIndex = (newIndex % _skinItems.Length + _skinItems.Length) % _skinItems.Length;
        else
            newIndex = Mathf.Clamp(newIndex, 0, _skinItems.Length - 1);

        SelectIndex(newIndex, direction);
    }

    private void SelectIndex(int index, int direction)
    {
        int previousIndex = _index;
        _index = index;

        GameObject fromPortal = GetPortalObject(previousIndex);
        GameObject toPortal   = GetPortalObject(index);

        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(Co_Transition(fromPortal, toPortal, direction));

        ShowSecondaryEffect(index);

        var item = _skinItems[_index];
        if (ShopInventory.IsOwned(item.itemId))
        {
            // Exakt dieselbe Bundle-Equip-Logik wie ShopController.OnEquipItem, damit Shop-Anzeige
            // (aktives Bundle-Badge) und Sound-Theme konsistent zum hier gewählten Skin bleiben.
            // _suppressExternalSync verhindert, dass die eigenen SetEquipped-Aufrufe über das
            // OnEquippedChanged-Event zurück in HandleEquippedChangedExternally laufen und die
            // gerade gestartete Animation sofort wieder abwürgen.
            _suppressExternalSync = true;
            ShopInventory.SetEquipped(ShopItemType.Bundle, item.itemId);
            ShopInventory.SetEquipped(ShopItemType.Skin,   item.itemId);
            ShopInventory.SetEquipped(ShopItemType.Sound,  item.itemId);
            _suppressExternalSync = false;

            SoundThemeManager.Instance?.Apply(item.soundTheme);
            if (SkinManager.Instance != null) SkinManager.Instance.Apply(item.skinTheme);
        }
        // Nicht besessene Skins: nur die Portal-Vorschau wechselt, es wird absichtlich NICHT
        // equipped/gespeichert — der Spieler "schaut sich das nur an".
    }

    private GameObject GetPortalObject(int index)
    {
        if (index < 0 || index >= _skinItems.Length) return null;
        SkinTheme theme = _skinItems[index].skinTheme;
        foreach (var entry in portalEntries)
            if (entry.theme == theme) return entry.portalObject;
        return null;
    }

    // direction > 0 (nach links gewischt, nächster Skin): altes Portal gleitet nach LINKS raus,
    // neues kommt von RECHTS rein — wie ein klassischer Karussell-Übergang in Wisch-Richtung.
    private IEnumerator Co_Transition(GameObject fromPortal, GameObject toPortal, int direction)
    {
        float exitSign  = direction > 0 ? -1f : 1f;
        float enterSign = -exitSign;

        (Vector3 pos, Vector3 scale) fromHome = default;
        (Vector3 pos, Vector3 scale) toHome   = default;
        bool hasFrom = fromPortal != null && _homeTransforms.TryGetValue(fromPortal, out fromHome);
        bool hasTo   = toPortal   != null && _homeTransforms.TryGetValue(toPortal,   out toHome);

        Vector3 toEnterPos = hasTo ? toHome.pos + Vector3.right * slideDistance * enterSign : Vector3.zero;
        if (hasTo)
        {
            toPortal.SetActive(true);
            toPortal.transform.localPosition = toEnterPos;
            toPortal.transform.localScale    = Vector3.zero;
        }

        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float k      = Mathf.Clamp01(t / transitionDuration);
            float eased  = Mathf.SmoothStep(0f, 1f, k);

            if (hasFrom)
            {
                fromPortal.transform.localPosition = fromHome.pos + Vector3.right * slideDistance * exitSign * eased;
                fromPortal.transform.localScale    = Vector3.LerpUnclamped(fromHome.scale, Vector3.zero, eased);
            }

            if (hasTo)
            {
                float popK = EaseOutBack(eased, popOvershoot);
                toPortal.transform.localPosition = Vector3.Lerp(toEnterPos, toHome.pos, eased);
                toPortal.transform.localScale    = Vector3.LerpUnclamped(Vector3.zero, toHome.scale, popK);
            }

            yield return null;
        }

        if (hasFrom)
        {
            fromPortal.transform.localPosition = fromHome.pos;
            fromPortal.transform.localScale    = fromHome.scale;
            fromPortal.SetActive(false);
        }
        if (hasTo)
        {
            toPortal.transform.localPosition = toHome.pos;
            toPortal.transform.localScale    = toHome.scale;
        }

        _transitionRoutine = null;
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }

    // Sofortiger, nicht-animierter Wechsel (Start + externe Shop-Equip-Syncs). Setzt außerdem JEDES
    // Portal-Objekt auf seine Home-Transform zurück — falls hier eine noch laufende Wisch-Animation
    // unterbrochen wurde, bleibt so nichts in einem halb-skalierten/verschobenen Zwischenzustand hängen.
    private void ShowPortal(int index)
    {
        if (_transitionRoutine != null) { StopCoroutine(_transitionRoutine); _transitionRoutine = null; }

        var item  = _skinItems[index];
        SkinTheme theme = item.skinTheme;

        if (theme == null)
        {
            Debug.LogWarning($"[MenuPortalSwitcher] ShopItem \"{item.itemId}\" hat kein Skin Theme zugewiesen — kein Portal wird angezeigt.");
            foreach (var entry in portalEntries)
                if (entry.portalObject != null) { ResetToHome(entry.portalObject); entry.portalObject.SetActive(false); }
            ShowSecondaryEffect(index);
            return;
        }

        bool matched = false;
        foreach (var entry in portalEntries)
        {
            bool match = entry.theme == theme;
            if (entry.portalObject != null)
            {
                ResetToHome(entry.portalObject);
                entry.portalObject.SetActive(match);
            }
            matched |= match;
        }

        if (!matched)
            Debug.LogWarning($"[MenuPortalSwitcher] Kein Portal Entry für Theme \"{theme.name}\" (Skin \"{item.itemId}\") gefunden — Portal Entries im Inspector prüfen.");

        ShowSecondaryEffect(index);
    }

    // Blendet pro Skin ein zusätzliches Objekt (z.B. weiteres Partikelsystem) synchron ein/aus —
    // ohne Slide-Animation, reines SetActive nach Theme-Treffer, analog zu ShowPortal.
    private void ShowSecondaryEffect(int index)
    {
        if (secondaryEffectEntries == null || secondaryEffectEntries.Length == 0) return;

        SkinTheme theme = _skinItems[index].skinTheme;
        foreach (var entry in secondaryEffectEntries)
            if (entry.portalObject != null) entry.portalObject.SetActive(entry.theme == theme);
    }

    private void ResetToHome(GameObject portal)
    {
        if (_homeTransforms.TryGetValue(portal, out var home))
        {
            portal.transform.localPosition = home.pos;
            portal.transform.localScale    = home.scale;
        }
    }
}
