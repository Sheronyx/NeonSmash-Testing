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
    [Tooltip("Gegenstueck zum Registrieren -- entfernt den Token aus Firestore.")]
    const string DeleteTokenUrl   = "https://deletefcmtoken-gtr5z6qthq-uc.a.run.app";

    /// <summary>True, sobald der Nutzer die Systemabfrage fuer Benachrichtigungen erlaubt hat.
    /// Solange das false ist, wird KEIN Push-Token an den Server uebertragen.</summary>
    public static bool BenachrichtigungenErlaubt { get; private set; }

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

        // Crashlytics — requires FirebaseCrashlytics.unitypackage to be imported.
        // Wie berichtet wird, nicht OB berichtet wird: das Sammeln selbst haengt an der
        // Einwilligung und wird direkt darunter gesetzt.
        Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;

        // Firebase ist jetzt bereit -- den aktuellen Einwilligungsstand durchsetzen. Ohne das
        // wuerde Analytics ab hier automatisch zu sammeln beginnen, das ist der SDK-Standard.
        NeonAnalytics.ReapplyToFirebase();

        // Beim letzten Abschalten kam die Loeschung nicht durch (keine Verbindung oder Funktion
        // noch nicht ausgerollt) -- jetzt nachholen, bevor irgendetwas anderes passiert.
        if (PushConsent.LoeschungAusstehend)
        {
            bool weg = await DeleteTokenAsync();
            PushConsent.LoeschungAusstehend = !weg;
            Debug.Log("[FCM] Nachgeholte Token-Loeschung: " + (weg ? "erfolgreich" : "weiter offen"));
        }

        // Ab hier geht es nur noch um Push. Alles darueber (Firebase, Crashlytics, Einwilligung
        // fuer Analytics) muss auch dann laufen, wenn der Spieler keine Benachrichtigungen will.
        if (!PushConsent.Enabled)
        {
            Debug.Log("[FCM] Push ist abgeschaltet -- kein Token, keine Uebertragung.");
            return;
        }

        await PushStartenAsync();
    }

    // Systemabfrage und Registrierung. Getrennt, damit der Schalter in den Einstellungen das
    // nachholen kann, ohne die App neu zu starten.
    async Task PushStartenAsync()
    {
        // Erst fragen, dann die Antwort abwarten -- und nur bei Zustimmung weitermachen. Vorher
        // wurde die Android-Abfrage nur angestossen und der Token sofort danach geholt, also noch
        // waehrend der Dialog offen stand. Damit landete er auch bei einer Ablehnung auf dem Server.
        bool erlaubt = await SystemabfrageAsync();
        BenachrichtigungenErlaubt = erlaubt;

        if (!erlaubt)
        {
            Debug.Log("[FCM] Benachrichtigungen vom System abgelehnt -- kein Token, keine Uebertragung.");
            return;
        }

        FirebaseMessaging.TokenReceived   += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;

        try
        {
            string token = await FirebaseMessaging.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                await SaveTokenAsync(token);
        }
        catch (System.Exception e)
        {
            // Auf iOS schlaegt das fehl, wenn keine APNs-Registrierung zustande kam. Dann gibt es
            // keinen Token -- und damit auch nichts zu uebertragen.
            Debug.LogWarning("[FCM] Token nicht verfuegbar: " + e.Message);
            return;
        }

        Debug.Log("[FCM] Firebase Messaging initialisiert.");
    }

    // Fragt die Systemberechtigung ab und liefert, ob sie erteilt wurde.
    async Task<bool> SystemabfrageAsync()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // Die Firebase-Schnittstelle meldet das Ergebnis nicht zurueck. Auf iOS ist eine Ablehnung
        // aber ohnehin bindend: ohne Erlaubnis gibt es keine APNs-Registrierung und GetTokenAsync
        // scheitert -- der Aufrufer bricht dann ab, ohne etwas zu senden.
        try { await FirebaseMessaging.RequestPermissionAsync(); }
        catch (System.Exception e) { Debug.LogWarning("[FCM] iOS-Abfrage: " + e.Message); return false; }
        return true;
#elif UNITY_ANDROID && !UNITY_EDITOR
        const string recht = "android.permission.POST_NOTIFICATIONS";

        // Vor Android 13 gibt es dieses Recht nicht -- eine Abfrage wuerde keinen Dialog zeigen
        // und keine Rueckmeldung liefern. Dort ist der Schalter in den Einstellungen die
        // Einwilligung, und bis hierher kommen wir nur, wenn er an ist.
        if (PushConsent.IstAltesAndroid) return true;
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(recht)) return true;

        var antwort = new TaskCompletionSource<bool>();
        var rueckmeldung = new UnityEngine.Android.PermissionCallbacks();
        rueckmeldung.PermissionGranted               += _ => antwort.TrySetResult(true);
        rueckmeldung.PermissionDenied                += _ => antwort.TrySetResult(false);
        rueckmeldung.PermissionDeniedAndDontAskAgain += _ => antwort.TrySetResult(false);
        UnityEngine.Android.Permission.RequestUserPermission(recht, rueckmeldung);

        // Wurde das Recht in den Systemeinstellungen dauerhaft entzogen, zeigt Android keinen
        // Dialog mehr. Ob dann eine Rueckmeldung kommt, ist nicht garantiert -- deshalb mit Frist
        // warten und im Zweifel als Ablehnung behandeln.
        var frist = Task.Delay(30000);
        if (await Task.WhenAny(antwort.Task, frist) == frist) return false;
        return antwort.Task.Result;
#else
        await Task.CompletedTask;
        return false;
#endif
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

    /// <summary>Entfernt den Push-Token dieses Geraets aus Firestore. VOR dem Loeschen des
    /// Unity-Kontos aufzurufen -- danach gibt es keine PlayerID mehr, ueber die der Eintrag
    /// zu finden waere.</summary>
    public async Task<bool> DeleteTokenAsync()
    {
        string token = null, pid = null;
        try
        {
            token = await FirebaseMessaging.GetTokenAsync();
            pid = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[FCM] Token/PlayerId nicht ermittelbar: " + e.Message);
        }

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pid))
        {
            Debug.LogWarning("[FCM] Token-Loeschung uebersprungen -- Token oder PlayerId fehlt.");
            return false;
        }

        var fertig = new TaskCompletionSource<bool>();
        _mainCtx?.Post(_ => StartCoroutine(DeleteTokenCoroutine(token, pid, fertig)), null);
        return await fertig.Task;
    }

    // ---- Schalter in den Einstellungen ----

    /// <summary>Schaltet Push-Benachrichtigungen an oder aus und zieht den Server nach.</summary>
    public static async Task SetEnabledAsync(bool an)
    {
        PushConsent.Merken(an);

        if (an)
        {
            if (Instance == null) { Begin(); return; }
            await Instance.PushStartenAsync();
            return;
        }

        BenachrichtigungenErlaubt = false;
        if (Instance == null) return;

        try { FirebaseMessaging.TokenReceived -= Instance.OnTokenReceived; } catch { }

        bool weg = await Instance.DeleteTokenAsync();
        // Schlaegt es fehl -- keine Verbindung, Funktion noch nicht ausgerollt --, wird es beim
        // naechsten Start nachgeholt. Lokal ist Push ab sofort aus.
        PushConsent.LoeschungAusstehend = !weg;
        Debug.Log("[FCM] Push abgeschaltet. Server-Loeschung: " + (weg ? "erledigt" : "offen"));
    }

    IEnumerator DeleteTokenCoroutine(string token, string playerId, TaskCompletionSource<bool> fertig)
    {
        string json = JsonUtility.ToJson(new RegisterTokenPayload { playerId = playerId, fcmToken = token });
        var req = new UnityWebRequest(DeleteTokenUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        bool ok = req.result == UnityWebRequest.Result.Success;
        Debug.Log("[FCM] Token-Loeschung: " + (ok ? req.downloadHandler.text : req.error));
        req.Dispose();
        fertig.TrySetResult(ok);
    }

    async Task ResaveCurrentTokenAsync()
    {
        if (!BenachrichtigungenErlaubt) return;
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
        if (!BenachrichtigungenErlaubt) return;
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

    [System.Serializable]
    struct RegisterTokenPayload
    {
        public string playerId;
        public string fcmToken;
    }

    IEnumerator RegisterTokenCoroutine(string token, string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || playerId == "?") yield break;

        string json = JsonUtility.ToJson(new RegisterTokenPayload { playerId = playerId, fcmToken = token });
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
