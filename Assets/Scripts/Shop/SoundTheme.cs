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

    [Header("Lautstärke (zum Angleichen lauter/leiser gemasterter Tracks)")]
    [Tooltip("Basis-Lautstärke der Menü-Musik für diesen Skin. 1 = unverändert.")]
    [Range(0f, 1f)] public float menuMusicVolume = 1f;

    [Tooltip("Basis-Lautstärke der Spiel-Musik für diesen Skin. 1 = unverändert.")]
    [Range(0f, 1f)] public float gameMusicVolume = 1f;

    // Später: tapClip, swipeClip
}
