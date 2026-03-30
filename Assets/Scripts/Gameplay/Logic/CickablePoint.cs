using UnityEngine;
using UnityEngine.VFX;

public class ClickablePoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("VFX")]
    [SerializeField] private VisualEffect explodeVFXPrefab;

    public void TryClick()
    {
        // 👉 NEU: prüfen ob es ein ComboPoint ist
        ComboPoint combo = GetComponent<ComboPoint>();

        if (combo != null)
        {
            combo.OnTapped();
            return; // ❗ wichtig: stoppt normalen Ablauf
        }

        // Score
        ScoreManager.Instance?.AddPoint();

        // VFX Explosion
        SpawnExplosion();

        // SFX
        AudioManager.Instance?.PlayNormalPoint();

        // Spawner benachrichtigen
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