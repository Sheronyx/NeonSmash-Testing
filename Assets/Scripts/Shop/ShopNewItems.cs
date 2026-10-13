using System.Collections.Generic;
using UnityEngine;

// Merkt sich pro Shop-Tab, welche Item-IDs der Spieler dort schon gesehen hat. Ein Item gilt
// als neu, solange seine ID nicht in der Liste des eigenen Tabs steht -- es braucht also keinen
// Haken am ShopItem, den man nach jedem Update wieder entfernen muesste.
public static class ShopNewItems
{
    const string Prefix = "shop_gesehen_";

    static string Key(ShopItemType tab) => Prefix + tab;

    static HashSet<string> Gesehen(ShopItemType tab)
    {
        var menge = new HashSet<string>();
        string roh = PlayerPrefs.GetString(Key(tab), "");
        if (string.IsNullOrEmpty(roh)) return menge;
        foreach (var id in roh.Split('|'))
            if (!string.IsNullOrEmpty(id)) menge.Add(id);
        return menge;
    }

    /// <summary>Gibt es in diesem Tab ein Item, das der Spieler dort noch nie gesehen hat?</summary>
    public static bool HasUnseen(ShopCatalogue katalog, ShopItemType tab)
    {
        if (katalog == null || katalog.allItems == null) return false;
        var gesehen = Gesehen(tab);
        foreach (var item in katalog.allItems)
            if (item != null && item.type == tab
                && !string.IsNullOrEmpty(item.itemId) && !gesehen.Contains(item.itemId))
                return true;
        return false;
    }

    /// <summary>Ist dieses eine Item in seinem Tab noch ungesehen?</summary>
    public static bool IsUnseen(ShopItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId)) return false;
        return !Gesehen(item.type).Contains(item.itemId);
    }

    /// <summary>Gibt es irgendwo im Shop etwas Ungesehenes?</summary>
    public static bool HasUnseen(ShopCatalogue katalog)
    {
        if (katalog == null || katalog.allItems == null) return false;
        // Nur die Tabs pruefen, die im Katalog ueberhaupt vorkommen.
        var tabs = new HashSet<ShopItemType>();
        foreach (var item in katalog.allItems)
            if (item != null) tabs.Add(item.type);
        foreach (var tab in tabs)
            if (HasUnseen(katalog, tab)) return true;
        return false;
    }

    /// <summary>Beim Oeffnen eines Tabs: alles darin gilt ab jetzt als gesehen.</summary>
    public static void MarkTabSeen(ShopCatalogue katalog, ShopItemType tab)
    {
        if (katalog == null || katalog.allItems == null) return;

        var gesehen = Gesehen(tab);
        bool neu = false;
        foreach (var item in katalog.allItems)
            if (item != null && item.type == tab && !string.IsNullOrEmpty(item.itemId))
                neu |= gesehen.Add(item.itemId);

        if (!neu) return;   // nichts dazugekommen -> kein Schreibvorgang
        PlayerPrefs.SetString(Key(tab), string.Join("|", gesehen));
        PlayerPrefs.Save();
    }

    /// <summary>Zum Testen: alles wieder auf ungesehen.</summary>
    public static void ResetAll()
    {
        foreach (ShopItemType tab in System.Enum.GetValues(typeof(ShopItemType)))
            PlayerPrefs.DeleteKey(Key(tab));
        PlayerPrefs.Save();
    }
}
