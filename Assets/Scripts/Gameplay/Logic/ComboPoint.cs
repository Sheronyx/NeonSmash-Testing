using UnityEngine;
using System.Collections;

public class ComboPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [SerializeField] private float lifetime = 3f;

    private bool isDestroyed = false;

    void Start()
    {
        StartCoroutine(AutoDestroy());
    }

    private IEnumerator AutoDestroy()
    {
        yield return new WaitForSeconds(lifetime);

        if (!isDestroyed)
        {
            DestroySelf();
        }
    }

    public void OnTapped()
    {
        if (isDestroyed) return;

        isDestroyed = true;

        Debug.Log("COMBO GETRIGGERT!");

        // 👉 später: Gold Mode starten

        DestroySelf();
    }

    private void DestroySelf()
    {
        if (spawner != null)
        {
            // Combo zählt NICHT als normaler Punkt!
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}