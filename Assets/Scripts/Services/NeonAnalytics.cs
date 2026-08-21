using Firebase.Analytics;
using UnityEngine;

/// <summary>
/// Thin wrapper around Firebase Analytics. All calls are no-ops in development
/// builds so test sessions never pollute the production dashboard.
/// </summary>
public static class NeonAnalytics
{
    // Release builds from the App Store / Play Store have isDebugBuild = false.
    // NEON_ANALYTICS_TEST overrides this for DebugView testing on device.
#if NEON_ANALYTICS_TEST
    static bool Active => !Application.isEditor;
#else
    static bool Active => !Debug.isDebugBuild && !Application.isEditor;
#endif

    const string KeyInstallDate = "na_install_ts";
    const string KeySessionNum  = "na_session_num";

    static float _gameStartTime;

    static string ModeStr(GameMode m) => m switch
    {
        GameMode.Multiplayer => "multiplayer",
        _                    => "infinity"
    };

    // ── App Session ───────────────────────────────────────────────────────────

    public static void LogSessionStart()
    {
        if (!Active) return;

        if (!PlayerPrefs.HasKey(KeyInstallDate))
        {
            PlayerPrefs.SetString(KeyInstallDate, System.DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();
        }

        int days = 0;
        if (System.DateTime.TryParse(PlayerPrefs.GetString(KeyInstallDate), out var installDate))
            days = (int)(System.DateTime.UtcNow - installDate).TotalDays;

        int sessionNum = PlayerPrefs.GetInt(KeySessionNum, 0) + 1;
        PlayerPrefs.SetInt(KeySessionNum, sessionNum);
        PlayerPrefs.Save();

        FirebaseAnalytics.LogEvent("session_start",
            new Parameter("session_num",        sessionNum),
            new Parameter("days_since_install", days));
    }

    // ── Gameplay ──────────────────────────────────────────────────────────────

    public static void LogGameStart(GameMode mode)
    {
        if (!Active) return;
        _gameStartTime = Time.realtimeSinceStartup;
        FirebaseAnalytics.LogEvent("game_start",
            new Parameter("mode",        ModeStr(mode)),
            new Parameter("session_num", PlayerPrefs.GetInt(KeySessionNum, 0)));
    }

    /// <param name="cause">"timeout" | "gravity" | "time_up"</param>
    public static void LogGameOver(GameMode mode, int score, string cause)
    {
        if (!Active) return;
        long duration = (long)(Time.realtimeSinceStartup - _gameStartTime);
        FirebaseAnalytics.LogEvent("game_over",
            new Parameter("mode",         ModeStr(mode)),
            new Parameter("score",        score),
            new Parameter("duration_sec", duration),
            new Parameter("cause",        cause));
    }

    public static void LogHighscoreBeat(GameMode mode, int newScore)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("highscore_beaten",
            new Parameter("mode",      ModeStr(mode)),
            new Parameter("new_score", newScore));
    }

    // ── Post-Game Actions ─────────────────────────────────────────────────────

    /// <param name="action">"restart" | "menu"</param>
    public static void LogGameOverAction(string action)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("game_over_action",
            new Parameter("action", action));
    }

    public static void LogPauseQuit(GameMode mode, int score)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("pause_quit",
            new Parameter("mode",  ModeStr(mode)),
            new Parameter("score", score));
    }

    // ── Special Modes ─────────────────────────────────────────────────────────

    /// <param name="type">"gravity" | "gold" | "fountain"</param>
    public static void LogSpecialModeTriggered(string type)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("special_mode_triggered",
            new Parameter("type", type));
    }

    // ── Tutorial ──────────────────────────────────────────────────────────────

    public static void LogTutorialCompleted()
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventTutorialComplete);
    }

    // ── Monetarisierung: Ads ─────────────────────────────────────────────────
    // Bisher komplett ungetrackt (Lücke gefunden 2026-08-22) — ohne diese Events gibt es keinerlei
    // Sichtbarkeit auf Ad-Funnel-Performance (Completion-Rate, Verfügbarkeit, Interstitial-Frequenz).

    /// <summary>Rewarded-Video wurde vollständig angesehen und die Belohnung vergeben.</summary>
    /// <param name="placement">z.B. "dream_energy" | "streak_freeze"</param>
    public static void LogAdRewardedCompleted(string placement)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("ad_rewarded_completed",
            new Parameter("placement", placement));
    }

    /// <summary>Rewarded-Video wurde angefragt, war aber nicht verfügbar (kein Fill/noch am Laden).</summary>
    public static void LogAdRewardedUnavailable(string placement)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("ad_rewarded_unavailable",
            new Parameter("placement", placement));
    }

    /// <summary>Interstitial wurde tatsächlich angezeigt (nach Due/Cooldown/Ready-Prüfung).</summary>
    public static void LogAdInterstitialShown()
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("ad_interstitial_shown");
    }

    // ── Monetarisierung: IAP ─────────────────────────────────────────────────

    /// <summary>Nutzt Firebases Standard-Purchase-Event für automatisches Revenue-Tracking im Dashboard.</summary>
    public static void LogPurchaseSuccess(string productId, double price, string currencyCode)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase,
            new Parameter(FirebaseAnalytics.ParameterItemID, productId),
            new Parameter(FirebaseAnalytics.ParameterValue, price),
            new Parameter(FirebaseAnalytics.ParameterCurrency, string.IsNullOrEmpty(currencyCode) ? "USD" : currencyCode));
    }

    public static void LogPurchaseFailed(string productId, string reason)
    {
        if (!Active) return;
        FirebaseAnalytics.LogEvent("purchase_failed",
            new Parameter("product_id", productId),
            new Parameter("reason",     reason));
    }

    // ── Crashlytics helpers ───────────────────────────────────────────────────
    // Requires FirebaseCrashlytics.unitypackage to be imported.

    public static void SetCrashlyticsUserId(string playerId)
    {
        if (!Active || string.IsNullOrEmpty(playerId)) return;
        Firebase.Crashlytics.Crashlytics.SetUserId(playerId);
    }

    public static void LogCrashlyticsKey(string key, string value)
    {
        if (!Active) return;
        Firebase.Crashlytics.Crashlytics.SetCustomKey(key, value);
    }
}
