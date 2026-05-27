using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyHandler
{
    const string RelayCodeKey = "RelayCode";
    const string LobbyName   = "NeonSmash";
    const int    MaxPlayers  = 2;

    public Lobby CurrentLobby { get; private set; }

    private float _heartbeatTimer;

    public async Task<string> CreatePublicAsync(string relayCode)
    {
        var options = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new Dictionary<string, DataObject>
            {
                { RelayCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayCode) }
            }
        };

        CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(LobbyName, MaxPlayers, options);
        Debug.Log($"[Lobby] Öffentlich erstellt. LobbyId: {CurrentLobby.Id}");
        return CurrentLobby.Id;
    }

    public async Task<string> CreatePrivateAsync(string relayCode)
    {
        var options = new CreateLobbyOptions
        {
            IsPrivate = true,
            Data = new Dictionary<string, DataObject>
            {
                { RelayCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayCode) }
            }
        };

        CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(LobbyName, MaxPlayers, options);
        Debug.Log($"[Lobby] Privat erstellt. LobbyCode: {CurrentLobby.LobbyCode}");
        return CurrentLobby.LobbyCode;
    }

    // Zufällige offene Lobby finden
    public async Task<string> QuickJoinAsync()
    {
        CurrentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
        var relayCode = CurrentLobby.Data[RelayCodeKey].Value;
        Debug.Log($"[Lobby] Quick Join. RelayCode: {relayCode}");
        return relayCode;
    }

    // Private Lobby per Code beitreten
    public async Task<string> JoinByCodeAsync(string lobbyCode)
    {
        CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
        var relayCode = CurrentLobby.Data[RelayCodeKey].Value;
        Debug.Log($"[Lobby] Per Code beigetreten. RelayCode: {relayCode}");
        return relayCode;
    }

    public async Task DeleteAsync()
    {
        if (CurrentLobby == null) return;
        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
            Debug.Log("[Lobby] Gelöscht.");
        }
        catch { /* bereits abgelaufen */ }
        CurrentLobby = null;
    }

    // Muss jeden Frame aufgerufen werden (z.B. aus MultiplayerManager.Update)
    public async void TickHeartbeat(float deltaTime)
    {
        if (CurrentLobby == null) return;

        _heartbeatTimer -= deltaTime;
        if (_heartbeatTimer > 0f) return;

        _heartbeatTimer = 15f;
        await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
    }
}
