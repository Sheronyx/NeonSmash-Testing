using UnityEngine;
using UnityEngine.VFX;

public class SwipePoint : MonoBehaviour
{
    [Header("Refs")]
    public MixedPointSpawner spawner;

    [Header("Strike Settings")]
    [SerializeField] private float directionAngleTolerance = 25f;
    [SerializeField] private float strikePadding = 0.1f;
    [SerializeField] private float minSwipeLengthWorld = 0.1f;
    [SerializeField] private float minSwipeLengthPixels = 30f;
    [SerializeField] private float minT = 0.02f;
    [SerializeField] private float maxT = 0.85f;

    [Header("Direction Settings")]
    [SerializeField] private bool allowBidirectional = true;
    [SerializeField] private bool onlyBidirectionalOnDiagonals = false;

    [Header("VFX")]
    [SerializeField] private VisualEffect explodeVFXPrefab;

    private SwipeDirection direction;
    private float effectiveRadius;

    private void Start()
    {
        direction = GetRandomDirection();
        RotateIcon(direction);
        CacheEffectiveRadius();
    }

    // =========================================================
    // STRIKE LOGIC
    // =========================================================

    public bool TryStrikeScreen(Vector2 screenStart, Vector2 screenEnd, Camera cam)
    {
        if ((screenEnd - screenStart).magnitude < minSwipeLengthPixels)
            return false;

        if (cam == null)
            cam = Camera.main;

        float z = cam.orthographic
            ? Mathf.Abs(cam.transform.position.z)
            : 10f;

        Vector2 worldStart = cam.ScreenToWorldPoint(new Vector3(screenStart.x, screenStart.y, z));
        Vector2 worldEnd   = cam.ScreenToWorldPoint(new Vector3(screenEnd.x,   screenEnd.y,   z));

        return TryStrikeWorld(worldStart, worldEnd);
    }

    public bool TryStrikeWorld(Vector2 worldStart, Vector2 worldEnd)
    {
        Vector2 seg = worldEnd - worldStart;

        if (seg.magnitude < minSwipeLengthWorld)
            return false;

        if (!IsCorrectSwipeDirection(seg))
            return false;

        Vector2 center = transform.position;

        if (!DoesSegmentCrossCircle(worldStart, worldEnd, center, effectiveRadius, out float tClosest, out _))
            return false;

        bool startOutside = Vector2.Distance(worldStart, center) > effectiveRadius * 0.85f;
        bool endOutside   = Vector2.Distance(worldEnd,   center) > effectiveRadius * 0.85f;

        if (!(startOutside || endOutside))
            return false;

        if (tClosest < minT || tClosest > maxT)
            return false;

        // ✅ SUCCESS
        ScoreManager.Instance?.AddPoint();
        AudioManager.Instance?.PlaySwipePoint();

        SpawnExplosion();

        if (spawner != null)
            spawner.PointCleared(gameObject);
        else
            Destroy(gameObject);

        return true;
    }

    // =========================================================
    // SIMPLE VFX SPAWN
    // =========================================================

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

    // =========================================================
    // HELPER METHODS
    // =========================================================

    private bool IsCorrectSwipeDirection(Vector2 deltaWorld)
    {
        Vector2 dir = deltaWorld.normalized;

        Vector2 target = direction switch
        {
            SwipeDirection.Up        => Vector2.up,
            SwipeDirection.Down      => Vector2.down,
            SwipeDirection.Left      => Vector2.left,
            SwipeDirection.Right     => Vector2.right,
            SwipeDirection.UpRight   => (Vector2.up + Vector2.right).normalized,
            SwipeDirection.UpLeft    => (Vector2.up + Vector2.left).normalized,
            SwipeDirection.DownRight => (Vector2.down + Vector2.right).normalized,
            SwipeDirection.DownLeft  => (Vector2.down + Vector2.left).normalized,
            _ => Vector2.up
        };

        float angleToTarget = Vector2.Angle(target, dir);

        bool isDiagonal = direction is SwipeDirection.UpRight
                                       or SwipeDirection.UpLeft
                                       or SwipeDirection.DownRight
                                       or SwipeDirection.DownLeft;

        bool bidirectional = allowBidirectional &&
                             (!onlyBidirectionalOnDiagonals || isDiagonal);

        if (bidirectional)
        {
            float angleToOpposite = Vector2.Angle(-target, dir);
            return Mathf.Min(angleToTarget, angleToOpposite) <= directionAngleTolerance;
        }

        return angleToTarget <= directionAngleTolerance;
    }

    private bool DoesSegmentCrossCircle(
        Vector2 a,
        Vector2 b,
        Vector2 center,
        float radius,
        out float tClosest,
        out float distance)
    {
        Vector2 ab = b - a;
        float abLen2 = ab.sqrMagnitude;

        if (abLen2 <= Mathf.Epsilon)
        {
            tClosest = 0f;
            distance = Vector2.Distance(a, center);
            return distance <= radius;
        }

        tClosest = Vector2.Dot(center - a, ab) / abLen2;
        tClosest = Mathf.Clamp01(tClosest);

        Vector2 closest = a + tClosest * ab;
        distance = Vector2.Distance(closest, center);

        return distance <= radius;
    }

    private void CacheEffectiveRadius()
    {
        float baseRadius = 0.5f;

        var col = GetComponent<CircleCollider2D>();
        if (col != null)
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            baseRadius = col.radius * scale;
        }

        effectiveRadius = baseRadius + strikePadding;
    }

    private SwipeDirection GetRandomDirection()
    {
        SwipeDirection[] directions =
            (SwipeDirection[])System.Enum.GetValues(typeof(SwipeDirection));

        return directions[Random.Range(0, directions.Length)];
    }

    private void RotateIcon(SwipeDirection dir)
    {
        float angle = dir switch
        {
            SwipeDirection.Up        => 0f,
            SwipeDirection.Right     => -90f,
            SwipeDirection.Down      => 180f,
            SwipeDirection.Left      => 90f,
            SwipeDirection.UpRight   => -45f,
            SwipeDirection.UpLeft    => 45f,
            SwipeDirection.DownRight => -135f,
            SwipeDirection.DownLeft  => 135f,
            _ => 0f
        };

        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}