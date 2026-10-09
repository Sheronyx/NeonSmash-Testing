using UnityEngine;

// Sitzt irgendwo auf einer Pink-Fee-Instanz (Home Menu oder Gameplay) und
// blendet das Accessoire-Prefab jedes aktuell equippten Accessoire-Items ein/aus (siehe
// ShopItem.accessoryPrefab, ShopInventory.IsAccessoryEquipped -- bewusst ein eigener Equip-Slot,
// getrennt vom Skin-Slot, siehe Kommentar dort). Reagiert live auf ShopInventory.OnEquippedChanged,
// damit ein Kauf/Equip im Shop sich sofort auf bereits in der Szene liegende Fee-Instanzen auswirkt,
// ohne dass die Szene neu geladen werden muss.
public class FairyAccessorySlot : MonoBehaviour
{
    // Benannter Socket am Kopfknochen. Das Accessoire-Prefab traegt seine Passlage
    // relativ zu diesem Punkt in sich, deshalb wird es unveraendert angehaengt.
    public const string HeadSocketName = "AccessoryAnchor_Head";

    [SerializeField] ShopCatalogue catalogue;

    GameObject _currentInstance;
    string     _currentItemId;
    Transform  _socket;
    bool       _socketGesucht;

    void OnEnable()
    {
        ShopInventory.OnEquippedChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        ShopInventory.OnEquippedChanged -= Refresh;
    }

    void Refresh()
    {
        string equippedId = null;
        if (catalogue != null)
        {
            foreach (var it in catalogue.allItems)
            {
                if (it != null && it.accessoryPrefab != null && ShopInventory.IsAccessoryEquipped(it.itemId))
                {
                    equippedId = it.itemId;
                    break;
                }
            }
        }

        if (equippedId == _currentItemId) return;
        _currentItemId = equippedId;

        if (_currentInstance != null)
        {
            Destroy(_currentInstance);
            _currentInstance = null;
        }

        if (catalogue == null || string.IsNullOrEmpty(equippedId)) return;

        var item = System.Array.Find(catalogue.allItems, i => i != null && i.itemId == equippedId);
        if (item == null || item.accessoryPrefab == null) return;

        var ziel = Socket();
        if (ziel == null)
        {
            Debug.LogWarning($"[FairyAccessorySlot] Kein '{HeadSocketName}' unter '{name}' gefunden - "
                           + "Accessoire wird nicht angezeigt.", this);
            return;
        }

        _currentInstance = Instantiate(item.accessoryPrefab, ziel);
    }

    // Sucht den Socket einmalig. Bewusst ueber den Namen statt ueber das Objekt, auf dem
    // diese Komponente zufaellig sitzt: beim Austausch der Fee ist genau das schon einmal
    // stillschweigend auseinandergelaufen.
    Transform Socket()
    {
        if (_socketGesucht && _socket != null) return _socket;
        _socketGesucht = true;

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == HeadSocketName) { _socket = t; return _socket; }
        }

        var wurzel = transform.root;
        foreach (var t in wurzel.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == HeadSocketName) { _socket = t; return _socket; }
        }
        return null;
    }
}
