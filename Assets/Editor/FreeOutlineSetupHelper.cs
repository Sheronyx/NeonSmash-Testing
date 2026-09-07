#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using LineworkLite.FreeOutline;
using LineworkLite.Common.Utils;

// Temporäres Hilfsskript, um das "Free Outline"-Paket (linework-lite) programmatisch für einen
// Test auf den drei Feen (FairyForest, FairyCrystal, FairyFountain) einzurichten. Kann nach dem
// Test wieder gelöscht werden, wenn das Setup manuell im Inspector weitergepflegt wird.
public static class FreeOutlineSetupHelper
{
    [MenuItem("Tools/Free Outline/Setup Fairy Test")]
    public static void SetupFairyTest()
    {
        var outline = ScriptableObject.CreateInstance<Outline>();
        outline.name = "FairyTest Outline";
        outline.color = Color.black;
        outline.width = 4f;
        outline.minWidth = 0f;
        outline.maskingStrategy = MaskingStrategy.Stencil;
        outline.occlusion = Occlusion.WhenNotOccluded;
        outline.extrusionMethod = ExtrusionMethod.ClipSpaceNormalVector;
        outline.layerMask = 1 << LayerMask.NameToLayer("OutlineTest");

        var settings = ScriptableObject.CreateInstance<FreeOutlineSettings>();
        settings.name = "Free Outline Settings - FairyTest";

        string settingsPath = "Assets/001 Fairy World/Free Outline Settings - FairyTest.asset";
        AssetDatabase.CreateAsset(settings, settingsPath);
        AssetDatabase.AddObjectToAsset(outline, settings);
        settings.Outlines.Add(outline);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        // Renderer Feature auf dem aktiven URP Renderer hinzufuegen
        string rendererPath = "Assets/Settings/Build Profiles/URP Universal Renderer.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(rendererPath);
        if (rendererData == null)
        {
            Debug.LogError("Renderer data not found at " + rendererPath);
            return;
        }

        var feature = ScriptableObject.CreateInstance<FreeOutline>();
        feature.name = "FreeOutline - FairyTest";
        AssetDatabase.AddObjectToAsset(feature, rendererData);

        var so = new SerializedObject(feature);
        var settingsProp = so.FindProperty("settings");
        settingsProp.objectReferenceValue = settings;
        so.ApplyModifiedProperties();

        var rendererSo = new SerializedObject(rendererData);
        var featuresProp = rendererSo.FindProperty("m_RendererFeatures");
        var mapProp = rendererSo.FindProperty("m_RendererFeatureMap");
        int newIndex = featuresProp.arraySize;
        featuresProp.arraySize++;
        featuresProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = feature;
        if (mapProp != null)
        {
            mapProp.arraySize++;
            mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = feature.GetInstanceID();
        }
        rendererSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Free Outline Fairy-Test-Setup abgeschlossen: Settings=" + settingsPath + ", Feature auf Renderer hinzugefuegt.");
    }
}
#endif
