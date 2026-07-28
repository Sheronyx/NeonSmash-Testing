using UnityEngine;

// Ein wählbarer Boost für die Boost-Auswahl zu Session-Beginn (siehe BoostSelectionUI). Vier Assets
// (einer pro BoostType) werden vom User im Editor angelegt und der BoostSelectionUI zugewiesen.
[CreateAssetMenu(fileName = "BoostDefinition", menuName = "NeonSmash/Boost Definition")]
public class BoostDefinition : ScriptableObject
{
    public BoostType type;
    public string    displayName;
    [TextArea]
    public string    description;
    public Sprite    icon;
}
