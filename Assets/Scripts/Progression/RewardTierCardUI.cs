using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Eine einzelne Stufen-Karte im Belohnungssystemfenster (siehe RewardWindowController): zeigt
// Stufennummer und alle Belohnungen dieser Stufe als kleine Icons. Eine Stufe ist "erreicht"
// (per Traumenergie-Schwelle) unabhängig davon, ob ihre Belohnungen schon abgeholt wurden — das
// passiert erst über den COLLECT-Button (siehe PlayerLevelManager.TryCollect). Erreichte, aber
// noch nicht abgeholte Karten sind normal eingefärbt mit klickbarem "COLLECT"; noch nicht
// erreichte Karten sind abgedunkelt; abgeholte zeigen "COLLECTED" + Checkmark oben links.
public class RewardTierCardUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] Transform rewardIconParent;
    [SerializeField] RewardIconUI rewardIconPrefab;
    [Header("Collect")]
    [SerializeField] Button collectButton;
    [SerializeField] TextMeshProUGUI collectButtonLabel;
    [SerializeField] GameObject collectedCheckmark;
    [SerializeField] GameObject lockOverlay;

    LevelRewardTrack.LevelTier _tier;

    public void Bind(LevelRewardTrack.LevelTier tier)
    {
        _tier = tier;
        Refresh();

        if (collectButton != null)
        {
            collectButton.onClick.RemoveAllListeners();
            collectButton.onClick.AddListener(OnCollectClicked);
        }
    }

    void Refresh()
    {
        if (_tier == null) return;
        bool reached   = PlayerLevelManager.IsReached(_tier);
        bool collected = PlayerLevelManager.IsCollected(_tier);

        if (levelLabel != null) levelLabel.text = "LEVEL " + _tier.level;

        if (rewardIconParent != null && rewardIconPrefab != null)
        {
            foreach (Transform child in rewardIconParent)
                Destroy(child.gameObject);

            if (_tier.rewards != null)
                foreach (var reward in _tier.rewards)
                {
                    if (reward == null) continue;
                    var icon = Instantiate(rewardIconPrefab, rewardIconParent);
                    icon.Bind(reward);
                }
        }

        if (collectedCheckmark != null) collectedCheckmark.SetActive(collected);
        if (lockOverlay != null) lockOverlay.SetActive(!reached);

        if (collectButton != null)
        {
            collectButton.gameObject.SetActive(true);
            collectButton.interactable = reached && !collected;
        }
        if (collectButtonLabel != null)
            collectButtonLabel.text = !reached ? "LOCKED" : collected ? "COLLECTED" : "COLLECT";
    }

    void OnCollectClicked()
    {
        if (_tier == null) return;
        if (PlayerLevelManager.TryCollect(_tier))
            Refresh();
    }
}
