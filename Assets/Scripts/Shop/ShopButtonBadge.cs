using TMPro;
using UnityEngine;

public class ShopButtonBadge : MonoBehaviour
{
    [SerializeField] GameObject        badgeRoot;  // the red dot / circle
    [SerializeField] TextMeshProUGUI   badgeText;  // optional — shows "1"
    [SerializeField] ShopItem          dailyItem;  // same asset as ShopController.dailyItem

    public void Refresh()
    {
        bool show = dailyItem != null && !ShopInventory.IsOwned(dailyItem.itemId);
        if (badgeRoot != null) badgeRoot.SetActive(show);
        if (badgeText != null) badgeText.text = "1";
    }
}
