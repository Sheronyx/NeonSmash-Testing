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
        // Der frühere OpenGLES2-Check ist entfallen: Unity unterstützt OpenGL ES 2.0 seit 2023.1
        // nicht mehr, SystemInfo.graphicsDeviceType kann diesen Wert also nie mehr liefern —
        // der Vergleich war seitdem permanent tot (CS0618-Warnung, kein Verhaltenseffekt entfernt).
        return SystemInfo.graphicsMemorySize < 2048;
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
