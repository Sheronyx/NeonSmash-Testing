using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemCardUI : MonoBehaviour
{
    [SerializeField] Image           thumbnail;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI priceLabel;
    [SerializeField] Button          actionButton;
    [SerializeField] GameObject      ownedBadge;
    [SerializeField] GameObject      coinIcon;

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

        bool owned    = ShopInventory.IsOwned(_item.itemId);
        bool equipped = owned && ShopInventory.GetEquipped(_item.type) == _item.itemId;

        if (ownedBadge     != null) ownedBadge.SetActive(owned);
        if (coinIcon       != null) coinIcon.SetActive(!owned && _item.coinPrice > 0);
        if (previewOverlay != null) previewOverlay.SetActive(_item.type == ShopItemType.Sound);

        if (actionButton != null)
        {
            actionButton.interactable = !equipped;
            SetButtonColor(equipped ? equippedColor : owned ? equipColor : buyColor);
        }

        if (priceLabel != null)
        {
            if (equipped)                  priceLabel.text = "EQUIPPED";
            else if (owned)                priceLabel.text = "EQUIP";
            else if (_item.coinPrice == 0) priceLabel.text = "FREE";
            else                           priceLabel.text = $"{_item.coinPrice:N0}";
        }

        SetPreviewIcons(isPlaying: _playingCard == this);
    }

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
