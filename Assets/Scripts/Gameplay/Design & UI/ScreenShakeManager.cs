using UnityEngine;
using System.Collections;

public class ScreenShakeManager : MonoBehaviour
{
    public static ScreenShakeManager Instance;

    [SerializeField] private Transform cameraTransform;

    private Vector3 originalPos;

    private void Awake()
    {
        Instance = this;

        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;

        originalPos = cameraTransform.localPosition;
    }

    public void Shake(float duration, float strength)
    {
        StartCoroutine(Co_Shake(duration, strength));
    }

    private IEnumerator Co_Shake(float duration, float strength)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float x = Random.Range(-1f, 1f) * strength;
            float y = Random.Range(-1f, 1f) * strength;

            cameraTransform.localPosition = originalPos + new Vector3(x, y, 0f);

            yield return null;
        }

        cameraTransform.localPosition = originalPos;
    }
}