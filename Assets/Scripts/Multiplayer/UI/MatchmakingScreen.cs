using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchmakingScreen : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject searchingPanel;
    [SerializeField] GameObject waitingPanel;
    [SerializeField] GameObject countdownPanel;

    [Header("Main Panel")]
    [SerializeField] Button quickMatchButton;
    [SerializeField] Button privateHostButton;
    [SerializeField] TMP_InputField codeInputField;
    [SerializeField] Button joinByCodeButton;
    [SerializeField] Button backButton;

    [Header("Searching Panel")]
    [SerializeField] TextMeshProUGUI searchingText;
    [SerializeField] Button cancelSearchButton;

    [Header("Waiting Panel (privat)")]
    [SerializeField] TextMeshProUGUI lobbyCodeText;
    [SerializeField] Button copyCodeButton;
    [SerializeField] Button cancelWaitButton;

    [Header("Countdown Panel")]
    [SerializeField] TextMeshProUGUI countdownText;

    void OnEnable()
    {
        MultiplayerManager.OnStateChanged         += OnStateChanged;
        MultiplayerManager.OnOpponentConnected    += OnOpponentConnected;
        MultiplayerManager.OnOpponentDisconnected += OnOpponentDisconnected;
    }

    void OnDisable()
    {
        MultiplayerManager.OnStateChanged         -= OnStateChanged;
        MultiplayerManager.OnOpponentConnected    -= OnOpponentConnected;
        MultiplayerManager.OnOpponentDisconnected -= OnOpponentDisconnected;
    }

    void Start()
    {
        quickMatchButton.onClick.AddListener(OnQuickMatch);
        privateHostButton.onClick.AddListener(OnPrivateHost);
        joinByCodeButton.onClick.AddListener(OnJoinByCode);
        backButton.onClick.AddListener(OnBack);
        cancelSearchButton.onClick.AddListener(OnCancel);
        cancelWaitButton.onClick.AddListener(OnCancel);
        copyCodeButton.onClick.AddListener(OnCopyCode);

        ShowPanel(mainPanel);
    }

    // ── Button Handler ───────────────────────────────────────────────────────

    void OnQuickMatch()
    {
        searchingText.text = "Suche Gegner...";
        ShowPanel(searchingPanel);
        _ = MultiplayerManager.Instance.SmartQuickMatchAsync();
    }

    void OnPrivateHost()
    {
        lobbyCodeText.text = "Code wird erstellt...";
        ShowPanel(waitingPanel);
        _ = MultiplayerManager.Instance.HostPrivateAsync();
    }

    void OnJoinByCode()
    {
        string code = codeInputField.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code)) return;

        searchingText.text = $"Verbinde mit {code}...";
        ShowPanel(searchingPanel);
        _ = MultiplayerManager.Instance.JoinPrivateAsync(code);
    }

    void OnBack()
    {
        gameObject.SetActive(false);
    }

    void OnCancel()
    {
        StopAllCoroutines();
        MultiplayerManager.Instance.Disconnect();
        ShowPanel(mainPanel);
    }

    void OnCopyCode()
    {
        var code = MultiplayerManager.Instance.PrivateLobbyCode;
        if (!string.IsNullOrEmpty(code))
            GUIUtility.systemCopyBuffer = code;
    }

    // ── Events ───────────────────────────────────────────────────────────────

    void OnStateChanged(MultiplayerManager.State state)
    {
        if (state == MultiplayerManager.State.Hosting)
        {
            var code = MultiplayerManager.Instance.PrivateLobbyCode;
            lobbyCodeText.text = string.IsNullOrEmpty(code) ? "Warte auf Code..." : $"Code: {code}";
        }

        if (state == MultiplayerManager.State.Disconnected)
        {
            StopAllCoroutines();
            ShowPanel(mainPanel);
        }
    }

    void OnOpponentConnected()
    {
        StartCoroutine(CountdownRoutine());
    }

    void OnOpponentDisconnected()
    {
        StopAllCoroutines();
        ShowPanel(mainPanel);
    }

    // ── Countdown ────────────────────────────────────────────────────────────

    IEnumerator CountdownRoutine()
    {
        ShowPanel(countdownPanel);

        foreach (var step in new[] { "3", "2", "1", "GO!" })
        {
            countdownText.text = step;
            yield return new WaitForSecondsRealtime(1f);
        }

        StartGame();
    }

    void StartGame()
    {
        MultiplayerManager.IsMultiplayerGame = true;
        GlobalGameManager.Instance?.SetMode(GameMode.Multiplayer);
        SceneFader.Instance.LoadScene("GameScene_InfinityMode");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    void ShowPanel(GameObject active)
    {
        mainPanel.SetActive(active == mainPanel);
        searchingPanel.SetActive(active == searchingPanel);
        waitingPanel.SetActive(active == waitingPanel);
        countdownPanel.SetActive(active == countdownPanel);
    }
}
