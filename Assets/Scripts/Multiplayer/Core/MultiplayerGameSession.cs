using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class MultiplayerGameSession : NetworkBehaviour
{
    public static MultiplayerGameSession Instance { get; private set; }

    [SerializeField] int winThreshold = 30;

    private NetworkVariable<int> _hostScore = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _clientScore = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> _hostReady = new(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> _clientReady = new(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<FixedString64Bytes> _hostName = new("",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<FixedString64Bytes> _clientName = new("",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public static event Action<int, int>    OnScoresUpdated; // local, opponent
    public static event Action<bool, string> OnGameOver;     // won, winnerName
    public static event Action              OnGameStarted;
    public static bool                      IsGameStarted { get; private set; }

    private int  _lastScore;
    private bool _gameEnded;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        IsGameStarted = false;

        _hostScore.OnValueChanged   += (_, _) => FireUpdate();
        _clientScore.OnValueChanged += (_, _) => FireUpdate();
        _hostReady.OnValueChanged   += (_, _) => TryStartGame();
        _clientReady.OnValueChanged += (_, _) => TryStartGame();
        MultiplayerManager.OnOpponentDisconnected += OnOpponentLeft;

        string localName = CleanName(LeaderboardApi.GetLocalDisplayName());
        if (IsHost)
        {
            _hostName.Value = new FixedString64Bytes(localName);
            _hostReady.Value = true;
        }
        else
        {
            SetClientNameServerRpc(new FixedString64Bytes(localName));
            SignalReadyServerRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        MultiplayerManager.OnOpponentDisconnected -= OnOpponentLeft;
        if (Instance == this) Instance = null;
    }

    [ServerRpc(RequireOwnership = false)]
    void SetClientNameServerRpc(FixedString64Bytes name) => _clientName.Value = name;

    [ServerRpc(RequireOwnership = false)]
    void SignalReadyServerRpc() => _clientReady.Value = true;

    void TryStartGame()
    {
        if (IsGameStarted || !_hostReady.Value || !_clientReady.Value) return;
        if (IsServer) StartGameClientRpc();
    }

    [ClientRpc]
    void StartGameClientRpc()
    {
        if (IsGameStarted) return;
        IsGameStarted = true;
        Debug.Log("[MP] Beide Spieler bereit – Spiel startet.");
        OnGameStarted?.Invoke();
    }

    void OnOpponentLeft()
    {
        if (!_gameEnded) EndGame(true);
    }

    void Update()
    {
        if (!IsSpawned || !IsGameStarted || _gameEnded || ScoreManager.Instance == null) return;

        int current = ScoreManager.Instance.CurrentScore;
        if (current == _lastScore) return;
        _lastScore = current;

        if (IsHost)
            _hostScore.Value = current;
        else
            SyncScoreServerRpc(current);
    }

    [ServerRpc(RequireOwnership = false)]
    void SyncScoreServerRpc(int score) => _clientScore.Value = score;

    public void DeclareLocalPlayerLost()
    {
        if (_gameEnded) return;
        DeclareLoserServerRpc(IsHost);
    }

    [ServerRpc(RequireOwnership = false)]
    void DeclareLoserServerRpc(bool hostLost) => EndGameClientRpc(hostLost);

    [ClientRpc]
    void EndGameClientRpc(bool hostLost)
    {
        bool iWon = IsHost ? !hostLost : hostLost;
        EndGame(iWon);
    }

    void FireUpdate()
    {
        if (_gameEnded) return;

        int local    = IsHost ? _hostScore.Value : _clientScore.Value;
        int opponent = IsHost ? _clientScore.Value : _hostScore.Value;

        OnScoresUpdated?.Invoke(local, opponent);

        int diff = local - opponent;
        if      (diff >=  winThreshold) EndGame(true);
        else if (diff <= -winThreshold) EndGame(false);
    }

    async void EndGame(bool won)
    {
        _gameEnded = true;
        MixedPointSpawner.Instance?.StopImmediate();

        string winnerName = won
            ? (IsHost ? _hostName.Value.ToString() : _clientName.Value.ToString())
            : (IsHost ? _clientName.Value.ToString() : _hostName.Value.ToString());

        OnGameOver?.Invoke(won, winnerName);

        try
        {
            await LeaderboardApi.SubmitScoreAsync(won ? 1 : -1, LeaderboardApi.MultiplayerId);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MP] Ranking-Upload fehlgeschlagen: {e.Message}");
        }
    }

    static string CleanName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Player";
        int hash = raw.IndexOf('#');
        string clean = hash > 0 ? raw.Substring(0, hash) : raw;
        return string.IsNullOrWhiteSpace(clean) ? "Player" : clean.Trim();
    }
}
