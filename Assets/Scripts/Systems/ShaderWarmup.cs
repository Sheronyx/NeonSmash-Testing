using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Wärmt alle Shader und VFX-Graphen beim App-Start auf, damit es beim ersten
/// Gameplay kein Stutter gibt. Dieses Script auf ein DontDestroyOnLoad-Objekt
/// in der ersten Szene (IntroScene) legen.
/// </summary>
public class ShaderWarmup : MonoBehaviour
{
    [Tooltip("Optional: ShaderVariantCollection aus Project Settings → Graphics zuweisen")]
    [SerializeField] private ShaderVariantCollection variantCollection;

    private void Awake()
    {
        // Alle im Projekt bekannten Shader-Varianten vorcompilieren
        if (variantCollection != null)
            variantCollection.WarmUp();

        // Fallback: alle geladenen Shader
        Shader.WarmupAllShaders();
    }
}
