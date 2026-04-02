using UnityEngine;
using UnityEngine.VFX;

public class TapPoint : BasePoint
{
    public MixedPointSpawner spawner;

    public void TryTap()
    {
        AudioManager.Instance?.PlayNormalPoint();
        spawner?.HandlePointHit(gameObject);
    }
}