namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Exclusive artwork directory classification, independent of the non-exclusive search tags.</summary>
public enum EmoteCategory
{
    None = 0,
    Relic = 1,
    Power = 2,
    Ancient = 3,
    Enchantment = 4,
    Potion = 5,
    Character = 6,
    Creature = 7,
    Meme = 8,
}

/// <summary>A stable emote identity, persistent settings key and its separately classified artwork.</summary>
public sealed record EmoteDefinition(EmoteType Type, string Name, string AssetName, EmoteCategory Category)
{
    /// <summary>Whether the artwork represents base-game content, independently of its tags or directory.</summary>
    public bool IsVanillaContent { get; init; }
}
