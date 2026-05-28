using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    public static ShopController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] CanvasGroup panel;

    [Header("Coin Display")]
    [SerializeField] TextMeshProUGUI coinBalanceLabel;

    [Header("Daily Banner")]
    [SerializeField] GameObject      dailyBannerRoot;
    [SerializeField] Image           dailyBannerThumbnail;
    [SerializeField] TextMeshProUGUI dailyBannerName;
    [SerializeField] Button          dailyClaimButton;
    [SerializeField] GameObject      dailyClaimedLabel;

    [Header("Tabs")]
    [SerializeField] Button tabSkins;
    [SerializeField] Button tabSounds;
    [SerializeField] Button tabCoins;

    [Header("Tab Colors")]
    [SerializeField] Color tabActiveColor   = Color.white;
    [SerializeField] Color tabInactiveColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Grid")]
    [SerializeField] Transform       gridParent;
    [SerializeField] ShopItemCardUI  itemCardPrefab;

    [Header("Items")]
    [SerializeField] ShopCatalogue catalogue;
    [SerializeField] ShopItem      dailyItem;

    [Header("Animation")]
    [SerializeField] float popInDuration  = 0.28f;
    [SerializeField] float popOutDuration = 0.2f;

    ShopItemType _activeTab = ShopItemType.Skin;
    bool         _open;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    [ContextMenu("DEBUG: Reset Shop Inventory")]
    void DebugResetInventory()
    {
        ShopInventory.DebugClearAll();
        if (_open) RefreshDailyBanner();
        Debug.Log("[Shop] Inventory cleared.");
    }

    void OnEnable()
    {
        CoinManager.OnCoinsChanged += RefreshCoinDisplay;
        if (tabSkins         != null) tabSkins.onClick.AddListener(() => SwitchTab(ShopItemType.Skin));
        if (tabSounds        != null) tabSounds.onClick.AddListener(() => SwitchTab(ShopItemType.Sound));
        if (tabCoins         != null) tabCoins.onClick.AddListener(() => SwitchTab(ShopItemType.Currency));
        if (dailyClaimButton != null) dailyClaimButton.onClick.AddListener(OnClaimDaily);
    }

    void OnDisable()
    {
        CoinManager.OnCoinsChanged -= RefreshCoinDisplay;
        if (tabSkins         != null) tabSkins.onClick.RemoveAllListeners();
        if (tabSounds        != null) tabSounds.onClick.RemoveAllListeners();
        if (tabCoins         != null) tabCoins.onClick.RemoveAllListeners();
        if (dailyClaimButton != null) dailyClaimButton.onClick.RemoveAllListeners();
    }

    public void Open()
    {
        if (_open) return;
        _open = true;
        RefreshCoinDisplay(CoinManager.Balance);
        RefreshDailyBanner();
        SwitchTab(ShopItemType.Skin);
        DimOverlay.Instance?.Show();
        StartCoroutine(Co_Open());
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        DimOverlay.Instance?.Hide();
        StartCoroutine(Co_Close());
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────

    void SwitchTab(ShopItemType tab)
    {
        _activeTab = tab;
        UpdateTabHighlights();
        PopulateGrid();
    }

    void UpdateTabHighlights()
    {
        SetTabColor(tabSkins,  _activeTab == ShopItemType.Skin);
        SetTabColor(tabSounds, _activeTab == ShopItemType.Sound);
        SetTabColor(tabCoins,  _activeTab == ShopItemType.Currency);
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = active ? tabActiveColor : tabInactiveColor;
    }

    // ── Grid ─────────────────────────────────────────────────────────────────

    void PopulateGrid()
    {
        if (gridParent == null || itemCardPrefab == null) return;

        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        if (catalogue == null) return;
        var items = System.Array.FindAll(catalogue.allItems,
            i => i != null && i.type == _activeTab);

        foreach (var item in items)
        {
            if (item == null) continue;
            var card = Instantiate(itemCardPrefab, gridParent);
            card.Bind(item, OnBuyItem, OnEquipItem);
        }
    }

    void OnEquipItem(ShopItem item)
    {
        ShopInventory.SetEquipped(item.type, item.itemId);

        if (item.type == ShopItemType.Skin)
            SkinManager.Instance?.Apply(item.skinTheme);
        else if (item.type == ShopItemType.Sound)
            SoundThemeManager.Instance?.Apply(item.soundTheme);

        PopulateGrid();
    }

    void OnBuyItem(ShopItem item)
    {
        if (item.type == ShopItemType.Currency)
        {
            Debug.Log($"[Shop] IAP '{item.displayName}' — noch nicht implementiert");
            return;
        }

        if (ShopInventory.TryPurchase(item))
        {
            RefreshCoinDisplay(CoinManager.Balance);
            PopulateGrid();
        }
    }

    // ── Daily Banner ─────────────────────────────────────────────────────────

    void RefreshDailyBanner()
    {
        if (dailyBannerRoot == null) return;
        if (dailyItem == null) { dailyBannerRoot.SetActive(false); return; }

        dailyBannerRoot.SetActive(true);
        bool claimed = ShopInventory.IsOwned(dailyItem.itemId);

        if (dailyBannerThumbnail != null) dailyBannerThumbnail.sprite = dailyItem.thumbnail;
        if (dailyBannerName      != null) dailyBannerName.text        = dailyItem.displayName;
        if (dailyClaimButton     != null) dailyClaimButton.gameObject.SetActive(!claimed);
        Debug.Log($"[Shop] claimed={claimed}  label-ref={dailyClaimedLabel != null}");
        if (dailyClaimedLabel    != null) dailyClaimedLabel.SetActive(claimed);
    }

    void OnClaimDaily()
    {
        if (dailyItem == null) return;
        ShopInventory.ClaimFree(dailyItem);
        RefreshDailyBanner();
    }

    // ── Coin Display ─────────────────────────────────────────────────────────

    void RefreshCoinDisplay(int balance)
    {
        if (coinBalanceLabel != null)
            coinBalanceLabel.text = balance.ToString("N0");
    }

    // ── Animation ────────────────────────────────────────────────────────────

    IEnumerator Co_Open()
    {
        if (panel != null) { panel.gameObject.SetActive(true); panel.alpha = 0f; }
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (rt != null) rt.localScale = Vector3.one * 0.85f;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popInDuration));
            if (panel != null) panel.alpha   = p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, p);
            yield return null;
        }
        if (panel != null) panel.alpha   = 1f;
        if (rt    != null) rt.localScale = Vector3.one;
    }

    IEnumerator Co_Close()
    {
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;

        float t = 0f;
        while (t < popOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popOutDuration));
            if (panel != null) panel.alpha   = 1f - p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.85f, p);
            yield return null;
        }
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
