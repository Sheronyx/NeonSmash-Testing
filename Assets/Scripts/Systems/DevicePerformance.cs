using UnityEngine;

public static class DevicePerformance
{
    private const string PrefKey = "QualityMode"; // "auto" | "high" | "low"

    public static bool IsLowEnd { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Evaluate()
    {
        string pref = PlayerPrefs.GetString(PrefKey, "auto");

        if (pref == "high")
            IsLowEnd = false;
        else if (pref == "low")
            IsLowEnd = true;
        else
            IsLowEnd = DetectLowEnd(); // auto

        Debug.Log($"[DevicePerformance] Mode={pref} IsLowEnd={IsLowEnd}");
    }

    private static bool DetectLowEnd()
    {
        bool lowGpuMemory = SystemInfo.graphicsMemorySize < 2048;
        bool oldApi = SystemInfo.graphicsDeviceType ==
                      UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2;
        return lowGpuMemory || oldApi;
    }

    // Wird vom Settings-UI aufgerufen
    public static void SetQuality(bool lowEnd)
    {
        IsLowEnd = lowEnd;
        PlayerPrefs.SetString(PrefKey, lowEnd ? "low" : "high");
        PlayerPrefs.Save();
    }

    public static bool IsUserOverride()
    {
        return PlayerPrefs.GetString(PrefKey, "auto") != "auto";
    }

    public static void ResetToAuto()
    {
        PlayerPrefs.SetString(PrefKey, "auto");
        PlayerPrefs.Save();
        IsLowEnd = DetectLowEnd();
    }
}
