using UnityEngine;

// Ein einzelnes Belohnungs-"Baustein"-Asset fürs Fortschritt-/Level-System (siehe LevelRewardTrack).
// Analog zu ShopItem: eine Klasse, ein Kind-Enum, generische Felder je nach Kind — neue Belohnung
// = neues Asset im Inspector anlegen, kein Code nötig.
public enum RewardKind
{
    DiamondSplinters,
    Diamonds,
    Sticker,
    Title,
    Booster,
    WorldUnlock,
}

// Seltenheit eines Stickers, bestimmt nur die Münz-Optik (siehe StickerCoinUI):
// Common = grau, Rare = blau, Epic = lila, Legendary = gold-orange.
public enum StickerRarity
{
    Common,
    Rare,
    Epic,
    Legendary,
}

[CreateAssetMenu(fileName = "RewardDefinition", menuName = "NeonSmash/Reward Definition")]
public class RewardDefinition : ScriptableObject
{
    [Tooltip("Eindeutige, stabile ID — wird für Persistenz verwendet (nicht umbenennen, wenn schon " +
             "Spieler sie besitzen könnten).")]
    public string rewardId;
    public string displayName;
    [TextArea]
    [Tooltip("Nur bei Kind=Title verwendet — kurzer Flavour-Text (siehe Titelbuch).")]
    public string description;
    public Sprite icon;
    public RewardKind kind;

    [Tooltip("Nur bei Kind=Title: Name/Beschreibung bleiben bis zur Freischaltung als \"???\" " +
             "versteckt (siehe TitleEntryUI). Entspricht \"geheim\"/\"geheimer Pfad\" in der " +
             "Titel-Excel — offene Titel (\"offener Pfad\") lassen dies aus.")]
    public bool secret;

    [Header("DiamondSplinters / Diamonds / Booster")]
    [Tooltip("Menge bei Kind=DiamondSplinters/Diamonds/Booster.")]
    public int amount;

    [Header("Sticker")]
    public StickerRarity stickerRarity;

    [Header("Booster")]
    [Tooltip("Optionaler Link zur bestehenden Boost-Definition (Icon/Beschreibung) — für die reine " +
             "Inventar-Zählung wird nur rewardId als Schlüssel benutzt, siehe BoosterInventoryManager.")]
    public BoostDefinition boosterDefinition;

    [Header("WorldUnlock")]
    [Tooltip("Eindeutige ID der Welt (z.B. \"TropicalJungle\") — siehe WorldUnlockManager.")]
    public string worldId;
    [Tooltip("Anzahl Freispiele, die beim Unlock zusätzlich zum dauerhaften Kaufangebot vergeben werden.")]
    public int freePlaysGranted;
}
