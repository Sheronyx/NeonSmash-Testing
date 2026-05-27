using System.Collections.Generic;
using UnityEngine;

public static class ShopInventory
{
    const string PrefKey = "shop_owned";

    static HashSet<string> _owned;

    static HashSet<string> Owned
    {
        get
        {
            if (_owned != null) return _owned;
            _owned = new HashSet<string>();
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var id in raw.Split(','))
                    if (!string.IsNullOrEmpty(id)) _owned.Add(id);
            return _owned;
        }
    }

    public static bool IsOwned(string itemId) => Owned.Contains(itemId);

    public static bool TryPurchase(ShopItem item)
    {
        if (IsOwned(item.itemId)) return false;
        if (item.coinPrice > 0 && !CoinManager.TrySpendCoins(item.coinPrice)) return false;

        Owned.Add(item.itemId);
        Save();
        return true;
    }

    public static void ClaimFree(ShopItem item)
    {
        if (item == null || IsOwned(item.itemId)) return;
        Owned.Add(item.itemId);
        Save();
    }

    static void Save()
    {
        PlayerPrefs.SetString(PrefKey, string.Join(",", Owned));
        PlayerPrefs.Save();
    }
}
