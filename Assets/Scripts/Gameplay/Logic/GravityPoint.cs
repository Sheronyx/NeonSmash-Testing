using UnityEngine;

public class GravityPoint : MonoBehaviour
{
    private MixedPointSpawner spawner;

    [SerializeField] private float fallSpeed = 3f;
    [SerializeField] private float portalY = -6f; // anpassen!

    private bool isDestroyed = false;

    private GravityModeSystem gravitySystem;

    public void Init(GravityModeSystem system)
    {
        gravitySystem = system;
    }

    void Update()
    {
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime);

        // 🌀 Portal erreicht
        if (!isDestroyed && transform.position.y <= portalY)
        {
            isDestroyed = true;
            gravitySystem.OnPointDestroyed(false);
            Destroy(gameObject);
        }
    }

public void TryTap()
{
    if (isDestroyed) return;

    isDestroyed = true;

    gravitySystem.OnPointDestroyed(true);

    Destroy(gameObject);
}
}