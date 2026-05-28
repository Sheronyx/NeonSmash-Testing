using UnityEngine;

public class SoundThemeManager : MonoBehaviour
{
    public static SoundThemeManager Instance { get; private set; }

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

    public void Apply(SoundTheme theme)
    {
        if (theme == null) return;
        MusicManager.Instance?.ApplyMenuClip(theme.menuMusicClip);
        MusicManager.Instance?.ApplyGameClip(theme.gameMusicClip);
    }

    void RestoreEquipped()
    {
        var catalogue = Resources.Load<ShopCatalogue>("ShopCatalogue");
        if (catalogue == null) return;

        string id   = ShopInventory.GetEquipped(ShopItemType.Sound);
        var    item = catalogue.FindById(id);

        if (item == null && catalogue.defaultSound != null)
        {
            item = catalogue.defaultSound;
            ShopInventory.ClaimFree(item);
            ShopInventory.SetEquipped(ShopItemType.Sound, item.itemId);
        }

        if (item?.soundTheme != null) Apply(item.soundTheme);
    }
}
