namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Category allocation shared by the base catalogue and each 10,000-ID content group.</summary>
public static class EmoteIdLayout
{
    /// <summary>Tests the category block; zero is never a valid emote.</summary>
    public static bool MatchesCategory(int id, EmoteCategory category)
    {
        var block = category switch
        {
            EmoteCategory.None => 0,
            EmoteCategory.Power => 1,
            EmoteCategory.Relic => 2,
            EmoteCategory.Enchantment => 3,
            EmoteCategory.Potion => 4,
            EmoteCategory.Character => 5,
            EmoteCategory.Creature => 6,
            EmoteCategory.Ancient => 7,
            EmoteCategory.Modifier => 8,
            EmoteCategory.Meme => 9,
            _ => -1,
        };
        return id > 0 && id % 10000 / 1000 == block;
    }
}
