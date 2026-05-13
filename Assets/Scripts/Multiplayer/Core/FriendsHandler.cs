using System;
using System.Threading.Tasks;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;

public static class FriendsHandler
{
    public static bool IsInitialized { get; private set; }

    public static System.Collections.Generic.IReadOnlyList<Relationship> Friends
        => FriendsService.Instance.Friends;

    public static System.Collections.Generic.IReadOnlyList<Relationship> IncomingRequests
        => FriendsService.Instance.IncomingFriendRequests;

    public static event Action                  OnRelationshipsChanged;
    public static event Action<string, string>  OnChallengeReceived;   // lobbyCode, senderName

    public static async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await FriendsService.Instance.InitializeAsync();
        await FriendsService.Instance.SetPresenceAvailabilityAsync(Availability.Online);

        FriendsService.Instance.RelationshipAdded   += _ => OnRelationshipsChanged?.Invoke();
        FriendsService.Instance.RelationshipDeleted += _ => OnRelationshipsChanged?.Invoke();
        FriendsService.Instance.PresenceUpdated     += _ => OnRelationshipsChanged?.Invoke();
        FriendsService.Instance.MessageReceived     += OnMessageReceived;

        IsInitialized = true;
        Debug.Log("[Friends] Initialisiert.");
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

    public static async Task SendChallengeAsync(string memberId, string lobbyCode)
    {
        var msg = new ChallengeMessage
        {
            LobbyCode  = lobbyCode,
            SenderName = LeaderboardApi.GetLocalDisplayName()
        };
        await FriendsService.Instance.MessageAsync(memberId, msg);
        Debug.Log($"[Friends] Challenge an {memberId} gesendet (Code: {lobbyCode}).");
    }
}
