#if UNITY_IOS
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

// Google-Pods (AdMob/UMP) bringen intern noch IPHONEOS_DEPLOYMENT_TARGET = 12.0 mit, aktuelle
// Xcode-Versionen akzeptieren als Build-Ziel aber nur noch 15.0+ -> "range of supported deployment
// target versions is 15.0 to 27.0.x"-Fehler beim Bauen in Xcode.
//
// Unity/EDM4U (External Dependency Manager) generiert die Podfile UND ruft "pod install" bereits im
// eigenen PostProcessBuild-Schritt auf -> wir können den Fix nicht VOR dem ersten "pod install"
// einschleusen. Stattdessen laeuft dieses Script bewusst SPAETER (hoher Order-Wert), haengt einen
// post_install-Hook an die bereits generierte Podfile an (der die Deployment-Targets aller Pod-Targets
// hochsetzt) und ruft "pod install" ein zweites Mal auf, damit der Fix tatsaechlich greift.
public static class IOSDeploymentTargetPostProcess
{
    private const string TargetVersion = "15.0";
    private const string MarkerComment = "# --- NeonSmash: auto-fix Pod deployment targets (IOSDeploymentTargetPostProcess) ---";

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.iOS) return;

        string podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
        if (!File.Exists(podfilePath))
        {
            UnityEngine.Debug.LogWarning("[IOSDeploymentTargetPostProcess] Keine Podfile gefunden, überspringe Deployment-Target-Fix: " + podfilePath);
            return;
        }

        string content = File.ReadAllText(podfilePath);
        if (!content.Contains(MarkerComment))
        {
            string hook =
                "\n" + MarkerComment + "\n" +
                "post_install do |installer|\n" +
                "  installer.pods_project.targets.each do |target|\n" +
                "    target.build_configurations.each do |config|\n" +
                "      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '" + TargetVersion + "'\n" +
                "    end\n" +
                "  end\n" +
                "end\n";
            File.AppendAllText(podfilePath, hook);
            UnityEngine.Debug.Log("[IOSDeploymentTargetPostProcess] post_install-Hook zur Podfile hinzugefügt (Deployment Target -> " + TargetVersion + ").");
        }

        RunPodInstall(pathToBuiltProject);
    }

    private static void RunPodInstall(string projectPath)
    {
        // Ueber eine Login-Shell aufrufen, damit rbenv/rvm/Homebrew-PATH-Anpassungen aus dem
        // Nutzerprofil greifen (ein direkter Aufruf von "pod" findet den Befehl in der GUI-App-
        // Umgebung von Unity sonst oft nicht).
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/zsh",
            Arguments = "-l -c \"pod install\"",
            WorkingDirectory = projectPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using (var process = Process.Start(psi))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log("[IOSDeploymentTargetPostProcess] pod install (Deployment-Target-Fix) erfolgreich:\n" + stdout);
                }
                else
                {
                    UnityEngine.Debug.LogError("[IOSDeploymentTargetPostProcess] pod install fehlgeschlagen (Exit " + process.ExitCode + "):\n" + stderr);
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[IOSDeploymentTargetPostProcess] Konnte pod install nicht starten: " + e.Message);
        }
    }
}
#endif
