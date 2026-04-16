using UnityEngine;
using UnityEngine.VFX;

public class TapPoint : BasePoint
{
    public MixedPointSpawner spawner;

    public void TryTap()
    {
        if (TutorialManager.IsOrbPhaseActive) return;

        if (TutorialManager.Instance != null)
        {
            bool isGold = GoldModeSystem.Instance != null && GoldModeSystem.Instance.IsActive;
            TutorialManager.Instance.OnActionPerformed(isGold ? TutorialPointType.GoldPoint : TutorialPointType.NormalPoint);
        }

        AudioManager.Instance?.PlayNormalPoint();
        spawner?.HandlePointHit(gameObject);
    }
}