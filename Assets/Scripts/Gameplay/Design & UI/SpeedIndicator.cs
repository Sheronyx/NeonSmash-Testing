using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zeigt die aktuelle Spielgeschwindigkeit über SpeedIcons an.
/// Aktive Icons (Index < Level) haben Alpha 1, inaktive Alpha 0.1.
/// Icons im Inspector in aufsteigender Reihenfolge zuweisen (Index 0 = Level 1).
/// </summary>
public class SpeedIndicator : MonoBehaviour
{
    [SerializeField] private Image[] speedIcons;
    [SerializeField] private LevelUp levelUp;

    [SerializeField] private float activeAlpha   = 1f;
    [SerializeField] private float inactiveAlpha = 0.1f;

    private void Start()
    {
        if (levelUp != null)
            levelUp.OnLevelChanged += UpdateIcons;

        UpdateIcons(levelUp != null ? levelUp.CurrentLevel : 1);
    }

    private void OnDestroy()
    {
        if (levelUp != null)
            levelUp.OnLevelChanged -= UpdateIcons;
    }

    private void UpdateIcons(int level)
    {
        for (int i = 0; i < speedIcons.Length; i++)
        {
            if (speedIcons[i] == null) continue;

            if (i < level)
            {
                // Aktiv: #00FFFD mit Alpha 1
                speedIcons[i].color = new Color(0f, 1f, 253f / 255f, activeAlpha);
            }
            else
            {
                // Inaktiv: Weiß mit reduziertem Alpha
                speedIcons[i].color = new Color(1f, 1f, 1f, inactiveAlpha);
            }
        }
    }
}
