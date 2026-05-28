using UnityEngine;

[CreateAssetMenu(fileName = "SkinTheme", menuName = "NeonSmash/Skin Theme")]
public class SkinTheme : ScriptableObject
{
    [Tooltip("Ersetzt das Standard Tap-Prefab. Null = Spawner-Default.")]
    public GameObject tapPointPrefab;

    [Tooltip("Ersetzt das Standard Swipe-Prefab. Null = Spawner-Default.")]
    public GameObject swipePointPrefab;
}
