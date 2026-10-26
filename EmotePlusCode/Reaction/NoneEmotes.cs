namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Base-game emotes whose artwork has no content category or model binding.</summary>
public static class NoneEmotes
{
    /// <summary>Public catalogue API version.</summary>
    public const int ApiVersion = 1;

    /// <summary>Stable identities, settings names and artwork for uncategorized base-game emotes.</summary>
    public static IReadOnlyList<EmoteDefinition> All { get; } = Array.AsReadOnly<EmoteDefinition>(
    [
        new(EmoteType.Exclamation, "exclaim", "exclaim", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.Skull, "skull", "skull", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.ThumbDown, "thumb_down", "thumb_down", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.SadSlime, "slime_sad", "slime_sad", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.QuestionMark, "question", "question", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.Heart, "heart", "heart", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.ThumbUp, "thumb_up", "thumb_up", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.HappyCultist, "happy_cultist", "happy_cultist", EmoteCategory.None) { IsVanillaContent = true },
        new(EmoteType.Gold, "gold", "gold", EmoteCategory.None) { IsVanillaContent = true },
    ]);
}
