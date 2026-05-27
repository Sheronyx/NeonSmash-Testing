using UnityEngine;

public enum GameMode
{
    Infinity,
    Multiplayer
}

public class GlobalGameManager : MonoBehaviour
{
    public static GlobalGameManager Instance { get; private set; }
    public GameMode SelectedMode { get; private set; } = GameMode.Infinity; // Default

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetMode(GameMode mode) => SelectedMode = mode;

public void OverrideSelectedMode(GameMode mode)
{
    SelectedMode = mode;
}


}



