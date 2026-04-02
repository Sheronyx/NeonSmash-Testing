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

    [Header("Einstellungen")]
    [SerializeField] private float swipeThresholdPixels = 80f;
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

                if (slashTrail != null)
                    slashTrail.Begin(ScreenToWorld2D(pos));
            }
            else
            {
                if (slashTrail != null)
                    slashTrail.Move(ScreenToWorld2D(pos));
            }
        };

        // TOUCH END
        controls.Player.TouchPress.canceled += ctx =>
        {
            if (PauseMenuController.IsPaused) return;

            if (trailStarted && slashTrail != null)
                slashTrail.End();

            isTouching = false;
            trailStarted = false;

            touchEndPos = controls.Player.TouchPosition.ReadValue<Vector2>();

            HandleTouchCompleted(touchStartPos, touchEndPos);
        };
    }

    private void HandleTouchCompleted(Vector2 screenStart, Vector2 screenEnd)
    {
        Vector2 delta = screenEnd - screenStart;

        // TAP
        if (delta.magnitude < swipeThresholdPixels)
        {
            Vector3 worldStart = ScreenToWorld2D(screenStart);

            RaycastHit2D hit = Physics2D.Raycast(worldStart, Vector2.zero, 0f, tapLayerMask);

            if (hit.collider != null)
            {
                // 🟡 1. Gold Mode Activation Point (PRIORITÄT!)
                var goldPoint = hit.collider.GetComponent<GoldModeActivationPoint>();
                if (goldPoint != null)
                {
                    goldPoint.OnTapped();
                    return;
                }

                // 🔵 2. Normale Tap Points
                var tapPoint = hit.collider.GetComponent<TapPoint>();
                if (tapPoint != null)
                {
                    tapPoint.TryTap();
                    return;
                }
            }

            return;
        }

        // SWIPE
        var swipePoint = spawner != null ? spawner.CurrentSwipePoint : null;

        if (swipePoint != null)
            swipePoint.TryStrikeScreen(screenStart, screenEnd, cam);
    }



    public void ResetTouch()
    {
        if (trailStarted && slashTrail != null)
            slashTrail.End();

        isTouching = false;
        trailStarted = false;
    }

    private void OnEnable()
    {
        if (controls == null)
            controls = new GameControls();

        controls.Enable();
    }

    private void OnDisable()
    {
        if (controls != null)
            controls.Disable();
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