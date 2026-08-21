using System;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

/// <summary>
/// Zentrale AdMob-Steuerung: holt zuerst das UMP-Consent ein, initialisiert dann das SDK
/// und verwaltet Rewarded- + Interstitial-Anzeigen inkl. automatischem Nachladen.
///
/// Selbst-bootstrappend (RuntimeInitializeOnLoadMethod) → kein Szenen-Setup nötig, DontDestroyOnLoad.
/// IDs/Test-vs-Live kommen aus <see cref="AdConfig"/> (gegated über NEONSMASH_PROD).
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }
    public static bool IsInitialized { get; private set; }

    // ----- Tuning -----
    public const int   RewardedDreamEnergyAmount    = 50;   // Dream Energy pro Free-Video
    const int          InterstitialEveryNGameOvers = 3;    // jedes N-te Game Over
    const float        InterstitialMinIntervalSec  = 60f;  // Mindestabstand zwischen Interstitials

    RewardedAd     _rewardedAd;
    InterstitialAd _interstitialAd;
    int            _gameOverCount;
    bool           _initStarted;
    float          _lastInterstitialTime = -9999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AdManager");
        go.AddComponent<AdManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
#if UNITY_EDITOR
        // Im Editor liefert UMP keine zuverlässigen Callbacks → direkt initialisieren.
        InitializeAds();
#else
        GatherConsentThenInit();
#endif
    }

    // ----------------------------------------------------------------- Consent (UMP)
    void GatherConsentThenInit()
    {
        try
        {
            var request = new ConsentRequestParameters();
            ConsentInformation.Update(request, _ =>
            {
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        Debug.LogWarning("[Ads] Consent-Form-Fehler: " + formError.Message);
                    InitializeAds();
                });
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Ads] Consent übersprungen: " + e.Message);
            InitializeAds();
        }
    }

    // ---- Datenschutz-Einstellungen (Einwilligung ändern/widerrufen) ----
    /// <summary>True, wenn UMP einen „Datenschutzeinstellungen"-Eintrag verlangt (→ Button anzeigen).</summary>
    public bool IsPrivacyOptionsRequired
    {
        get
        {
            try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
            catch { return false; }
        }
    }

    /// <summary>Öffnet das UMP-Datenschutz-Formular (Einwilligung ändern/widerrufen) — GDPR-Pflicht.</summary>
    public void ShowPrivacyOptions()
    {
        try
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) Debug.LogWarning("[Ads] Privacy-Options-Form Fehler: " + error.Message);
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Ads] ShowPrivacyOptions nicht verfügbar: " + e.Message);
        }
    }

    void InitializeAds()
    {
        if (_initStarted) return;

        bool canRequest;
        try { canRequest = ConsentInformation.CanRequestAds(); }
        catch { canRequest = true; }   // Editor-Fallback

        if (!canRequest)
        {
            // EEA ohne erteilte Einwilligung → keine Ads anfordern (compliance-korrekt).
            Debug.Log("[Ads] CanRequestAds=false → keine Ads (Consent ausstehend/abgelehnt).");
            return;
        }

        _initStarted = true;
        // Ad-Events auf dem Unity-Mainthread → wir dürfen in Callbacks Unity/DreamEnergyManager anfassen.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        MobileAds.Initialize(_ =>
        {
            IsInitialized = true;
            Debug.Log($"[Ads] MobileAds initialisiert ({AdConfig.EnvLabel}).");
            LoadRewarded();
            LoadInterstitial();
        });
    }

    // ----------------------------------------------------------------- Rewarded
    public bool IsRewardedReady => _rewardedAd != null && _rewardedAd.CanShowAd();

    void LoadRewarded()
    {
        _rewardedAd?.Destroy();
        _rewardedAd = null;

        RewardedAd.Load(AdConfig.RewardedId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning("[Ads] Rewarded laden fehlgeschlagen: " + error);
                return;
            }
            _rewardedAd = ad;
            ad.OnAdFullScreenContentClosed += LoadRewarded;          // nächstes vorladen
            ad.OnAdFullScreenContentFailed += _ => LoadRewarded();
        });
    }

    /// <summary>Zeigt ein Rewarded-Video. onReward NUR, wenn der Nutzer es zu Ende schaut.</summary>
    public void ShowRewarded(Action onReward, Action onUnavailable = null)
    {
        if (!IsRewardedReady)
        {
            onUnavailable?.Invoke();
            if (IsInitialized) LoadRewarded();
            return;
        }
        _rewardedAd.Show(_ => onReward?.Invoke());
    }

    /// <summary>Free-Dream-Energy-Button: Video → RewardedDreamEnergyAmount Dream Energy gutschreiben.</summary>
    public void ShowRewardedForDreamEnergy(Action<int> onGranted = null, Action onUnavailable = null)
    {
        ShowRewarded(
            () => { DreamEnergyManager.AddDreamEnergy(RewardedDreamEnergyAmount); onGranted?.Invoke(RewardedDreamEnergyAmount); },
            onUnavailable);
    }

    /// <summary>Streak-Freeze-Button: Video → 1 Freeze-Charge für DailyRewardManager gutschreiben.</summary>
    public void ShowRewardedForStreakFreeze(Action onGranted = null, Action onUnavailable = null)
    {
        ShowRewarded(
            () => { DailyRewardManager.AddFreezeCharge(1); onGranted?.Invoke(); },
            onUnavailable);
    }

    // ----------------------------------------------------------------- Interstitial
    void LoadInterstitial()
    {
        _interstitialAd?.Destroy();
        _interstitialAd = null;

        InterstitialAd.Load(AdConfig.InterstitialId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning("[Ads] Interstitial laden fehlgeschlagen: " + error);
                if (isActiveAndEnabled) StartCoroutine(RetryLoadInterstitial());  // No-Fill heilt sich
                return;
            }
            _interstitialAd = ad;
            ad.OnAdFullScreenContentClosed += LoadInterstitial;       // nächstes vorladen
            ad.OnAdFullScreenContentFailed += _ => LoadInterstitial();
        });
    }

    System.Collections.IEnumerator RetryLoadInterstitial()
    {
        yield return new WaitForSecondsRealtime(30f);
        if (_interstitialAd == null) LoadInterstitial();
    }

    /// <summary>
    /// Beim Game Over aufrufen (direkt nach dem GAME-OVER-Banner, vor dem Score-Panel).
    /// Zählt hoch und zeigt jedes N-te Mal (+ Cooldown, sofern geladen) ein Interstitial.
    /// <paramref name="onClosed"/> wird IMMER aufgerufen — nach dem Schließen der Anzeige
    /// oder sofort, wenn keine gezeigt wird → der Game-Over-Flow läuft garantiert weiter.
    /// </summary>
    public void MaybeShowInterstitial(Action onClosed)
    {
        _gameOverCount++;

        bool due    = _gameOverCount % InterstitialEveryNGameOvers == 0;
        bool cooled = Time.realtimeSinceStartup - _lastInterstitialTime >= InterstitialMinIntervalSec;
        bool ready  = _interstitialAd != null && _interstitialAd.CanShowAd();

        if (!IsInitialized || !due || !cooled || !ready)
        {
            if (IsInitialized && _interstitialAd == null) LoadInterstitial();
            onClosed?.Invoke();
            return;
        }

        _lastInterstitialTime = Time.realtimeSinceStartup;

        bool fired = false;
        void Done() { if (fired) return; fired = true; onClosed?.Invoke(); }

        _interstitialAd.OnAdFullScreenContentClosed += Done;
        _interstitialAd.OnAdFullScreenContentFailed += _ => Done();
        _interstitialAd.Show();
    }
}
