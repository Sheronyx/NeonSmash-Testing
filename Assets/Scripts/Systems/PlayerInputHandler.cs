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
            if (LivesManager.IsLifeLostAnimating) return;

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
            if (LivesManager.IsLifeLostAnimating) return;
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
            ProcessSlash(worldPrev, worldNow, fromSwipe: true);
        };

        // TOUCH END
        controls.Player.TouchPress.canceled += ctx =>
        {
            if (PauseMenuController.IsPaused) return;
            if (LivesManager.IsLifeLostAnimating) return;

            if (trailStarted)
                slashTrail?.End();

            isTouching = false;
            trailStarted = false;

            Vector2 touchEndPos = controls.Player.TouchPosition.ReadValue<Vector2>();

            Vector2 delta = touchEndPos - touchStartPos;

            // 👉 SWIPE
            if (delta.magnitude >= swipeThresholdPixels)
            {
                if (PeekABooSystem.IsActive)
                {
                    // Peek-a-boo: mehrere Swipe-Elemente möglich → alle prüfen
                    foreach (var sp in FindObjectsByType<SwipePoint>(FindObjectsSortMode.None))
                        sp.TryStrikeScreen(touchStartPos, touchEndPos, cam);
                }
                else
                {
                    var swipePoints = spawner?.GetActiveSwipePoints();
                    if (swipePoints != null)
                        foreach (var sp in swipePoints)
                            if (sp.TryStrikeScreen(touchStartPos, touchEndPos, cam))
                                break;
                }
            }
            else
            {
                // 👉 TAP fallback
                ProcessTap(ScreenToWorld2D(touchEndPos));
            }
        };
    }


    private void ProcessSlash(Vector3 from, Vector3 to, bool fromSwipe = false)
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

            ProcessHit(hit.collider, fromSwipe);
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
    private void ProcessHit(Collider2D col, bool fromSwipe = false)
    {
        // ☁️ Peek-a-boo Elemente — Klick löst beide gekoppelten Elemente aus
        if (!fromSwipe)
        {
            var peek = col.GetComponent<PeekElement>();
            if (peek != null)
            {
                peek.OnHit();
                return;
            }
        }

        // 🔴 Gravity Points — nie per Swipe treffbar
        if (!fromSwipe)
        {
            var gravityPoint = col.GetComponent<GravityPoint>();
            if (gravityPoint != null)
            {
                gravityPoint.TryTap();
                return;
            }
        }

        // 🔵 Fountain Points — nie per Swipe treffbar
        if (!fromSwipe)
        {
            var fountainPoint = col.GetComponent<FountainPoint>();
            if (fountainPoint != null)
            {
                fountainPoint.TryTap();
                return;
            }
        }

        // ⚪ Fake Points — nie per Swipe treffbar, verpuffen nur (Ablenkung)
        if (!fromSwipe)
        {
            var fakePoint = col.GetComponent<FakePoint>();
            if (fakePoint != null)
            {
                fakePoint.TryTap();
                return;
            }
        }

        // ⚡ Thunder Points — nie per Swipe treffbar, Antippen kostet ein Leben
        if (!fromSwipe)
        {
            var thunderPoint = col.GetComponent<ThunderPoint>();
            if (thunderPoint != null)
            {
                thunderPoint.TryTap();
                return;
            }
        }

        // 🔵 Normale Tap Points — nie per Swipe treffbar
        if (!fromSwipe)
        {
            var tapPoint = col.GetComponent<TapPoint>();
            if (tapPoint != null)
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