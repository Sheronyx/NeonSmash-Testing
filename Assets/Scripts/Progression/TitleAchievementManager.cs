using UnityEngine;

// Vergibt Titel aus allen TitleFamilyTrack-Assets (Resources/TitleFamilies/*) anhand des
// jeweiligen Stat-Stands. Wird nach jeder Session aufgerufen (siehe MixedPointSpawner.GameOver).
// Idempotent über TitleManager.Unlock (no-op bei bereits besessenem Titel) — prüft daher bei
// jedem Aufruf einfach alle Schwellen erneut, statt einen Fortschritts-Index zu pflegen. Das
// erlaubt auch, eine Welt-Variante nachträglich zu bekommen, wenn der Spieler eine Schwelle
// schon früher (mit anderem Skin) erreicht hatte und die passende Welt erst jetzt spielt.
public static class TitleAchievementManager
{
    const string FamiliesResourcesFolder = "TitleFamilies";
    const string MappingResourcePath     = "WorldSkinMapping";

    static TitleFamilyTrack[] _families;
    static TitleFamilyTrack[] Families => _families ??= Resources.LoadAll<TitleFamilyTrack>(FamiliesResourcesFolder);

    static WorldSkinMapping _mapping;
    static WorldSkinMapping Mapping => _mapping != null ? _mapping : (_mapping = Resources.Load<WorldSkinMapping>(MappingResourcePath));

    public static void CheckAndGrantTitles()
    {
        var families = Families;
        if (families == null || families.Length == 0) return;

        TitleWorld currentWorld = ResolveCurrentWorld();

        foreach (var family in families)
        {
            if (family == null) continue;
            int statValue = GetStatValue(family.statType);

            foreach (var threshold in family.SortedThresholds())
            {
                if (statValue < threshold.value) continue;

                if (threshold.independentTitle != null)
                    TitleManager.Unlock(threshold.independentTitle.rewardId);

                var worldTitle = family.TitleFor(threshold, currentWorld);
                if (worldTitle != null)
                    TitleManager.Unlock(worldTitle.rewardId);
            }
        }
    }

    static TitleWorld ResolveCurrentWorld()
    {
        var mapping = Mapping;
        if (mapping == null) return TitleWorld.Independent;
        var active = SkinManager.Instance != null ? SkinManager.Instance.ActiveTheme : null;
        return mapping.ResolveWorld(active);
    }

    static int GetStatValue(TitleFamilyTrack.StatType type) => type switch
    {
        TitleFamilyTrack.StatType.DreamEnergyLifetime => DreamEnergyManager.LifetimeEarned,
        _ => 0,
    };
}
