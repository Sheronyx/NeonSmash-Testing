using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using Unity.Services.Authentication;
using UnityEngine;

public static class LeaderboardApi
{
    public const string InfinityId    = "infinity_highscore";
    public const string MultiplayerId = "multiplayer_rank";

    public static string DefaultId = null;

    public static string GetLocalDisplayName()
    {
        string authName = null;

        try { authName = AuthenticationService.Instance?.PlayerName; }
        catch { }

        if (!string.IsNullOrWhiteSpace(authName) && !authName.Contains("#"))
            return authName;

        var pref = PlayerPrefs.GetString("display_name", "");

        if (!string.IsNullOrWhiteSpace(pref))
            return pref;

        var pid = AuthenticationService.Instance?.PlayerId ?? "Player";

        return pid.Length > 8 ? pid.Substring(0, 8) : pid;
    }

    public static async Task SubmitScoreAsync(long score, string id = null)
    {
        id ??= DefaultId;

        await UgsBootstrap.Initialization;

        await LeaderboardsService.Instance.AddPlayerScoreAsync(id, score);
    }

    public static async Task<LeaderboardScoresPage> GetTopAsync(int limit = 20, string id = null)
    {
        id ??= DefaultId;

        await UgsBootstrap.Initialization;

        return await LeaderboardsService.Instance.GetScoresAsync(
            id,
            new GetScoresOptions
            {
                Limit = limit,
                Offset = 0
            }
        );
    }

    public static async Task<LeaderboardScoresPage> GetScoresAsync(int limit, int offset, string id = null)
    {
        id ??= DefaultId;

        await UgsBootstrap.Initialization;

        return await LeaderboardsService.Instance.GetScoresAsync(
            id,
            new GetScoresOptions
            {
                Limit = limit,
                Offset = offset
            }
        );
    }

    public static async Task<LeaderboardEntry> GetMyScoreAsync(string id = null)
    {
        id ??= DefaultId;

        await UgsBootstrap.Initialization;

        try
        {
            return await LeaderboardsService.Instance.GetPlayerScoreAsync(id);
        }
        catch
        {
            return null;
        }
    }
}