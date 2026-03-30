using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class UVScroller : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.0f, -0.1f); // Richtung & Tempo
    private Renderer rend;
    private Vector2 offset;

    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

    void Awake() => rend = GetComponent<Renderer>();

    void Update()
    {
        offset += speed * Time.deltaTime;
        if (rend.material.HasProperty(MainTex))
            rend.material.SetTextureOffset(MainTex, offset);
        else if (rend.material.HasProperty(BaseMap))
            rend.material.SetTextureOffset(BaseMap, offset);
    }
}
