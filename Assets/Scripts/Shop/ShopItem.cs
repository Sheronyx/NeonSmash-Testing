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
    [Tooltip("Leer lassen — Platzhalter für spätere IAP-Integration")]
    public string       iapProductId;
}
