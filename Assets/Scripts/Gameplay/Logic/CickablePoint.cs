using UnityEngine;
using UnityEngine.VFX;

public class ClickablePoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("VFX")]
    [SerializeField] private VisualEffect explodeVFXPrefab;

 public void TryClick()
{
    ComboPoint combo = GetComponent<ComboPoint>();

    if (combo != null)
    {
        combo.OnTapped();
        return;
    }

    int points = 1;

    if (spawner != null && spawner.IsGoldModeActive())
    {
        points = 2;
    }

    ScoreManager.Instance?.AddPoints(points);

    SpawnExplosion();

    AudioManager.Instance?.PlayNormalPoint();

    if (spawner != null)
        spawner.PointCleared(gameObject);
    else
        Destroy(gameObject);
}

    private void SpawnExplosion()
    {
        if (explodeVFXPrefab == null)
            return;

        Instantiate(
            explodeVFXPrefab,
            transform.position,
            Quaternion.identity
        );
    }
}