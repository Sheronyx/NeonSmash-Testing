using UnityEngine;

public enum ShopItemType { Skin, Sound, Currency }

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
    [Tooltip("Leer lassen — Platzhalter für spätere IAP-Integration")]
    public string       iapProductId;

    [Header("Gameplay Assets")]
    public SkinTheme  skinTheme;   // nur für type == Skin
    public SoundTheme soundTheme;  // nur für type == Sound
}
