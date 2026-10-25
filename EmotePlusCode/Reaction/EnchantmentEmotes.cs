using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Enchantment artwork and its optional official model association.</summary>
public sealed record EnchantmentEmoteDefinition(string? TypeName, EmoteType Emote, string AssetName, bool Unused);

/// <summary>Optional-model lookup without statically referencing individual enchantments.</summary>
public static class EnchantmentEmotes
{
    /// <summary>Public API version for enchantment lookup.</summary>
    public const int ApiVersion = 1;

    // IDs are permanent. Missing models are still usable as artwork-only emotes.
    private static readonly EnchantmentEmoteDefinition[] Definitions =
    [
        new("Adroit", EmoteType.EnchantmentAdroit, "adroit", false),
        new("Clone", EmoteType.EnchantmentClone, "clone", false),
        new("Corrupted", EmoteType.EnchantmentCorrupted, "corrupted", false),
        new("Glam", EmoteType.EnchantmentGlam, "glam", false),
        new("Goopy", EmoteType.EnchantmentGoopy, "goopy", false),
        new("Imbued", EmoteType.EnchantmentImbued, "imbued", false),
        new("Inky", EmoteType.EnchantmentInky, "inky", false),
        new("Instinct", EmoteType.EnchantmentInstinct, "instinct", false),
        new("Momentum", EmoteType.EnchantmentMomentum, "momentum", false),
        new("Nimble", EmoteType.EnchantmentNimble, "nimble", false),
        new("PerfectFit", EmoteType.EnchantmentPerfectFit, "perfect_fit", false),
        new("RoyallyApproved", EmoteType.EnchantmentRoyallyApproved, "royally_approved", false),
        new("Sharp", EmoteType.EnchantmentSharp, "sharp", false),
        new("Slither", EmoteType.EnchantmentSlither, "slither", false),
        new("SlumberingEssence", EmoteType.EnchantmentSlumberingEssence, "slumbering_essence", false),
        new("SoulsPower", EmoteType.EnchantmentSoulsPower, "souls_power", false),
        new("Sown", EmoteType.EnchantmentSown, "sown", false),
        new("Spiral", EmoteType.EnchantmentSpiral, "spiral", false),
        new("Steady", EmoteType.EnchantmentSteady, "steady", false),
        new("Swift", EmoteType.EnchantmentSwift, "swift", false),
        new("TezcatarasEmber", EmoteType.EnchantmentTezcatarasEmber, "tezcataras_ember", false),
        new("Vigorous", EmoteType.EnchantmentVigorous, "vigorous", false),
    ];
    private static readonly IReadOnlyDictionary<string, EnchantmentEmoteDefinition> ByType = Definitions
        .Where(d => d.TypeName != null).ToDictionary(d => d.TypeName!, StringComparer.Ordinal);

    /// <summary>All registered artwork, including entries without a model in either inspected branch.</summary>
    public static IReadOnlyList<EnchantmentEmoteDefinition> All { get; } = Array.AsReadOnly(Definitions);

    /// <summary>Maps a registered content or exact official model type; unknown, null and unregistered types return false/None.</summary>
    public static bool TryGetEmote(Type? modelType, out EmoteType emote)
    {
        if (ModdedEmotes.TryGetEmote(modelType, EmoteCategory.Enchantment, out emote)) return true;
        if (modelType == null || modelType.Assembly != typeof(EnchantmentModel).Assembly ||
            modelType.Namespace != "MegaCrit.Sts2.Core.Models.Enchantments" ||
            !typeof(EnchantmentModel).IsAssignableFrom(modelType) || !ByType.TryGetValue(modelType.Name, out var entry))
            return false;
        emote = entry.Emote;
        return true;
    }

    /// <summary>Maps an existing model without creating, modifying or loading any model resources.</summary>
    public static bool TryGetEmote(EnchantmentModel? model, out EmoteType emote) => TryGetEmote(model?.GetType(), out emote);
}
