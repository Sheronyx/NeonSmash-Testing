using UnityEngine;

[CreateAssetMenu(fileName = "ShopCatalogue", menuName = "NeonSmash/Shop Catalogue")]
public class ShopCatalogue : ScriptableObject
{
    public ShopItem[] allItems;

    [Header("Defaults (beim ersten Start automatisch equipped)")]
    public ShopItem defaultSkin;
    public ShopItem defaultSound;

    public ShopItem FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var item in allItems)
            if (item != null && item.itemId == id) return item;
        return null;
    }
}
