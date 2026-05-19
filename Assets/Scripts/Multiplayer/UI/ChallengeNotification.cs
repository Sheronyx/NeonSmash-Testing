using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChallengeNotification : MonoBehaviour
{
    public static ChallengeNotification Instance { get; private set; }

    [SerializeField] GameObject          panel;
    [SerializeField] TextMeshProUGUI     challengerText;
    [SerializeField] Button              acceptButton;
    [SerializeField] Button              declineButton;

    string _pendingLobbyCode;
    bool   _isAcceptor;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        panel.SetActive(false);
    }

    void OnEnable()
    {
        FriendsHandler.OnChallengeReceived         += Show;
        MultiplayerManager.OnOpponentConnected     += OnConnected;
        MultiplayerManager.OnOpponentDisconnected  += OnDisconnected;
    }

    void OnDisable()
    {
        FriendsHandler.OnChallengeReceived         -= Show;
        MultiplayerManager.OnOpponentConnected     -= OnConnected;
        MultiplayerManager.OnOpponentDisconnected  -= OnDisconnected;
    }

    void Start()
    {
        acceptButton.onClick.AddListener(OnAccept);
        declineButton.onClick.AddListener(OnDecline);
    }

    void Show(string lobbyCode, string senderName)
    {
        _pendingLobbyCode    = lobbyCode;
        _isAcceptor          = false;
        challengerText.text  = $"{senderName} challenges you!";
        acceptButton.gameObject.SetActive(true);
        declineButton.gameObject.SetActive(true);
        panel.SetActive(true);
    }

    async void OnAccept()
    {
        if (string.IsNullOrEmpty(_pendingLobbyCode)) return;

        panel.SetActive(false);
        _isAcceptor = true;

        string code = _pendingLobbyCode;
        _pendingLobbyCode = null;

        try
        {
            await MultiplayerManager.Instance.JoinPrivateAsync(code);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Challenge] Beitreten fehlgeschlagen: {e.Message}");
            _isAcceptor = false;
        }
    }

    void OnDecline()
    {
        _pendingLobbyCode = null;
        _isAcceptor       = false;
        panel.SetActive(false);
    }

    // Feuert auf dem Client (Acceptor) wenn er erfolgreich mit dem Host verbunden ist
    void OnConnected()
    {
        if (!_isAcceptor) return;
        _isAcceptor = false;
        StartCoroutine(CountdownAndStart());
    }

    void OnDisconnected()
    {
        _isAcceptor = false;
        panel.SetActive(false);
        StopAllCoroutines();
    }

    IEnumerator CountdownAndStart()
    {
        acceptButton.gameObject.SetActive(false);
        declineButton.gameObject.SetActive(false);
        panel.SetActive(true);
        foreach (var step in new[] { "3", "2", "1", "GO!" })
        {
            challengerText.text = step;
            yield return new WaitForSecondsRealtime(1f);
        }
        panel.SetActive(false);

        MultiplayerManager.IsMultiplayerGame = true;
        GlobalGameManager.Instance?.SetMode(GameMode.Multiplayer);
        SceneFader.Instance.LoadScene("GameScene_InfinityMode");
    }
}
