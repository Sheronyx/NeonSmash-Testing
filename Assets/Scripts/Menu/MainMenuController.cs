using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject tutorialUI;
    [SerializeField] private GameObject matchmakingScreen;
    [SerializeField] private GameObject friendsScreen;

    [Header("Time Mode – Lock")]
    [SerializeField] private Button timeModeButton;
    [SerializeField] private Image timeModeButtonImage;
    [SerializeField] private Material timeModeUnlockedMaterial;
    [SerializeField] private GameObject timeLockIcon;
    [Tooltip("Icon, LockIcon – Image-Komponenten, werden bei Unlock auf Weiß animiert")]
    [SerializeField] private Image[] timeModeLockedImages;
    [Tooltip("Text Classic – TMP-Komponenten, werden bei Unlock auf Weiß animiert")]
    [SerializeField] private TextMeshProUGUI[] timeModeLockedTexts;
    [SerializeField] private float colorFadeDuration = 0.4f;

    [Header("Time Mode Unlock Notification")]
    [SerializeField] private CanvasGroup unlockNotificationPanel;
    [SerializeField] private RectTransform lockIconInNotification;
    [Tooltip("Sekunden, die das Panel sichtbar bleibt")]
    [SerializeField] private float notificationHoldDuration = 2.5f;
    [SerializeField] private float popInDuration  = 0.3f;
    [SerializeField] private float popOutDuration = 0.25f;

    private void Start()
    {
        TimeModeProgress.OnProgressLoaded += RefreshTimeModeUI;
        RefreshTimeModeUI();

        // Unlock-Notification anzeigen (einmalig nach erstem Infinity-Spiel)
        int showNotif = PlayerPrefs.GetInt("ShowTimeModeUnlockNotification", 0);
        bool unlocked = TimeModeProgress.IsUnlocked;
        Debug.Log($"[MainMenu] ShowNotif={showNotif}  Unlocked={unlocked}  Panel={unlockNotificationPanel}");
        if (showNotif == 1)
        {
            PlayerPrefs.SetInt("ShowTimeModeUnlockNotification", 0);
            PlayerPrefs.Save();
            if (unlockNotificationPanel != null)
            {
                Debug.Log("[MainMenu] Starting Co_ShowUnlockNotification");
                StartCoroutine(Co_ShowUnlockNotification());
            }
            else
            {
                Debug.LogWarning("[MainMenu] unlockNotificationPanel is NULL – Coroutine nicht gestartet!");
            }
        }
    }

    private void OnDestroy()
    {
        TimeModeProgress.OnProgressLoaded -= RefreshTimeModeUI;
    }

    private void RefreshTimeModeUI()
    {
        bool unlocked = TimeModeProgress.IsUnlocked;
        if (timeLockIcon   != null) timeLockIcon.SetActive(!unlocked);
        if (timeModeButton != null) timeModeButton.interactable = unlocked;

        if (unlocked)
        {
            SetTimeModeColors(Color.white);
            if (timeModeButtonImage != null && timeModeUnlockedMaterial != null)
                timeModeButtonImage.material = timeModeUnlockedMaterial;
        }
    }

    // ── Spielmodus-Buttons ──────────────────────────────────────────────────

    public void OnInfinity()
    {
        if (GlobalGameManager.Instance != null)
            GlobalGameManager.Instance.SetMode(GameMode.Infinity);

        SceneFader.Instance.LoadScene("GameScene_InfinityMode");
    }

    public void OnTime()
    {
        if (GlobalGameManager.Instance != null)
            GlobalGameManager.Instance.SetMode(GameMode.Time);

        SceneFader.Instance.LoadScene("GameScene_TimeMode");
    }

    public void OnMultiplayer()
    {
        if (matchmakingScreen != null)
            matchmakingScreen.SetActive(true);
    }

    public void OnFriends()
    {
        if (friendsScreen != null)
            friendsScreen.SetActive(true);
    }

    // ── Sonstige Buttons ────────────────────────────────────────────────────

    public void OpenTutorial()
    {
        if (tutorialUI != null)
            tutorialUI.SetActive(true);
    }

    public void OpenLeaderboard()
    {
        Debug.Log("Leaderboard wird später implementiert.");
    }

    public void OpenSettings()
    {
        Debug.Log("Einstellungen werden später implementiert.");
    }

    public void OpenTasks()
    {
        TasksPopupController.Instance?.Open();
    }

    // ── Unlock-Notification ─────────────────────────────────────────────────

    private IEnumerator Co_ShowUnlockNotification()
    {
        Debug.Log("[MainMenu] Coroutine gestartet");
        // Warten bis SceneFader-Einblendung wirklich abgeschlossen ist
        if (SceneFader.Instance != null)
        {
            Debug.Log($"[MainMenu] Warte auf Fade… IsFading={SceneFader.Instance.IsFading}");
            yield return new WaitUntil(() => SceneFader.Instance == null || !SceneFader.Instance.IsFading);
            Debug.Log("[MainMenu] Fade fertig – starte Notification");
        }
        else
        {
            Debug.Log("[MainMenu] SceneFader null – warte 1s");
            yield return new WaitForSecondsRealtime(1f);
        }

        // Time-Mode-Button sofort freischalten (Lock-Icon weg, Button aktiv, Material setzen)
        if (timeLockIcon   != null) timeLockIcon.SetActive(false);
        if (timeModeButton != null) timeModeButton.interactable = true;
        if (timeModeButtonImage != null && timeModeUnlockedMaterial != null)
            timeModeButtonImage.material = timeModeUnlockedMaterial;

        DimOverlay.Instance?.Show();
        unlockNotificationPanel.gameObject.SetActive(true);
        unlockNotificationPanel.alpha = 0f;
        var rt = unlockNotificationPanel.GetComponent<RectTransform>();
        rt.localScale = Vector3.one * 0.6f;

        // Pop-in (gleichzeitig Dim einblenden)
        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popInDuration));
            unlockNotificationPanel.alpha = p;
            rt.localScale = Vector3.Lerp(Vector3.one * 0.6f, Vector3.one, p);
            yield return null;
        }
        unlockNotificationPanel.alpha = 1f;
        rt.localScale = Vector3.one;

        // Lock-Icon im Popup: Rotation → Skalierung → Wegfaden
        if (lockIconInNotification != null)
        {
            float lockT = 0f;
            float lockDur = 0.5f;
            Quaternion startRot = lockIconInNotification.localRotation;
            Quaternion endRot   = Quaternion.Euler(0f, 0f, -25f);
            Vector3 startScale  = lockIconInNotification.localScale;

            while (lockT < lockDur)
            {
                lockT += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lockT / lockDur));
                lockIconInNotification.localRotation = Quaternion.Lerp(startRot, endRot, p);
                lockIconInNotification.localScale    = Vector3.Lerp(startScale, startScale * 1.3f, p);
                yield return null;
            }

            // Wegfaden
            lockT = 0f;
            float fadeDur = 0.3f;
            CanvasGroup lockCg = lockIconInNotification.GetComponent<CanvasGroup>();
            while (lockT < fadeDur)
            {
                lockT += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(lockT / fadeDur);
                if (lockCg != null) lockCg.alpha = 1f - p;
                lockIconInNotification.localScale = Vector3.Lerp(startScale * 1.3f, Vector3.zero, p);
                yield return null;
            }
            lockIconInNotification.gameObject.SetActive(false);
        }

        // Icon + Text Classic: Farbe von Gray → White animieren
        {
            Color[] imgStart = new Color[timeModeLockedImages  != null ? timeModeLockedImages.Length  : 0];
            Color[] tmpStart = new Color[timeModeLockedTexts   != null ? timeModeLockedTexts.Length   : 0];
            if (timeModeLockedImages != null)
                for (int i = 0; i < timeModeLockedImages.Length; i++)
                    imgStart[i] = timeModeLockedImages[i] != null ? timeModeLockedImages[i].color : Color.white;
            if (timeModeLockedTexts != null)
                for (int i = 0; i < timeModeLockedTexts.Length; i++)
                    tmpStart[i] = timeModeLockedTexts[i]  != null ? timeModeLockedTexts[i].color  : Color.white;

            float ct = 0f;
            while (ct < colorFadeDuration)
            {
                ct += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ct / colorFadeDuration));
                if (timeModeLockedImages != null)
                    for (int i = 0; i < timeModeLockedImages.Length; i++)
                        if (timeModeLockedImages[i] != null)
                            timeModeLockedImages[i].color = Color.Lerp(imgStart[i], Color.white, p);
                if (timeModeLockedTexts != null)
                    for (int i = 0; i < timeModeLockedTexts.Length; i++)
                        if (timeModeLockedTexts[i] != null)
                            timeModeLockedTexts[i].color = Color.Lerp(tmpStart[i], Color.white, p);
                yield return null;
            }
            SetTimeModeColors(Color.white);
        }

        // Halten
        yield return new WaitForSecondsRealtime(notificationHoldDuration);

        // Pop-out (gleichzeitig Dim ausblenden)
        DimOverlay.Instance?.Hide();

        t = 0f;
        while (t < popOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popOutDuration));
            unlockNotificationPanel.alpha = 1f - p;
            rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.7f, p);
            yield return null;
        }
        unlockNotificationPanel.gameObject.SetActive(false);
    }

    private void SetTimeModeColors(Color c)
    {
        if (timeModeLockedImages != null)
            foreach (var img in timeModeLockedImages)
                if (img != null) img.color = c;
        if (timeModeLockedTexts != null)
            foreach (var tmp in timeModeLockedTexts)
                if (tmp != null) tmp.color = c;
    }
}
