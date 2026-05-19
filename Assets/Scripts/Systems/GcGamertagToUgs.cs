#if UNITY_IOS
using UnityEngine.SocialPlatforms;
using UnityEngine.SocialPlatforms.GameCenter;
#endif
using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

public static class GcGamertagToUgs
{
    static bool _forcedOnceThisRun;

    /// <summary>
    /// Meldet den Spieler bei Game Center an, verlinkt (oder signed-in) die GC-Identität
    /// mit Unity Authentication und übernimmt den GC-Anzeigenamen in UGS.
    /// </summary>
    public static async Task TryApplyAsync()
    {
#if UNITY_IOS
        await Task.Yield(); // sicherstellen, dass wir auf dem Mainthread sind
        GameCenterPlatform.ShowDefaultAchievementCompletionBanner(true);

        // Safety: Bridge existiert?
        if (AppleGameCenterAuth.Instance == null)
            new GameObject("AppleGameCenterAuth").AddComponent<AppleGameCenterAuth>();

        var tcs = new TaskCompletionSource<bool>();

        Social.localUser.Authenticate(async success =>
        {
            try
            {
                Debug.Log("[GC] Authenticate -> " + success);

                if (!success)
                {
                    // einmalig interaktiven Fallback
                    if (!_forcedOnceThisRun)
                    {
                        _forcedOnceThisRun = true;
                        try { Social.ShowLeaderboardUI(); } catch { /* ignore */ }
                        await Task.Delay(1500);
                        Social.localUser.Authenticate(s2 => { tcs.TrySetResult(s2); });
                        return;
                    }

                    tcs.TrySetResult(false);
                    return;
                }

                // --- ab hier: GC ist eingeloggt ---

                // 1) GC-Identity-Payload holen
                GcIdentityPayload payload = null;
                try
                {
                    payload = await AppleGameCenterAuth.Instance.RequestIdentityAsync(20000);
                }
                catch (Exception reqEx)
                {
                    Debug.LogWarning("[UGS][GC] payload request failed: " + reqEx.Message);
                }

                // 2) Link versuchen → bei AlreadyLinked auf diesen Account SIGN-IN
                bool gcExistingAccount = false;
                if (payload != null && string.IsNullOrEmpty(payload.error))
                {
                    try
                    {
                        await AuthenticationService.Instance.LinkWithAppleGameCenterAsync(
                            payload.signature,
                            payload.teamPlayerId ?? "",
                            payload.publicKeyURL,
                            payload.salt,
                            payload.timestamp
                        );
                        Debug.Log("[UGS][GC] Linked Game Center identity!");
                    }
                    catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                    {
                        Debug.Log("[UGS][GC] Already linked – signing in to that account.");
                        await AuthenticationService.Instance.SignInWithAppleGameCenterAsync(
                            payload.signature,
                            payload.teamPlayerId ?? "",
                            payload.publicKeyURL,
                            payload.salt,
                            payload.timestamp
                        );
                        gcExistingAccount = true;
                    }
                    catch (Exception linkEx)
                    {
                        Debug.LogWarning("[UGS][GC] Link failed: " + linkEx.Message);
                    }
                }
                else
                {
                    Debug.Log($"[UGS][GC] identity payload error (non-fatal): {payload?.error}");
                }

                // 3) Namen aus GC übernehmen (immer versuchen)
                var gcName = Social.localUser.userName;
                await TryUpdateUgsNameAsync(gcName, gcExistingAccount);

                tcs.TrySetResult(true);
            }
            catch (Exception outer)
            {
                Debug.LogError("[UGS][GC] Authenticate callback error: " + outer.Message);
                tcs.TrySetResult(false);
            }
        });

        // Safety Timeout
        _ = FailSafe(tcs, 20000);
        await tcs.Task;
#else
        await Task.CompletedTask;
#endif
    }

#if UNITY_IOS
    static async Task FailSafe(TaskCompletionSource<bool> tcs, int ms)
    {
        await Task.Delay(ms);
        if (!tcs.Task.IsCompleted)
        {
            Debug.LogWarning("[GC] Authenticate-Callback kam nicht – prüfe Capability/Entitlements/ASC/Device Login.");
            tcs.TrySetResult(false);
        }
    }
#endif

    /// <summary>Schreibt den Namen in UGS (falls sinnvoll) und in PlayerPrefs (Fallback).</summary>
    public static async Task TryUpdateUgsNameAsync(string newName, bool isExistingAccount = false)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            string current = null; try { current = AuthenticationService.Instance.PlayerName; } catch { }
            // Strip #discriminator before comparing — PlayerName is "Name#1234", newName is "Name"
            string currentBase = current;
            if (current != null) { int h = current.IndexOf('#'); if (h > 0) currentBase = current.Substring(0, h); }

            // Guard: PlayerName null auf bestehendem Account → Update überspringen um Discriminator zu bewahren.
            // Nur neue Accounts (frische Verlinkung) dürfen den Namen erstmalig setzen wenn current null ist.
            if (isExistingAccount && string.IsNullOrEmpty(current))
            {
                Debug.LogWarning("[UGS] PlayerName null auf bestehendem Account — Name-Update übersprungen um Discriminator zu bewahren.");
                PlayerPrefs.SetString("display_name", newName);
                PlayerPrefs.Save();
                return;
            }

            if (!string.Equals(currentBase, newName, StringComparison.Ordinal))
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(newName);
                Debug.Log("[UGS] PlayerName updated to: " + newName);
            }

            PlayerPrefs.SetString("display_name", newName);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UGS] UpdatePlayerName failed: " + e.Message);
            PlayerPrefs.SetString("display_name", newName);
            PlayerPrefs.Save();
        }
    }
}
