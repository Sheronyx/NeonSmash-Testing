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

        if (spawner != null)
        {
            spawner.ActivateGoldMode();
        }

        DestroySelf();
    }

    private void DestroySelf()
    {
        if (spawner != null)
        {
            spawner.OnComboDestroyed();
        }

        Destroy(gameObject);
    }
}