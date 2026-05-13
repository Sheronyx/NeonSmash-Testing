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

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        panel.SetActive(false);
    }

    void OnEnable()  => FriendsHandler.OnChallengeReceived += Show;
    void OnDisable() => FriendsHandler.OnChallengeReceived -= Show;

    void Start()
    {
        acceptButton.onClick.AddListener(OnAccept);
        declineButton.onClick.AddListener(OnDecline);
    }

    void Show(string lobbyCode, string senderName)
    {
        _pendingLobbyCode    = lobbyCode;
        challengerText.text  = $"{senderName} fordert dich heraus!";
        panel.SetActive(true);
    }

    async void OnAccept()
    {
        panel.SetActive(false);
        if (string.IsNullOrEmpty(_pendingLobbyCode)) return;

        try
        {
            await MultiplayerManager.Instance.JoinPrivateAsync(_pendingLobbyCode);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Challenge] Beitreten fehlgeschlagen: {e.Message}");
        }
    }

    void OnDecline()
    {
        _pendingLobbyCode = null;
        panel.SetActive(false);
    }
}
