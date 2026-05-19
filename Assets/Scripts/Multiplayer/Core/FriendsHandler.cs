using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;

public static class FriendsHandler
{
    public static bool IsInitialized { get; private set; }

    // Deduplication: concurrent callers share one init task instead of running twice
    static Task _initTask;

    // Stored delegates so we can unsubscribe them properly
    static readonly Action<IRelationshipAddedEvent>   _onRelAdded   = _ => { Debug.Log("[DIAG] EVENT RelationshipAdded"); OnRelationshipsChanged?.Invoke(); };
    static readonly Action<IRelationshipDeletedEvent> _onRelDeleted = _ => { Debug.Log("[DIAG] EVENT RelationshipDeleted"); OnRelationshipsChanged?.Invoke(); };
    static readonly Action<IPresenceUpdatedEvent>     _onPresence   = _ => { Debug.Log("[DIAG] EVENT PresenceUpdated"); OnRelationshipsChanged?.Invoke(); };

    public static IReadOnlyList<Relationship> Friends
        => FriendsService.Instance.Friends;

    public static IReadOnlyList<Relationship> IncomingRequests
        => FriendsService.Instance.IncomingFriendRequests;

    public static event Action                  OnRelationshipsChanged;
    public static event Action<string, string>  OnChallengeReceived;   // lobbyCode, senderName

    public static Task InitializeAsync()
    {
        if (_initTask == null)
            _initTask = RunInitAsync();
        return _initTask;
    }

    static async Task RunInitAsync()
    {
        string pidAtStart = "?";
        try { pidAtStart = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId; } catch {}
        Debug.Log($"[DIAG] RunInitAsync START — PlayerId={pidAtStart}");

        await FriendsService.Instance.InitializeAsync();
        Debug.Log("[DIAG] FriendsService.InitializeAsync() OK");

        FriendsService.Instance.RelationshipAdded   += _onRelAdded;
        FriendsService.Instance.RelationshipDeleted += _onRelDeleted;
        FriendsService.Instance.PresenceUpdated     += _onPresence;
        FriendsService.Instance.MessageReceived     += OnMessageReceived;

        IsInitialized = true;
        Debug.Log("[Friends] Initialisiert.");

        try
        {
            await FriendsService.Instance.SetPresenceAvailabilityAsync(Availability.Online);
            Debug.Log("[DIAG] SetPresence(Online) bei Init OK");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DIAG] SetPresence(Online) bei Init FEHLGESCHLAGEN: {e.Message}");
        }
    }

    // Set presence when app goes to background / foreground.
    public static async Task SetPresenceAsync(bool online)
    {
        Debug.Log($"[DIAG] SetPresenceAsync({(online ? "Online" : "Offline")}) called — IsInitialized={IsInitialized}");
        if (!IsInitialized) { Debug.Log("[DIAG] SetPresenceAsync abgebrochen: nicht initialisiert"); return; }
        try
        {
            await FriendsService.Instance.SetPresenceAvailabilityAsync(
                online ? Availability.Online : Availability.Offline);
            Debug.Log($"[DIAG] SetPresenceAsync({(online ? "Online" : "Offline")}) OK");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DIAG] SetPresenceAsync({(online ? "Online" : "Offline")}) FEHLGESCHLAGEN: {e.Message}");
        }
    }

    // Force a server-side pull of all relationships (refreshes presence data).
    // Safe to call at any time; no-op if not initialized.
    public static async Task ForceRefreshAsync()
    {
        if (!IsInitialized) return;
        Debug.Log("[DIAG] ForceRefreshAsync START");
        try
        {
            await FriendsService.Instance.ForceRelationshipsRefreshAsync();

            var friends = FriendsService.Instance.Friends;
            Debug.Log($"[DIAG] ForceRefreshAsync DONE — {friends.Count} Freund(e):");
            foreach (var r in friends)
            {
                string presence = r.Member.Presence?.Availability.ToString() ?? "null";
                string name     = r.Member.Profile?.Name ?? "?";
                Debug.Log($"[DIAG]   Freund: Id={r.Member.Id}, Name={name}, Presence={presence}");
            }

            OnRelationshipsChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DIAG] ForceRefreshAsync FEHLGESCHLAGEN: {e.Message}");
        }
    }

    public static async Task RefreshPresenceAsync()
    {
        if (!IsInitialized) return;
        try
        {
            await FriendsService.Instance.SetPresenceAvailabilityAsync(Availability.Online);
            Debug.Log("[Friends] Presence erneuert nach Platform-Auth.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Friends] RefreshPresence fehlgeschlagen: {e.Message}");
        }
    }

    static void OnMessageReceived(IMessageReceivedEvent evt)
    {
        try
        {
            var msg = evt.GetAs<ChallengeMessage>();
            if (!string.IsNullOrEmpty(msg?.LobbyCode))
                OnChallengeReceived?.Invoke(msg.LobbyCode, msg.SenderName ?? evt.UserId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Friends] Nachricht konnte nicht gelesen werden: {e.Message}");
        }
    }

    public static async Task AddFriendByNameAsync(string name)
    {
        await FriendsService.Instance.AddFriendByNameAsync(name);
        OnRelationshipsChanged?.Invoke();
    }

    public static async Task AcceptRequestAsync(string memberId)
    {
        await FriendsService.Instance.AddFriendAsync(memberId);
        OnRelationshipsChanged?.Invoke();
    }

    public static async Task DeclineRequestAsync(string memberId)
    {
        await FriendsService.Instance.DeleteIncomingFriendRequestAsync(memberId);
        OnRelationshipsChanged?.Invoke();
    }

    public static async Task DeleteFriendAsync(string memberId)
    {
        await FriendsService.Instance.DeleteFriendAsync(memberId);
        OnRelationshipsChanged?.Invoke();
    }

    // Sends a push-only nudge to an offline friend — no lobby, no real-time message.
    public static Task SendNudgePushAsync(string memberId)
    {
        string senderName = LeaderboardApi.GetLocalDisplayName();
        Debug.Log($"[DIAG] SendNudgePushAsync — recipientId={memberId}, senderName={senderName}");
        return SendPushNotificationAsync(memberId, senderName);
    }

    public static async Task SendChallengeAsync(string memberId, string lobbyCode)
    {
        string senderName = LeaderboardApi.GetLocalDisplayName();

        var msg = new ChallengeMessage
        {
            LobbyCode  = lobbyCode,
            SenderName = senderName
        };
        await FriendsService.Instance.MessageAsync(memberId, msg);
        Debug.Log($"[Friends] Challenge an {memberId} gesendet (Code: {lobbyCode}).");

        // Push notification — fire-and-forget, never blocks the challenge flow
        _ = SendPushNotificationAsync(memberId, senderName);
    }

    static async Task SendPushNotificationAsync(string recipientPlayerId, string senderName)
    {
        Debug.Log($"[DIAG] SendPushNotificationAsync START — recipient={recipientPlayerId}, sender={senderName}");
        try
        {
            var result = await CloudCodeService.Instance.CallEndpointAsync<object>(
                "SendChallengeNotification",
                new Dictionary<string, object>
                {
                    { "recipientPlayerId", recipientPlayerId },
                    { "senderName",        senderName        }
                }
            );
            string resultStr = result?.ToString()?.Replace("\n"," ").Replace("\r","") ?? "null";
            Debug.Log($"[DIAG] SendPushNotificationAsync OK — result={resultStr}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DIAG] SendPushNotificationAsync FEHLGESCHLAGEN: {e.GetType().Name}: {e.Message}");
        }
    }
}
