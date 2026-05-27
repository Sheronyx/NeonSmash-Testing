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

    ShopItem                  _item;
    System.Action<ShopItem>   _onBuy;

    public void Bind(ShopItem item, System.Action<ShopItem> onBuy)
    {
        _item  = item;
        _onBuy = onBuy;

        if (thumbnail != null) thumbnail.sprite = item.thumbnail;
        if (nameLabel != null) nameLabel.text   = item.displayName;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnClick);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_item == null) return;
        bool owned = ShopInventory.IsOwned(_item.itemId);

        if (ownedBadge   != null) ownedBadge.SetActive(owned);
        if (actionButton != null) actionButton.interactable = !owned;

        if (priceLabel != null)
        {
            if (owned)
                priceLabel.text = "✓";
            else if (_item.coinPrice == 0)
                priceLabel.text = "FREE";
            else
                priceLabel.text = $"🪙 {_item.coinPrice:N0}";
        }
    }

    void OnClick() => _onBuy?.Invoke(_item);
}
