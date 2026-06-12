using UnityEngine;
using UnityEngine.UI;

// Färbt die Neon-Linien der UI Top Bar laut aktivem Skin um (Farb-Override).
// Für kompletten Top-Bar-Tausch (eigenes Objekt) stattdessen SkinObjectSwap nutzen.
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
