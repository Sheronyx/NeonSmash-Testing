using UnityEngine;

// Unsichtbarer Fixpunkt an der alten Portal-Position. Ersetzt ArcanePortalFlash als Sog-Ziel
// (GravityPoint), Schuss-Ursprung (FountainModeSystem) und Activation-Orb-Flugziel (Gravity/
// Fountain/Vortex ActivationPoint) — rein positionell, kein sichtbares Objekt, keine Logik.
public class PortalAnchor : MonoBehaviour
{
    public static PortalAnchor Instance { get; private set; }

    private void Awake() => Instance = this;
}
