/// <summary>
/// Zentrale Schalter fuer Inhalte, die vorerst nicht im Spiel auftauchen sollen.
///
/// Bewusst nur ausgeblendet statt geloescht: die Daten (ShopItems, RewardDefinitions, Sticker-
/// Katalog, Welten) bleiben vollstaendig erhalten, und ein Schalter auf true holt sie zurueck.
/// Wer hier etwas einschaltet, muss die zugehoerigen Tabs in den Szenen wieder aktivieren --
/// die Sichtbarkeit der Knoepfe haengt an denselben Schaltern (siehe ShopController und
/// RewardWindowController), die Objekte selbst bleiben in der Szene liegen.
/// </summary>
public static class InhaltsSchalter
{
    // Stand 16.10.2026: Sticker und weitere Welten kommen nicht in die erste Fassung.
    public static readonly bool StickerAktiv = false;
    public static readonly bool WeltenAktiv  = false;

    // Shop-Tabs, die vorerst niemand sehen soll.
    public static readonly bool BundlesAktiv = false;
    public static readonly bool SoundsAktiv  = false;

    /// <summary>Darf diese Belohnung vergeben und angezeigt werden?</summary>
    public static bool Sichtbar(RewardDefinition reward)
    {
        if (reward == null) return false;
        switch (reward.kind)
        {
            case RewardKind.Sticker:     return StickerAktiv;
            case RewardKind.WorldUnlock: return WeltenAktiv;
            default:                     return true;
        }
    }

    /// <summary>Darf dieser Shop-Bereich angezeigt werden?</summary>
    public static bool Sichtbar(ShopItemType typ)
    {
        switch (typ)
        {
            case ShopItemType.Sticker: return StickerAktiv;
            case ShopItemType.Bundle:  return BundlesAktiv;
            case ShopItemType.Sound:   return SoundsAktiv;
            default:                   return true;
        }
    }

    /// <summary>Darf dieser Shop-Artikel angezeigt werden?</summary>
    public static bool Sichtbar(ShopItem item) => item != null && Sichtbar(item.type);
}
