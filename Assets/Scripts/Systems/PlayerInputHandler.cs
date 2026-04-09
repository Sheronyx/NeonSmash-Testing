using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance;

    private GameControls controls;

    private Vector2 touchStartPos;
    private Vector2 touchCurrentPos;

    private bool isTouching;
    private bool trailStarted;

    private Camera cam;

    [Header("Settings")]
    [SerializeField] private float swipeThresholdPixels = 80f;
    [SerializeField] private float slashRadius = 0.45f;

    [Header("Layer Masks")]
    // 👉 Alle normalen Objekte (GravityPoints, TapPoints) – ActivationOrb Layer AUSSCHLIESSEN
    [SerializeField] private LayerMask hitLayerMask = ~0;
    // 👉 Nur der "ActivationOrb" Layer – für präzisen direkten Tap
    [SerializeField] private LayerMask activationOrbLayerMask;

    [Header("Refs")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private SlashTrail slashTrail;

    // 👉 verhindert mehrfaches Treffen desselben Objekts pro Swipe
    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

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
            alreadyHit.Clear();

            touchStartPos = controls.Player.TouchPosition.ReadValue<Vector2>();
            touchCurrentPos = touchStartPos;
        };

        // TOUCH MOVE
        controls.Player.TouchPosition.performed += ctx =>
        {
            if (PauseMenuController.IsPaused) return;
            if (!isTouching) return;

            Vector2 newPos = ctx.ReadValue<Vector2>();

            Vector3 worldPrev = ScreenToWorld2D(touchCurrentPos);
            Vector3 worldNow = ScreenToWorld2D(newPos);

            touchCurrentPos = newPos;

            // 👉 Trail zeichnen
            if (!trailStarted)
            {
                trailStarted = true;
                slashTrail?.Begin(worldNow);
            }
            else
            {
                slashTrail?.Move(worldNow);
            }

            // 🔥 CONTINUOUS HIT DETECTION
            ProcessSlash(worldPrev, worldNow);
        };

        // TOUCH END
        controls.Player.TouchPress.canceled += ctx =>
        {
            if (PauseMenuController.IsPaused) return;

            if (trailStarted)
                slashTrail?.End();

            isTouching = false;
            trailStarted = false;

            Vector2 touchEndPos = controls.Player.TouchPosition.ReadValue<Vector2>();

            Vector2 delta = touchEndPos - touchStartPos;

            // 👉 SWIPE
            if (delta.magnitude >= swipeThresholdPixels)
            {
                var swipePoint = spawner != null ? spawner.CurrentSwipePoint : null;

                if (swipePoint != null)
                {
                    swipePoint.TryStrikeScreen(touchStartPos, touchEndPos, cam);
                }
            }
            else
            {
                // 👉 TAP fallback
                ProcessTap(ScreenToWorld2D(touchEndPos));
            }
        };
    }

    // =========================================
    // 🔥 SLASH SYSTEM (Fruit Ninja Style)
    // =========================================
    private void ProcessSlash(Vector3 from, Vector3 to)
    {
        Vector2 dir = (to - from);
        float distance = dir.magnitude;

        if (distance < 0.001f) return;

        dir.Normalize();

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            from,
            slashRadius,
            dir,
            distance,
            hitLayerMask
        );

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            GameObject obj = hit.collider.gameObject;

            if (alreadyHit.Contains(obj)) continue;
            alreadyHit.Add(obj);

            ProcessHit(hit.collider);
        }
    }

    // =========================================
    // 🔥 TAP FALLBACK
    // =========================================
    private void ProcessTap(Vector3 worldPos)
    {
        // ✅ SCHRITT 1: ActivationOrbs – nur bei präzisem direktem Tap (kleiner Radius)
        Collider2D[] orbHits = Physics2D.OverlapCircleAll(worldPos, slashRadius * 0.55f, activationOrbLayerMask);

        if (orbHits.Length > 0)
        {
            Collider2D bestOrb = null;
            float bestOrbDist = float.MaxValue;

            foreach (var col in orbHits)
            {
                float dist = Vector2.Distance(worldPos, col.transform.position);
                if (dist < bestOrbDist)
                {
                    bestOrbDist = dist;
                    bestOrb = col;
                }
            }

            if (bestOrb != null)
            {
                ProcessHit(bestOrb);
                return;
            }
        }

        // ✅ SCHRITT 2: Normale Objekte – Collider-Offset erledigt die Prediction (in GravityPoint)
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, slashRadius, hitLayerMask);

        Collider2D best = null;
        float bestDist = float.MaxValue;

        foreach (var col in hits)
        {
            float dist = Vector2.Distance(worldPos, col.transform.position);

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
    // 🔥 HIT LOGIK (Priorität)
    // =========================================
    private void ProcessHit(Collider2D col)
    {
        // 🟡 Gold Orb
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

        // 🔴 Gravity Points
        var gravityPoint = col.GetComponent<GravityPoint>();
        if (gravityPoint != null)
        {
            gravityPoint.TryTap();
            return;
        }

        // 🔵 Fountain Orb
        var fountainOrb = col.GetComponent<FountainModeActivationPoint>();
        if (fountainOrb != null)
        {
            fountainOrb.TryTap();
            return;
        }

        // 🔵 Fountain Points
        var fountainPoint = col.GetComponent<FountainPoint>();
        if (fountainPoint != null)
        {
            fountainPoint.TryTap();
            return;
        }

        // 🔵 Normale Tap Points
        var tapPoint = col.GetComponent<TapPoint>();
        if (tapPoint != null)
        {
            tapPoint.TryTap();
        }
    }

    // =========================================

    public void ResetTouch()
    {
        if (trailStarted)
            slashTrail?.End();

        isTouching = false;
        trailStarted = false;
        alreadyHit.Clear();
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