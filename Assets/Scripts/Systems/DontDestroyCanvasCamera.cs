using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Canvas))]
public class DontDestroyCanvasCamera : MonoBehaviour
{
    Canvas _canvas;

    void Awake() => _canvas = GetComponent<Canvas>();

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Camera.main != null)
            _canvas.worldCamera = Camera.main;
    }
}
