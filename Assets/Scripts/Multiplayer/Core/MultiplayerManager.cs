using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    public enum State { Idle, Hosting, Joining, Connected, Disconnected }
    public State CurrentState { get; private set; } = State.Idle;

    public string PrivateLobbyCode { get; private set; }

    public static bool IsMultiplayerGame { get; set; }

    public static event Action<State> OnStateChanged;
    public static event Action        OnOpponentConnected;
    public static event Action        OnOpponentDisconnected;

    private LobbyHandler _lobby;
    private bool _isHost;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (_isHost) _lobby?.TickHeartbeat(Time.deltaTime);
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task HostPublicAsync()
    {
        SetState(State.Hosting);
        _isHost = true;
        _lobby  = new LobbyHandler();
        try
        {
            await WaitForUgsAsync();
            var relayCode = await RelayHandler.CreateAsync();
            await _lobby.CreatePublicAsync(relayCode);
            StartHost();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MP] HostPublic fehlgeschlagen: {e.Message}");
            SetState(State.Disconnected);
        }
    }

    public async Task HostPrivateAsync()
    {
        SetState(State.Joining);
        _isHost = true;
        _lobby  = new LobbyHandler();
        try
        {
            await WaitForUgsAsync();
            var relayCode = await RelayHandler.CreateAsync();
            PrivateLobbyCode = await _lobby.CreatePrivateAsync(relayCode);
            Debug.Log($"[MP] Lobby-Code zum Teilen: {PrivateLobbyCode}");
            SetState(State.Hosting);
            StartHost();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MP] HostPrivate fehlgeschlagen: {e.Message}");
            SetState(State.Disconnected);
        }
    }

    public async Task QuickMatchAsync()
    {
        SetState(State.Joining);
        _isHost = false;
        _lobby  = new LobbyHandler();
        try
        {
            await WaitForUgsAsync();
            var relayCode = await _lobby.QuickJoinAsync();
            await RelayHandler.JoinAsync(relayCode);
            StartClient();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MP] QuickMatch fehlgeschlagen: {e.Message}");
            SetState(State.Disconnected);
        }
    }

    // Versucht erst zu joinen, hostet falls keine Lobby offen ist
    public async Task SmartQuickMatchAsync()
    {
        await WaitForUgsAsync();

        // 1) Versuch: sofort joinen
        if (await TryJoinPublicLobbyAsync()) return;

        // 2) Kurze Pause – gibt einer gerade erstellten Lobby Zeit sich zu propagieren
        //    (Race Condition: beide scheitern beim ersten Join und würden sonst beide hosten)
        await Task.Delay(UnityEngine.Random.Range(1500, 2500));

        if (CurrentState != State.Joining && CurrentState != State.Idle) return;

        // 3) Zweiter Versuch: joinen
        if (await TryJoinPublicLobbyAsync()) return;

        // 4) Fallback: Host werden und auf Gegner warten
        _isHost = true;
        _lobby  = new LobbyHandler();
        try
        {
            var relayCode = await RelayHandler.CreateAsync();
            await _lobby.CreatePublicAsync(relayCode);
            SetState(State.Hosting);
            StartHost();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MP] SmartQuickMatch Host fehlgeschlagen: {e.Message}");
            SetState(State.Disconnected);
        }
    }

    private async Task<bool> TryJoinPublicLobbyAsync()
    {
        try
        {
            SetState(State.Joining);
            _isHost = false;
            _lobby  = new LobbyHandler();
            var relayCode = await _lobby.QuickJoinAsync();
            await RelayHandler.JoinAsync(relayCode);
            StartClient();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task JoinPrivateAsync(string lobbyCode)
    {
        SetState(State.Joining);
        _isHost = false;
        _lobby  = new LobbyHandler();
        try
        {
            await WaitForUgsAsync();
            var relayCode = await _lobby.JoinByCodeAsync(lobbyCode);
            await RelayHandler.JoinAsync(relayCode);
            StartClient();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MP] JoinPrivate fehlgeschlagen: {e.Message}");
            SetState(State.Disconnected);
        }
    }

    public async void Disconnect()
    {
        NetworkManager.Singleton?.Shutdown();
        if (_isHost) await _lobby?.DeleteAsync();
        _lobby = null;
        SetState(State.Idle);
    }

    // ── NGO Callbacks ───────────────────────────────────────────────────────

    void OnEnable()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback    += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback   += OnClientDisconnected;
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback    -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback   -= OnClientDisconnected;
    }

    void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsHost && clientId != NetworkManager.ServerClientId)
        {
            Debug.Log("[MP] Gegner verbunden.");
            SetState(State.Connected);
            OnOpponentConnected?.Invoke();
        }
        else if (!NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[MP] Mit Host verbunden.");
            SetState(State.Connected);
            OnOpponentConnected?.Invoke();
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[MP] Gegner getrennt.");
            OnOpponentDisconnected?.Invoke();
        }
    }

    // ── UGS-Warten ──────────────────────────────────────────────────────────

    static async Task WaitForUgsAsync()
    {
        if (UgsBootstrap.IsReadyOnline) return;

        UgsBootstrap.Begin();
        await UgsBootstrap.Initialization;
    }

    // ── Intern ──────────────────────────────────────────────────────────────

    void StartHost()
    {
        if (NetworkManager.Singleton.IsListening) return;
        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.StartHost();
        Debug.Log("[MP] Host gestartet.");
    }

    void StartClient()
    {
        if (NetworkManager.Singleton.IsListening) return;
        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.StartClient();
        Debug.Log("[MP] Client gestartet.");
    }

    void SetState(State newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }
}
