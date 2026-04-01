using UnityEngine;
using UnityEngine.VFX;

public class ClickablePoint : BasePoint
{
    public MixedPointSpawner spawner;

 public void TryClick()
{
    ComboPoint combo = GetComponent<ComboPoint>();

    if (combo != null)
    {
        combo.OnTapped();
        return;
    }

    int points = spawner != null ? spawner.GetPointsForCurrentMode() : 1;

    AudioManager.Instance?.PlayNormalPoint();

    spawner?.HandlePointHit(gameObject);
    }
}