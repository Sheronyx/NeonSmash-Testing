using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;

public class UgsBootstrap : MonoBehaviour
{
    public static UgsBootstrap Instance { get; private set; }

    [Header("UGS Init")]
    [SerializeField] string environmentName = "production";
    [SerializeField, Tooltip("Timeout für anonymes Sign-In (ms)")] int signInTimeoutMs = 1800;
    [SerializeField, Tooltip("Verzögerung für Plattform-Login (s)")] float platformDeferSeconds = 1.5f;

    private static TaskCompletionSource<bool> _tcs;
    public static Task<bool> Initialization => _tcs?.Task ?? Task.FromResult(false);
    public static bool IsReadyOnline => _tcs != null && _tcs.Task.IsCompleted && _tcs.Task.Result;

    // Resolves after GPGS / Game Center auth — scores must wait for this before submitting
    private static TaskCompletionSource<bool> _platformAuthTcs;
    public static Task<bool> PlatformAuthReady => _platformAuthTcs?.Task ?? Task.FromResult(true);

    // Resolves after platform auth + Friends SDK init — use this instead of Initialization in FriendsScreen
    private static TaskCompletionSource<bool> _friendsTcs;
    public static Task<bool> FriendsReady => _friendsTcs?.Task ?? Task.FromResult(false);

    static bool _started;
    public static bool HasBegun => _started;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void Begin()
    {
        if (_started) return;
        if (Instance == null)
        {
            var go = new GameObject("UgsBootstrap");
            Instance = go.AddComponent<UgsBootstrap>();
            DontDestroyOnLoad(go);
        }
        _started = true;
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _platformAuthTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _friendsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Instance.InitAsync();
    }

    async Task InitAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var opts = new InitializationOptions().SetEnvironmentName(environmentName);
#if UNITY_EDITOR
                if (ParrelSync.ClonesManager.IsClone())
                    opts.SetProfile("clone");
#endif
                await UnityServices.InitializeAsync(opts);
                Debug.Log("[UGS] Initialized (" + environmentName + ")");
            }

            // 1) Lagfreies anonymes Sign-In
            await SignInAnonWithTimeout(signInTimeoutMs);

            string pidAfterAnon = "?";
            try { pidAfterAnon = AuthenticationService.Instance.PlayerId; } catch {}
            Debug.Log($"[DIAG] AnonSignIn fertig — PlayerId={pidAfterAnon}, IsSignedIn={AuthenticationService.Instance.IsSignedIn}");

            // 2) Fortschritt aus Cloud laden — vor Initialization, damit Scenes direkt korrekte Werte sehen
            await TimeModeProgress.LoadFromCloudAsync();

            // 3) Online bereit
            _tcs.TrySetResult(true);

            // 3b) Firebase Push Notifications initialisieren
            // Friends SDK wird erst nach Platform-Auth initialisiert (in PlatformNameCoroutine),
            // damit der Notification-Channel für die finale Player-ID aufgebaut wird.
            FirebaseNotificationManager.Begin();

            // 3) Plattform-Login & Name (Mainthread, kleine Verzögerung)
            // OfflineScoreSync wird erst nach Platform-Auth geflusht (in PlatformNameCoroutine)
            StartCoroutine(PlatformNameCoroutine());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UGS] Init failed: " + e.Message);
            _tcs.TrySetResult(true);
            _platformAuthTcs.TrySetResult(false);
            StartCoroutine(PlatformNameCoroutine());
        }
    }

   System.Collections.IEnumerator PlatformNameCoroutine()
{
    yield return null;
    if (platformDeferSeconds > 0f)
        yield return new WaitForSecondsRealtime(platformDeferSeconds);

    string pidStart = "?";
    try { pidStart = AuthenticationService.Instance.PlayerId; } catch {}
    Debug.Log($"[DIAG] PlatformNameCoroutine START — PlayerId={pidStart}");

#if UNITY_IOS
    if (AppleGameCenterAuth.Instance == null)
        new GameObject("AppleGameCenterAuth").AddComponent<AppleGameCenterAuth>();

    string iosIdBefore = null;
    try { iosIdBefore = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId; } catch {}
    Debug.Log($"[DIAG] GC-Auth START — PlayerId vor Auth={iosIdBefore}");

    var gcTask = GcGamertagToUgs.TryApplyAsync();
    while (!gcTask.IsCompleted) yield return null;

    string iosIdAfter = null;
    try { iosIdAfter = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId; } catch {}
    bool iosIdChanged = iosIdAfter != null && !string.Equals(iosIdBefore, iosIdAfter, StringComparison.Ordinal);
    Debug.Log($"[DIAG] GC-Auth DONE — PlayerId vorher={iosIdBefore}, nachher={iosIdAfter}, geändert={iosIdChanged}");

    // Re-save FCM token if player ID changed so push notifications reach the correct account
    if (iosIdChanged)
        FirebaseNotificationManager.Instance?.ResaveTokenForNewPlayer();

    LogAuthState();
#endif

#if UNITY_ANDROID
    // --- GPGS Login + Server-Auth-Code ---
    if (GooglePlayGamesAuth.Instance == null)
        new GameObject("GooglePlayGamesAuth").AddComponent<GooglePlayGamesAuth>();

    string androidIdBefore = "?";
    try { androidIdBefore = AuthenticationService.Instance.PlayerId; } catch {}
    Debug.Log($"[DIAG] GPGS-Auth START — PlayerId vor Auth={androidIdBefore}");

    var req = GooglePlayGamesAuth.Instance.RequestServerAuthCodeAsync(6000);
    while (!req.IsCompleted) yield return null;
    var (code, err) = req.Result;

    bool actualIdChanged = false;
    if (!string.IsNullOrEmpty(err))
    {
        Debug.LogWarning($"[DIAG] GPGS auth code error: {err}");
    }
    else
    {
        // 1) Link versuchen
        var linkT = Unity.Services.Authentication.AuthenticationService.Instance
                        .LinkWithGooglePlayGamesAsync(code);
        while (!linkT.IsCompleted) yield return null;

        bool playerIdChanged = false;

        if (linkT.IsFaulted)
        {
            Debug.Log($"[DIAG] GPGS Link faulted ({linkT.Exception?.GetBaseException().Message}) → fordere neuen Code an");
            // Auth code is single-use — consumed by the Link attempt even when it fails.
            // Request a fresh code before SignIn, otherwise Google rejects it.
            var req2 = GooglePlayGamesAuth.Instance.RequestServerAuthCodeAsync(6000);
            while (!req2.IsCompleted) yield return null;
            var (code2, err2) = req2.Result;

            if (!string.IsNullOrEmpty(err2))
            {
                Debug.LogWarning($"[DIAG] GPGS zweiter Auth-Code fehlgeschlagen: {err2}");
            }
            else
            {
                // SignOut required before SignInWithGooglePlayGames when already signed in
                Debug.Log("[DIAG] GPGS SignOut vor SignIn...");
                Unity.Services.Authentication.AuthenticationService.Instance.SignOut(false);

                Debug.Log($"[DIAG] GPGS SignIn mit neuem Code...");
                var signT = Unity.Services.Authentication.AuthenticationService.Instance
                                .SignInWithGooglePlayGamesAsync(code2);
                while (!signT.IsCompleted) yield return null;

                if (signT.IsFaulted)
                {
                    Debug.LogWarning("[DIAG] GPGS SignIn failed: " + signT.Exception?.GetBaseException().Message);
                    // Restore anonymous session so the player isn't stuck logged-out
                    Debug.Log("[DIAG] GPGS SignIn fehlgeschlagen → anonymes Fallback-Login");
                    var anonT = Unity.Services.Authentication.AuthenticationService.Instance.SignInAnonymouslyAsync();
                    while (!anonT.IsCompleted) yield return null;
                    if (anonT.IsFaulted)
                        Debug.LogWarning("[DIAG] Anonymes Fallback-Login fehlgeschlagen: " + anonT.Exception?.GetBaseException().Message);
                    else
                        Debug.Log("[DIAG] Anonymes Fallback-Login OK");
                }
                else
                {
                    Debug.Log("[UGS][GPGS] Signed in to existing linked account.");
                    playerIdChanged = true;
                }
            }
        }
        else
        {
            Debug.Log("[UGS][GPGS] Linked Google Play Games identity!");
        }

        string androidIdAfter = "?";
        try { androidIdAfter = AuthenticationService.Instance.PlayerId; } catch {}
        actualIdChanged = !string.Equals(androidIdBefore, androidIdAfter, StringComparison.Ordinal);
        Debug.Log($"[DIAG] GPGS-Auth DONE — PlayerId vorher={androidIdBefore}, nachher={androidIdAfter}, playerIdChanged={playerIdChanged}, actualIdChanged={actualIdChanged}");

        // Re-save FCM token if player ID changed so push notifications reach the correct account
        if (playerIdChanged || actualIdChanged)
            FirebaseNotificationManager.Instance?.ResaveTokenForNewPlayer();
    }

    // 2) Namen aus GPGS → UGS übernehmen (ohne try/catch+yields)
    var gpgsName = GooglePlayGamesAuth.Instance.CurrentUserNameOrNull();
    if (!string.IsNullOrWhiteSpace(gpgsName))
    {
        string current = null;
        try { current = Unity.Services.Authentication.AuthenticationService.Instance.PlayerName; } catch {}

        // Strip #discriminator before comparing — PlayerName is "Name#1234", gpgsName is "Name"
        string currentBase = current;
        if (current != null) { int h = current.IndexOf('#'); if (h > 0) currentBase = current.Substring(0, h); }

        // Guard: if PlayerName is null on an existing account, skip update — calling UpdatePlayerNameAsync
        // with null current would assign a new random discriminator and change "Max#1234" to "Max#5678".
        bool isExistingAccount = actualIdChanged;
        bool nameNotLoaded     = string.IsNullOrEmpty(current);
        Debug.Log($"[DIAG][Discriminator] gpgsName='{gpgsName}', current='{current}', currentBase='{currentBase}', isExistingAccount={isExistingAccount}, nameNotLoaded={nameNotLoaded}");
        if (isExistingAccount && nameNotLoaded)
        {
            Debug.LogWarning("[UGS][GPGS] PlayerName null auf bestehendem Account — Name-Update übersprungen um Discriminator zu bewahren.");
        }
        else if (!string.Equals(currentBase, gpgsName, StringComparison.Ordinal))
        {
            Debug.Log($"[DIAG][Discriminator] Namen unterschiedlich → UpdatePlayerNameAsync wird aufgerufen (vorher='{current}', neu='{gpgsName}')");
            var setTask = Unity.Services.Authentication.AuthenticationService.Instance
                              .UpdatePlayerNameAsync(gpgsName);
            while (!setTask.IsCompleted) yield return null;

            if (setTask.IsFaulted)
            {
                Debug.LogWarning("[UGS][GPGS] UpdatePlayerName failed: " +
                                 setTask.Exception?.GetBaseException().Message);
            }
            else
            {
                PlayerPrefs.SetString("display_name", gpgsName);
                PlayerPrefs.Save();
                Debug.Log("[UGS][GPGS] DisplayName gesetzt: " + gpgsName);
            }
        }
        else
        {
            Debug.Log($"[DIAG][Discriminator] Namen gleich → kein Update, Discriminator bleibt erhalten.");
        }
    }

    LogAuthState();
#endif

    // Platform-Auth ist abgeschlossen — finale Player-ID steht fest
    _platformAuthTcs.TrySetResult(true);

    // Fortschritt aus Cloud laden (jetzt unter der korrekten Player-ID)
    var progressTask = TimeModeProgress.LoadFromCloudAsync();
    while (!progressTask.IsCompleted) yield return null;

    if (OfflineScoreSync.Instance != null)
        _ = OfflineScoreSync.Instance.TryFlushAsync();

    // Friends SDK mit der finalen Player-ID initialisieren (läuft auf allen Plattformen)
    string pidBeforeFriends = "?";
    bool signedIn = false;
    try { pidBeforeFriends = AuthenticationService.Instance.PlayerId; signedIn = AuthenticationService.Instance.IsSignedIn; } catch {}
    Debug.Log($"[DIAG] Vor Friends-Init — PlayerId={pidBeforeFriends}, IsSignedIn={signedIn}, IsInitialized={FriendsHandler.IsInitialized}");

    if (!FriendsHandler.IsInitialized && signedIn)
    {
        var friendsTask = FriendsHandler.InitializeAsync();
        while (!friendsTask.IsCompleted) yield return null;
        Debug.Log($"[DIAG] Friends-Init Task beendet — IsFaulted={friendsTask.IsFaulted}, IsInitialized={FriendsHandler.IsInitialized}");
        if (friendsTask.IsFaulted)
            Debug.LogWarning($"[DIAG] Friends-Init Exception: {friendsTask.Exception?.GetBaseException().Message}");
    }
    else if (!signedIn)
    {
        Debug.LogWarning("[DIAG] Friends-Init ÜBERSPRUNGEN — IsSignedIn=false!");
    }
    else
    {
        Debug.Log("[DIAG] Friends-Init ÜBERSPRUNGEN — war bereits initialisiert");
    }

    Debug.Log($"[DIAG] FriendsReady TCS → {FriendsHandler.IsInitialized}");
    _friendsTcs?.TrySetResult(FriendsHandler.IsInitialized);
}

    void OnApplicationPause(bool paused)
    {
        string pid = "?";
        try { pid = AuthenticationService.Instance.PlayerId; } catch {}
        Debug.Log($"[DIAG] OnApplicationPause(paused={paused}) — IsInitialized={FriendsHandler.IsInitialized}, PlayerId={pid}");
        if (paused)
            _ = FriendsHandler.SetPresenceAsync(false);
        else
            StartCoroutine(DelayedResumePresenceCoroutine());
    }

    System.Collections.IEnumerator DelayedResumePresenceCoroutine()
    {
        // Wait for OS-queued Offline requests to land before setting Online,
        // otherwise the Offline from the pause arrives ~47s late and overwrites Online.
        yield return new WaitForSecondsRealtime(2f);
        Debug.Log("[DIAG] DelayedResumePresence: Setze Online nach 2s Wartezeit");
        _ = FriendsHandler.SetPresenceAsync(true);
    }

    static async Task SignInAnonWithTimeout(int timeoutMs)
    {
        if (AuthenticationService.Instance.IsSignedIn) return;
        try
        {
            var t = AuthenticationService.Instance.SignInAnonymouslyAsync();
            var done = await Task.WhenAny(t, Task.Delay(timeoutMs));
            if (done != t)
            {
                Debug.Log("[UGS] anon sign-in timeout → weiter offline");
                return;
            }
            await t;
            Debug.Log("[UGS] signed in anonymously");
        }
        catch (Exception ex)
        {
            Debug.Log("[UGS] anon sign-in failed: " + ex.Message);
        }
    }

    // -------- Debug: zeigt PlayerId, PlayerName (robust, ohne SDK-Property-Fallen) --------
    void LogAuthState()
    {
        try
        {
            var auth = AuthenticationService.Instance;
            Debug.Log($"[AUTH] PlayerId={auth.PlayerId}, Name='{auth.PlayerName}'");

            var info = auth.PlayerInfo;
            var ids = info?.Identities;

            if (ids == null || ids.Count == 0)
            {
                Debug.Log("[AUTH] Identities: (none)");
            }
            else
            {
                Debug.Log($"[AUTH] Identities count = {ids.Count}");
                for (int i = 0; i < ids.Count; i++)
                    Debug.Log($"[AUTH] Identity[{i}] (provider details hidden)");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AUTH] LogAuthState failed: " + e.Message);
        }
    }
}
