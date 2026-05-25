using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class TimeModeProgress
{
    const string CloudKeyTutorial = "tutorial_completed";
    const string PrefKeyTutorial  = "TutorialCompleted_v1";

    public static bool IsTutorialCompleted => PlayerPrefs.GetInt(PrefKeyTutorial, 0) == 1;

    public static async Task LoadFromCloudAsync()
    {
        try
        {
            string pid = "?";
            try { pid = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId; } catch {}
            Debug.Log($"[DIAG][Progress] LoadFromCloudAsync START — PlayerId={pid}");

            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKeyTutorial });

            Debug.Log($"[DIAG][Progress] Cloud geladen — Keys gefunden: {result.Count} ({string.Join(",", result.Keys)})");

            if (result.TryGetValue(CloudKeyTutorial, out var tutorialItem) && tutorialItem.Value.GetAs<int>() == 1 && !IsTutorialCompleted)
            {
                PlayerPrefs.SetInt(PrefKeyTutorial, 1);
                Debug.Log("[Progress] Cloud: Tutorial wiederhergestellt.");
            }

            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DIAG][Progress] Cloud Load fehlgeschlagen: {e.Message}");
        }
    }

    public static async Task SetTutorialCompletedAsync()
    {
        PlayerPrefs.SetInt(PrefKeyTutorial, 1);
        PlayerPrefs.Save();

        try
        {
            string pid = "?";
            try { pid = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId; } catch {}
            Debug.Log($"[DIAG][Progress] SetTutorialCompletedAsync — PlayerId={pid}");

            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKeyTutorial, 1 } });
            Debug.Log("[DIAG][Progress] Cloud: Tutorial gespeichert OK.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DIAG][Progress] Cloud Save fehlgeschlagen: {e.Message}");
        }
    }
}
