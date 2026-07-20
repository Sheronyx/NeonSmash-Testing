using TMPro;
using UnityEngine;

// Gruppierte Box für Currency-Shop-Karten (z.B. "DIAMONDS BOX"), die im Shop-Grid einen
// eigenen Titel trägt und mehrere ShopItemCard-Instanzen nebeneinander aufnimmt.
// Der ShopController instanziiert für jede vorkommende Währung eine Box und befüllt sie.
// Die Box-Höhe ergibt sich automatisch aus Titel + Cards Container + Padding/Spacing über die
// VerticalLayoutGroup + ContentSizeFitter-Kette auf dem Prefab — kein manuelles Nachrechnen nötig.
public class ShopItemBoxUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private Transform       cardsContainer;

    public Transform CardsContainer => cardsContainer;

    public void SetTitle(string title)
    {
        if (titleLabel != null) titleLabel.text = title;
    }
}
