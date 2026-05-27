using System.Collections;
using TMPro;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UI;

public class FriendsScreen : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject addFriendPanel;
    [SerializeField] GameObject challengingPanel;

    [Header("Main Panel")]
    [SerializeField] Transform       friendsListContent;
    [SerializeField] Transform       requestsListContent;
    [SerializeField] GameObject      requestsSection;
    [SerializeField] Button          addFriendButton;
    [SerializeField] Button          backButton;
    [SerializeField] TextMeshProUGUI myNameText;
    [SerializeField] Button          copyMyNameButton;

    [Header("Add Friend Panel")]
    [SerializeField] TMP_InputField nameInputField;
    [SerializeField] Button         sendRequestButton;
    [SerializeField] Button         addFriendBackButton;
    [SerializeField] TextMeshProUGUI addFriendStatusText;

    [Header("Challenging Panel")]
    [SerializeField] TextMeshProUGUI challengingText;
    [SerializeField] Button          cancelChallengeButton;

    [Header("Prefabs")]
    [SerializeField] FriendRowView        friendRowPrefab;
    [SerializeField] FriendRequestRowView requestRowPrefab;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void OnEnable()
    {
        FriendsHandler.OnRelationshipsChanged += RefreshList;
        MultiplayerManager.OnOpponentConnected += OnOpponentConnected;
    }

    void OnDisable()
    {
        FriendsHandler.OnRelationshipsChanged  -= RefreshList;
        MultiplayerManager.OnOpponentConnected -= OnOpponentConnected;
    }

    async void Start()
    {
        addFriendButton.onClick.AddListener(() => ShowPanel(addFriendPanel));
        backButton.onClick.AddListener(OnBack);
        addFriendBackButton.onClick.AddListener(() => ShowPanel(mainPanel));
        sendRequestButton.onClick.AddListener(OnSendRequest);
        cancelChallengeButton.onClick.AddListener(OnCancelChallenge);

        if (copyMyNameButton) copyMyNameButton.onClick.AddListener(OnCopyMyName);

        ShowPanel(mainPanel);

        Debug.Log($"[DIAG] FriendsScreen.Start — IsInitialized={FriendsHandler.IsInitialized}");

        if (!FriendsHandler.IsInitialized)
        {
            UgsBootstrap.Begin();
            // Wait until platform auth + Friends SDK init are both done (end of PlatformNameCoroutine)
            bool ready = await UgsBootstrap.FriendsReady;
            Debug.Log($"[DIAG] FriendsReady aufgelöst — ready={ready}");
            if (!ready) { Debug.LogWarning("[DIAG] FriendsScreen: ready=false → kein Friends-Screen"); return; }
        }
        else
        {
            // Pull fresh presence data from the server each time the screen opens.
            Debug.Log("[DIAG] FriendsScreen: bereits initialisiert → ForceRefreshAsync");
            _ = FriendsHandler.ForceRefreshAsync();
        }

        var fullName = Unity.Services.Authentication.AuthenticationService.Instance?.PlayerName;
        if (myNameText) myNameText.text = string.IsNullOrEmpty(fullName) ? "—" : fullName;

        Debug.Log($"[DIAG] FriendsScreen.Start: RefreshList (erster Aufruf, mögl. cached)");
        RefreshList();
    }

    // ── List ─────────────────────────────────────────────────────────────────

    void RefreshList()
    {
        var friends = FriendsHandler.Friends;
        Debug.Log($"[DIAG] RefreshList — {friends.Count} Freund(e):");
        foreach (var r in friends)
        {
            string presence = r.Member.Presence?.Availability.ToString() ?? "null";
            string name     = r.Member.Profile?.Name ?? "?";
            Debug.Log($"[DIAG]   Id={r.Member.Id}, Name={name}, Presence={presence}");
        }

        ClearChildren(friendsListContent);
        ClearChildren(requestsListContent);

        foreach (var r in friends)
        {
            var row = Instantiate(friendRowPrefab, friendsListContent);
            string name     = CleanName(r.Member.Profile?.Name, r.Member.Id);
            bool   isOnline = r.Member.Presence?.Availability == Unity.Services.Friends.Models.Availability.Online;
            row.Bind(r.Member.Id, name, isOnline, OnChallengeFriend, OnRemoveFriend);
        }

        bool hasRequests = FriendsHandler.IncomingRequests.Count > 0;
        requestsSection.SetActive(hasRequests);

        foreach (var r in FriendsHandler.IncomingRequests)
        {
            var row = Instantiate(requestRowPrefab, requestsListContent);
            string name = CleanName(r.Member.Profile?.Name, r.Member.Id);
            row.Bind(r.Member.Id, name, OnAcceptRequest, OnDeclineRequest);
        }
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // ── Button Handler ───────────────────────────────────────────────────────

    void OnBack()
    {
        gameObject.SetActive(false);
    }

    void OnCopyMyName()
    {
        var fullName = Unity.Services.Authentication.AuthenticationService.Instance?.PlayerName;
        if (!string.IsNullOrEmpty(fullName))
            GUIUtility.systemCopyBuffer = fullName;
    }

    async void OnSendRequest()
    {
        string name = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        sendRequestButton.interactable = false;
        addFriendStatusText.text = "Sende Anfrage...";

        try
        {
            await FriendsHandler.AddFriendByNameAsync(name);
            addFriendStatusText.text = $"Anfrage an {name} gesendet!";
            nameInputField.text = "";
        }
        catch (System.Exception e)
        {
            addFriendStatusText.text = $"Fehler: {e.Message}";
        }
        finally
        {
            sendRequestButton.interactable = true;
        }
    }

    async void OnAcceptRequest(string memberId)
    {
        try { await FriendsHandler.AcceptRequestAsync(memberId); }
        catch (System.Exception e) { Debug.LogWarning($"[Friends] Accept fehlgeschlagen: {e.Message}"); }
    }

    async void OnDeclineRequest(string memberId)
    {
        try { await FriendsHandler.DeclineRequestAsync(memberId); }
        catch (System.Exception e) { Debug.LogWarning($"[Friends] Decline fehlgeschlagen: {e.Message}"); }
    }

    async void OnChallengeFriend(string memberId)
    {
        var friendName = GetFriendName(memberId);

        bool isOnline = false;
        foreach (var r in FriendsHandler.Friends)
            if (r.Member.Id == memberId)
            {
                isOnline = r.Member.Presence?.Availability == Unity.Services.Friends.Models.Availability.Online;
                break;
            }

        Debug.Log($"[DIAG] OnChallengeFriend — memberId={memberId}, isOnline={isOnline}");
        if (!isOnline)
        {
            challengingText.text = $"Push an {friendName} gesendet...";
            ShowPanel(challengingPanel);
            try { await FriendsHandler.SendNudgePushAsync(memberId); }
            catch (System.Exception e) { Debug.LogWarning($"[Friends] Nudge fehlgeschlagen: {e.Message}"); }
            await System.Threading.Tasks.Task.Delay(2000);
            ShowPanel(mainPanel);
            return;
        }

        challengingText.text = $"Warte auf {friendName}...";
        ShowPanel(challengingPanel);
        try
        {
            await MultiplayerManager.Instance.HostPrivateAsync();
            var code = MultiplayerManager.Instance.PrivateLobbyCode;
            await FriendsHandler.SendChallengeAsync(memberId, code);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Friends] Challenge fehlgeschlagen: {e.Message}");
            MultiplayerManager.Instance.Disconnect();
            ShowPanel(mainPanel);
        }
    }

    async void OnRemoveFriend(string memberId)
    {
        try { await FriendsHandler.DeleteFriendAsync(memberId); }
        catch (System.Exception e) { Debug.LogWarning($"[Friends] Remove fehlgeschlagen: {e.Message}"); }
    }

    void OnCancelChallenge()
    {
        MultiplayerManager.Instance.Disconnect();
        ShowPanel(mainPanel);
    }

    void OnOpponentConnected()
    {
        // Nur der Host (Herausforderer) startet hier – der Acceptor nutzt ChallengeNotification
        if (!Unity.Netcode.NetworkManager.Singleton.IsHost) return;
        if (mainPanel.transform.parent.gameObject.activeSelf)
            StartCoroutine(CountdownAndStart());
    }

    IEnumerator CountdownAndStart()
    {
        ShowPanel(challengingPanel);
        foreach (var step in new[] { "3", "2", "1", "GO!" })
        {
            challengingText.text = step;
            yield return new WaitForSecondsRealtime(1f);
        }

        MultiplayerManager.IsMultiplayerGame = true;
        GlobalGameManager.Instance?.SetMode(GameMode.Multiplayer);
        SceneFader.Instance.LoadScene("GameScene_InfinityMode");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    void ShowPanel(GameObject active)
    {
        mainPanel.SetActive(active == mainPanel);
        addFriendPanel.SetActive(active == addFriendPanel);
        challengingPanel.SetActive(active == challengingPanel);
    }

    string GetFriendName(string memberId)
    {
        foreach (var r in FriendsHandler.Friends)
            if (r.Member.Id == memberId)
                return CleanName(r.Member.Profile?.Name, memberId);
        return memberId;
    }

    static string CleanName(string serverName, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(serverName))
        {
            int hash = serverName.IndexOf('#');
            var clean = (hash > 0) ? serverName.Substring(0, hash) : serverName;
            if (!string.IsNullOrEmpty(clean.Trim())) return clean.Trim();
        }
        return fallback.Length > 8 ? fallback.Substring(0, 8) : fallback;
    }
}
