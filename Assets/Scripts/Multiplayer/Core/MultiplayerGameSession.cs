using System;
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

    public static event Action<int, int> OnScoresUpdated; // local, opponent
    public static event Action<bool>     OnGameOver;      // true = won

    private int  _lastScore;
    private bool _gameEnded;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        _hostScore.OnValueChanged   += (_, _) => FireUpdate();
        _clientScore.OnValueChanged += (_, _) => FireUpdate();
        MultiplayerManager.OnOpponentDisconnected += OnOpponentLeft;
    }

    public override void OnNetworkDespawn()
    {
        MultiplayerManager.OnOpponentDisconnected -= OnOpponentLeft;
        if (Instance == this) Instance = null;
    }

    void OnOpponentLeft()
    {
        if (!_gameEnded) EndGame(true);
    }

    void Update()
    {
        if (!IsSpawned || _gameEnded || ScoreManager.Instance == null) return;

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
        OnGameOver?.Invoke(won);

        try
        {
            await LeaderboardApi.SubmitScoreAsync(won ? 1 : -1, LeaderboardApi.MultiplayerId);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MP] Ranking-Upload fehlgeschlagen: {e.Message}");
        }
    }
}
