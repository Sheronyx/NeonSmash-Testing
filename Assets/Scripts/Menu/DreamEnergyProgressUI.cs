using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Fortschrittsleiste im Hauptmenü (unter dem Profilnamen): zeigt, wie nah die Spielerin am
// nächsten per Dream Energy freischaltbaren Shop-Item (siehe DreamEnergyRewardTrack) ist.
// Läuft auf "insgesamt jemals verdienter" Dream Energy (DreamEnergyManager.LifetimeEarned), NICHT
// auf dem ausgebbaren Kontostand — Einkäufe im Shop lassen die Leiste also nie zurückspringen.
//
// Beim Zurückkehren aus einer Session (Start hier = Szene neu geladen) wird die Differenz zum
// zuletzt angezeigten Stand (PlayerPrefs) sanft animiert (Leiste füllt sich, Zahl zählt hoch).
// Wird dabei ein neuer Tier erreicht, poppt das Next-Item-Icon kurz auf und das Badge aktiviert
// sich — bis AcknowledgeBadge() aufgerufen wird (verdrahtet auf den Button-OnClick, der den Shop
// öffnet), bleibt es an.
public class DreamEnergyProgressUI : MonoBehaviour
{
    [Header("Daten")]
    [SerializeField] private DreamEnergyRewardTrack rewardTrack;

    [Header("UI-Referenzen")]
    [Tooltip("Image mit Type=Filled, Fill Method=Horizontal — stellt den Füllstand der Leiste dar.")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI dreamEnergyLabel;
    [Tooltip("Ganzer Next-Item-Bereich rechts von der Leiste (Icon + evtl. Rahmen) — wird komplett " +
             "ausgeblendet, wenn kein weiterer Tier mehr offen ist.")]
    [SerializeField] private GameObject nextItemRoot;
    [SerializeField] private Image nextItemIcon;
    [Tooltip("Benachrichtigungs-Badge auf dem Button — an, solange ein erreichter Tier noch nicht " +
             "durch Öffnen des Shops bestätigt wurde (siehe AcknowledgeBadge).")]
    [SerializeField] private GameObject badgeRoot;

    [Header("Animation")]
    [SerializeField] private float fillAnimDuration = 1.2f;
    [SerializeField] private float itemPopScale     = 1.3f;
    [SerializeField] private float itemPopDuration  = 0.35f;

    const string LastShownKey    = "dream_energy_progress_last_shown";
    const string BadgeAckTierKey = "dream_energy_badge_ack_tier";
    const string GrantedTierKey  = "dream_energy_granted_tier";

    private void Start()
    {
        if (rewardTrack == null) { gameObject.SetActive(false); return; }
        var tiers = rewardTrack.SortedTiers();
        if (tiers.Length == 0) { gameObject.SetActive(false); return; }

        int lifetime = DreamEnergyManager.LifetimeEarned;

        // Grants nachholen — idempotent (über grantedTierIndex, nicht ShopInventory.IsOwned), deckt
        // auch den Fall ab, dass eine Schwelle erreicht wurde, während dieses Menü nicht aktiv war.
        // Currency-Items (Diamonds/Splinters) eigener Pfad: ShopInventory.ClaimFree würde sie fälschlich
        // als "besessen" markieren (Currency-Items sind bewusst NICHT Owned-getrackt, siehe
        // ShopInventory — sie sind ein wiederholbarer Tausch, kein Unlock), was ihre Shop-Karte kaputt
        // machen würde (zeigt dann "USE" statt Kaufpreis) und nie tatsächlich Guthaben gutschreibt.
        int grantedTierIndex = PlayerPrefs.GetInt(GrantedTierKey, -1);
        for (int i = 0; i < tiers.Length; i++)
        {
            var tier = tiers[i];
            if (tier.item == null || i <= grantedTierIndex || lifetime < tier.threshold) continue;

            if (tier.item.type == ShopItemType.Currency)
                GrantCurrency(tier.item);
            else if (!ShopInventory.IsOwned(tier.item.itemId))
                ShopInventory.ClaimFree(tier.item);

            grantedTierIndex = i;
        }
        PlayerPrefs.SetInt(GrantedTierKey, grantedTierIndex);
        PlayerPrefs.Save();

        int previous = Mathf.Clamp(PlayerPrefs.GetInt(LastShownKey, 0), 0, lifetime);

        ApplyVisual(previous, tiers);
        UpdateBadge(tiers, lifetime);

        if (lifetime > previous)
            StartCoroutine(Co_AnimateFill(previous, lifetime, tiers));
        else
            PlayerPrefs.SetInt(LastShownKey, lifetime);
    }

    // Wertet einen beliebigen Lifetime-Stand aus: Füllstand innerhalb des aktuellen Segments (0..1),
    // Index des zuletzt erreichten Tiers (-1 = noch keiner), Index des nächsten offenen Tiers
    // (-1 = Track komplett abgeschlossen).
    private (float fill, int currentTierIndex, int nextTierIndex) Evaluate(int lifetimeValue, DreamEnergyRewardTrack.Tier[] tiers)
    {
        int currentTierIndex = -1;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].threshold <= lifetimeValue) currentTierIndex = i;
            else break; // aufsteigend sortiert — ab hier sind alle weiteren Schwellen erst recht nicht erreicht
        }

        int nextTierIndex = currentTierIndex + 1;
        if (nextTierIndex >= tiers.Length) return (1f, currentTierIndex, -1);

        int segStart = currentTierIndex >= 0 ? tiers[currentTierIndex].threshold : 0;
        int segEnd   = tiers[nextTierIndex].threshold;
        float fill = segEnd > segStart ? Mathf.Clamp01((float)(lifetimeValue - segStart) / (segEnd - segStart)) : 1f;
        return (fill, currentTierIndex, nextTierIndex);
    }

    private void ApplyVisual(int lifetimeValue, DreamEnergyRewardTrack.Tier[] tiers)
    {
        var (fill, _, nextTierIndex) = Evaluate(lifetimeValue, tiers);

        if (fillImage != null) fillImage.fillAmount = fill;
        if (dreamEnergyLabel != null) dreamEnergyLabel.text = CurrencyFormat.Format(lifetimeValue);

        bool hasNext = nextTierIndex >= 0;
        if (nextItemRoot != null) nextItemRoot.SetActive(hasNext);
        if (hasNext && nextItemIcon != null && tiers[nextTierIndex].item != null)
            nextItemIcon.sprite = tiers[nextTierIndex].item.thumbnail;
    }

    private IEnumerator Co_AnimateFill(int from, int to, DreamEnergyRewardTrack.Tier[] tiers)
    {
        var (_, fromTierIndex, _) = Evaluate(from, tiers);

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

        var (_, toTierIndex, _) = Evaluate(to, tiers);
        if (toTierIndex > fromTierIndex)
        {
            yield return Co_PopNextItem();
            UpdateBadge(tiers, to);
        }

        PlayerPrefs.SetInt(LastShownKey, to);
        PlayerPrefs.Save();
    }

    private IEnumerator Co_PopNextItem()
    {
        if (nextItemIcon == null) yield break;

        Transform iconT     = nextItemIcon.transform;
        Vector3   baseScale = iconT.localScale;

        float t = 0f;
        while (t < itemPopDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / itemPopDuration);
            float s = 1f + Mathf.Sin(p * Mathf.PI) * (itemPopScale - 1f); // hoch und wieder zurück auf 1
            iconT.localScale = baseScale * s;
            yield return null;
        }
        iconT.localScale = baseScale;
    }

    private void UpdateBadge(DreamEnergyRewardTrack.Tier[] tiers, int lifetime)
    {
        var (_, currentTierIndex, _) = Evaluate(lifetime, tiers);
        int ackTierIndex = PlayerPrefs.GetInt(BadgeAckTierKey, -1);
        if (badgeRoot != null) badgeRoot.SetActive(currentTierIndex > ackTierIndex);
    }

    /// <summary>Auf den Button-OnClick verdrahtet (öffnet den Shop) — markiert den aktuell
    /// erreichten Tier als gesehen, damit das Badge verschwindet.</summary>
    public void AcknowledgeBadge()
    {
        if (rewardTrack == null) return;
        var tiers = rewardTrack.SortedTiers();
        var (_, currentTierIndex, _) = Evaluate(DreamEnergyManager.LifetimeEarned, tiers);

        PlayerPrefs.SetInt(BadgeAckTierKey, currentTierIndex);
        PlayerPrefs.Save();
        if (badgeRoot != null) badgeRoot.SetActive(false);
    }

    // Currency-Tiers (Diamonds/Splinters) schreiben das Guthaben direkt gut statt das Item über
    // ShopInventory.ClaimFree als "besessen" zu markieren — exakt derselbe Verteil-Switch wie
    // ShopInventory.TryExchangeForCurrency.
    private static void GrantCurrency(ShopItem item)
    {
        switch (item.currencyKind)
        {
            case CurrencyRewardKind.Diamonds:         DiamondManager.AddDiamonds(item.rewardAmount); break;
            case CurrencyRewardKind.DiamondSplinters: DiamondSplinterManager.AddSplinters(item.rewardAmount); break;
        }
    }
}
