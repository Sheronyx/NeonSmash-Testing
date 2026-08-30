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
    [Tooltip("Verfügbarkeitsanzahl oben rechts auf der Karte (z.B. \"x3\") — aus BoosterInventoryManager.")]
    [SerializeField] private TextMeshProUGUI countLabel;

    [Header("Ausverkauft-Zustand (count == 0)")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color depletedColor  = new Color(1f, 1f, 1f, 0.4f);

    private BoostDefinition        _definition;
    private Action<BoostDefinition> _onPick;

    public void Bind(BoostDefinition definition, int availableCount, Action<BoostDefinition> onPick)
    {
        _definition = definition;
        _onPick     = onPick;

        if (iconImage        != null) iconImage.sprite      = definition.icon;
        if (titleLabel        != null) titleLabel.text        = definition.displayName;
        if (descriptionLabel != null) descriptionLabel.text  = definition.description;

        SetCount(availableCount);

        if (pickButton != null)
        {
            pickButton.onClick.RemoveAllListeners();
            pickButton.onClick.AddListener(() => _onPick?.Invoke(_definition));
        }
    }

    /// <summary>Aktualisiert nur die Stückzahl (z.B. bei BoosterInventoryManager.OnCountChanged),
    /// ohne die Karte komplett neu zu binden.</summary>
    public void SetCount(int availableCount)
    {
        bool hasNone = availableCount <= 0;

        if (countLabel != null)
        {
            countLabel.text = "x" + availableCount;
            countLabel.gameObject.SetActive(true);
        }

        // "No Boost" (Skip) hat keine Stückzahl-Beschränkung — nur echte Boost-Typen können
        // ausverkauft sein und die Karte damit unklickbar machen.
        bool isRealBoost = _definition == null || _definition.type != BoostType.None;
        if (pickButton != null) pickButton.interactable = !isRealBoost || !hasNone;
        var color = (isRealBoost && hasNone) ? depletedColor : availableColor;
        if (iconImage        != null) iconImage.color        = color;
        if (titleLabel        != null) titleLabel.color        = color;
        if (descriptionLabel != null) descriptionLabel.color  = color;
    }
}
