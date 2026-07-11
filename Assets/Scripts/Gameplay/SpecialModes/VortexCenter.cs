using UnityEngine;

// Markiert die Position, zu der Vortex-Mode-Elemente eingesaugt werden (analog ArcanePortalFlash
// für Gravity/Fountain, aber ein eigener Punkt — typischerweise Bildschirmmitte).
// GameObject in der Szene an die gewünschte Position setzen und dieses Script dranhängen.
public class VortexCenter : MonoBehaviour
{
    public static VortexCenter Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }
}
