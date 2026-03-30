#if UNITY_ANDROID
using System;
using System.Threading.Tasks;
using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi; // SignInStatus

/// <summary>
/// GPGS-Init + Login + Server-Auth-Code (für UGS).
/// v2-kompatibel.
/// </summary>
public class GooglePlayGamesAuth : MonoBehaviour
{
    public static GooglePlayGamesAuth Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        PlayGamesPlatform.DebugLogEnabled = false;
        PlayGamesPlatform.Activate(); // v2: reicht aus
        Debug.Log("[GPGS] Activated (v2).");
    }

    /// <summary>Meldet bei GPGS an und liefert einen Server-Auth-Code (oder Fehlertext).</summary>
    public Task<(string authCode, string error)> RequestServerAuthCodeAsync(int timeoutMs = 6000)
    {
        var tcs = new TaskCompletionSource<(string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);

        PlayGamesPlatform.Instance.Authenticate((SignInStatus status) =>
        {
            try
            {
                Debug.Log("[GPGS] SignInStatus=" + status);
                if (status != SignInStatus.Success)
                {
                    tcs.TrySetResult((null, "gpgs_signin_status_" + status));
                    return;
                }

                // Server-Auth-Code anfordern (forceRefresh=false)
                PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                {
                    tcs.TrySetResult(string.IsNullOrEmpty(code)
                        ? (null, "auth_code_null")
                        : (code, null));
                });
            }
            catch (Exception e)
            {
                tcs.TrySetResult((null, "exception: " + e.Message));
            }
        });

        // einfacher Timeout
        _ = Task.Run(async () =>
        {
            await Task.Delay(timeoutMs);
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult((null, "timeout"));
        });

        return tcs.Task;
    }

    /// <summary>Gibt den GPGS-Spielernamen zurück (oder null, wenn nicht eingeloggt).</summary>
    public string CurrentUserNameOrNull()
    {
        try
        {
            // In v2 nur nutzen, wenn authentifiziert:
            if (PlayGamesPlatform.Instance != null &&
                PlayGamesPlatform.Instance.IsAuthenticated())
            {
                var n = PlayGamesPlatform.Instance.GetUserDisplayName();
                if (!string.IsNullOrWhiteSpace(n)) return n;
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
#endif
