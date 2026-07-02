using UnityEngine;

public enum ShopItemType { Skin, Sound, Currency, Bundle }

[CreateAssetMenu(fileName = "ShopItem", menuName = "NeonSmash/Shop Item")]
public class ShopItem : ScriptableObject
{
    public string       itemId;
    public string       displayName;
    public ShopItemType type;
    public Sprite       thumbnail;
    public int          coinPrice;     // 0 = free
    public bool         isDaily;       // täglich kostenlos abrufbar
    public bool         isFeatured;    // im Daily-Banner oben anzeigen
    public bool         isDefault;     // beim ersten Start automatisch equipped
    [Tooltip("Store-Produkt-ID (Google Play / App Store). Leer = kein IAP.")]
    public string       iapProductId;
    [Tooltip("Coins die bei einem IAP-Kauf gutgeschrieben werden (nur bei type == Currency).")]
    public int          coinReward;

    [Header("Gameplay Assets")]
    public SkinTheme  skinTheme;   // nur für type == Skin
    public SoundTheme soundTheme;  // nur für type == Sound
}
