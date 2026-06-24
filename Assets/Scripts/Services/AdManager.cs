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
    public const int   RewardedCoinAmount         = 50;   // Coins pro Free-Coins-Video
    const int          InterstitialEveryNGameOvers = 3;   // jedes N-te Game Over
    const float        InterstitialMinIntervalSec  = 60f; // Mindestabstand zwischen Interstitials

    RewardedAd     _rewardedAd;
    InterstitialAd _interstitialAd;
    int            _gameOverCount;
    bool           _initStarted;
    float          _lastInterstitialTime = -9999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        Debug.Log("[Ads] Bootstrap → erstelle AdManager");
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
        Debug.Log($"[Ads] Start ({AdConfig.EnvLabel}) — Consent/Init startet…");

#if UNITY_EDITOR
        // Im Editor liefert UMP keine zuverlässigen Callbacks → direkt initialisieren.
        InitializeAds();
#else
        GatherConsentThenInit();
#if !NEONSMASH_PROD
        StartCoroutine(InitFallback());   // nur Dev/Test: falls UMP nicht zurückkommt, trotzdem initialisieren
#endif
#endif
    }

    // ----------------------------------------------------------------- Consent (UMP)
    void GatherConsentThenInit()
    {
        Debug.Log("[Ads] Hole UMP-Consent (ConsentInformation.Update)…");
        try
        {
            var request = new ConsentRequestParameters();

#if !NEONSMASH_PROD
            // Dev/Test: UMP-Formular erzwingen (EEA simulieren) + gespeicherte Einwilligung
            // zurücksetzen, damit der Consent-Dialog beim Testen zuverlässig erscheint.
            request.ConsentDebugSettings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.EEA,
                TestDeviceHashedIds = new System.Collections.Generic.List<string>
                {
                    // ⚠️ Diese Hash-IDs ändern sich bei jedem Reinstall! Aktuelle ID steht im Log:
                    //    "To get test ads on this device, set: …[ID]". Bei Bedarf hier ersetzen.
                    "80bbb5dfb1665d319f1060f458a82dec",  // iOS (iPhone 15) — Stand 2026-06-22
                    "714678B3ADDAC0476842625EE223415",   // Android (Samsung)
                }
            };
            ConsentInformation.Reset();   // vorhandene Einwilligung verwerfen → Formular kommt wieder
            Debug.Log("[Ads] (DEV) UMP-Debug: EEA erzwungen + Consent zurückgesetzt.");
#endif

            ConsentInformation.Update(request, updateError =>
            {
                Debug.Log("[Ads] Consent-Update zurück" +
                          (updateError != null ? " (Fehler: " + updateError.Message + ")" : ""));

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    Debug.Log("[Ads] Consent abgeschlossen" +
                              (formError != null ? " (Fehler: " + formError.Message + ")" : ""));
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

    void InitializeAds(bool force = false)
    {
        if (_initStarted) return;

        bool canRequest;
        try { canRequest = ConsentInformation.CanRequestAds(); }
        catch { canRequest = true; }   // Editor-Fallback

        if (!force && !canRequest)
        {
            // EEA ohne (noch) erteilte Einwilligung → keine Ads anfordern (compliance-korrekt).
            Debug.Log("[Ads] CanRequestAds=false → noch keine Ads (Consent ausstehend/abgelehnt).");
            return;
        }

        _initStarted = true;
        // Ad-Events auf dem Unity-Mainthread → wir dürfen in Callbacks Unity/CoinManager anfassen.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        // (Keine TestDeviceIds nötig: wir nutzen im Dev ohnehin Googles Test-Ad-Units, die immer
        //  Test-Ads liefern. Die Geräte-Hash-ID ändert sich zudem bei jedem Reinstall.)

        Debug.Log($"[Ads] initialisiere MobileAds… (canRequest={canRequest}, force={force})");

        MobileAds.Initialize(_ =>
        {
            IsInitialized = true;
            Debug.Log($"[Ads] MobileAds initialisiert ({AdConfig.EnvLabel}).");
            LoadRewarded();
            LoadInterstitial();
        });
    }

#if !NEONSMASH_PROD
    // Nur Dev/Test: wenn der UMP-Consent-Callback nicht zurückkommt, nach 5s trotzdem
    // initialisieren, damit Test-Ads laufen. In Produktion (NEONSMASH_PROD) NICHT kompiliert.
    System.Collections.IEnumerator InitFallback()
    {
        yield return new WaitForSecondsRealtime(5f);
        if (!_initStarted)
        {
            Debug.LogWarning("[Ads] (DEV) UMP-Consent nach 5s nicht zurück → initialisiere trotzdem für den Test.");
            InitializeAds(force: true);
        }
    }
#endif

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
            Debug.Log("[Ads] Rewarded geladen, bereit.");
            ad.OnAdFullScreenContentClosed += LoadRewarded;          // nächstes vorladen
            ad.OnAdFullScreenContentFailed += _ => LoadRewarded();
        });
    }

    /// <summary>Zeigt ein Rewarded-Video. onReward NUR, wenn der Nutzer es zu Ende schaut.</summary>
    public void ShowRewarded(Action onReward, Action onUnavailable = null)
    {
        if (!IsRewardedReady)
        {
            Debug.Log("[Ads] Rewarded noch nicht bereit.");
            onUnavailable?.Invoke();
            if (IsInitialized) LoadRewarded();
            return;
        }
        _rewardedAd.Show(_ => onReward?.Invoke());
    }

    /// <summary>Free-Coins-Button: Video → RewardedCoinAmount Coins gutschreiben.</summary>
    public void ShowRewardedForCoins(Action<int> onGranted = null, Action onUnavailable = null)
    {
        ShowRewarded(
            () => { CoinManager.AddCoins(RewardedCoinAmount); onGranted?.Invoke(RewardedCoinAmount); },
            onUnavailable);
    }

    // ----------------------------------------------------------------- Interstitial
    void LoadInterstitial()
    {
        _interstitialAd?.Destroy();
        _interstitialAd = null;

        Debug.Log($"[Ads] Lade Interstitial: {AdConfig.InterstitialId}");
        InterstitialAd.Load(AdConfig.InterstitialId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning("[Ads] Interstitial laden fehlgeschlagen: " + error);
                if (isActiveAndEnabled) StartCoroutine(RetryLoadInterstitial());  // No-Fill heilt sich
                return;
            }
            _interstitialAd = ad;
            Debug.Log("[Ads] Interstitial geladen, bereit.");
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

        Debug.Log($"[Ads] GameOver #{_gameOverCount} → due={due} cooled={cooled} ready={ready} init={IsInitialized}");

        if (!IsInitialized || !due || !cooled || !ready)
        {
            if (IsInitialized && _interstitialAd == null) LoadInterstitial();
            onClosed?.Invoke();
            return;
        }

        _lastInterstitialTime = Time.realtimeSinceStartup;
        Debug.Log("[Ads] Zeige Interstitial.");

        bool fired = false;
        void Done() { if (fired) return; fired = true; onClosed?.Invoke(); }

        _interstitialAd.OnAdFullScreenContentClosed += Done;
        _interstitialAd.OnAdFullScreenContentFailed += _ => Done();
        _interstitialAd.Show();
    }
}
