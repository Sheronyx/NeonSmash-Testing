using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication; // optional fürs Logging

public static class HighscoreUploader
{
    // <<< Wähle dein Verhalten für den lokalen Bestwert >>>
    // true  = sofort updaten (optimistisch; UI fühlt sich snappier an)
    // false = wie früher: erst nach erfolgreichem Submit
    private static readonly bool OPTIMISTIC_LOCAL_SAVE = true;

    // <<< Erhöhe bei Bedarf, um alle lokalen Bestwerte zu invalidieren >>>
    private const int LB_LOCAL_VERSION = 1;

    private static string Key(string leaderboardId) => $"lb_v{LB_LOCAL_VERSION}_best_{leaderboardId}";

    /// Liefert den lokal gespeicherten Bestwert (0, wenn keiner vorhanden)
    public static int GetLocalBest(string leaderboardId)
        => PlayerPrefs.GetInt(Key(leaderboardId), 0);

    /// Löscht den lokal gespeicherten Bestwert (z. B. für Tests auf dem aktuellen Gerät)
    public static void ClearLocalBest(string leaderboardId)
    {
        PlayerPrefs.DeleteKey(Key(leaderboardId));
        PlayerPrefs.Save();
        Debug.Log($"[HighscoreUploader] Cleared local best for '{leaderboardId}' (v{LB_LOCAL_VERSION}).");
    }

    /// Convenience: Score ins angegebene Leaderboard posten (nur bei neuem lokalen Bestwert)
    public static Task<bool> TrySubmitAsync(int currentScore, string leaderboardId)
        => TrySubmitAsync(currentScore, leaderboardId, force: false);

    /// Fallback-Überladung: nutzt LeaderboardApi.DefaultId
    public static Task<bool> TrySubmitAsync(int currentScore)
        => TrySubmitAsync(currentScore, LeaderboardApi.DefaultId, force: false);

    /// Volle Kontrolle
    public static async Task<bool> TrySubmitAsync(int currentScore, string leaderboardId, bool force)
    {
        if (string.IsNullOrWhiteSpace(leaderboardId))
        {
            Debug.LogWarning("[HighscoreUploader] leaderboardId is null/empty.");
            return false;
        }

        if (currentScore < 0) currentScore = 0;

        int localBest = GetLocalBest(leaderboardId);
        if (!force && currentScore <= localBest)
        {
            Debug.Log($"[HighscoreUploader] Skip submit for '{leaderboardId}' (current={currentScore} <= localBest={localBest}, v{LB_LOCAL_VERSION}).");
            return false;
        }

        // Stelle sicher, dass die Offline-Queue existiert
        OfflineScoreSync.Ensure();

        // --- Lokalen Bestwert setzen? (wahlweise optimistisch oder konservativ) ---
        if (OPTIMISTIC_LOCAL_SAVE)
        {
            PlayerPrefs.SetInt(Key(leaderboardId), currentScore);
            PlayerPrefs.Save();
        }

        try
        {
            // 1) UGS bereit + Internet? Dann auf GPGS/Game Center warten bevor Score submitted wird
            bool online = await UgsBootstrap.Initialization;
            if (!online || Application.internetReachability == NetworkReachability.NotReachable)
                throw new System.Exception("offline");
            await UgsBootstrap.PlatformAuthReady;

            // Optionales Logging
            string playerId = null;
            try { playerId = AuthenticationService.Instance?.PlayerId; } catch { /* ignore */ }
            Debug.Log($"[HighscoreUploader] Submit start -> LB='{leaderboardId}', score={currentScore}, playerId={(playerId ?? "n/a")}, v{LB_LOCAL_VERSION}");

            // 2) Score posten (API erwartet long)
            await LeaderboardApi.SubmitScoreAsync(currentScore, leaderboardId);

            // 3) Lokalen Bestwert NACH erfolgreichem Submit aktualisieren (konservativer Pfad)
            if (!OPTIMISTIC_LOCAL_SAVE)
            {
                PlayerPrefs.SetInt(Key(leaderboardId), currentScore);
                PlayerPrefs.Save();
            }

            Debug.Log($"[HighscoreUploader] Submit OK -> LB='{leaderboardId}', newLocalBest={currentScore}, v{LB_LOCAL_VERSION}");

            // 4) Nach Erfolg evtl. ältere Pending-Scores flushen
            _ = OfflineScoreSync.Instance.TryFlushAsync();
            return true;
        }
        catch (System.Exception e)
        {
            // ❗ Offline oder Fehler → in die Queue (höchsten Score pro LB behalten wir dort)
            OfflineScoreSync.Instance.Enqueue(leaderboardId, currentScore);

            // Falls du konservativ speichern willst und es noch keinen lokalen Bestwert gab,
            // setze ihn zumindest jetzt, damit der Spieler seinen Fortschritt sieht.
            if (!OPTIMISTIC_LOCAL_SAVE && currentScore > localBest)
            {
                PlayerPrefs.SetInt(Key(leaderboardId), currentScore);
                PlayerPrefs.Save();
            }

            Debug.LogWarning($"[HighscoreUploader] Submit deferred ('{leaderboardId}'): {e.Message}");
            return false;
        }
    }
}
