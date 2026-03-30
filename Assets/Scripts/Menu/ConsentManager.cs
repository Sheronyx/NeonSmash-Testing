using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class ConsentManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject consentCanvas;

    [Tooltip("Canvas mit Impressumsinformationen.")]
    public GameObject impressumCanvas;

    [Header("Perf")]
    [Tooltip("Kleine Verzögerung, damit erste Frames (Intro/Fade) nicht ruckeln.")]
    [SerializeField] float deferInitSeconds = 0.3f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Dev")]
    [Tooltip("Erzwingt das Consent-Panel in Editor/Dev-Builds, ohne PlayerPrefs zu löschen.")]
    [SerializeField] bool forceConsentPanelInDev = false;
#endif

    private void Start()
    {
        bool consentGiven = PlayerPrefs.GetInt("consent_given", 0) == 1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (forceConsentPanelInDev)
        {
            consentGiven = false;
            Debug.Log("[Consent] Dev-Flag aktiv → Consent erzwingen.");
        }
#endif

        if (consentGiven)
        {
            StartCoroutine(DeferInit());
        }
        else
        {
            if (consentCanvas) consentCanvas.SetActive(true);
        }
    }

    public void OnConsentGiven()
    {
        PlayerPrefs.SetInt("consent_given", 1);
        PlayerPrefs.Save();
        if (consentCanvas) consentCanvas.SetActive(false);
        StartCoroutine(DeferInit());
    }

    private IEnumerator DeferInit()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(deferInitSeconds);

        UgsBootstrap.Begin();
        _ = UgsBootstrap.Initialization;
        _ = InitializeAnalyticsAsync();
    }

    private async Task InitializeAnalyticsAsync()
    {
        try
        {
            if (UgsBootstrap.Initialization != null)
                _ = await UgsBootstrap.Initialization;

            await Task.Delay(150);

            Debug.Log("[Analytics] Consent gesetzt → Analytics aktiv.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Analytics] delayed start: " + ex.Message);
        }
    }

    public void OnMoreInfo()
    {
        Application.OpenURL("https://sheronyx.com/privacy");
    }

    // 👇 NEU: Öffnet das Impressum-Canvas
    public void OnImpressumCanvas()
    {
        if (impressumCanvas != null)
        {
            impressumCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[Consent] Kein ImpressumCanvas im Inspector zugewiesen!");
        }
    }

    // 👇 NEU: Schließt das Impressum-Canvas
    public void OnCloseImpressumCanvas()
    {
        if (impressumCanvas != null)
        {
            impressumCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[Consent] Kein ImpressumCanvas im Inspector zugewiesen!");
        }
    }

    public void OpenPrivacyWebsite()
    {
        Application.OpenURL("https://sheronyx.com/privacy");
    }


}
