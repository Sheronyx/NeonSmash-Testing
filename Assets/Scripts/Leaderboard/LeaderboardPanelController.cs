using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Leaderboards.Models;

public class LeaderboardPanelController : MonoBehaviour
{
    private bool startAroundPlayer = true;

    [Header("Leaderboard")]
    [SerializeField] private string leaderboardId = LeaderboardApi.InfinityId;

    [Header("Wiring")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Mode Buttons")]
    [SerializeField] private Button infinityModeButton;
    [SerializeField] private Button multiplayerModeButton;

    [Header("Button Sprites")]
    [SerializeField] private Sprite infinityNormalSprite;
    [SerializeField] private Sprite infinityActiveSprite;
    [SerializeField] private Sprite multiplayerNormalSprite;
    [SerializeField] private Sprite multiplayerActiveSprite;

    [Header("List")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private LeaderboardEntryView entryPrefab;

    private readonly List<LeaderboardEntryView> pool = new();

    private LeaderboardEntry myEntryCache;
    private bool shouldScrollToPlayer = false;

    private readonly List<LeaderboardEntry> allEntries = new();

    private void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Close);

        if (infinityModeButton)
            infinityModeButton.onClick.AddListener(OnClickInfinityMode);

        if (multiplayerModeButton)
            multiplayerModeButton.onClick.AddListener(OnClickMultiplayerMode);

        panelRoot.SetActive(false);
        loadingOverlay.SetActive(false);

        UpdateModeButtons();
    }

    public void Open()
    {
        panelRoot.SetActive(true);
        SetLeaderboard(LeaderboardApi.InfinityId);
    }

    public void Close()
    {
        panelRoot.SetActive(false);
    }

    public void OnClickInfinityMode()
    {
        SetLeaderboard(LeaderboardApi.InfinityId);
    }

    public void OnClickMultiplayerMode()
    {
        SetLeaderboard(LeaderboardApi.MultiplayerId);
    }

    public void SetLeaderboard(string id)
    {
        shouldScrollToPlayer = true;

        leaderboardId = id;

        allEntries.Clear();
        ClearList();

        UpdateModeButtons();

        _ = RefreshAsync();
    }

    private void UpdateModeButtons()
    {
        if (!infinityModeButton) return;

        bool isMultiplayer = leaderboardId == LeaderboardApi.MultiplayerId;

        var infinityImage   = infinityModeButton?.GetComponent<Image>();
        var multiplayerImage = multiplayerModeButton?.GetComponent<Image>();

        if (infinityImage)
            infinityImage.sprite = !isMultiplayer ? infinityActiveSprite : infinityNormalSprite;

        if (multiplayerImage && multiplayerNormalSprite && multiplayerActiveSprite)
            multiplayerImage.sprite = isMultiplayer ? multiplayerActiveSprite : multiplayerNormalSprite;
    }

    // ------------------------------------------------------------
    // LOAD EVERYTHING
    // ------------------------------------------------------------

    private async Task RefreshAsync()
    {
        loadingOverlay.SetActive(true);

        // 🔥 Scroll temporär deaktivieren
        scrollRect.enabled = false;

        bool online = await UgsBootstrap.Initialization;

        if (!online)
        {
            ClearList();
            loadingOverlay.SetActive(false);
            scrollRect.enabled = true;
            return;
        }

        myEntryCache = await LeaderboardApi.GetMyScoreAsync(leaderboardId);

        allEntries.Clear();

        int offset = 0;
        const int batchSize = 50;

        while (true)
        {
            var page = await LeaderboardApi.GetScoresAsync(batchSize, offset, leaderboardId);

            if (page == null || page.Results == null || page.Results.Count == 0)
                break;

            foreach (var e in page.Results)
            {
                if (!allEntries.Exists(x => x.PlayerId == e.PlayerId))
                    allEntries.Add(e);
            }

            offset += page.Results.Count;

            if (page.Results.Count < batchSize)
                break;
        }

        RenderList();

        await Task.Yield();
        Canvas.ForceUpdateCanvases();

        // 🔥 WICHTIG: erst wieder aktivieren
        scrollRect.enabled = true;

        // 🔥 DANN scrollen (Fix!)
        if (shouldScrollToPlayer)
        {
            shouldScrollToPlayer = false;
            StartCoroutine(Co_ScrollToPlayer());
        }

        loadingOverlay.SetActive(false);
    }

    // ------------------------------------------------------------
    // RENDER
    // ------------------------------------------------------------

    private void RenderList()
    {
        ClearList();

        allEntries.Sort((a, b) => a.Rank.CompareTo(b.Rank));

        foreach (var e in allEntries)
        {
            var item = GetOrCreate();

            bool isMe = (!string.IsNullOrEmpty(myEntryCache?.PlayerId) &&
                         e.PlayerId == myEntryCache.PlayerId);

            string displayName = CleanDisplayName(e.PlayerName, e.PlayerId);

            item.Bind(e.Rank + 1, displayName, e.Score, isMe);
            item.gameObject.SetActive(true);
        }
    }

    // ------------------------------------------------------------
    // SCROLL TO PLAYER
    // ------------------------------------------------------------

private void ScrollToPlayer()
{
    if (myEntryCache == null) return;

    foreach (var entry in pool)
    {
        if (!entry.IsPlayer) continue;

        RectTransform item = entry.GetComponent<RectTransform>();

        float contentHeight = contentRoot.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;

        // Position des Items im Content
        float itemY = Mathf.Abs(item.anchoredPosition.y);

        // 🔥 Ziel: Item in die Mitte bringen
        float targetY = itemY - (viewportHeight / 2f);

        // 🔥 Clamp → verhindert Überscrollen oben/unten
        float maxScroll = contentHeight - viewportHeight;
        targetY = Mathf.Clamp(targetY, 0f, maxScroll);

        float normalized = (maxScroll <= 0f) ? 1f : targetY / maxScroll;

        scrollRect.verticalNormalizedPosition = 1f - normalized;

        break;
    }
}

private System.Collections.IEnumerator Co_ScrollToPlayer()
{
    // 🔥 mehrere Frames stabilisieren
    for (int i = 0; i < 5; i++)
    {
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        ScrollToPlayer();
    }
}

    // ------------------------------------------------------------
    // POOL
    // ------------------------------------------------------------

    private void ClearList()
    {
        foreach (var v in pool)
            v.gameObject.SetActive(false);
    }

    private LeaderboardEntryView GetOrCreate()
    {
        foreach (var v in pool)
            if (!v.gameObject.activeSelf)
                return v;

        var inst = Instantiate(entryPrefab, contentRoot, false);

        var rt = (RectTransform)inst.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.localScale = Vector3.one;

        pool.Add(inst);
        return inst;
    }

    private static string CleanDisplayName(string serverName, string playerIdFallback)
    {
        if (!string.IsNullOrWhiteSpace(serverName))
        {
            int hash = serverName.IndexOf('#');
            var clean = (hash > 0) ? serverName.Substring(0, hash) : serverName;
            clean = clean.Trim();

            if (!string.IsNullOrEmpty(clean))
                return clean;
        }

        if (!string.IsNullOrEmpty(playerIdFallback))
            return playerIdFallback.Length > 8
                ? playerIdFallback.Substring(0, 8)
                : playerIdFallback;

        return "Player";
    }
}