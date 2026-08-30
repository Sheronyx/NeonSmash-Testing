using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Vergrößerte Detailansicht eines Stickers aus dem Album (siehe RewardWindowController /
// StickerCoinUI) — mit eigenem, transparentem Hintergrund: Klick irgendwo auf den Hintergrund
// schließt das Popup wieder (eigener Klick-Handler statt der geteilten DimOverlay, damit das
// Schließen nicht versehentlich auch das dahinterliegende Belohnungsfenster mit-schließt).
// Bietet zwei Aktionen: als Porträt auswählen (bisherige StickerManager.SelectSticker-Funktion,
// jetzt hierher verschoben statt direkt beim Antippen der Münze im Album) und — bei 3 oder mehr
// identischen Stickern — Duplikate gegen Traumsplitter verkaufen (wiederholbar, siehe
// StickerManager.TrySellDuplicates).
public class StickerDetailPopupController : MonoBehaviour
{
    public static StickerDetailPopupController Instance { get; private set; }

    [Header("Hintergrund (eigener, schließt bei Klick)")]
    [SerializeField] CanvasGroup panel;
    [SerializeField] Button backgroundButton;

    [Header("Inhalt")]
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI countLabel;

    [Header("Auswahl")]
    [SerializeField] Button selectButton;
    [SerializeField] TextMeshProUGUI selectButtonLabel;

    [Header("Verkauf")]
    [SerializeField] Button sellButton;
    [SerializeField] TextMeshProUGUI sellButtonLabel;

    RewardDefinition _sticker;
    bool _open;

    void Awake()
    {
        Instance = this;
        if (panel != null) { panel.gameObject.SetActive(false); }
    }

    void OnEnable()
    {
        if (backgroundButton != null) backgroundButton.onClick.AddListener(Close);
        if (selectButton     != null) selectButton.onClick.AddListener(OnSelectClicked);
        if (sellButton       != null) sellButton.onClick.AddListener(OnSellClicked);
        StickerManager.OnCountChanged   += HandleCountChanged;
        StickerManager.OnStickerGranted += Open;
    }

    void OnDisable()
    {
        if (backgroundButton != null) backgroundButton.onClick.RemoveListener(Close);
        if (selectButton     != null) selectButton.onClick.RemoveListener(OnSelectClicked);
        if (sellButton       != null) sellButton.onClick.RemoveListener(OnSellClicked);
        StickerManager.OnCountChanged   -= HandleCountChanged;
        StickerManager.OnStickerGranted -= Open;
    }

    public void Open(RewardDefinition sticker)
    {
        if (sticker == null) return;
        _sticker = sticker;
        _open = true;
        if (panel != null) panel.gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        _open = false;
        _sticker = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    void HandleCountChanged(string rewardId, int newCount)
    {
        if (!_open || _sticker == null || _sticker.rewardId != rewardId) return;
        if (newCount <= 0) { Close(); return; }
        Refresh();
    }

    void Refresh()
    {
        if (_sticker == null) return;

        if (iconImage != null) iconImage.sprite = _sticker.icon;
        if (nameLabel != null) nameLabel.text   = _sticker.displayName;

        int count = StickerManager.GetCount(_sticker.rewardId);
        if (countLabel != null) countLabel.text = "x" + count;

        bool isSelected = StickerManager.SelectedStickerId == _sticker.rewardId;
        if (selectButton      != null) selectButton.interactable = !isSelected;
        if (selectButtonLabel != null) selectButtonLabel.text     = isSelected ? "SELECTED" : "SELECT";

        int sellPrice = StickerManager.SellPriceFor(_sticker.stickerRarity);
        bool canSell = count >= 3;
        if (sellButton      != null) sellButton.gameObject.SetActive(true);
        if (sellButton      != null) sellButton.interactable = canSell;
        if (sellButtonLabel != null) sellButtonLabel.text     = "Sell 3 Coins for " + sellPrice;
    }

    void OnSelectClicked()
    {
        if (_sticker == null) return;
        StickerManager.SelectSticker(_sticker.rewardId);
        Refresh();
    }

    void OnSellClicked()
    {
        if (_sticker == null) return;
        if (StickerManager.TrySellDuplicates(_sticker))
            Refresh(); // Close() passiert automatisch via HandleCountChanged, falls Bestand < 3
    }
}
