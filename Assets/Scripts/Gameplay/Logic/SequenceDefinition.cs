using UnityEngine;

[System.Serializable]
public class BonusDelivery
{
    [Tooltip("Sekunden nach Animationsstart, wann dieser Bonus ausgegeben wird.")]
    public float delay;
    [Tooltip("Anteil der bonusPoints für diese Lieferung (0–1). Alle Einträge zusammen sollten 1.0 ergeben.")]
    [Range(0f, 1f)] public float fraction = 1f;
    [Tooltip("Relative Textgröße des Floating Score (1 = normal, 2 = doppelt so groß).")]
    public float textScale = 1f;
    [Tooltip("Position in Viewport-Koordinaten: (0,0)=unten links, (1,1)=oben rechts. (0.5,0.5)=Mitte.")]
    public Vector2 viewportPosition = new Vector2(0.5f, 0.6f);
    [Tooltip("Farbe des Floating Score Texts.")]
    public PointColor textColor = PointColor.Pink;
}

[CreateAssetMenu(fileName = "SequenceDefinition", menuName = "NeonSmash/Sequence Definition")]
public class SequenceDefinition : ScriptableObject
{
    public string sequenceName = "Sequence";
    public PointColor[] steps;
    public int bonusPoints = 150;
    public Sprite abilityIcon;

    [Header("Fullscreen-Effekt (optional)")]
    [Tooltip("Material für den Vollbild-Shader. Leer = kein Fullscreen-Effekt.")]
    public Material fullScreenMaterial;
    [Tooltip("Renderer Feature das für dieses Material aktiviert werden muss.")]
    public UnityEngine.Rendering.Universal.ScriptableRendererFeature fullScreenFeature;

    [Header("Kombo-Effekt")]
    [Tooltip("Prefab das beim Abschluss dieser Sequenz gespawnt wird (VFX, Partikel, Canvas-Overlay, oder Kombination). Position/Rotation werden aus dem Prefab übernommen.")]
    public GameObject comboEffectPrefab;
    [Tooltip("Wie lange das Gameplay pausiert während der Effekt läuft (Sekunden, unscaled).")]
    public float effectDuration = 2f;

    [Header("Bonus-Punktzahl (Timing & Position)")]
    [Tooltip("Wann und wie die bonusPoints ausgegeben werden. Leer = sofort beim Hit (kein Effekt-Prefab nötig). " +
             "Sortiere Einträge nach aufsteigendem Delay. Fractions zusammen = 1.0.")]
    public BonusDelivery[] bonusDeliveries;
}
