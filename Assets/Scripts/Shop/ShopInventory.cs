using System.Collections.Generic;
using UnityEngine;

public static class ShopInventory
{
    const string PrefKey     = "shop_owned";
    const string EquipPrefix = "shop_equipped_";

    // Feuert bei JEDEM SetEquipped-Aufruf (ein Bundle-Equip ruft es z.B. 3x auf, für
    // Bundle/Skin/Sound) — für UI, die unabhängig davon aktualisiert werden muss, WER den
    // Equip ausgelöst hat (z.B. Shop-Grid und Hauptmenü-Portal sollen sich gegenseitig
    // synchron halten, egal ob über den Shop oder das Portal-Swipe equippt wurde).
    public static event System.Action OnEquippedChanged;

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
        if (item.dreamEnergyPrice > 0 && !DreamEnergyManager.TrySpendDreamEnergy(item.dreamEnergyPrice)) return false;

        Owned.Add(item.itemId);
        Save();
        return true;
    }

    // Currency-Items (Diamonds/Diamond Splinters) sind ein wiederholbarer Tausch gegen Dream
    // Energy, kein einmaliger Unlock wie Skins/Sounds/Bundles — deshalb eigener Pfad OHNE
    // Owned-Tracking (sonst ließe sich das Item nur genau einmal kaufen).
    public static bool TryExchangeForCurrency(ShopItem item)
    {
        if (item.dreamEnergyPrice <= 0) return false;
        if (!DreamEnergyManager.TrySpendDreamEnergy(item.dreamEnergyPrice)) return false;

        switch (item.currencyKind)
        {
            case CurrencyRewardKind.Diamonds:         DiamondManager.AddDiamonds(item.rewardAmount); break;
            case CurrencyRewardKind.DiamondSplinters: DiamondSplinterManager.AddSplinters(item.rewardAmount); break;
        }
        return true;
    }

    public static string GetEquipped(ShopItemType type) =>
        PlayerPrefs.GetString(EquipPrefix + (int)type, "");

    public static void SetEquipped(ShopItemType type, string itemId)
    {
        PlayerPrefs.SetString(EquipPrefix + (int)type, itemId);
        PlayerPrefs.Save();
        OnEquippedChanged?.Invoke();
    }

    public static void ClaimFree(ShopItem item)
    {
        if (item == null || IsOwned(item.itemId)) return;
        Owned.Add(item.itemId);
        Save();
    }

    public static void DebugClearAll()
    {
        _owned = null;
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    static void Save()
    {
        PlayerPrefs.SetString(PrefKey, string.Join(",", Owned));
        PlayerPrefs.Save();
    }
}
