using UnityEngine;

// Auf dem explosion_water Prefab: sucht BarCollisionPlane in der Szene
// und weist es dem Particle System Collision Modul zu.
[RequireComponent(typeof(ParticleSystem))]
public class CollisionPlaneLinker : MonoBehaviour
{
    [Tooltip("Name des GameObjects in der Szene das als Collision Plane dient.")]
    [SerializeField] private string planeName = "BarCollisionPlane";

    private void Start()
    {
        var plane = GameObject.Find(planeName);
        if (plane == null) { Debug.LogWarning($"CollisionPlaneLinker: '{planeName}' nicht gefunden."); return; }

        var ps  = GetComponent<ParticleSystem>();
        ps.collision.SetPlane(0, plane.transform);

        // Neu starten damit keine Partikel aus dem Awake-Frame ohne Plane existieren
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();
    }
}
