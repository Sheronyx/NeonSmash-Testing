using UnityEngine;

// Ordnet die drei Welt-Titel-Varianten den tatsächlichen Skin-Themes zu (Welt = aktuell
// ausgerüsteter Skin, siehe SkinManager.ActiveTheme). Ein einziges Asset in Resources, von
// TitleAchievementManager geladen.
[CreateAssetMenu(fileName = "WorldSkinMapping", menuName = "NeonSmash/World Skin Mapping")]
public class WorldSkinMapping : ScriptableObject
{
    public SkinTheme traumhoehleTheme;
    public SkinTheme dschungelTheme;
    public SkinTheme himmelTheme;

    public TitleWorld ResolveWorld(SkinTheme activeTheme)
    {
        if (activeTheme == null) return TitleWorld.Independent;
        if (activeTheme == traumhoehleTheme) return TitleWorld.Traumhoehle;
        if (activeTheme == dschungelTheme)   return TitleWorld.Dschungel;
        if (activeTheme == himmelTheme)      return TitleWorld.Himmel;
        return TitleWorld.Independent;
    }
}
