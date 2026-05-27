using UnityEditor;
using UnityEditor.SceneManagement;

// Option+1..4 öffnen die Projekt-Scenes direkt im Editor.
// Ungespeicherte Änderungen werden vorher abgefragt.
public static class SceneShortcuts
{
    const string Bootstrap  = "Assets/Scenes/BootstrapScene.unity";
    const string Intro      = "Assets/Scenes/IntroScene.unity";
    const string MainMenu   = "Assets/Scenes/MainMenuScene.unity";
    const string GameInfinity = "Assets/Scenes/GameScenes/GameScene_InfinityMode.unity";

    [MenuItem("Scenes/1 - Bootstrap &1")]
    static void OpenBootstrap() => Open(Bootstrap);

    [MenuItem("Scenes/2 - Intro &2")]
    static void OpenIntro() => Open(Intro);

    [MenuItem("Scenes/3 - MainMenu &3")]
    static void OpenMainMenu() => Open(MainMenu);

    [MenuItem("Scenes/4 - GameScene Infinity &4")]
    static void OpenGameInfinity() => Open(GameInfinity);

    static void Open(string path)
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(path);
    }
}
