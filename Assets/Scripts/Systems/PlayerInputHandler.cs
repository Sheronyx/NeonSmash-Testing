using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance;

    private GameControls controls;
    private Vector2 touchStartPos;
    private Vector2 touchEndPos;
    private bool isTouching;
    private bool trailStarted;

    [Header("Settings")]
    [SerializeField] private float swipeThresholdPixels = 80f;
    [SerializeField] private float tapRadius = 0.5f;
    [SerializeField] private LayerMask tapLayerMask = ~0;

    [Header("Refs")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private SlashTrail slashTrail;

    private Camera cam;

    private void Awake()
    {
        Instance = this;

        cam = Camera.main;
        controls = new GameControls();

        // TOUCH START
        controls.Player.TouchPress.started += ctx =>
        {
            if (PauseMenuController.IsPaused) return;

            isTouching = true;
            trailStarted = false;

            touchStartPos = controls.Player.TouchPosition.ReadValue<Vector2>();
            touchEndPos = touchStartPos;
        };

        // TOUCH MOVE
        controls.Player.TouchPosition.performed += ctx =>
        {
            if (PauseMenuController.IsPaused) return;
            if (!isTouching) return;

            var pos = ctx.ReadValue<Vector2>();
            touchEndPos = pos;

            if (!trailStarted)
            {
                trailStarted = true;
                slashTrail?.Begin(ScreenToWorld2D(pos));
            }
            else
            {
                slashTrail?.Move(ScreenToWorld2D(pos));
            }
        };

        // TOUCH END
        controls.Player.TouchPress.canceled += ctx =>
        {
            if (PauseMenuController.IsPaused) return;

            if (trailStarted)
                slashTrail?.End();

            isTouching = false;
            trailStarted = false;

            touchEndPos = controls.Player.TouchPosition.ReadValue<Vector2>();

            HandleTouchCompleted(touchStartPos, touchEndPos);
        };
    }

    private void HandleTouchCompleted(Vector2 screenStart, Vector2 screenEnd)
    {
        Vector2 delta = screenEnd - screenStart;

        // =========================
        // 🔥 TAP
        // =========================
        if (delta.magnitude < swipeThresholdPixels)
        {
            Vector3 worldStart = ScreenToWorld2D(screenStart);
            Vector3 worldEnd = ScreenToWorld2D(screenEnd);

            Vector2 direction = (worldEnd - worldStart);
            float distance = direction.magnitude;

            if (distance < 0.01f)
                direction = Vector2.up; // fallback
            else
                direction.Normalize();

            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                worldStart,
                tapRadius,
                direction,
                distance,
                tapLayerMask
            );

            // fallback wenn kein Treffer
            if (hits.Length == 0)
            {
                Collider2D[] overlapHits = Physics2D.OverlapCircleAll(worldStart, tapRadius, tapLayerMask);

                if (overlapHits.Length > 0)
                {
                    HandleBestOverlap(overlapHits, worldStart);
                    return;
                }
            }
            else
            {
                HandleBestRaycast(hits, worldStart);
                return;
            }

            return;
        }

        // =========================
        // 🔥 SWIPE
        // =========================
        var swipePoint = spawner != null ? spawner.CurrentSwipePoint : null;

        if (swipePoint != null)
        {
            swipePoint.TryStrikeScreen(screenStart, screenEnd, cam);
        }
    }

    // =========================================
    // 🔥 BEST HIT AUS RAYCAST
    // =========================================
    private void HandleBestRaycast(RaycastHit2D[] hits, Vector3 origin)
    {
        Collider2D best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            float dist = Vector2.Distance(origin, hit.point);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = hit.collider;
            }
        }

        if (best != null)
            ProcessHit(best);
    }

    // =========================================
    // 🔥 BEST HIT AUS OVERLAP
    // =========================================
    private void HandleBestOverlap(Collider2D[] hits, Vector3 origin)
    {
        Collider2D best = null;
        float bestDist = float.MaxValue;

        foreach (var col in hits)
        {
            float dist = Vector2.Distance(origin, col.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = col;
            }
        }

        if (best != null)
            ProcessHit(best);
    }

    // =========================================
    // 🔥 HIT LOGIK (PRIORITÄT)
    // =========================================
    private void ProcessHit(Collider2D col)
    {
        // 🟡 Gold
        var gold = col.GetComponent<GoldModeActivationPoint>();
        if (gold != null)
        {
            gold.OnTapped();
            return;
        }

        // 🔴 Gravity Orb
        var gravityOrb = col.GetComponent<GravityModeActivationPoint>();
        if (gravityOrb != null)
        {
            gravityOrb.TryTap();
            return;
        }

        // 🔴 Gravity Point
        var gravityPoint = col.GetComponent<GravityPoint>();
        if (gravityPoint != null)
        {
            gravityPoint.TryTap();
            return;
        }

        // 🔵 Normal
        var tapPoint = col.GetComponent<TapPoint>();
        if (tapPoint != null)
        {
            tapPoint.TryTap();
        }
    }

    public void ResetTouch()
    {
        if (trailStarted)
            slashTrail?.End();

        isTouching = false;
        trailStarted = false;
    }

    private void OnEnable()
    {
        controls ??= new GameControls();
        controls.Enable();
    }

    private void OnDisable()
    {
        controls?.Disable();
    }

    private Vector3 ScreenToWorld2D(Vector2 screenPos)
    {
        var plane = new Plane(Vector3.forward, Vector3.zero);
        var ray = cam.ScreenPointToRay(screenPos);

        if (plane.Raycast(ray, out float enter))
        {
            var p = ray.GetPoint(enter);
            p.z = 0f;
            return p;
        }

        var fb = cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

        fb.z = 0f;
        return fb;
    }
}