using UnityEngine;

[CreateAssetMenu(fileName = "SequenceDefinition", menuName = "NeonSmash/Sequence Definition")]
public class SequenceDefinition : ScriptableObject
{
    public string sequenceName = "Sequence";
    public PointColor[] steps;
    public int bonusPoints = 150;
    public Sprite abilityIcon;
}
