using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Kontobereich im Einstellungsfenster: zeigt, ob der Spielstand an ein Plattform-Konto gebunden
// ist, bietet das Nachholen der Verknuepfung an und stellt Kaeufe wieder her.
//
// Warum beides noetig ist: Die Verknuepfung mit Google Play Games bzw. Apple Game Center laeuft
// beim Start automatisch (UgsBootstrap.PlatformNameCoroutine). Lehnt der Spieler dort ab oder ist
// er offline, bleibt das Konto anonym -- und anonyme Konten ueberstehen keine Neuinstallation.
// Ohne eine sichtbare Stelle im Spiel haette er keine Moeglichkeit, das je nachzuholen.
public class AccountPanelUI : MonoBehaviour
{
    [Header("Status")]
    [Tooltip("Zeigt an, ob der Fortschritt an ein Konto gebunden ist.")]
    [SerializeField] private TextMeshProUGUI statusLabel;
    [Tooltip("Spieler-Kennung zum Weitergeben an den Support.")]
    [SerializeField] private TextMeshProUGUI playerIdLabel;

    [Header("Knöpfe")]
    [Tooltip("Nur sichtbar, solange das Konto NICHT verknuepft ist.")]
    [SerializeField] private Button linkButton;
    [Tooltip("Stellt Kaeufe und freigeschaltete Inhalte wieder her.")]
    [SerializeField] private Button restoreButton;
    [SerializeField] private TextMeshProUGUI restoreResultLabel;

    [Header("Konto löschen")]
    [Tooltip("Oeffnet die Sicherheitsabfrage.")]
    [SerializeField] private Button deleteButton;
    [Tooltip("Die Sicherheitsabfrage selbst. Startet ausgeschaltet.")]
    [SerializeField] private GameObject deleteConfirmPanel;
    [SerializeField] private TextMeshProUGUI deleteConfirmText;
    [Tooltip("Bestaetigt das Loeschen -- danach gibt es kein Zurueck.")]
    [SerializeField] private Button deleteConfirmButton;
    [SerializeField] private Button deleteCancelButton;

    [Header("Texte")]
    [SerializeField] private string textLinked     = "Progress is saved to your account";
    [SerializeField] private string textNotLinked  = "Progress is only on this device";
    [SerializeField] private string textRestoring  = "Restoring...";
    [SerializeField] private string textRestored   = "Restored";
    [SerializeField] private string textRestoreErr = "Could not restore. Check your connection.";
    [TextArea]
    [SerializeField] private string textDeleteConfirm =
        "Delete your account?\n\nAll progress, currencies and purchased content will be permanently "
      + "deleted. This cannot be undone and purchases cannot be refunded.";
    [SerializeField] private string textDeleting   = "Deleting...";
    [SerializeField] private string textDeleteErr  = "Could not delete. Check your connection.";

    private bool _laeuft;

    private void OnEnable()
    {
        if (linkButton    != null) linkButton.onClick.AddListener(OnLink);
        if (restoreButton != null) restoreButton.onClick.AddListener(OnRestore);
        if (deleteButton        != null) deleteButton.onClick.AddListener(OnDeleteClicked);
        if (deleteConfirmButton != null) deleteConfirmButton.onClick.AddListener(OnDeleteConfirmed);
        if (deleteCancelButton  != null) deleteCancelButton.onClick.AddListener(SchliesseAbfrage);
        if (restoreResultLabel != null) restoreResultLabel.text = "";

        // Die Abfrage startet immer zu -- sonst stuende sie nach einem Abbruch beim naechsten
        // Oeffnen der Einstellungen wieder offen.
        SchliesseAbfrage();
        Refresh();
    }

    private void OnDisable()
    {
        if (linkButton    != null) linkButton.onClick.RemoveListener(OnLink);
        if (restoreButton != null) restoreButton.onClick.RemoveListener(OnRestore);
        if (deleteButton        != null) deleteButton.onClick.RemoveListener(OnDeleteClicked);
        if (deleteConfirmButton != null) deleteConfirmButton.onClick.RemoveListener(OnDeleteConfirmed);
        if (deleteCancelButton  != null) deleteCancelButton.onClick.RemoveListener(SchliesseAbfrage);
    }

    // ── Konto löschen ─────────────────────────────────────────────────────────

    private void OnDeleteClicked()
    {
        if (deleteConfirmText != null) deleteConfirmText.text = textDeleteConfirm;
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(true);
    }

    private void SchliesseAbfrage()
    {
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
    }

    private void OnDeleteConfirmed()
    {
        if (_laeuft) return;
        _ = DeleteAsync();
    }

    private async Task DeleteAsync()
    {
        _laeuft = true;
        if (deleteConfirmButton != null) deleteConfirmButton.interactable = false;
        if (deleteCancelButton  != null) deleteCancelButton.interactable  = false;
        if (deleteConfirmText   != null) deleteConfirmText.text = textDeleting;

        try
        {
            // ZUERST der Push-Token in Firestore -- danach gibt es keine PlayerID mehr, ueber
            // die der Eintrag aufzufinden waere. Ein Fehlschlag hier darf die Kontoloeschung
            // aber nicht verhindern, sonst bliebe der Spieler auf seinem Konto sitzen.
            if (FirebaseNotificationManager.Instance != null)
            {
                try { await FirebaseNotificationManager.Instance.DeleteTokenAsync(); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Account] Push-Token konnte nicht geloescht werden: " + e.Message);
                }
            }

            // Ebenso der Aktivitaetseintrag in Firestore -- auch der ist nur ueber die PlayerID
            // auffindbar und waere danach ein Waisenkind.
            var aktivitaet = new System.Threading.Tasks.TaskCompletionSource<bool>();
            StartCoroutine(ActivityStamp.LoeschenCoroutine(ok => aktivitaet.TrySetResult(ok)));
            if (!await aktivitaet.Task)
                Debug.LogWarning("[Account] Aktivitaetseintrag konnte nicht geloescht werden.");

            // Dann die Cloud-Save-Daten -- und zwar VOR dem Konto. Unitys Kontoloeschung
            // entfernt NUR das Authentifizierungskonto; die Spielstaende bleiben sonst liegen
            // und waeren danach nicht einmal mehr zuzuordnen, weil die PlayerID weg ist.
            // Unity sagt das in der eigenen Doku ausdruecklich: "It is your obligation to
            // ensure that you delete all associated player data."
            try
            {
                await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.DeleteAllAsync(
                    new Unity.Services.CloudSave.Models.Data.Player.DeleteAllOptions());
                Debug.Log("[Account] Cloud-Save-Daten geloescht.");
            }
            catch (System.Exception e)
            {
                // Schlaegt es fehl, wird trotzdem weitergeloescht -- ein halb geloeschtes Konto
                // ist schlechter als eines ohne Spielstand. Der Fehler muss aber sichtbar sein.
                Debug.LogError("[Account] Cloud-Save-Daten konnten NICHT geloescht werden: " + e.Message);
            }

            // Zuletzt das Konto selbst auf Unitys Seite.
            await AuthenticationService.Instance.DeleteAccountAsync();
            Debug.Log("[Account] Konto geloescht.");

            // Zuletzt Firebase von der bisherigen Identitaet trennen.
            NeonAnalytics.ResetAfterAccountDeletion();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Account] Loeschen fehlgeschlagen: " + e.Message);
            if (deleteConfirmText   != null) deleteConfirmText.text = textDeleteErr;
            if (deleteConfirmButton != null) deleteConfirmButton.interactable = true;
            if (deleteCancelButton  != null) deleteCancelButton.interactable  = true;
            _laeuft = false;
            return;
        }

        // Lokale Reste mit entfernen -- sonst traegt das Geraet den alten Stand weiter und der
        // naechste anonyme Start wuerde ihn wieder in die Cloud schreiben.
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // Den Startvorgang zuruecksetzen, BEVOR die Szene neu geladen wird. UgsBootstrap
        // ueberlebt den Wechsel und wuerde sonst seinen alten Zustand mitnehmen -- Begin()
        // taete nichts und die App haenge mit einer abgemeldeten Sitzung fest.
        UgsBootstrap.ResetForRestart();

        // Pausierung sicherheitshalber aufheben: ein stehengebliebenes timeScale = 0 wuerde
        // den Startvorgang der neuen Szene blockieren.
        Time.timeScale = 1f;

        Debug.Log("[Account] Lade Startszene neu.");
        SceneManager.LoadScene(0, LoadSceneMode.Single);
    }

    public void Refresh()
    {
        bool verknuepft = UgsBootstrap.IsPlatformLinked;

        if (statusLabel != null) statusLabel.text = verknuepft ? textLinked : textNotLinked;
        if (linkButton  != null) linkButton.gameObject.SetActive(!verknuepft);

        // Die Kennung nur zeigen, wenn es eine gibt -- vor der Anmeldung waere das Feld leer
        // und wuerde nur Fragen aufwerfen.
        // Loeschen nur anbieten, wenn ueberhaupt ein Konto besteht.
        bool angemeldet = !string.IsNullOrEmpty(UgsBootstrap.PlayerIdOrEmpty);
        if (deleteButton != null) deleteButton.gameObject.SetActive(angemeldet);

        string id = UgsBootstrap.PlayerIdOrEmpty;
        if (playerIdLabel != null)
        {
            playerIdLabel.gameObject.SetActive(!string.IsNullOrEmpty(id));
            playerIdLabel.text = "ID: " + id;
        }
    }

    private void OnLink()
    {
        if (UgsBootstrap.Instance == null) return;
        UgsBootstrap.Instance.RetryPlatformLink();
        // Die Verknuepfung laeuft als Coroutine mit Plattform-Dialogen -- den Status deshalb
        // verzoegert nachziehen statt sofort.
        Invoke(nameof(Refresh), 3f);
    }

    private void OnRestore()
    {
        if (_laeuft) return;
        _ = RestoreAsync();
    }

    private async Task RestoreAsync()
    {
        _laeuft = true;
        if (restoreButton != null) restoreButton.interactable = false;
        if (restoreResultLabel != null) restoreResultLabel.text = textRestoring;

        bool ok = true;
        try
        {
            // 1) Store-seitig: holt frühere NICHT-verbrauchbare Käufe zurück. Aktuell verkaufen
            //    wir nur Verbrauchsgüter, der Aufruf ist also eine Vorsorge für später -- und auf
            //    iOS ohnehin Pflicht, sobald ein dauerhaftes Produkt dazukommt.
            IAPManager.Instance?.RestorePurchases();

            // 2) Der eigentliche Weg bei uns: den Kontostand aus der Cloud neu ziehen. Daran
            //    haengen Guthaben, gekaufte Inhalte und freigeschaltete Welten.
            await DreamEnergyManager.LoadFromCloudAsync();
            await DiamondManager.LoadFromCloudAsync();
            await DiamondSplinterManager.LoadFromCloudAsync();
            await ShopInventory.LoadFromCloudAsync();
            await WorldUnlockManager.LoadFromCloudAsync();
        }
        catch (System.Exception e)
        {
            ok = false;
            Debug.LogWarning("[Account] Wiederherstellen fehlgeschlagen: " + e.Message);
        }

        if (restoreResultLabel != null) restoreResultLabel.text = ok ? textRestored : textRestoreErr;
        if (restoreButton != null) restoreButton.interactable = true;
        _laeuft = false;
        Refresh();
    }
}
