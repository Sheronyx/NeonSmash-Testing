using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
internal static class GCNative
{
    [DllImport("__Internal")]
    public static extern void GCRequestIdentitySignature(string unityObject, string unityMethod);
}
#endif

[Serializable]
public class GcIdentityPayload
{
    public string signature;
    public string teamPlayerId;
    public string publicKeyURL;
    public string salt;
    public ulong timestamp;
    public string error;
}

public class AppleGameCenterAuth : MonoBehaviour
{
    public static AppleGameCenterAuth Instance { get; private set; }
    TaskCompletionSource<GcIdentityPayload> _tcs;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public Task<GcIdentityPayload> RequestIdentityAsync(int timeoutMs = 5000)
    {
        if (_tcs != null && !_tcs.Task.IsCompleted) return _tcs.Task;
        _tcs = new TaskCompletionSource<GcIdentityPayload>(TaskCreationOptions.RunContinuationsAsynchronously);

#if UNITY_IOS && !UNITY_EDITOR
        try { GCNative.GCRequestIdentitySignature(gameObject.name, nameof(OnGcIdentitySignature)); }
        catch (Exception e) { _tcs.TrySetResult(new GcIdentityPayload { error = e.Message }); }
#else
        _tcs.TrySetResult(new GcIdentityPayload { error = "not_ios" });
#endif

        _ = Task.Run(async () =>
        {
            await Task.Delay(timeoutMs);
            if (!_tcs.Task.IsCompleted)
                _tcs.TrySetResult(new GcIdentityPayload { error = "timeout" });
        });

        return _tcs.Task;
    }

    public void OnGcIdentitySignature(string json)
    {
        try
        {
            var payload = JsonUtility.FromJson<GcIdentityPayload>(string.IsNullOrEmpty(json) ? "{}" : json);
            _tcs?.TrySetResult(payload ?? new GcIdentityPayload { error = "json_null" });
        }
        catch (Exception e)
        {
            _tcs?.TrySetResult(new GcIdentityPayload { error = "json_error: " + e.Message });
        }
    }
}
