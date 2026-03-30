using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;

public static class DisplayNameSync
{
    const string PrefLastSyncedName = "last_synced_platform_name";

    /// <summary>
    /// Schreibt preferredName in UGS, wenn sinnvoll (abweichend vom aktuellen/zuletzt synchronisierten Namen).
    /// </summary>
    public static async Task TrySyncAsync(string preferredName)
    {
        if (string.IsNullOrWhiteSpace(preferredName)) return;

        // UGS-Init abwarten
        try { if (UgsBootstrap.Initialization != null) _ = await UgsBootstrap.Initialization; } catch { }

        if (!AuthenticationService.Instance.IsSignedIn) return;

        string currentUgs = null;
        try { currentUgs = AuthenticationService.Instance.PlayerName; } catch { /* ignore */ }

        var last = PlayerPrefs.GetString(PrefLastSyncedName, string.Empty);

        bool differsFromUgs  = string.IsNullOrWhiteSpace(currentUgs) || !StringEqualsOrdinal(currentUgs, preferredName);
        bool differsFromLast = !StringEqualsOrdinal(last, preferredName);

        if (differsFromUgs && differsFromLast)
        {
            try
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(preferredName);
                PlayerPrefs.SetString("display_name", preferredName);
                PlayerPrefs.SetString(PrefLastSyncedName, preferredName);
                PlayerPrefs.Save();
                Debug.Log("[NameSync] UGS PlayerName aktualisiert: " + preferredName);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NameSync] UpdatePlayerNameAsync fehlgeschlagen: " + e.Message);
            }
        }
        else
        {
            Debug.Log($"[NameSync] Übersprungen (UGS='{currentUgs}', last='{last}', pref='{preferredName}')");
        }
    }

    static bool StringEqualsOrdinal(string a, string b)
        => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
}
