using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class ShopInventory
{
    const string PrefKey     = "shop_owned";
    const string EquipPrefix = "shop_equipped_";
    const string CloudKey    = "shop_owned";
    const string CloudDirtyPrefKey = "shop_owned_cloud_dirty";

    static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

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

    // Getrennt vom normalen Skin-Equip-Slot (SetEquipped/GetEquipped) -- Accessoires (z.B.
    // Sonnenbrille) werden NICHT ueber ShopItemType.Skin equippt, weil dieser Slot auch von
    // MenuPortalSwitcher als Welt-Auswahl gelesen wird. Ein Accessoire-Equip wuerde dort
    // faelschlich als ungueltige Welt interpretiert und die Startseite sperren.
    const string AccessoryPrefKey = "shop_accessory_equipped";

    public static bool IsAccessoryEquipped(string itemId) =>
        PlayerPrefs.GetString(AccessoryPrefKey, "") == itemId;

    public static void SetAccessoryEquipped(string itemId, bool on)
    {
        PlayerPrefs.SetString(AccessoryPrefKey, on ? itemId : "");
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
        _ = SaveToCloudAsync();
    }

    // ── Cloud Save ────────────────────────────────────────────────────────────
    // Besitz gehoert zum Konto, nicht zum Geraet: eine freigeschaltete Welt oder ein gekaufter
    // Skin muss eine Neuinstallation ueberleben. Muster wie DiamondManager, aber mit einem
    // wichtigen Unterschied beim Zusammenfuehren -- siehe LoadFromCloudAsync.

    /// <summary>Beim App-Start aufzurufen, nachdem die Anmeldung steht.</summary>
    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKey });

            if (result.TryGetValue(CloudKey, out var item))
            {
                var ausCloud = Zerlegen(item.Value.GetAs<string>());

                // VEREINIGUNG, nicht "groesserer Wert gewinnt": Besitz geht nie verloren. Was auf
                // einem Geraet gekauft wurde, gilt auch auf dem anderen -- und ein lokaler Kauf,
                // der noch nicht hochgeladen war, darf nicht von der Cloud ueberschrieben werden.
                int vorher = Owned.Count;
                Owned.UnionWith(ausCloud);

                if (Owned.Count != vorher)
                {
                    PlayerPrefs.SetString(PrefKey, string.Join(",", Owned));
                    PlayerPrefs.Save();
                    Debug.Log($"[ShopInventory] Aus der Cloud ergaenzt: {Owned.Count - vorher} Eintraege, jetzt {Owned.Count}.");
                    OnEquippedChanged?.Invoke();   // damit Shop und Portal den neuen Besitz zeigen
                }

                // Hatte die Cloud weniger als wir, den vereinigten Stand gleich zurueckschreiben.
                if (ausCloud.Count != Owned.Count) await SaveToCloudAsync();
            }
            else if (Owned.Count > 0)
            {
                // Noch nichts in der Cloud (z. B. erster Start nach dem Update) -- lokalen Stand sichern.
                await SaveToCloudAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ShopInventory] Cloud Load fehlgeschlagen: " + e.Message);
        }

        await RetryPendingCloudSaveIfNeeded();
    }

    static HashSet<string> Zerlegen(string roh)
    {
        var menge = new HashSet<string>();
        if (string.IsNullOrEmpty(roh)) return menge;
        foreach (var id in roh.Split(','))
            if (!string.IsNullOrEmpty(id)) menge.Add(id);
        return menge;
    }

    static async Task SaveToCloudAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            string wert = string.Join(",", Owned);
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, wert } });
            Debug.Log($"[ShopInventory] Cloud gespeichert: {Owned.Count} Eintraege.");
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 0);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ShopInventory] Cloud Save fehlgeschlagen: " + e.Message);
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 1);
            PlayerPrefs.Save();
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public static async Task RetryPendingCloudSaveIfNeeded()
    {
        if (PlayerPrefs.GetInt(CloudDirtyPrefKey, 0) == 0) return;
        Debug.Log("[ShopInventory] Offener Cloud-Save aus vorherigem Fehlversuch wird nachgeholt.");
        await SaveToCloudAsync();
    }
}
