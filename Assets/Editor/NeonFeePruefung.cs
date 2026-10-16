#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

// Einmal-Werkzeug: vergleicht das neu importierte Modell mit dem Prefab, das es benutzt.
// Nach dem Ausfuehren wieder loeschen.
public static class NeonFeePruefung
{
    const string FbxPfad    = "Assets/001 Cuby Dreams/Characters/WaterPriestFairy/WaterPriestFairy_Character.fbx";
    const string PrefabPfad = "Assets/001 Cuby Dreams/Characters/WaterPriestFairy/WaterPriestFairy_3D.prefab";

    [MenuItem("NeonSmash/Temp/Fee pruefen")]
    public static void Pruefen()
    {
        var sb = new StringBuilder();

        var modell = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPfad);
        sb.AppendLine("##### MODELL " + FbxPfad);
        if (modell == null) sb.AppendLine("  NICHT GELADEN");
        else
        {
            foreach (var smr in modell.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sb.AppendLine("  Mesh " + smr.name
                            + "  verts=" + (smr.sharedMesh != null ? smr.sharedMesh.vertexCount : -1)
                            + "  submeshes=" + (smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : -1)
                            + "  materialSlots=" + smr.sharedMaterials.Length
                            + "  bones=" + smr.bones.Length);

            int knochen = 0;
            foreach (var t in modell.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("mixamorig:")) knochen++;
            sb.AppendLine("  Knochen im Modell: " + knochen);

            var imp = AssetImporter.GetAtPath(FbxPfad) as ModelImporter;
            if (imp != null)
                sb.AppendLine("  Import: animationType=" + imp.animationType
                            + " avatarSetup=" + imp.avatarSetup
                            + " useFileScale=" + imp.useFileScale
                            + " globalScale=" + imp.globalScale);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPfad);
        sb.AppendLine("##### PREFAB " + PrefabPfad);
        if (prefab == null) sb.AppendLine("  NICHT GELADEN");
        else
        {
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new StringBuilder();
                foreach (var m in smr.sharedMaterials) mats.Append(m != null ? m.name : "FEHLT").Append(" | ");
                sb.AppendLine("  Mesh " + smr.name
                            + "  sharedMesh=" + (smr.sharedMesh != null ? smr.sharedMesh.name : "FEHLT")
                            + "  verts=" + (smr.sharedMesh != null ? smr.sharedMesh.vertexCount : -1)
                            + "  slots=" + smr.sharedMaterials.Length
                            + "  rootBone=" + (smr.rootBone != null ? smr.rootBone.name : "FEHLT")
                            + "  bones=" + smr.bones.Length);
                sb.AppendLine("      Materialien: " + mats);
            }
            var anim = prefab.GetComponentInChildren<Animator>(true);
            if (anim != null)
                sb.AppendLine("  Animator: avatar=" + (anim.avatar != null ? anim.avatar.name : "FEHLT")
                            + " controller=" + (anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "FEHLT"));
        }

        System.IO.File.WriteAllText("Assets/../fee.txt", sb.ToString());
        Debug.Log("[FEE] geschrieben");
    }
}
#endif
