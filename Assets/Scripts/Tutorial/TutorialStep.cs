using UnityEngine;

[System.Serializable]
public class TutorialStep
{
    public string headline;
    [TextArea(3, 10)] public string text;
    public Sprite image;
}
