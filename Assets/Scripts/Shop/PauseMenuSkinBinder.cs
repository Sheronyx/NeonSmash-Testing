using UnityEngine;

// Wählt je nach aktivem Skin das passende Pause-Panel und meldet es dem
// PauseMenuController. Die Panels bleiben versteckt (werden erst bei Pause
// gezeigt) — daher KEIN Enable/Disable des aktiven Panels, nur Auswahl.
//
// Die Buttons jeder Panel-Variante werden im Inspector auf den
// PauseMenuController verdrahtet (Resume / Settings / ReturnToMainMenu).
//
// Auf ein immer aktives Objekt legen (z.B. den PauseMenuController selbst).
public class PauseMenuSkinBinder : MonoBehaviour
{
    [System.Serializable]
    public class Variant
    {
        [Tooltip("Null = Default-Variante (wenn kein Skin passt).")]
        public SkinTheme  theme;
        public GameObject panel;
    }

    [SerializeField] private PauseMenuController pauseController;
    [SerializeField] private GameObject defaultPanel;
    [SerializeField] private Variant[] skinVariants;

    void Start()
    {
        if (pauseController == null) pauseController = FindFirstObjectByType<PauseMenuController>();
        if (pauseController == null) return;

        var active = SkinManager.Instance != null ? SkinManager.Instance.ActiveTheme : null;

        GameObject chosen = defaultPanel;
        foreach (var v in skinVariants)
        {
            if (active != null && v.theme == active && v.panel != null)
                chosen = v.panel;
        }

        // Alle Panels verstecken, dann das gewählte als aktives Pause-Panel setzen
        if (defaultPanel != null) defaultPanel.SetActive(false);
        foreach (var v in skinVariants)
            if (v.panel != null) v.panel.SetActive(false);

        pauseController.SetActivePanel(chosen);
    }
}
