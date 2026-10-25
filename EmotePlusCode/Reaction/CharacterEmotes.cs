using MegaCrit.Sts2.Core.Models;
namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>A character model and its independently categorized artwork.</summary>
public sealed record CharacterEmoteDefinition(string TypeName, EmoteType Emote, string AssetName);

/// <summary>Official character lookup without constructing models or referencing individual classes.</summary>
public static class CharacterEmotes
{
    /// <summary>Public character lookup API version.</summary>
    public const int ApiVersion = 1;
    private static readonly CharacterEmoteDefinition[] Definitions =
    [
        new("Defect", EmoteType.CharacterDefect, "defect"),
        new("Ironclad", EmoteType.CharacterIronclad, "ironclad"),
        new("Necrobinder", EmoteType.CharacterNecrobinder, "necrobinder"),
        new("Regent", EmoteType.CharacterRegent, "regent"),
        new("Silent", EmoteType.CharacterSilent, "silent"),
    ];
    private static readonly IReadOnlyDictionary<string, CharacterEmoteDefinition> ByType =
        Definitions.ToDictionary(d => d.TypeName, StringComparer.Ordinal);
    /// <summary>All supported characters.</summary>
    public static IReadOnlyList<CharacterEmoteDefinition> All { get; } = Array.AsReadOnly(Definitions);
    /// <summary>Maps registered content or exact official types; null, unknown and unregistered types return false/None.</summary>
    public static bool TryGetEmote(Type? type, out EmoteType emote)
    {
        if (ModdedEmotes.TryGetEmote(type, EmoteCategory.Character, out emote)) return true;
        if (type == null || type.Assembly != typeof(CharacterModel).Assembly ||
            type.Namespace != "MegaCrit.Sts2.Core.Models.Characters" || !typeof(CharacterModel).IsAssignableFrom(type) ||
            !ByType.TryGetValue(type.Name, out var entry)) return false;
        emote = entry.Emote;
        return true;
    }
    /// <summary>Maps an existing character instance without mutation.</summary>
    public static bool TryGetEmote(CharacterModel? model, out EmoteType emote) => TryGetEmote(model?.GetType(), out emote);
}
