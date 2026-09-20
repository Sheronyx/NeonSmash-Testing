using UnityEngine;

// Sitzt auf dem "Armature_Crystal"-Objekt einer Pink-Fee-Instanz (Home Menu oder Gameplay) und
// blendet das Accessoire-Prefab jedes aktuell equippten Accessoire-Items ein/aus (siehe
// ShopItem.accessoryPrefab, ShopInventory.IsAccessoryEquipped -- bewusst ein eigener Equip-Slot,
// getrennt vom Skin-Slot, siehe Kommentar dort). Reagiert live auf ShopInventory.OnEquippedChanged,
// damit ein Kauf/Equip im Shop sich sofort auf bereits in der Szene liegende Fee-Instanzen auswirkt,
// ohne dass die Szene neu geladen werden muss.
public class FairyAccessorySlot : MonoBehaviour
{
    [SerializeField] ShopCatalogue catalogue;

    GameObject _currentInstance;
    string     _currentItemId;

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

        _currentInstance = Instantiate(item.accessoryPrefab, transform);
    }
}
