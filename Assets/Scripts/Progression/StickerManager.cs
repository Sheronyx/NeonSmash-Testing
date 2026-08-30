using UnityEngine;

// Besitz- und Auswahl-Tracking für Sticker (Münzen mit Stickerbild, vier Seltenheiten — siehe
// StickerRarity). Sticker sind IMMER zufällig vergeben (Kauf im Shop oder Stufen-Belohnung) — nie
// ein fixer, vom Design gewählter Sticker — deshalb hier Stückzahl statt reinem Owned-Set, damit
// Duplikate gezählt und später verkauft werden können (siehe TrySellDuplicates).
public static class StickerManager
{
    const string CountPrefPrefix = "sticker_count_";
    const string SelectedPrefKey = "stickers_selected";

    // Seltenheits-Gewichtung für die Zufallsvergabe (siehe GrantRandom) und Verkaufspreis pro
    // 3er-Satz identischer Sticker (siehe TrySellDuplicates).
    const float CommonWeight    = 0.70f;
    const float RareWeight      = 0.20f;
    const float EpicWeight      = 0.08f;
    const float LegendaryWeight = 0.02f;
    const int   DuplicatesPerSale = 3;

    public static event System.Action<string, int> OnCountChanged; // (rewardId, newCount)
    public static event System.Action<string> OnSelectedStickerChanged;

    // Feuert bei JEDER Zufallsvergabe (Shop-Kauf ODER Level-Belohnung, siehe GrantRandom) — für die
    // Reveal-Anzeige (StickerDetailPopupController), damit der Spieler immer sieht, welchen Sticker
    // er bekommen hat, unabhängig vom Vergabeweg.
    public static event System.Action<RewardDefinition> OnStickerGranted;

    public static int GetCount(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId)) return 0;
        return PlayerPrefs.GetInt(CountPrefPrefix + rewardId, 0);
    }

    public static bool IsOwned(string rewardId) => GetCount(rewardId) > 0;

    /// <summary>Aktuell für die Anzeige ausgewählter Sticker (rewardId der RewardDefinition),
    /// oder leer, wenn noch keiner ausgewählt/besessen ist.</summary>
    public static string SelectedStickerId => PlayerPrefs.GetString(SelectedPrefKey, "");

    public static void SelectSticker(string rewardId)
    {
        if (!string.IsNullOrEmpty(rewardId) && !IsOwned(rewardId)) return;
        PlayerPrefs.SetString(SelectedPrefKey, rewardId ?? "");
        PlayerPrefs.Save();
        OnSelectedStickerChanged?.Invoke(rewardId);
    }

    static void Add(string rewardId, int amount = 1)
    {
        if (string.IsNullOrEmpty(rewardId) || amount <= 0) return;
        bool wasUnowned = !IsOwned(rewardId);
        int newCount = GetCount(rewardId) + amount;
        PlayerPrefs.SetInt(CountPrefPrefix + rewardId, newCount);
        PlayerPrefs.Save();
        OnCountChanged?.Invoke(rewardId, newCount);

        // Erster besessener Sticker wird automatisch ausgewählt, damit im Hauptmenü nicht
        // dauerhaft das alte Platzhalter-Porträt steht, bis der Spieler manuell ins Album geht.
        if (wasUnowned && string.IsNullOrEmpty(SelectedStickerId))
            SelectSticker(rewardId);
    }

    /// <summary>Verbraucht `amount` Stück (z.B. beim Verkaufen). Gibt false zurück, wenn nicht
    /// genug vorhanden sind.</summary>
    static bool TryConsume(string rewardId, int amount)
    {
        int count = GetCount(rewardId);
        if (count < amount) return false;
        int newCount = count - amount;
        PlayerPrefs.SetInt(CountPrefPrefix + rewardId, newCount);
        PlayerPrefs.Save();
        OnCountChanged?.Invoke(rewardId, newCount);
        return true;
    }

    /// <summary>Vergibt EINEN zufälligen Sticker aus dem Katalog, gewichtet nach Seltenheit
    /// (Common 70% / Rare 20% / Epic 8% / Legendary 2%). Seltenheiten ohne vorhandene Sticker-
    /// Assets werden übersprungen und die übrigen Gewichte neu normalisiert, damit das System auch
    /// funktioniert, solange z.B. noch keine Epic/Legendary-Assets existieren. Gibt den vergebenen
    /// Sticker zurück (oder null, wenn der Katalog leer ist).</summary>
    public static RewardDefinition GrantRandom(RewardCatalogue catalogue)
    {
        if (catalogue?.allStickers == null || catalogue.allStickers.Length == 0) return null;

        var byRarity = new System.Collections.Generic.Dictionary<StickerRarity, System.Collections.Generic.List<RewardDefinition>>();
        foreach (var s in catalogue.allStickers)
        {
            if (s == null) continue;
            if (!byRarity.TryGetValue(s.stickerRarity, out var list))
                byRarity[s.stickerRarity] = list = new System.Collections.Generic.List<RewardDefinition>();
            list.Add(s);
        }
        if (byRarity.Count == 0) return null;

        float Weight(StickerRarity r) => r switch
        {
            StickerRarity.Common    => CommonWeight,
            StickerRarity.Rare      => RareWeight,
            StickerRarity.Epic      => EpicWeight,
            StickerRarity.Legendary => LegendaryWeight,
            _                        => 0f,
        };

        float totalWeight = 0f;
        foreach (var kv in byRarity) totalWeight += Weight(kv.Key);

        System.Collections.Generic.List<RewardDefinition> pool;
        if (totalWeight <= 0f)
        {
            // Keiner der vorhandenen Sticker hat eine bekannte Seltenheit mit Gewicht > 0 — rein
            // uniform über alle verfügbaren Sticker verteilen, statt komplett zu versagen.
            pool = new System.Collections.Generic.List<RewardDefinition>(catalogue.allStickers);
        }
        else
        {
            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            pool = null;
            foreach (var kv in byRarity)
            {
                cumulative += Weight(kv.Key);
                if (roll <= cumulative) { pool = kv.Value; break; }
            }
            pool ??= byRarity[StickerRarity.Common] ?? new System.Collections.Generic.List<RewardDefinition>(catalogue.allStickers);
        }

        var chosen = pool[Random.Range(0, pool.Count)];
        Add(chosen.rewardId);
        OnStickerGranted?.Invoke(chosen);
        return chosen;
    }

    /// <summary>Splitter-Erlös für den Verkauf von 3 identischen Stickern dieser Seltenheit.</summary>
    public static int SellPriceFor(StickerRarity rarity) => rarity switch
    {
        StickerRarity.Common    => 20,
        StickerRarity.Rare      => 50,
        StickerRarity.Epic      => 100,
        StickerRarity.Legendary => 200,
        _                        => 0,
    };

    /// <summary>Verkauft 3 identische Sticker gegen Traumsplitter (siehe SellPriceFor). Gibt false
    /// zurück, wenn weniger als 3 Stück vorhanden sind.</summary>
    public static bool TrySellDuplicates(RewardDefinition sticker)
    {
        if (sticker == null || !TryConsume(sticker.rewardId, DuplicatesPerSale)) return false;
        DiamondSplinterManager.AddSplinters(SellPriceFor(sticker.stickerRarity));
        return true;
    }
}
