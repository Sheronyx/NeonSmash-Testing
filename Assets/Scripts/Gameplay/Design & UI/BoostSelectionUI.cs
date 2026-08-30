using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Vollbild-Auswahl zu Session-Beginn (Infinity Mode, nach der Feen-Ankunft, siehe
// GameStartCoordinator.HandleCountdownFinished): der Spieler wählt eine von vier Boost-Karten, bevor
// der Spawner startet. Animations-/Struktur-Muster identisch zu ShopController (CanvasGroup +
// Co_Open/Co_Close, Pop-Scale + Fade über unscaled Time), aber ohne Tabs/Owned-Zustand — die 4 Karten
// sind fix und werden einmalig aus dem Inspector-Array befüllt.
public class BoostSelectionUI : MonoBehaviour
{
    public static BoostSelectionUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private CanvasGroup panel;

    [Header("Karten")]
    [SerializeField] private Transform      cardParent;
    [SerializeField] private BoostCardUI    cardPrefab;
    [SerializeField] private BoostDefinition[] boosts;

    [Header("Skip (ohne Boost spielen)")]
    [SerializeField] private Button skipButton;

    [Header("Animation")]
    [SerializeField] private float popInDuration  = 0.28f;
    [SerializeField] private float popOutDuration = 0.2f;

    private Action<BoostType> _onChosen;
    private readonly System.Collections.Generic.Dictionary<BoostType, BoostCardUI> _cardsByType = new();

    private void Awake()
    {
        Instance = this;

        // GameObject bleibt durchgehend aktiv (Panel und Script sitzen bei uns auf demselben
        // Objekt) — sonst könnte Show() später keine Coroutine mehr starten. "Unsichtbar/nicht
        // klickbar" wird stattdessen rein über Alpha + Interactable/BlocksRaycasts erreicht.
        if (panel != null)
        {
            panel.alpha          = 0f;
            panel.interactable   = false;
            panel.blocksRaycasts = false;
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => Choose(BoostType.None));
        }

        PopulateCards();
    }

    private void PopulateCards()
    {
        if (cardPrefab == null || cardParent == null || boosts == null) return;

        foreach (Transform child in cardParent)
            Destroy(child.gameObject);
        _cardsByType.Clear();

        foreach (var boost in boosts)
        {
            if (boost == null) continue;
            var card = Instantiate(cardPrefab, cardParent);
            card.Bind(boost, BoosterInventoryManager.GetCount(boost.type.ToString()), OnCardPicked);
            _cardsByType[boost.type] = card;
        }
    }

    private void OnEnable()  => BoosterInventoryManager.OnCountChanged += HandleCountChanged;
    private void OnDisable() => BoosterInventoryManager.OnCountChanged -= HandleCountChanged;

    // rewardId der geänderten Booster-Karte entspricht BoostType.ToString() (siehe PopulateCards) —
    // nur die betroffene Karte aktualisieren, nicht alle neu binden (kein Flacker).
    private void HandleCountChanged(string rewardId, int newCount)
    {
        foreach (var kv in _cardsByType)
            if (kv.Key.ToString() == rewardId) { kv.Value.SetCount(newCount); break; }
    }

    public void Show(Action<BoostType> onChosen)
    {
        _onChosen = onChosen;
        PopulateCards(); // frische Stückzahlen bei jedem Öffnen (z.B. nach Shop-Kauf)
        StartCoroutine(Co_Open());
    }

    private void OnCardPicked(BoostDefinition def) => Choose(def.type);

    private void Choose(BoostType type)
    {
        // Echte Boosts (nicht "None"/Skip) verbrauchen beim Auswählen ein Stück aus dem Inventar —
        // schlägt der Verbrauch fehl (z.B. 0 übrig, Button hätte eigentlich deaktiviert sein sollen),
        // wird die Auswahl abgebrochen und die Karte bleibt offen.
        if (type != BoostType.None && !BoosterInventoryManager.TryConsume(type.ToString()))
            return;

        if (BoostManager.Instance != null) BoostManager.Instance.Select(type);
        var callback = _onChosen;
        _onChosen = null;
        StartCoroutine(Co_Close(() => callback?.Invoke(type)));
    }

    // ── Animation (1:1 Muster wie ShopController.Co_Open/Co_Close) ─────────────

    private IEnumerator Co_Open()
    {
        if (panel != null) panel.alpha = 0f;
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (rt != null) rt.localScale = Vector3.one * 0.85f;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popInDuration));
            if (panel != null) panel.alpha   = p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, p);
            yield return null;
        }
        if (panel != null)
        {
            panel.alpha          = 1f;
            panel.interactable   = true;
            panel.blocksRaycasts = true;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private IEnumerator Co_Close(Action onClosed)
    {
        if (panel != null)
        {
            panel.interactable   = false;
            panel.blocksRaycasts = false;
        }
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;

        float t = 0f;
        while (t < popOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popOutDuration));
            if (panel != null) panel.alpha   = 1f - p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.85f, p);
            yield return null;
        }
        if (panel != null) panel.alpha = 0f;
        onClosed?.Invoke();
    }
}
