using UnityEngine;
using UnityEngine.Serialization;

public enum ShopItemType { Skin, Sound, Currency, Bundle }

// Welche Währung ein Currency-Item gutschreibt — steuert außerdem, in welcher Box (siehe
// ShopController.PopulateCurrencyGrid) die Karte im Shop einsortiert wird. Dream Energy gibt
// es hier bewusst nicht: die kleinste Währung wird nur durchs Spielen verdient (siehe
// DreamEnergyManager), nie gekauft/gutgeschrieben.
public enum CurrencyRewardKind { Diamonds, DiamondSplinters }

[CreateAssetMenu(fileName = "ShopItem", menuName = "NeonSmash/Shop Item")]
public class ShopItem : ScriptableObject
{
    public string       itemId;
    public string       displayName;
    public ShopItemType type;
    public Sprite       thumbnail;
    [FormerlySerializedAs("coinPrice")]
    [Tooltip("Preis in Dream Energy (kleinste, nur durchs Spielen verdiente Währung). 0 = kostenlos.")]
    public int          dreamEnergyPrice;
    public bool         isDaily;       // täglich kostenlos abrufbar
    public bool         isFeatured;    // im Daily-Banner oben anzeigen
    public bool         isDefault;     // beim ersten Start automatisch equipped
    [Tooltip("Store-Produkt-ID (Google Play / App Store). Leer = kein IAP.")]
    public string       iapProductId;
    [Tooltip("Welche Währung bei type == Currency gutgeschrieben wird (Diamonds oder Diamond Splinters).")]
    public CurrencyRewardKind currencyKind = CurrencyRewardKind.Diamonds;
    [FormerlySerializedAs("coinReward")]
    [Tooltip("Betrag der oben gewählten Währung, die bei Kauf gutgeschrieben wird (nur bei type == Currency) — " +
             "egal ob per IAP (echtes Geld) oder per dreamEnergyPrice (Dream-Energy-Tausch) bezahlt.")]
    public int          rewardAmount;

    [Header("Gameplay Assets")]
    public SkinTheme  skinTheme;   // nur für type == Skin
    public SoundTheme soundTheme;  // nur für type == Sound
}
