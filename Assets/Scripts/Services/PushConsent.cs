using UnityEngine;

/// <summary>
/// Haelt die Entscheidung des Spielers ueber Push-Benachrichtigungen.
///
/// Warum das ueberhaupt noetig ist: Auf iOS und ab Android 13 fragt das Betriebssystem selbst,
/// und ein Widerruf in den Systemeinstellungen wird beim naechsten Start erkannt. Vor Android 13
/// gibt es diese Abfrage NICHT -- dort wuerde der Push-Token ohne jede Entscheidung des Spielers
/// registriert, und ein Abschalten in den Systemeinstellungen beendet nur die Anzeige, nicht die
/// Uebertragung. Dieser Schalter ist dort die Einwilligung und zugleich der Widerrufsweg; auf den
/// neueren Systemen ist er der bequeme Weg, ohne das Konto loeschen zu muessen.
/// </summary>
public static class PushConsent
{
    const string PrefKey        = "push_enabled";
    const string LoeschungOffen = "push_delete_pending";

    /// <summary>Hat der Spieler Push-Benachrichtigungen erlaubt?</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, Standardwert() ? 1 : 0) == 1;
        private set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>True, solange der Spieler noch nie selbst entschieden hat.</summary>
    public static bool NieGefragt => !PlayerPrefs.HasKey(PrefKey);

    /// <summary>
    /// Eine Loeschung des Tokens auf dem Server steht noch aus -- etwa weil beim Abschalten
    /// keine Verbindung bestand. Wird beim naechsten Start nachgeholt.
    /// </summary>
    public static bool LoeschungAusstehend
    {
        get => PlayerPrefs.GetInt(LoeschungOffen, 0) == 1;
        set { PlayerPrefs.SetInt(LoeschungOffen, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void Merken(bool erlaubt) => Enabled = erlaubt;

    // Vor Android 13 bewusst AUS: ohne Systemabfrage gaebe es sonst eine Uebertragung, der
    // niemand zugestimmt hat. Ueberall sonst AN, weil dort das Betriebssystem selbst fragt und
    // eine Ablehnung dort ohnehin greift.
    static bool Standardwert() => !IstAltesAndroid;

    /// <summary>Android aelter als 13 (API 33) -- dort gibt es keine System-Berechtigungsabfrage.</summary>
    public static bool IstAltesAndroid
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        get => AndroidApiStufe() < 33;
#else
        get => false;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // SystemInfo.operatingSystem sieht auf Android so aus: "Android OS 13 / API-33 (TQ1A/...)".
    static int AndroidApiStufe()
    {
        string s = SystemInfo.operatingSystem ?? string.Empty;
        int i = s.IndexOf("API-", System.StringComparison.Ordinal);
        if (i < 0) return 0;
        int j = i + 4, wert = 0;
        while (j < s.Length && char.IsDigit(s[j])) { wert = wert * 10 + (s[j] - '0'); j++; }
        return wert;
    }
#endif
}
