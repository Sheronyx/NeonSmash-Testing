using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Messaging;
using Unity.Services.CloudSave;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseNotificationManager : MonoBehaviour
{
    public static FirebaseNotificationManager Instance { get; private set; }

    const string TokenKey         = "fcm_token";
    const string RegisterTokenUrl = "https://registerfcmtoken-gtr5z6qthq-uc.a.run.app";

    // Captured in Awake (main thread) so we can dispatch back to it from Firebase callbacks
    System.Threading.SynchronizationContext _mainCtx;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _mainCtx = System.Threading.SynchronizationContext.Current;
    }

    public static void Begin()
    {
        if (Instance != null) return;
        if (Application.isEditor) return;
        var go = new GameObject("FirebaseNotificationManager");
        go.AddComponent<FirebaseNotificationManager>();
        _ = Instance.InitializeAsync();
    }

    async Task InitializeAsync()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            Debug.LogWarning($"[FCM] Firebase nicht verfügbar: {status}");
            return;
        }

        // Crashlytics — requires FirebaseCrashlytics.unitypackage to be imported
        Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;

#if UNITY_IOS
        await FirebaseMessaging.RequestPermissionAsync();
#elif UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
            UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
#endif

        FirebaseMessaging.TokenReceived   += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;

        string token = await FirebaseMessaging.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            await SaveTokenAsync(token);

        Debug.Log("[FCM] Firebase Messaging initialisiert.");
    }

    public void ResaveTokenForNewPlayer()
    {
        _ = ResaveCurrentTokenAsync();
        try
        {
            string pid = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            NeonAnalytics.SetCrashlyticsUserId(pid);
        }
        catch { }
    }

    async Task ResaveCurrentTokenAsync()
    {
        try
        {
            string token = await FirebaseMessaging.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                await SaveTokenAsync(token);
                Debug.Log("[FCM] Token für neue Player-ID neu gespeichert.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[FCM] Token-Neuregistrierung fehlgeschlagen: {e.Message}");
        }
    }

    void OnTokenReceived(object sender, TokenReceivedEventArgs e)
    {
        _ = SaveTokenAsync(e.Token);
    }

    async Task SaveTokenAsync(string token)
    {
        string pid = "?";
        try { pid = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId; } catch {}
        Debug.Log($"[DIAG] FCM SaveTokenAsync — PlayerId={pid}");

        // UnityWebRequest must run on the main thread — dispatch via captured SynchronizationContext
        _mainCtx?.Post(_ => StartCoroutine(RegisterTokenCoroutine(token, pid)), null);

        try
        {
            var data = new Dictionary<string, object> { { TokenKey, token } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[FCM] Cloud Save write failed: {e.Message}");
        }
    }

    IEnumerator RegisterTokenCoroutine(string token, string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || playerId == "?") yield break;

        string json = "{\"playerId\":\"" + playerId + "\",\"fcmToken\":\"" + token + "\"}";
        var req = new UnityWebRequest(RegisterTokenUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        Debug.Log($"[FCM] Firebase token registration: {(req.result == UnityWebRequest.Result.Success ? "OK" : req.error)}");
        req.Dispose();
    }

    void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        Debug.Log($"[FCM] Notification empfangen: {e.Message.Notification?.Title}");
    }

    void OnDestroy()
    {
#if !UNITY_EDITOR
        FirebaseMessaging.TokenReceived   -= OnTokenReceived;
        FirebaseMessaging.MessageReceived -= OnMessageReceived;
#endif
    }
}
