using UnityEngine;

// UNGENUTZT seit PointFlyIn auf reine Scale-Pop-Spawn-Animation umgestellt wurde (kein Dissolve-Shader
// mehr). Bewusst nicht gelöscht, um "Missing Script" auf Prefabs zu vermeiden, die diese Komponente
// noch als Kind-Marker gesetzt haben — kann dort in Ruhe manuell entfernt werden, ist ansonsten inert.
[RequireComponent(typeof(SpriteRenderer))]
public class SpawnDissolveTarget : MonoBehaviour
{
}
