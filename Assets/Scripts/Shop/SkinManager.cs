using UnityEngine;

public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    public SkinTheme ActiveTheme { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        RestoreEquipped();
    }

    public void Apply(SkinTheme theme)
    {
        ActiveTheme = theme;
    }

    void RestoreEquipped()
    {
        var catalogue = Resources.Load<ShopCatalogue>("ShopCatalogue");
        if (catalogue == null) return;

        string id   = ShopInventory.GetEquipped(ShopItemType.Skin);
        var    item = catalogue.FindById(id);

        if (item == null)
        {
            item = catalogue.FindDefault(ShopItemType.Skin);
            if (item != null)
            {
                ShopInventory.ClaimFree(item);
                ShopInventory.SetEquipped(ShopItemType.Skin, item.itemId);
            }
        }

        if (item?.skinTheme != null) Apply(item.skinTheme);
    }
}
