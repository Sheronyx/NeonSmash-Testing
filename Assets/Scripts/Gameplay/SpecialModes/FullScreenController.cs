using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;

public class FullScreenController : MonoBehaviour
{

    [Header("Time Stats")]
    [SerializeField] private float HurtDisplayTime = 1.5f;
    [SerializeField] private float HurtFadeOutTime  = 0.5f;

    [Header("References")]
    [SerializeField] private ScriptableRendererFeature FullScreenDamage;
    [SerializeField] private Material Material;

    [Header("Intensity Stats")]
    [SerializeField] private float VoronoiIntensityStats = 2.5f;
    [SerializeField] private float VignetteIntensityStats = 1.25f;

    private int VoronoiIntensity = Shader.PropertyToID("_VoronoiIntensity");
    private int VignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        FullScreenDamage.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartCoroutine(Hurt());
        }
    }


    private IEnumerator Hurt()
    {
        FullScreenDamage.SetActive(true);
        Material.SetFloat(VoronoiIntensity, VoronoiIntensityStats);
        Material.SetFloat(VignetteIntensity, VignetteIntensityStats);

        yield return new WaitForSeconds(HurtDisplayTime);

        float elapsedTime = 0f;
        while (elapsedTime < HurtFadeOutTime)
        {
            elapsedTime += Time.deltaTime;
            float lerpedVoronoi = Mathf.Lerp(VoronoiIntensityStats, 0f, elapsedTime / HurtFadeOutTime);
            float lerpedVignette = Mathf.Lerp(VignetteIntensityStats, 0f, elapsedTime / HurtFadeOutTime);

            Material.SetFloat(VoronoiIntensity, lerpedVoronoi);
            Material.SetFloat(VignetteIntensity, lerpedVignette);
            yield return null;
        }
    
        FullScreenDamage.SetActive(false);
    }
}
