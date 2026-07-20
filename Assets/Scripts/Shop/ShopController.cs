using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    public static ShopController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] CanvasGroup panel;

    [Header("Dream Energy Display")]
    [FormerlySerializedAs("coinBalanceLabel")]
    [SerializeField] TextMeshProUGUI dreamEnergyBalanceLabel;

    [Header("Daily Banner")]
    [Tooltip("Daily Banner erstmal komplett deaktiviert — Feature pausiert, Wiring bleibt erhalten.")]
    [SerializeField] bool            dailyBannerEnabled = false;
    [SerializeField] GameObject      dailyBannerRoot;
    [SerializeField] Image           dailyBannerThumbnail;
    [SerializeField] TextMeshProUGUI dailyBannerName;
    [SerializeField] Button          dailyClaimButton;
    [SerializeField] GameObject      dailyClaimedLabel;

    [Header("Tabs")]
    [SerializeField] Button tabSkins;
    [SerializeField] Button tabSounds;
    [SerializeField] Button tabCoins;
    [SerializeField] Button tabBundles;

    [Header("Tab Colors")]
    [SerializeField] Color tabActiveColor   = Color.white;
    [SerializeField] Color tabInactiveColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Grid")]
    [SerializeField] ScrollRect      itemScrollRect;
    [SerializeField] Transform       gridParent;
    [SerializeField] ShopItemCardUI  itemCardPrefab;
    [Tooltip("Gruppierte Box für Diamonds/Diamond Splinters im Currency-Tab (siehe PopulateCurrencyGrid).")]
    [SerializeField] ShopItemBoxUI   itemBoxPrefab;
    [Tooltip("Eigener Container für den Currency-Tab — Karten bleiben dort in voller Standardgröße " +
             "(kein GridLayoutGroup-Downscale wie bei gridParent). Wird per ScrollRect.content getauscht.")]
    [SerializeField] Transform       currencyGridParent;

    [Header("Items")]
    [SerializeField] ShopCatalogue catalogue;
    [SerializeField] ShopItem      dailyItem;

    [Header("Shop Button Badge")]
    [SerializeField] ShopButtonBadge shopButtonBadge;

    [Header("Animation")]
    [SerializeField] float popInDuration  = 0.28f;
    [SerializeField] float popOutDuration = 0.2f;

    ShopItemType _activeTab = ShopItemType.Bundle;
    bool         _open;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    void Start()
    {
        shopButtonBadge?.Refresh();
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
        DreamEnergyManager.OnDreamEnergyChanged += RefreshDreamEnergyDisplay;
        ShopInventory.OnEquippedChanged += HandleEquippedChanged;
        if (tabSkins         != null) tabSkins.onClick.AddListener(() => SwitchTab(ShopItemType.Skin));
        if (tabSounds        != null) tabSounds.onClick.AddListener(() => SwitchTab(ShopItemType.Sound));
        if (tabCoins         != null) tabCoins.onClick.AddListener(() => SwitchTab(ShopItemType.Currency));
        if (tabBundles       != null) tabBundles.onClick.AddListener(() => SwitchTab(ShopItemType.Bundle));
        if (dailyClaimButton != null) dailyClaimButton.onClick.AddListener(OnClaimDaily);
    }

    void OnDisable()
    {
        DreamEnergyManager.OnDreamEnergyChanged -= RefreshDreamEnergyDisplay;
        ShopInventory.OnEquippedChanged -= HandleEquippedChanged;
        if (tabSkins         != null) tabSkins.onClick.RemoveAllListeners();
        if (tabSounds        != null) tabSounds.onClick.RemoveAllListeners();
        if (tabCoins         != null) tabCoins.onClick.RemoveAllListeners();
        if (tabBundles       != null) tabBundles.onClick.RemoveAllListeners();
        if (dailyClaimButton != null) dailyClaimButton.onClick.RemoveAllListeners();
    }

    // Reagiert auf Equip-Änderungen egal woher (Shop-Karten selbst, oder z.B. das
    // Hauptmenü-Portal-Swipe) — hält das Grid synchron, auch wenn nicht der Shop selbst
    // die Änderung ausgelöst hat. Nur bei offenem Panel nötig, sonst wird beim nächsten
    // Open() ohnehin frisch befüllt.
    void HandleEquippedChanged()
    {
        if (_open) PopulateGrid();
    }

    public void Open()
    {
        if (_open) return;
        _open = true;
        RefreshDreamEnergyDisplay(DreamEnergyManager.Balance);
        RefreshDailyBanner();
        SwitchTab(ShopItemType.Bundle);
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
        SetTabColor(tabSkins,   _activeTab == ShopItemType.Skin);
        SetTabColor(tabSounds,  _activeTab == ShopItemType.Sound);
        SetTabColor(tabCoins,   _activeTab == ShopItemType.Currency);
        SetTabColor(tabBundles, _activeTab == ShopItemType.Bundle);
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
        if (itemCardPrefab == null || catalogue == null) return;

        // Currency-Tab nutzt einen eigenen Container (currencyGridParent), damit die Karten dort
        // in voller Standardgröße bleiben, statt von gridParent's GridLayoutGroup auf Zellgröße
        // heruntergezwungen zu werden. ScrollRect.content wird passend mitgetauscht.
        bool useCurrencyGrid = _activeTab == ShopItemType.Currency
            && itemBoxPrefab != null && currencyGridParent != null;

        if (gridParent         != null) gridParent.gameObject.SetActive(!useCurrencyGrid);
        if (currencyGridParent != null) currencyGridParent.gameObject.SetActive(useCurrencyGrid);
        if (itemScrollRect     != null) itemScrollRect.content = useCurrencyGrid ? (RectTransform)currencyGridParent : (RectTransform)gridParent;

        var items = System.Array.FindAll(catalogue.allItems,
            i => i != null && i.type == _activeTab);

        if (useCurrencyGrid)
        {
            foreach (Transform child in currencyGridParent)
                Destroy(child.gameObject);
            PopulateCurrencyGrid(items);
            return;
        }

        if (gridParent == null) return;
        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        foreach (var item in items)
        {
            if (item == null) continue;
            var card = Instantiate(itemCardPrefab, gridParent);
            card.Bind(item, OnBuyItem, OnEquipItem);
        }
    }

    // Currency-Tab: Diamonds und Diamond Splinters bekommen jeweils eine eigene beschriftete
    // Box (2 Karten pro Reihe, siehe Shop Item Box for Cards-Prefab), in voller Kartengröße.
    void PopulateCurrencyGrid(ShopItem[] items)
    {
        SpawnCurrencyBox(items, CurrencyRewardKind.Diamonds,         "DIAMONDS BOX");
        SpawnCurrencyBox(items, CurrencyRewardKind.DiamondSplinters, "DIAMOND SPLINTERS BOX");
    }

    void SpawnCurrencyBox(ShopItem[] items, CurrencyRewardKind kind, string title)
    {
        var matching = System.Array.FindAll(items, i => i != null && i.currencyKind == kind);
        if (matching.Length == 0) return;

        var box = Instantiate(itemBoxPrefab, currencyGridParent);
        box.SetTitle(title);

        foreach (var item in matching)
        {
            var card = Instantiate(itemCardPrefab, box.CardsContainer);
            card.Bind(item, OnBuyItem, OnEquipItem);
        }
    }

    void OnEquipItem(ShopItem item)
    {
        if (item.type == ShopItemType.Bundle)
        {
            // Bundle equippt Skin + Sound zugleich. Damit RestoreEquipped (das
            // per Typ nachschlägt) das Bundle wiederfindet, equipped[Skin] und
            // equipped[Sound] ebenfalls auf die Bundle-ID setzen — FindById
            // liefert dann das Bundle und liest dessen skinTheme/soundTheme.
            ShopInventory.SetEquipped(ShopItemType.Bundle, item.itemId);
            ShopInventory.SetEquipped(ShopItemType.Skin,   item.itemId);
            ShopInventory.SetEquipped(ShopItemType.Sound,  item.itemId);

            SkinManager.Instance?.Apply(item.skinTheme);
            SoundThemeManager.Instance?.Apply(item.soundTheme);
        }
        else
        {
            ShopInventory.SetEquipped(item.type, item.itemId);

            // Standalone-Equip löst ein aktives Bundle auf, damit dessen Card
            // nicht fälschlich "EQUIPPED" zeigt.
            ShopInventory.SetEquipped(ShopItemType.Bundle, "");

            if (item.type == ShopItemType.Skin)
                SkinManager.Instance?.Apply(item.skinTheme);
            else if (item.type == ShopItemType.Sound)
                SoundThemeManager.Instance?.Apply(item.soundTheme);
        }

        // Kein direkter PopulateGrid()-Aufruf mehr nötig — ShopInventory.SetEquipped oben
        // feuert OnEquippedChanged, HandleEquippedChanged() übernimmt das Refresh.
    }

    void OnBuyItem(ShopItem item)
    {
        // IAP-Produkt (Currency oder Unlock via echtem Geld)
        if (!string.IsNullOrEmpty(item.iapProductId))
        {
            IAPManager.Instance?.BuyProduct(item.iapProductId);
            return;
        }

        // Currency-Items (Diamonds/Diamond Splinters) sind ein wiederholbarer Tausch gegen
        // Dream Energy, kein einmaliger Unlock wie Skins/Sounds/Bundles.
        if (item.type == ShopItemType.Currency)
        {
            if (ShopInventory.TryExchangeForCurrency(item))
                PopulateGrid();
            return;
        }

        // Dream-Energy-Kauf (Skins/Sounds/Bundles)
        if (ShopInventory.TryPurchase(item))
        {
            PopulateGrid();
        }
    }

    // ── Daily Banner ─────────────────────────────────────────────────────────

    void RefreshDailyBanner()
    {
        if (dailyBannerRoot == null) return;
        if (!dailyBannerEnabled || dailyItem == null) { dailyBannerRoot.SetActive(false); return; }

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
        shopButtonBadge?.Refresh();
    }

    // ── Dream Energy Display ────────────────────────────────────────────────

    void RefreshDreamEnergyDisplay(int balance)
    {
        if (dreamEnergyBalanceLabel != null)
            dreamEnergyBalanceLabel.text = CurrencyFormat.Format(balance);
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
