using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ShopItemCardUI : MonoBehaviour
{
    [SerializeField] Image           thumbnail;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI priceLabel;
    [SerializeField] Button          actionButton;
    [SerializeField] GameObject      ownedBadge;
    [FormerlySerializedAs("coinIcon")]
    [SerializeField] GameObject      dreamEnergyIcon;
    [Tooltip("Splitter-Icon für Booster-Items (type == Booster) — Preis dort ist in Diamond Splinters, nicht Dream Energy.")]
    [SerializeField] GameObject      diamondSplinterIcon;
    [Tooltip("Paketgröße oben rechts (z.B. \"x3\") — nur für type == Booster (aus item.packAmount).")]
    [SerializeField] TextMeshProUGUI packCountLabel;
    [Tooltip("Diamonds-Icon für Welt-Bundles (item.diamondsPrice > 0) — Preis dort ist in Diamonds " +
             "(Traumkristalle), nicht Dream Energy.")]
    [SerializeField] GameObject      diamondIcon;

    [Header("Sound Preview")]
    [SerializeField] GameObject previewOverlay;  // über dem Thumbnail, nur für Sound-Items
    [SerializeField] Button     previewButton;
    [SerializeField] GameObject playIcon;
    [SerializeField] GameObject stopIcon;

    [Header("Preview")]
    [SerializeField] float previewFadeIn = 0.6f;

    [Header("Button Colors")]
    [SerializeField] Color buyColor      = new(0.20f, 0.78f, 1.00f);
    [SerializeField] Color equipColor    = new(0.60f, 0.30f, 1.00f);
    [SerializeField] Color equippedColor = new(0.25f, 0.75f, 0.35f);

    // ── Shared preview audio (einmal für alle Cards) ──────────────────────────
    static AudioSource       _previewSource;
    static ShopItemCardUI    _playingCard;

    static AudioSource PreviewSource
    {
        get
        {
            if (_previewSource != null) return _previewSource;
            var go = new GameObject("ShopPreviewAudio");
            DontDestroyOnLoad(go);
            _previewSource = go.AddComponent<AudioSource>();
            _previewSource.playOnAwake = false;
            return _previewSource;
        }
    }

    // ── Instance state ────────────────────────────────────────────────────────
    ShopItem                _item;
    System.Action<ShopItem> _onBuy;
    System.Action<ShopItem> _onEquip;
    Coroutine               _watchRoutine;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Bind(ShopItem item, System.Action<ShopItem> onBuy, System.Action<ShopItem> onEquip)
    {
        _item    = item;
        _onBuy   = onBuy;
        _onEquip = onEquip;

        if (thumbnail != null) thumbnail.sprite = item.thumbnail;
        if (nameLabel != null) nameLabel.text   = item.displayName;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnClick);
        }

        if (previewButton != null)
        {
            previewButton.onClick.RemoveAllListeners();
            previewButton.onClick.AddListener(OnPreviewClick);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_item == null) return;

        bool isBooster    = _item.type == ShopItemType.Booster;
        bool isWorldPrice = _item.diamondsPrice > 0;

        // Booster-Pakete sind wie Currency-Items ein wiederholbarer Kauf ohne Owned-Zustand — itemId
        // wird nie in ShopInventory.Owned aufgenommen, "owned"/"equipped" bleiben also immer false.
        bool owned    = ShopInventory.IsOwned(_item.itemId);
        bool equipped = owned && ShopInventory.GetEquipped(_item.type) == _item.itemId;

        bool hasPreview = _item.soundTheme != null && _item.soundTheme.previewClip != null;

        if (ownedBadge          != null) ownedBadge.SetActive(owned);
        if (dreamEnergyIcon     != null) dreamEnergyIcon.SetActive(!owned && !isBooster && !isWorldPrice && _item.dreamEnergyPrice > 0);
        if (diamondSplinterIcon != null) diamondSplinterIcon.SetActive(isBooster && _item.diamondSplinterPrice > 0);
        if (diamondIcon         != null) diamondIcon.SetActive(!owned && isWorldPrice);
        if (previewOverlay      != null) previewOverlay.SetActive(hasPreview);

        if (packCountLabel != null)
        {
            packCountLabel.gameObject.SetActive(isBooster && _item.packAmount > 1);
            if (isBooster) packCountLabel.text = "x" + Mathf.Max(1, _item.packAmount);
        }

        if (actionButton != null)
        {
            actionButton.interactable = !equipped;
            SetButtonColor(equipped ? equippedColor : owned ? equipColor : buyColor);
        }

        if (priceLabel != null)
        {
            if (isBooster)
                priceLabel.text = CurrencyFormat.Format(_item.diamondSplinterPrice);
            else if (equipped)
                priceLabel.text = "USED";
            else if (owned)
                priceLabel.text = "USE";
            else if (isWorldPrice)
                priceLabel.text = CurrencyFormat.Format(_item.diamondsPrice);
            else if (!string.IsNullOrEmpty(_item.iapProductId))
                priceLabel.text = IAPManager.Instance?.GetLocalizedPrice(_item.iapProductId) ?? "...";
            else if (_item.dreamEnergyPrice == 0)
                priceLabel.text = "FREE";
            else
                priceLabel.text = CurrencyFormat.Format(_item.dreamEnergyPrice);
        }

        SetPreviewIcons(isPlaying: _playingCard == this);
    }

    void OnEnable()
    {
        IAPManager.OnStoreInitialized += Refresh;
        IAPManager.OnPurchaseSuccess  += HandlePurchaseSuccess;
    }

    void OnDisable()
    {
        IAPManager.OnStoreInitialized -= Refresh;
        IAPManager.OnPurchaseSuccess  -= HandlePurchaseSuccess;
    }

    void HandlePurchaseSuccess(string _) => Refresh();

    void OnDestroy()
    {
        if (_playingCard == this) StopPreview();
    }

    // ── Buy / Equip ───────────────────────────────────────────────────────────

    void OnClick()
    {
        if (ShopInventory.IsOwned(_item.itemId))
            _onEquip?.Invoke(_item);
        else
            _onBuy?.Invoke(_item);
    }

    void SetButtonColor(Color c)
    {
        var colors = actionButton.colors;
        colors.normalColor = c;
        actionButton.colors = colors;
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    void OnPreviewClick()
    {
        if (_playingCard == this) { StopPreview(); return; }

        if (_playingCard != null) _playingCard.StopPreview();

        if (_item.soundTheme == null) return;
        var clip = _item.soundTheme.previewClip;
        if (clip == null) return;

        _playingCard = this;
        if (MusicManager.Instance != null) MusicManager.Instance.PauseForPreview();
        PreviewSource.volume = 0f;
        PreviewSource.clip   = clip;
        PreviewSource.Play();
        SetPreviewIcons(true);

        if (_watchRoutine != null) StopCoroutine(_watchRoutine);
        _watchRoutine = StartCoroutine(Co_WatchPlayback());
        StartCoroutine(Co_FadeIn());
    }

    void StopPreview()
    {
        if (_watchRoutine != null) { StopCoroutine(_watchRoutine); _watchRoutine = null; }
        PreviewSource.Stop();
        PreviewSource.volume = 1f;
        _playingCard = null;
        if (MusicManager.Instance != null) MusicManager.Instance.ResumeAfterPreview();
        SetPreviewIcons(false);
    }

    IEnumerator Co_FadeIn()
    {
        float t = 0f;
        while (t < previewFadeIn)
        {
            t += Time.unscaledDeltaTime;
            PreviewSource.volume = Mathf.Clamp01(t / previewFadeIn);
            yield return null;
        }
        PreviewSource.volume = 1f;
    }

    IEnumerator Co_WatchPlayback()
    {
        yield return new WaitWhile(() => PreviewSource.isPlaying);
        if (_playingCard == this) StopPreview();
    }

    void SetPreviewIcons(bool isPlaying)
    {
        if (playIcon != null) playIcon.SetActive(!isPlaying);
        if (stopIcon != null) stopIcon.SetActive(isPlaying);
    }
}
