using UnityEngine;

[CreateAssetMenu(fileName = "ShopCatalogue", menuName = "NeonSmash/Shop Catalogue")]
public class ShopCatalogue : ScriptableObject
{
    public ShopItem[] allItems;

    public ShopItem FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var item in allItems)
            if (item != null && item.itemId == id) return item;
        return null;
    }

    public ShopItem FindDefault(ShopItemType type)
    {
        foreach (var item in allItems)
            if (item != null && item.type == type && item.isDefault) return item;
        return null;
    }
}
