using UnityEngine;

public class ConsentManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject consentCanvas;

    [Tooltip("Canvas mit Impressumsinformationen.")]
    public GameObject impressumCanvas;

    [Header("Rechtliches")]
    [Tooltip("Adresse der Datenschutzerklaerung.")]
    [SerializeField] private string privacyUrl = "https://sheronyx.com/privacy";
    [Tooltip("Adresse der Nutzungsbedingungen. MUSS vor dem Release auf die echte Seite zeigen -- "
           + "ein toter Link ist schlechter als gar keiner.")]
    [SerializeField] private string termsUrl = "https://sheronyx.com/tos";

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

    public void OnMoreInfo() => OpenPrivacyWebsite();

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
        if (string.IsNullOrEmpty(privacyUrl)) { Debug.LogWarning("[Consent] Keine Datenschutz-Adresse gesetzt."); return; }
        Application.OpenURL(privacyUrl);
    }

    /// <summary>Oeffnet die Nutzungsbedingungen im Browser. Haengt am Einstellungs-Knopf und an
    /// der Hinweiszeile im Startmenue.</summary>
    public void OpenTermsWebsite()
    {
        if (string.IsNullOrEmpty(termsUrl)) { Debug.LogWarning("[Consent] Keine AGB-Adresse gesetzt."); return; }
        Application.OpenURL(termsUrl);
    }

    // Öffnet das UMP-Datenschutz-Formular, damit Nutzer ihre Einwilligung jederzeit
    // ändern/widerrufen können (GDPR-Pflicht). An einen „Einwilligung verwalten"-Button hängen.
    public void OnManageConsent()
    {
        // Frueher ein stilles "AdManager.Instance?.ShowPrivacyOptions()". Wenn dabei nichts
        // passierte, war nicht erkennbar warum -- jeder der drei Schritte konnte lautlos
        // scheitern. Jetzt sagt die Konsole, woran es liegt.
        if (AdManager.Instance == null)
        {
            Debug.LogWarning("[Consent] Kein AdManager in der Szene -- das Datenschutz-Formular "
                           + "kann nicht geoeffnet werden.");
            return;
        }
        AdManager.Instance.ShowPrivacyOptions();
    }
}
