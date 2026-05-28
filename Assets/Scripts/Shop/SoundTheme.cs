using UnityEngine;

[CreateAssetMenu(fileName = "SoundTheme", menuName = "NeonSmash/Sound Theme")]
public class SoundTheme : ScriptableObject
{
    [Tooltip("Kurzer Vorschau-Clip (~10s) für den Shop-Preview-Button.")]
    public AudioClip previewClip;

    [Tooltip("Hintergrundmusik im Hauptmenü. Null = Default behalten.")]
    public AudioClip menuMusicClip;

    [Tooltip("Hintergrundmusik im Spiel. Null = Default behalten.")]
    public AudioClip gameMusicClip;

    // Später: tapClip, swipeClip
}
