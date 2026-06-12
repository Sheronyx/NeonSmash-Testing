using UnityEngine;
using UnityEngine.UI;

// Färbt die Neon-Linien der UI Top Bar laut aktivem Skin um.
// Auf das Top-Bar-Objekt legen und die Neon-Line Graphics zuweisen.
public class TopBarSkinApplier : MonoBehaviour
{
    [Tooltip("Alle Neon-Linien-Grafiken der Top Bar (Image / RawImage).")]
    [SerializeField] private Graphic[] neonLines;

    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        var theme = SkinManager.Instance?.ActiveTheme;
        if (theme == null || !theme.overrideTopBarColor) return;

        foreach (var g in neonLines)
        {
            if (g != null) g.color = theme.topBarColor;
        }
    }
}
