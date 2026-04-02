using UnityEngine;
using System.Collections;

public class ScreenPixelate : MonoBehaviour
{
    public static ScreenPixelate Instance;

    [SerializeField] private Material pixelateMaterial;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayEffect(float duration)
    {
        StartCoroutine(Co_Pixelate(duration));
    }

    private IEnumerator Co_Pixelate(float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float strength = Mathf.Lerp(80f, 500f, t / duration);
            pixelateMaterial.SetFloat("_PixelSize", strength);

            yield return null;
        }

        pixelateMaterial.SetFloat("_PixelSize", 500f);
    }
}