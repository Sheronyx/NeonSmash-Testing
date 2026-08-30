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

    // Feuert bei jedem erfolgreichen einmaligen Kauf (TryPurchase, z.B. Welt-Bundle) — unabhängig
    // davon, ob danach auch equippt wird. Für UI, die auf den Owned-Zustand reagieren muss, ohne
    // dass gleichzeitig OnEquippedChanged feuert (z.B. MenuPortalSwitcher: Schloss-Overlay soll
    // sofort verschwinden, sobald eine Welt gekauft wurde, auch ohne aktivem Equip-Wechsel).
    public static event System.Action<string> OnItemPurchased;

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

        // Welt-Bundles mit diamondsPrice > 0 werden in Diamonds (Traumkristalle) bezahlt, nicht in
        // Dream Energy — siehe World-Locking (WorldUnlockManager: Freispiele) für den Kontext.
        if (item.diamondsPrice > 0)
        {
            if (!DiamondManager.TrySpendDiamonds(item.diamondsPrice)) return false;
        }
        else if (item.dreamEnergyPrice > 0 && !DreamEnergyManager.TrySpendDreamEnergy(item.dreamEnergyPrice))
        {
            return false;
        }

        Owned.Add(item.itemId);
        Save();
        OnItemPurchased?.Invoke(item.itemId);
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

    // Booster-Pakete sind wie Currency-Items ein wiederholbarer Kauf ohne Owned-Tracking — bezahlt
    // wird in Diamond Splinters, gutgeschrieben wird direkt ins verbrauchbare Booster-Inventar
    // (siehe BoosterInventoryManager), nicht in eine der beiden Kristall-Währungen.
    public static bool TryPurchaseBoosterPack(ShopItem item)
    {
        if (item.boostDefinition == null || item.diamondSplinterPrice <= 0) return false;
        if (!DiamondSplinterManager.TrySpendSplinters(item.diamondSplinterPrice)) return false;

        BoosterInventoryManager.Add(item.boostDefinition.type.ToString(), Mathf.Max(1, item.packAmount));
        return true;
    }

    // Sticker-Kauf: immer ein zufälliger Sticker (gewichtet nach Seltenheit, siehe
    // StickerManager.GrantRandom) gegen Diamond Splinters — kein Owned-Tracking, wiederholbar wie
    // Booster-Pakete/Currency-Tausch. Gibt den gewonnenen Sticker zurück (oder null bei Fehlschlag).
    public static RewardDefinition TryPurchaseSticker(RewardCatalogue catalogue, int price)
    {
        if (catalogue == null || price <= 0) return null;
        if (!DiamondSplinterManager.TrySpendSplinters(price)) return null;
        return StickerManager.GrantRandom(catalogue);
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
