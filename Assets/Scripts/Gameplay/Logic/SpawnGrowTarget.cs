using UnityEngine;

// Markiert ein Kind-Objekt, das beim Spawnen smooth von Skalierung 0 auf seine Originalgröße
// wachsen soll (siehe PointFlyIn.PlayDissolveSpawn) — z.B. innerRect/outerRect/FuseVisual. Auf
// beliebig viele Kind-Objekte pro Skin setzbar.
//
// NICHT zusätzlich auf Objekte setzen, die ihre Skalierung bereits selbst verwalten (z.B.
// CountdownSquare, das sein eigenes Wachsen vor dem anschließenden Countdown selbst übernimmt) —
// sonst konkurrieren zwei Systeme um dieselbe Transform-Skalierung (sichtbares Ruckeln).
public class SpawnGrowTarget : MonoBehaviour
{
}
