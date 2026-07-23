using UnityEngine;

// Markiert das SpriteRenderer-Kind, das beim Spawnen den Dissolve-Effekt bekommt (siehe
// PointFlyIn.PlayDissolveSpawn) — das mit dem Dissolve-Material (Shader Graphs/2D Dissolve Shader).
// Pro Skin auf genau EIN Kind-Objekt setzen (z.B. "middleRect" bei Tap-, "NeonSlideElementInnerRect"
// bei Swipe-Elementen — Name ist pro Skin unterschiedlich, deshalb Marker statt fester Namenssuche).
[RequireComponent(typeof(SpriteRenderer))]
public class SpawnDissolveTarget : MonoBehaviour
{
}
