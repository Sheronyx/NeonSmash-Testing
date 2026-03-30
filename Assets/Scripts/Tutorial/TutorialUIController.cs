using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class TutorialUIController : MonoBehaviour
{
    [Header("Root & UI")]
    [SerializeField] private GameObject tutorialPanel; // wird beim Öffnen aktiviert/deaktiviert
    [SerializeField] private Image slideImage;         // UI-Image, das deine Sprites anzeigt
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeButton;       // optional, kann leer bleiben

    [Header("Sprites in Reihenfolge")]
    public List<Sprite> sprites;

    [Header("Optionen")]
    public bool setNativeSize = true;

    // optional: GameScene-Start nach Schließen
    public GameSceneController gameSceneController;
    public bool startGameplayOnClose = false;

    private int current = 0;
    private bool wired = false;

    void Awake()
    {
        // Falls nicht gesetzt, innerhalb des Panels automatisch suchen (auch wenn inaktiv)
        if (tutorialPanel == null)
            Debug.LogError("TutorialUIController: 'tutorialPanel' nicht zugewiesen.");

        if (!slideImage && tutorialPanel)
            slideImage = FindInChildren<Image>(tutorialPanel.transform, "TutorialImage") ?? tutorialPanel.GetComponentInChildren<Image>(true);

        if (!nextButton && tutorialPanel)
            nextButton = FindInChildren<Button>(tutorialPanel.transform, "NextButton") ?? tutorialPanel.GetComponentInChildren<Button>(true);

        if (!backButton && tutorialPanel)
            backButton = FindInChildren<Button>(tutorialPanel.transform, "BackButton");

        if (!closeButton && tutorialPanel)
            closeButton = FindInChildren<Button>(tutorialPanel.transform, "CloseButton");

        WireButtons();
        if (tutorialPanel) tutorialPanel.SetActive(false); // Panel ist zu Beginn aus
    }

    // Wird vom Menü-Button aufgerufen
    public void OpenTutorialPanel()
    {
        if (sprites == null || sprites.Count == 0 || slideImage == null || tutorialPanel == null)
        {
            Debug.LogWarning("TutorialUIController: Bitte 'tutorialPanel', 'slideImage' und 'sprites' zuweisen.");
            return;
        }

        current = 0;
        ShowCurrent();
        tutorialPanel.SetActive(true);
    }

    // Optional: eigenes Close (z.B. für X-Button)
    public void CloseTutorialPanel()
    {
        if (startGameplayOnClose && SceneManager.GetActiveScene().name == "GameScene" && gameSceneController != null)
            gameSceneController.OnTutorialClosed();
        else if (tutorialPanel)
            tutorialPanel.SetActive(false);
    }

    private void Next()
    {
        if (current < sprites.Count - 1)
        {
            current++;
            ShowCurrent();
        }
        else
        {
            CloseTutorialPanel();
        }
    }

    private void Back()
    {
        if (current > 0)
        {
            current--;
            ShowCurrent();
        }
    }

    private void ShowCurrent()
    {
        current = Mathf.Clamp(current, 0, sprites.Count - 1);

        if (!sprites[current])
        {
            Debug.LogWarning($"ShowCurrent: Sprite {current} ist NULL.");
            return;
        }

        slideImage.enabled = true;
        slideImage.preserveAspect = true;
        slideImage.sprite = sprites[current];
        if (setNativeSize) slideImage.SetNativeSize();

        // Buttons ein-/ausblenden
        if (backButton) backButton.gameObject.SetActive(current > 0);
        if (nextButton) nextButton.gameObject.SetActive(current < sprites.Count - 1);

        Debug.Log($"Tutorial: Zeige Slide {current} -> {slideImage.sprite.name}");
    }


    private void WireButtons()
    {
        if (wired) return;
        if (nextButton)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(Next);
        }
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Back);
        }
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseTutorialPanel);
        }
        wired = true;
    }

    // Helper: sucht erst exakten Namen, sonst erstes Vorkommen
    private T FindInChildren<T>(Transform root, string exactName) where T : Component
    {
        foreach (var c in root.GetComponentsInChildren<T>(true))
            if (c.name == exactName) return c;
        return null;
    }
}
