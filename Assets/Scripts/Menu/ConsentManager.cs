using UnityEngine;

public class ConsentManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject consentCanvas;

    [Tooltip("Canvas mit Impressumsinformationen.")]
    public GameObject impressumCanvas;

    // Das frühere Custom-Consent-Panel wird NICHT mehr automatisch beim Start gezeigt.
    // Die GDPR-/Ads-Einwilligung übernimmt komplett Google UMP (siehe AdManager).
    // Impressum/Datenschutz erscheinen nur noch auf Klick (OnImpressumCanvas / OnManageConsent).

    public void OnConsentGiven()
    {
        // Beibehalten für evtl. noch vorhandene alte Buttons — schließt nur das Panel.
        PlayerPrefs.SetInt("consent_given", 1);
        PlayerPrefs.Save();
        if (consentCanvas) consentCanvas.SetActive(false);
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

    // Öffnet das UMP-Datenschutz-Formular, damit Nutzer ihre Einwilligung jederzeit
    // ändern/widerrufen können (GDPR-Pflicht). An einen „Einwilligung verwalten"-Button hängen.
    public void OnManageConsent()
    {
        AdManager.Instance?.ShowPrivacyOptions();
    }
}
