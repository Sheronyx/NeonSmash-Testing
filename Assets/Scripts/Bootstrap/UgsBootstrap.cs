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

    static bool _started;

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
        _ = Instance.InitAsync();
    }

    async Task InitAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var opts = new InitializationOptions().SetEnvironmentName(environmentName);
                await UnityServices.InitializeAsync(opts);
                Debug.Log("[UGS] Initialized (" + environmentName + ")");
            }

            // 1) Lagfreies anonymes Sign-In
            await SignInAnonWithTimeout(signInTimeoutMs);

            // 2) Online bereit
            _tcs.TrySetResult(true);

            // 3) Offline-Queue prüfen
            if (OfflineScoreSync.Instance != null)
                _ = OfflineScoreSync.Instance.TryFlushAsync();

            // 4) Plattform-Login & Name (Mainthread, kleine Verzögerung)
            StartCoroutine(PlatformNameCoroutine());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UGS] Init failed: " + e.Message);
            _tcs.TrySetResult(true);
        }
    }

   System.Collections.IEnumerator PlatformNameCoroutine()
{
    yield return null;
    if (platformDeferSeconds > 0f)
        yield return new WaitForSecondsRealtime(platformDeferSeconds);

#if UNITY_IOS
    if (AppleGameCenterAuth.Instance == null)
        new GameObject("AppleGameCenterAuth").AddComponent<AppleGameCenterAuth>();
    var gcTask = GcGamertagToUgs.TryApplyAsync();
    while (!gcTask.IsCompleted) yield return null;
    LogAuthState();
#endif

#if UNITY_ANDROID
    // --- GPGS Login + Server-Auth-Code ---
    if (GooglePlayGamesAuth.Instance == null)
        new GameObject("GooglePlayGamesAuth").AddComponent<GooglePlayGamesAuth>();

    var req = GooglePlayGamesAuth.Instance.RequestServerAuthCodeAsync(6000);
    while (!req.IsCompleted) yield return null;
    var (code, err) = req.Result;

    if (!string.IsNullOrEmpty(err))
    {
        Debug.LogWarning("[UGS][GPGS] auth code error: " + err);
    }
    else
    {
        // 1) Link versuchen
        var linkT = Unity.Services.Authentication.AuthenticationService.Instance
                        .LinkWithGooglePlayGamesAsync(code);
        while (!linkT.IsCompleted) yield return null;

        if (linkT.IsFaulted)
        {
            // Wenn bereits verknüpft → sign-in probieren
            var signT = Unity.Services.Authentication.AuthenticationService.Instance
                            .SignInWithGooglePlayGamesAsync(code);
            while (!signT.IsCompleted) yield return null;

            if (signT.IsFaulted)
                Debug.LogWarning("[UGS][GPGS] SignIn failed: " + signT.Exception?.GetBaseException().Message);
            else
                Debug.Log("[UGS][GPGS] Signed in to existing linked account.");
        }
        else
        {
            Debug.Log("[UGS][GPGS] Linked Google Play Games identity!");
        }
    }

    // 2) Namen aus GPGS → UGS übernehmen (ohne try/catch+yields)
    var gpgsName = GooglePlayGamesAuth.Instance.CurrentUserNameOrNull();
    if (!string.IsNullOrWhiteSpace(gpgsName))
    {
        string current = null;
        try { current = Unity.Services.Authentication.AuthenticationService.Instance.PlayerName; } catch {}

        if (!string.Equals(current, gpgsName, StringComparison.Ordinal))
        {
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
    }

    LogAuthState();
#endif
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
