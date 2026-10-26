using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>An official ancient's model name and corresponding existing emote.</summary>
public sealed record AncientEmoteDefinition(string TypeName, EmoteType Emote)
{
    /// <summary>Artwork and persistent settings name, independently of the model name.</summary>
    public string AssetName { get; init; } = TypeName.ToLowerInvariant();
}

/// <summary>Ancient lookup without a static dependency on individual ancient classes.</summary>
public static class AncientEmotes
{
    /// <summary>Public API version for ancient lookup.</summary>
    public const int ApiVersion = 1;

    private static readonly AncientEmoteDefinition[] Definitions =
    [
        new("Darv", EmoteType.AncientDarv),
        new("Neow", EmoteType.AncientNeow),
        new("Nonupeipe", EmoteType.AncientNonupeipe),
        new("Orobas", EmoteType.AncientOrobas),
        new("Pael", EmoteType.AncientPael),
        new("Tanx", EmoteType.AncientTanx),
        new("Tezcatara", EmoteType.AncientTezcatara),
        new("Vakuu", EmoteType.AncientVakuu),
    ];
    private static readonly IReadOnlyDictionary<string, AncientEmoteDefinition> ByType =
        Definitions.ToDictionary(d => d.TypeName, StringComparer.Ordinal);

    /// <summary>The eight supported ancients; unknown future ancients are not inferred by name alone.</summary>
    public static IReadOnlyList<AncientEmoteDefinition> All { get; } = Array.AsReadOnly(Definitions);

    /// <summary>Maps a registered content or exact official ancient model type. Unknown and unregistered third-party models return false/None.</summary>
    public static bool TryGetEmote(Type? ancientType, out EmoteType emote)
    {
        if (ModdedEmotes.TryGetEmote(ancientType, EmoteCategory.Ancient, out emote)) return true;
        if (ancientType == null || ancientType.Assembly != typeof(AncientEventModel).Assembly ||
            ancientType.Namespace != "MegaCrit.Sts2.Core.Models.Events" ||
            !typeof(AncientEventModel).IsAssignableFrom(ancientType) ||
            !ByType.TryGetValue(ancientType.Name, out var entry)) return false;
        emote = entry.Emote;
        return true;
    }

    /// <summary>Maps an ancient instance without constructing or mutating any game model.</summary>
    public static bool TryGetEmote(AncientEventModel? ancient, out EmoteType emote) =>
        TryGetEmote(ancient?.GetType(), out emote);
}
