using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class BurnFuseShader : MonoBehaviour
{
    private SpriteRenderer sr;
    private Material mat;
    private Coroutine burnRoutine;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        // Verwende ein instanziiertes Material, nicht das Shared Material
        mat = sr.material;
        if (mat != null)
        {
            mat.SetFloat("_BurnAmount", 1f);
            Debug.Log($"[BurnFuse] Material: {mat.name}, Shader: {mat.shader.name}");
        }
        else
        {
            Debug.LogWarning("[BurnFuse] Kein Material gefunden!");
        }
    }

    public void StartBurn(float duration)
    {
        if (burnRoutine != null) StopCoroutine(burnRoutine);
        Debug.Log($"[BurnFuse] StartBurn: {duration}s");
        burnRoutine = StartCoroutine(Co_Burn(duration));
    }

    private IEnumerator Co_Burn(float duration)
    {
        if (mat == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - (elapsed / duration);
            mat.SetFloat("_BurnAmount", remaining);

            yield return null;
        }

        mat.SetFloat("_BurnAmount", 0f);
        burnRoutine = null;
    }
}
