using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

public class ArcanePortalFlash : MonoBehaviour
{
    public VisualEffect vfx;

    public float fadeInTime = 0.15f;
    public float holdTime   = 0.05f;
    public float fadeOutTime = 0.25f;

    Coroutine flashRoutine;

    public void FlashParticles()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(CoFlash());
    }

    IEnumerator CoFlash()
    {
        Vector4 normalParticles = vfx.GetVector4("Color Particles");
        Vector4 normalVoronoi   = vfx.GetVector4("Color Voronoi");

        Vector4 clearedParticles = vfx.GetVector4("Color ParticlesPointCleared");
        Vector4 clearedVoronoi   = vfx.GetVector4("Color VoronoiPointCleared");

        // --- Fade IN ---
        float t = 0f;
        while (t < fadeInTime)
        {
            float n = t / fadeInTime;

            vfx.SetVector4("Color Particles", Vector4.Lerp(normalParticles, clearedParticles, n));
            vfx.SetVector4("Color Voronoi",   Vector4.Lerp(normalVoronoi, clearedVoronoi, n));

            t += Time.deltaTime;
            yield return null;
        }

        // Sicherstellen dass Ziel exakt gesetzt ist
        vfx.SetVector4("Color Particles", clearedParticles);
        vfx.SetVector4("Color Voronoi", clearedVoronoi);

        yield return new WaitForSeconds(holdTime);

        // --- Fade OUT ---
        t = 0f;
        while (t < fadeOutTime)
        {
            float n = t / fadeOutTime;

            vfx.SetVector4("Color Particles", Vector4.Lerp(clearedParticles, normalParticles, n));
            vfx.SetVector4("Color Voronoi",   Vector4.Lerp(clearedVoronoi, normalVoronoi, n));

            t += Time.deltaTime;
            yield return null;
        }

        // Zurücksetzen exakt
        vfx.SetVector4("Color Particles", normalParticles);
        vfx.SetVector4("Color Voronoi", normalVoronoi);

        flashRoutine = null;
    }
}