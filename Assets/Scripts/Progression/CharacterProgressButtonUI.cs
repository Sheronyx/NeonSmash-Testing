using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Treibt den "Button Character & Progress Menu" im Hauptmenü: Fortschrittsleiste bis zur
// nächsten Stufen-Belohnung (ohne nackte Zahl — nur die Leiste + Icon der nächsten Belohnung),
// aktuell ausgewählter Titel unter dem Namen, und der zuletzt freigeschaltete Sticker als Münze
// in der Mitte. Ersetzt die Rolle, die DreamEnergyProgressUI hier vorher hatte (die blieb dort
// an Dream-Energy-Lifetime-Shop-Items gebunden statt am echten Stufensystem).
public class CharacterProgressButtonUI : MonoBehaviour
{
    [Header("Daten")]
    [SerializeField] LevelRewardTrack rewardTrack;
    [Tooltip("Für die Sticker-Auswahl (Mitte-Münze) — echte Sticker werden nur zufällig vergeben " +
             "(siehe StickerManager.GrantRandom) und stehen NICHT einzeln im LevelRewardTrack, " +
             "sondern nur im Katalog.")]
    [SerializeField] RewardCatalogue rewardCatalogue;

    [Header("Fortschrittsleiste")]
    [Tooltip("Image mit Type=Filled, Fill Method=Horizontal.")]
    [SerializeField] Image fillImage;
    [SerializeField] GameObject nextItemRoot;
    [SerializeField] RewardIconUI nextItemIcon;
    [SerializeField] GameObject badgeRoot;

    [Header("Name & Titel")]
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI titleLabel;

    [Header("Sticker (Mitte)")]
    [SerializeField] Image stickerCoinImage;
    [SerializeField] GameObject stickerCoinRoot;

    [Header("Animation")]
    [SerializeField] float fillAnimDuration = 1.2f;

    const string LastShownKey    = "player_level_progress_last_shown";
    const string BadgeAckTierKey = "player_level_badge_ack_tier";

    void Start()
    {
        if (nameLabel != null) nameLabel.text = LeaderboardApi.GetLocalDisplayName();
        RefreshTitle();
        RefreshSticker();

        if (rewardTrack == null) { if (nextItemRoot != null) nextItemRoot.SetActive(false); return; }
        var tiers = rewardTrack.SortedTiers();
        if (tiers.Length == 0) { if (nextItemRoot != null) nextItemRoot.SetActive(false); return; }

        int lifetime = DreamEnergyManager.LifetimeEarned;
        int previous = Mathf.Clamp(PlayerPrefs.GetInt(LastShownKey, 0), 0, lifetime);

        ApplyVisual(previous, tiers);
        UpdateBadge(lifetime);

        if (lifetime > previous)
            StartCoroutine(Co_AnimateFill(previous, lifetime, tiers));
        else
            PlayerPrefs.SetInt(LastShownKey, lifetime);

        TitleManager.OnSelectedTitleChanged     += HandleTitleChanged;
        StickerManager.OnSelectedStickerChanged += HandleStickerChanged;
    }

    void OnDestroy()
    {
        TitleManager.OnSelectedTitleChanged     -= HandleTitleChanged;
        StickerManager.OnSelectedStickerChanged -= HandleStickerChanged;
    }

    void HandleTitleChanged(string _)   => RefreshTitle();
    void HandleStickerChanged(string _) => RefreshSticker();

    void RefreshTitle()
    {
        if (titleLabel == null) return;
        string selectedId = TitleManager.SelectedTitleId;
        if (string.IsNullOrEmpty(selectedId)) { titleLabel.text = ""; return; }

        // Titel werden nicht (mehr) nur über den LevelRewardTrack vergeben (z.B. per Zufall/Event
        // aus dem Titel-Pool) — deshalb zuerst im Katalog suchen, mit Track als Fallback.
        var reward = FindTitleById(selectedId) ?? FindRewardById(selectedId);
        titleLabel.text = reward != null ? reward.displayName : "";
    }

    void RefreshSticker()
    {
        if (stickerCoinImage == null) return;

        // Solange noch kein Sticker ausgewählt ist, bleibt das ursprüngliche Charakterporträt
        // stehen (kein leerer Kreis) — die Münze ersetzt es erst, sobald der Spieler im Album
        // einen Sticker ausgewählt hat.
        string selectedId = StickerManager.SelectedStickerId;
        if (string.IsNullOrEmpty(selectedId)) return;

        var reward = FindStickerById(selectedId);
        if (reward == null || reward.icon == null) return;

        // Sprite einfach einfügen — Farbe/Optik bleibt komplett dem Artwork überlassen.
        stickerCoinImage.sprite = reward.icon;
        stickerCoinImage.color = Color.white;
    }

    RewardDefinition FindRewardById(string rewardId)
    {
        if (rewardTrack == null) return null;
        foreach (var tier in rewardTrack.SortedTiers())
        {
            if (tier.rewards == null) continue;
            foreach (var r in tier.rewards)
                if (r != null && r.rewardId == rewardId) return r;
        }
        return null;
    }

    // Echte Sticker werden nie fix im LevelRewardTrack hinterlegt (dort steht nur ein generischer
    // Platzhalter-Marker, siehe Reward_Sticker_LevelTrackMarker) — die tatsächlich besitzbaren
    // Sticker-Assets stehen im RewardCatalogue, deshalb eigene Suche statt FindRewardById.
    RewardDefinition FindStickerById(string rewardId)
    {
        if (rewardCatalogue == null || rewardCatalogue.allStickers == null) return null;
        foreach (var s in rewardCatalogue.allStickers)
            if (s != null && s.rewardId == rewardId) return s;
        return null;
    }

    RewardDefinition FindTitleById(string rewardId)
    {
        if (rewardCatalogue == null || rewardCatalogue.allTitles == null) return null;
        foreach (var t in rewardCatalogue.allTitles)
            if (t != null && t.rewardId == rewardId) return t;
        return null;
    }

    (float fill, int currentTierIndex, int nextTierIndex) Evaluate(int lifetimeValue, LevelRewardTrack.LevelTier[] tiers)
    {
        int currentTierIndex = -1;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].xpThreshold <= lifetimeValue) currentTierIndex = i;
            else break;
        }

        int nextTierIndex = currentTierIndex + 1;
        if (nextTierIndex >= tiers.Length) return (1f, currentTierIndex, -1);

        int segStart = currentTierIndex >= 0 ? tiers[currentTierIndex].xpThreshold : 0;
        int segEnd   = tiers[nextTierIndex].xpThreshold;
        float fill = segEnd > segStart ? Mathf.Clamp01((float)(lifetimeValue - segStart) / (segEnd - segStart)) : 1f;
        return (fill, currentTierIndex, nextTierIndex);
    }

    void ApplyVisual(int lifetimeValue, LevelRewardTrack.LevelTier[] tiers)
    {
        var (fill, _, nextTierIndex) = Evaluate(lifetimeValue, tiers);
        if (fillImage != null) fillImage.fillAmount = fill;

        bool hasNext = nextTierIndex >= 0;
        if (nextItemRoot != null) nextItemRoot.SetActive(hasNext);
        if (hasNext && nextItemIcon != null && tiers[nextTierIndex].rewards != null && tiers[nextTierIndex].rewards.Length > 0)
            nextItemIcon.Bind(tiers[nextTierIndex].rewards[0]);
    }

    IEnumerator Co_AnimateFill(int from, int to, LevelRewardTrack.LevelTier[] tiers)
    {
        float t = 0f;
        while (t < fillAnimDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fillAnimDuration));
            int animatedValue = Mathf.RoundToInt(Mathf.Lerp(from, to, p));
            ApplyVisual(animatedValue, tiers);
            yield return null;
        }
        ApplyVisual(to, tiers);
        UpdateBadge(to);

        PlayerPrefs.SetInt(LastShownKey, to);
        PlayerPrefs.Save();
    }

    void UpdateBadge(int lifetime)
    {
        var tiers = rewardTrack.SortedTiers();
        var (_, currentTierIndex, _) = Evaluate(lifetime, tiers);
        int ackTierIndex = PlayerPrefs.GetInt(BadgeAckTierKey, -1);
        if (badgeRoot != null) badgeRoot.SetActive(currentTierIndex > ackTierIndex);
    }

    /// <summary>Auf den Button-OnClick verdrahtet (öffnet das Belohnungsfenster) — markiert die
    /// aktuell erreichte Stufe als gesehen, damit das Badge verschwindet.</summary>
    public void AcknowledgeBadge()
    {
        if (rewardTrack == null) return;
        var tiers = rewardTrack.SortedTiers();
        var (_, currentTierIndex, _) = Evaluate(DreamEnergyManager.LifetimeEarned, tiers);

        PlayerPrefs.SetInt(BadgeAckTierKey, currentTierIndex);
        PlayerPrefs.Save();
        if (badgeRoot != null) badgeRoot.SetActive(false);
    }
}
