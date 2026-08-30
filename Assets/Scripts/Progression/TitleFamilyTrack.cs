using UnityEngine;

// Eine Titel-"Familie" aus der Titel-Excel (z.B. "Traumenergie", später "Highscore",
// "Elementreise", ...): ein Stat-Typ + eine Schwellen-Leiter, an jeder Schwelle bis zu 4 Titel
// (Unabhängig + je einer pro Welt). Unabhängig wird immer vergeben, sobald der Stat die Schwelle
// erreicht; die Welt-Variante zusätzlich, wenn beim Erreichen das passende Skin-Theme aktiv war
// (siehe WorldSkinMapping/TitleAchievementManager). Neue Familie = neues Asset, kein Code nötig.
[CreateAssetMenu(fileName = "TitleFamilyTrack", menuName = "NeonSmash/Title Family Track")]
public class TitleFamilyTrack : ScriptableObject
{
    public enum StatType
    {
        DreamEnergyLifetime,
    }

    [System.Serializable]
    public class Threshold
    {
        public int value;
        public RewardDefinition independentTitle;
        public RewardDefinition traumhoehleTitle;
        public RewardDefinition dschungelTitle;
        public RewardDefinition himmelTitle;
    }

    public StatType statType;
    [Tooltip("Muss nicht sortiert gepflegt werden — SortedThresholds() sortiert selbst.")]
    [SerializeField] private Threshold[] thresholds;

    public Threshold[] SortedThresholds()
    {
        var sorted = (Threshold[])(thresholds ?? new Threshold[0]).Clone();
        System.Array.Sort(sorted, (a, b) => a.value.CompareTo(b.value));
        return sorted;
    }

    public RewardDefinition TitleFor(Threshold t, TitleWorld world) => world switch
    {
        TitleWorld.Traumhoehle => t.traumhoehleTitle,
        TitleWorld.Dschungel   => t.dschungelTitle,
        TitleWorld.Himmel      => t.himmelTitle,
        _                       => null,
    };
}
