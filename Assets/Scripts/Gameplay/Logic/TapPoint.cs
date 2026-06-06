using UnityEngine;
using UnityEngine.VFX;

public class TapPoint : BasePoint
{
    public MixedPointSpawner spawner;

    public void TryTap()
    {
        if (TutorialManager.IsOrbPhaseActive) return;

        TutorialManager.Instance?.OnActionPerformed(TutorialPointType.NormalPoint);

        AudioManager.Instance?.PlayNormalPoint();
        spawner?.HandlePointHit(gameObject);
    }
}
