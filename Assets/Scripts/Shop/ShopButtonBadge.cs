using TMPro;
using UnityEngine;

// Das Ausrufezeichen am Shop-Knopf. Es zeigt sich, wenn einer dieser Faelle zutrifft:
//  - Im Shop liegt etwas Neues in einem Tab, den der Spieler seit dem Hinzukommen nicht geoeffnet hat.
//  - Das taegliche Gratis-Item ist noch nicht abgeholt.
// Refresh() wird vom ShopController gerufen (Start, Tabwechsel, nach dem Abholen).
public class ShopButtonBadge : MonoBehaviour
{
    [SerializeField] GameObject      badgeRoot;  // der rote Punkt / das Ausrufezeichen
    [SerializeField] TextMeshProUGUI badgeText;  // optional
    [Tooltip("Derselbe Katalog wie am ShopController -- daraus kommt, was es ueberhaupt gibt.")]
    [SerializeField] ShopCatalogue   catalogue;
    [Tooltip("Dasselbe Asset wie ShopController.dailyItem.")]
    [SerializeField] ShopItem        dailyItem;

    public void Refresh()
    {
        bool gratisOffen = dailyItem != null && !ShopInventory.IsOwned(dailyItem.itemId);
        bool etwasNeues  = ShopNewItems.HasUnseen(catalogue);

        if (badgeRoot != null) badgeRoot.SetActive(gratisOffen || etwasNeues);
        if (badgeText != null) badgeText.text = "1";
    }
}
