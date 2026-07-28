using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Einzelne Boost-Karte in der BoostSelectionUI — Aufbau/Bind-Muster wie ShopItemCardUI, aber ohne
// Owned/Equip/Preview-Logik: eine Karte, ein Klick, ein Callback.
public class BoostCardUI : MonoBehaviour
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI descriptionLabel;
    [SerializeField] private Button          pickButton;

    private BoostDefinition        _definition;
    private Action<BoostDefinition> _onPick;

    public void Bind(BoostDefinition definition, Action<BoostDefinition> onPick)
    {
        _definition = definition;
        _onPick     = onPick;

        if (iconImage        != null) iconImage.sprite      = definition.icon;
        if (titleLabel        != null) titleLabel.text        = definition.displayName;
        if (descriptionLabel != null) descriptionLabel.text  = definition.description;

        if (pickButton != null)
        {
            pickButton.onClick.RemoveAllListeners();
            pickButton.onClick.AddListener(() => _onPick?.Invoke(_definition));
        }
    }
}
