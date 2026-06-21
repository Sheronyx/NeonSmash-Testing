using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Kopiert vor jedem iOS-Build automatisch die passende GoogleService-Info.plist
/// aus FirebaseConfigs/ (Projekt-Root, außerhalb von Assets) nach Assets/GoogleService-Info.plist.
///
///   iOS-Build mit Scripting-Define NEONSMASH_PROD → prod-plist (com.sheronyx.neonsmash)
///   iOS-Build ohne Define                         → dev-plist  (com.sheronyx.neonsmash.dev)
///
/// Android wird NICHT angefasst — dort enthält die eine google-services.json beide
/// Package Names und der Gradle-Plugin wählt automatisch anhand der applicationId.
/// </summary>
public class FirebaseConfigSwitcher : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    const string ActivePlist = "Assets/GoogleService-Info.plist";
    const string ConfigDir   = "FirebaseConfigs"; // relativ zum Projekt-Root
    const string ProdFile    = "GoogleService-Info-prod.plist";
    const string DevFile     = "GoogleService-Info-dev.plist";
    const string Define      = "NEONSMASH_PROD";

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS)
            return;

        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
        bool isProd = defines.Split(';').Select(d => d.Trim()).Contains(Define);

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string srcName = isProd ? ProdFile : DevFile;
        string src = Path.Combine(projectRoot, ConfigDir, srcName);
        string dst = Path.Combine(projectRoot, ActivePlist);

        if (!File.Exists(src))
        {
            throw new BuildFailedException(
                $"[FirebaseConfig] Quell-Config nicht gefunden: {ConfigDir}/{srcName}. " +
                "Build abgebrochen, damit nicht die falsche Firebase-Umgebung ausgeliefert wird.");
        }

        File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(ActivePlist);

        Debug.Log($"[FirebaseConfig] iOS-Build → {(isProd ? "PROD" : "DEV")} " +
                  $"({srcName}) nach {ActivePlist} kopiert.");
    }
}
