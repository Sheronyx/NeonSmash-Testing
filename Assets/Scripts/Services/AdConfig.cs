/// <summary>
/// AdMob Ad-Unit-IDs. Im Dev/Editor IMMER Googles offizielle Test-IDs (kein Sperr-Risiko),
/// nur im echten Release-Build (Scripting-Define NEONSMASH_PROD) die echten Units.
/// Analog zur Environment-Trennung von Leaderboards/Firebase.
/// </summary>
public static class AdConfig
{
#if NEONSMASH_PROD
    public const bool   UseTestAds = false;
    public const string EnvLabel   = "LIVE";
#else
    public const bool   UseTestAds = true;
    public const string EnvLabel   = "TEST";
#endif

    // Googles öffentliche Test-Ad-Units — https://developers.google.com/admob/unity/test-ads
    const string TestRewardedAndroid     = "ca-app-pub-3940256099942544/5224354917";
    const string TestRewardedIOS         = "ca-app-pub-3940256099942544/1712485313";
    const string TestInterstitialAndroid = "ca-app-pub-3940256099942544/1033173712";
    const string TestInterstitialIOS     = "ca-app-pub-3940256099942544/4411468910";

    // Echte NeonSmash-Units (nur im Release aktiv)
    const string LiveRewardedAndroid     = "ca-app-pub-4729994058287341/5678345426";
    const string LiveRewardedIOS         = "ca-app-pub-4729994058287341/9916973334";
    const string LiveInterstitialAndroid = "ca-app-pub-4729994058287341/7723834509";
    const string LiveInterstitialIOS     = "ca-app-pub-4729994058287341/4838916799";

    public static string RewardedId
    {
        get
        {
#if UNITY_ANDROID
            return UseTestAds ? TestRewardedAndroid : LiveRewardedAndroid;
#elif UNITY_IOS
            return UseTestAds ? TestRewardedIOS : LiveRewardedIOS;
#else
            return TestRewardedAndroid; // Editor-Fallback
#endif
        }
    }

    public static string InterstitialId
    {
        get
        {
#if UNITY_ANDROID
            return UseTestAds ? TestInterstitialAndroid : LiveInterstitialAndroid;
#elif UNITY_IOS
            return UseTestAds ? TestInterstitialIOS : LiveInterstitialIOS;
#else
            return TestInterstitialAndroid; // Editor-Fallback
#endif
        }
    }
}
