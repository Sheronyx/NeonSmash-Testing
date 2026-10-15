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
    // Wird gesetzt, wenn der Nutzer die Einwilligung im Datenschutz-Formular widerrufen hat.
    // Verhindert, dass in derselben Sitzung noch Anzeigen nachgeladen werden.
    bool           _einwilligungEntzogen;
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
        // Das Formular gibt es nur, wenn UMP es fuer diesen Nutzer vorsieht -- ausserhalb des
        // EWR und im Editor in der Regel nicht. Vorher pruefen, sonst passiert beim Antippen
        // kommentarlos nichts und es sieht nach einem kaputten Knopf aus.
        try
        {
            var status = ConsentInformation.PrivacyOptionsRequirementStatus;
            if (status != PrivacyOptionsRequirementStatus.Required)
            {
                Debug.Log("[Ads] Datenschutz-Formular nicht erforderlich (Status: " + status
                        + "). Das ist kein Fehler -- ausserhalb des EWR und im Editor ist das normal.");
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Ads] Status des Datenschutz-Formulars nicht abfragbar: " + e.Message);
        }

#if UNITY_EDITOR
        // Im Editor laeuft nur der Platzhalter des Plugins (GoogleMobileAds.Ump.Unity). Der hat
        // kein echtes Formular und meldet immer "Form not found!" -- das ist kein Fehler und
        // sagt nichts ueber das Verhalten auf dem Geraet. Gar nicht erst versuchen.
        Debug.Log("[Ads] Datenschutz-Formular: im Editor nicht verfuegbar (Platzhalter des Plugins). "
                + "Nur auf einem echten Geraet testbar.");
        return;
#else
        Zeige(ersterVersuch: true);
#endif
    }

    // UMP haelt das Formular nur im Speicher, wenn es in dieser Sitzung geladen wurde. Wurde die
    // Einwilligung frueher schon erteilt, steigt LoadAndShowConsentFormIfRequired beim Start
    // sofort aus -- dann ist spaeter nichts da und ShowPrivacyOptionsForm meldet "Form not found".
    // Beim ersten Fehlschlag deshalb die Einwilligungsdaten neu anfordern und ein Mal wiederholen.
    void Zeige(bool ersterVersuch)
    {
        try
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error == null)
                {
                    Debug.Log("[Ads] Datenschutz-Formular geschlossen.");
                    NachFormularNeuBewerten();
                    return;
                }

                Debug.LogWarning("[Ads] Privacy-Options-Form Fehler: " + error.Message);
                if (!ersterVersuch)
                {
                    Debug.LogWarning("[Ads] Auch der zweite Versuch schlug fehl. Moegliche Ursachen: "
                                   + "die Datenschutz-Nachricht ist in der AdMob-Konsole nicht angelegt "
                                   + "oder nicht veroeffentlicht, oder der Nutzer liegt ausserhalb des "
                                   + "Geltungsbereichs der Nachricht.");
                    return;
                }

                Debug.Log("[Ads] Formular nicht geladen -- fordere Einwilligungsdaten neu an und versuche es erneut.");
                try
                {
                    ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
                    {
                        if (updateError != null)
                        {
                            Debug.LogWarning("[Ads] Update der Einwilligungsdaten fehlgeschlagen: " + updateError.Message);
                            return;
                        }
                        Zeige(ersterVersuch: false);
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ads] Zweiter Versuch nicht moeglich: " + e.Message);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Ads] ShowPrivacyOptions nicht verfügbar: " + e.Message);
        }
    }

    // Nach dem Datenschutz-Formular kann der Stand ein anderer sein als beim Start. Ohne diese
    // Auswertung wuerde ein Widerruf erst beim naechsten App-Start greifen -- Messung und
    // Absturzberichte liefen in der laufenden Sitzung weiter. Deshalb hier sofort nachziehen.
    void NachFormularNeuBewerten()
    {
        bool canRequest;
        try { canRequest = ConsentInformation.CanRequestAds(); }
        catch (Exception e)
        {
            Debug.LogWarning("[Ads] Einwilligungsstand nach dem Formular nicht lesbar: " + e.Message);
            return;
        }

        NeonAnalytics.ApplyConsent(canRequest);

        if (!canRequest)
        {
            // Widerruf: geladene Anzeigen verwerfen und nichts mehr nachladen. Das SDK selbst
            // laesst sich nicht zurueckdrehen, aber ab hier wird keine Anzeige mehr angefordert
            // oder gezeigt.
            _einwilligungEntzogen = true;
            _rewardedAd?.Destroy();     _rewardedAd     = null;
            _interstitialAd?.Destroy(); _interstitialAd = null;
            Debug.Log("[Ads] Einwilligung widerrufen -- keine weiteren Anzeigen, Messung aus.");
            return;
        }

        // Umgekehrter Fall: erst abgelehnt, jetzt zugestimmt. Dann soll es ohne Neustart laufen.
        _einwilligungEntzogen = false;
        if (!_initStarted) InitializeAds();
        else if (IsInitialized)
        {
            if (_rewardedAd     == null) LoadRewarded();
            if (_interstitialAd == null) LoadInterstitial();
        }
    }

    void InitializeAds()
    {
        if (_initStarted) return;

        bool canRequest;
        try { canRequest = ConsentInformation.CanRequestAds(); }
        catch { canRequest = true; }   // Editor-Fallback

        // Analytics und Crashlytics an denselben Stand koppeln. CanRequestAds ist true, wenn
        // der Nutzer eingewilligt hat ODER UMP fuer ihn keine Einwilligung verlangt (ausserhalb
        // des EWR). Ein Nutzer, der ablehnt, bekommt damit weder Werbung noch Messung.
        NeonAnalytics.ApplyConsent(canRequest);

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
        if (_einwilligungEntzogen) return;
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
    public void ShowRewarded(Action onReward, Action onUnavailable = null) =>
        ShowRewarded("unknown", onReward, onUnavailable);

    /// <param name="placement">Analytics-Bezeichner für diesen Rewarded-Button (z.B. "dream_energy").</param>
    public void ShowRewarded(string placement, Action onReward, Action onUnavailable = null)
    {
        if (!IsRewardedReady)
        {
            NeonAnalytics.LogAdRewardedUnavailable(placement);
            onUnavailable?.Invoke();
            if (IsInitialized) LoadRewarded();
            return;
        }
        _rewardedAd.Show(_ =>
        {
            NeonAnalytics.LogAdRewardedCompleted(placement);
            onReward?.Invoke();
        });
    }

    /// <summary>Free-Dream-Energy-Button: Video → RewardedDreamEnergyAmount Dream Energy gutschreiben.</summary>
    public void ShowRewardedForDreamEnergy(Action<int> onGranted = null, Action onUnavailable = null)
    {
        ShowRewarded("dream_energy",
            () => { DreamEnergyManager.AddDreamEnergy(RewardedDreamEnergyAmount); onGranted?.Invoke(RewardedDreamEnergyAmount); },
            onUnavailable);
    }

    // ----------------------------------------------------------------- Interstitial
    void LoadInterstitial()
    {
        if (_einwilligungEntzogen) return;
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
        NeonAnalytics.LogAdInterstitialShown();
        _interstitialAd.Show();
    }
}
