using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Meldet bei jedem Start, dass dieses Spielerkonto noch benutzt wird.
///
/// Wozu: Unity bietet keine Moeglichkeit, Spieler nach Inaktivitaet aufzulisten -- nur das
/// Loeschen eines bekannten Spielers. Ohne eine eigene, abfragbare Liste laesst sich die
/// Loeschfrist fuer inaktive Konten also gar nicht umsetzen. Deshalb liegt der Zeitstempel in
/// Firestore und nicht in Cloud Save: nur dort kann der taegliche Loeschauftrag danach suchen.
///
/// Gespeichert wird ausschliesslich die Unity-PlayerID und der Zeitpunkt des letzten Starts.
/// Beim Loeschen des Kontos wird der Eintrag mit entfernt (siehe AccountPanelUI).
///
/// Geschrieben wird hoechstens einmal taeglich -- fuer eine Frist in Monaten reicht das, und es
/// spart Aufrufe.
/// </summary>
public static class ActivityStamp
{
    // TODO: Nach dem ersten Deploy gegen die echte Adresse tauschen (firebase functions:list).
    const string MeldeUrl  = "https://registeractivity-gtr5z6qthq-uc.a.run.app";
    const string LoeschUrl = "https://deleteactivity-gtr5z6qthq-uc.a.run.app";

    const string LetzteMeldung = "last_seen_lokal";   // Epoch-Millisekunden
    const long   EinTagMs      = 24L * 60L * 60L * 1000L;

    [Serializable]
    struct Inhalt { public string playerId; }

    /// <summary>Faellig, wenn seit der letzten Meldung mehr als ein Tag vergangen ist.</summary>
    public static bool IstFaellig
    {
        get
        {
            long zuletzt = GetLokal();
            return zuletzt <= 0 || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - zuletzt >= EinTagMs;
        }
    }

    /// <summary>Meldet den Start. Als Coroutine, weil UnityWebRequest den Hauptthread braucht.</summary>
    public static IEnumerator MeldenCoroutine()
    {
        if (!IstFaellig) yield break;

        string pid = SpielerId();
        if (string.IsNullOrEmpty(pid)) yield break;

        using (var req = Anfrage(MeldeUrl, pid))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                long jetzt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                PlayerPrefs.SetString(LetzteMeldung, jetzt.ToString());
                PlayerPrefs.Save();
            }
            else
            {
                // Kein Grund, irgendetwas aufzuhalten -- beim naechsten Start wieder versuchen.
                Debug.LogWarning("[Aktivitaet] Meldung fehlgeschlagen: " + req.error);
            }
        }
    }

    /// <summary>
    /// Entfernt den Eintrag. VOR dem Loeschen des Unity-Kontos aufzurufen -- danach gibt es keine
    /// PlayerID mehr, ueber die er zu finden waere.
    /// </summary>
    public static IEnumerator LoeschenCoroutine(Action<bool> fertig = null)
    {
        string pid = SpielerId();
        if (string.IsNullOrEmpty(pid)) { fertig?.Invoke(false); yield break; }

        using (var req = Anfrage(LoeschUrl, pid))
        {
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.LogWarning("[Aktivitaet] Loeschung fehlgeschlagen: " + req.error);
            PlayerPrefs.DeleteKey(LetzteMeldung);
            fertig?.Invoke(ok);
        }
    }

    static UnityWebRequest Anfrage(string url, string pid)
    {
        string json = JsonUtility.ToJson(new Inhalt { playerId = pid });
        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }

    static string SpielerId()
    {
        try { return Unity.Services.Authentication.AuthenticationService.Instance.PlayerId; }
        catch { return null; }
    }

    static long GetLokal()
    {
        string roh = PlayerPrefs.GetString(LetzteMeldung, string.Empty);
        return long.TryParse(roh, out long wert) ? wert : 0L;
    }
}
