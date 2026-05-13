using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Leaderboards.Models;

public class LeaderboardController : MonoBehaviour
{
    [Header("Leaderboard")]
    [SerializeField] private string leaderboardId = LeaderboardApi.TimeModeId;

    [Header("Wiring")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Mode Buttons")]
    [SerializeField] private Button timeModeButton;
    [SerializeField] private Button infinityModeButton;
    [SerializeField] private Button multiplayerModeButton;

    [Header("List")]
    [SerializeField] private LeaderboardEntryView entryPrefab;

    [SerializeField] private Image timeButtonBackground;
    [SerializeField] private Image timeButtonIcon;

    [SerializeField] private Image infinityButtonBackground;
    [SerializeField] private Image infinityButtonIcon;

    [SerializeField] private Image multiplayerButtonBackground;
    [SerializeField] private Image multiplayerButtonIcon;

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Material outlineDarkMaterial;

    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private readonly List<LeaderboardEntryView> pool = new();
    private readonly List<LeaderboardEntry> allEntries = new();

    private LeaderboardEntry myEntryCache;
    private bool shouldScrollToPlayer = true;

    // ------------------------------------------------------------
    // INIT
    // ------------------------------------------------------------

    private void Awake()
    {
        if (timeModeButton)
            timeModeButton.onClick.AddListener(() => SetLeaderboard(LeaderboardApi.TimeModeId));

        if (infinityModeButton)
            infinityModeButton.onClick.AddListener(() => SetLeaderboard(LeaderboardApi.InfinityId));

        if (multiplayerModeButton)
            multiplayerModeButton.onClick.AddListener(() => SetLeaderboard(LeaderboardApi.MultiplayerId));
    }

    private void Start()
    {
        panelRoot.SetActive(true);
        loadingOverlay.SetActive(true);

        StartCoroutine(DelayedOpen());
    }

    private IEnumerator DelayedOpen()
    {
        Debug.Log("⏳ Warte bis alles ready ist...");

        yield return null;
        yield return null;
        yield return null;

        yield return new WaitUntil(() => UgsBootstrap.IsReadyOnline);

        Debug.Log("✅ Alles ready → lade Leaderboard");

        Open();
    }

    public void Open()
    {
        Debug.Log("📂 Open Leaderboard");
        panelRoot.SetActive(true);
        SetLeaderboard(leaderboardId);
    }

    // ------------------------------------------------------------
    // SET MODE
    // ------------------------------------------------------------

    public void SetLeaderboard(string id)
    {
        leaderboardId = id;

        shouldScrollToPlayer = true;

        allEntries.Clear();
        ClearList();

        loadingOverlay.SetActive(true);

        // 🔥 NEU
        UpdateModeUI();

        _ = RefreshAsync();
    }

    // ------------------------------------------------------------
    // LOAD
    // ------------------------------------------------------------

    private async Task RefreshAsync()
    {
        Debug.Log("🔄 RefreshAsync gestartet");

        loadingOverlay.SetActive(true);

        // 🔥 sorgt dafür, dass Spinner sichtbar wird
        await Task.Yield();

        scrollRect.enabled = false;

        // ✅ Internet Check
        bool online = Application.internetReachability != NetworkReachability.NotReachable;
        Debug.Log("🌐 Online: " + online);

        if (!online)
        {
            Debug.Log("📴 Offline Mode → kein Leaderboard");

            loadingOverlay.SetActive(false);
            scrollRect.enabled = true;
            return;
        }

        // ------------------------------------------------------------
        // SAFE API CALLS
        // ------------------------------------------------------------

        try
        {
            myEntryCache = await LeaderboardApi.GetMyScoreAsync(leaderboardId);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("⚠️ GetMyScore failed: " + e.Message);

            loadingOverlay.SetActive(false);
            scrollRect.enabled = true;
            return;
        }

        allEntries.Clear();

        int offset = 0;
        const int batchSize = 50;

        while (true)
        {
            LeaderboardScoresPage page = null;

            try
            {
                page = await LeaderboardApi.GetScoresAsync(batchSize, offset, leaderboardId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("⚠️ GetScores failed: " + e.Message);
                break;
            }

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

        Debug.Log("✅ Gesamt Entries: " + allEntries.Count);

        RenderList();

        await Task.Yield();
        Canvas.ForceUpdateCanvases();

        scrollRect.enabled = true;

        // 🔥 Scroll zum Player
        if (shouldScrollToPlayer)
        {
            shouldScrollToPlayer = false;
            StartCoroutine(Co_ScrollToPlayer());
        }

        loadingOverlay.SetActive(false);
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

            float contentHeight = scrollRect.content.rect.height;
            float viewportHeight = scrollRect.viewport.rect.height;

            float itemY = Mathf.Abs(item.anchoredPosition.y);

            float targetY = itemY - (viewportHeight / 2f);

            float maxScroll = contentHeight - viewportHeight;
            targetY = Mathf.Clamp(targetY, 0f, maxScroll);

            float normalized = (maxScroll <= 0f) ? 1f : targetY / maxScroll;

            scrollRect.verticalNormalizedPosition = 1f - normalized;

            break;
        }
    }

    private IEnumerator Co_ScrollToPlayer()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            ScrollToPlayer();
        }
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

            string cleanName = CleanPlayerName(e.PlayerName);
            item.Bind(e.Rank + 1, cleanName, e.Score, isMe);
            item.gameObject.SetActive(true);
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

        var inst = Instantiate(entryPrefab, scrollRect.content, false);

        var rt = (RectTransform)inst.transform;
        rt.localScale = Vector3.one;

        pool.Add(inst);
        return inst;
    }

    private string CleanPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Player";

        int hashIndex = name.IndexOf('#');

        if (hashIndex > 0)
            return name.Substring(0, hashIndex);

        return name;
    }

    private void UpdateModeUI()
    {
        bool isTime       = leaderboardId == LeaderboardApi.TimeModeId;
        bool isMultiplayer = leaderboardId == LeaderboardApi.MultiplayerId;
        bool isInfinity   = !isTime && !isMultiplayer;

        if (timeButtonBackground != null)
            timeButtonBackground.material = isTime ? outlineMaterial : outlineDarkMaterial;
        if (timeButtonIcon != null)
            timeButtonIcon.color = isTime ? activeColor : inactiveColor;

        if (infinityButtonBackground != null)
            infinityButtonBackground.material = isInfinity ? outlineMaterial : outlineDarkMaterial;
        if (infinityButtonIcon != null)
            infinityButtonIcon.color = isInfinity ? activeColor : inactiveColor;

        if (multiplayerButtonBackground != null)
            multiplayerButtonBackground.material = isMultiplayer ? outlineMaterial : outlineDarkMaterial;
        if (multiplayerButtonIcon != null)
            multiplayerButtonIcon.color = isMultiplayer ? activeColor : inactiveColor;
    }
}