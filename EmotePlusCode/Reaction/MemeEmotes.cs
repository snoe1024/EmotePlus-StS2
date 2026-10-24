namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Custom artwork catalogue; every entry automatically receives the Meme search tag.</summary>
public static class MemeEmotes
{
    /// <summary>Definitions in their default grid order: ID, settings key, filename without extension, category.</summary>
    public static IReadOnlyList<EmoteDefinition> All { get; } = Array.AsReadOnly<EmoteDefinition>(
    [
        new(EmoteType.RIP, "blasphemyrip", "blasphemyrip", EmoteCategory.Meme),
        new(EmoteType.MemeBuffSts1, "buff_sts1_meme", "buff_sts1", EmoteCategory.Meme),
        new(EmoteType.Devotion, "devotion", "devotion", EmoteCategory.Meme),
        new(EmoteType.MemeThonking, "thonking_meme", "thonking", EmoteCategory.Meme),
    ]);
}
