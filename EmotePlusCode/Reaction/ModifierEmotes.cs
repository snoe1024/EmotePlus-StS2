using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>A modifier model and its fixed artwork identity.</summary>
public sealed record ModifierEmoteDefinition(string? TypeName, EmoteType Emote, string AssetName, bool Unused = false);

/// <summary>Modifier lookup without constructing models or loading optional assemblies.</summary>
public static class ModifierEmotes
{
    /// <summary>Public modifier lookup API version.</summary>
    public const int ApiVersion = 1;
    /// <summary>Official modifiers with supplied emote artwork.</summary>
    public static IReadOnlyList<ModifierEmoteDefinition> All { get; } = Array.AsReadOnly<ModifierEmoteDefinition>(
    [
        new("AllStar", EmoteType.ModifierAllStar, "all_star"),
        new("BigGameHunter", EmoteType.ModifierBigGameHunter, "big_game_hunter"),
        new(null, EmoteType.ModifierBinary, "binary", true),
        new("CharacterCards", EmoteType.ModifierCharacterCards, "character_cards"),
        new("CursedRun", EmoteType.ModifierCursedRun, "cursed_run"),
        new("DeadlyEvents", EmoteType.ModifierDeadlyEvents, "deadly_events"),
        new("Draft", EmoteType.ModifierDraft, "draft"),
        new("Flight", EmoteType.ModifierFlight, "flight"),
        new("Hoarder", EmoteType.ModifierHoarder, "hoarder"),
        new("Insanity", EmoteType.ModifierInsanity, "insanity"),
        new("Midas", EmoteType.ModifierMidas, "midas"),
        new("Murderous", EmoteType.ModifierMurderous, "murderous"),
        new("NightTerrors", EmoteType.ModifierNightTerrors, "night_terrors"),
        new(null, EmoteType.ModifierPrismatic, "prismatic", true),
        new("SealedDeck", EmoteType.ModifierSealedDeck, "sealed_deck"),
        new("Specialized", EmoteType.ModifierSpecialized, "specialized"),
        new("Terminal", EmoteType.ModifierTerminal, "terminal"),
        new("Vintage", EmoteType.ModifierVintage, "vintage"),
    ]);

    /// <summary>Maps exact registered modifier types; unknown and unavailable models return false/None.</summary>
    public static bool TryGetEmote(Type? type, out EmoteType emote)
    {
        if (ModdedEmotes.TryGetEmote(type, EmoteCategory.Modifier, out emote)) return true;
        emote = EmoteType.None;
        if (type == null || type.Assembly != typeof(ModifierModel).Assembly ||
            type.Namespace != "MegaCrit.Sts2.Core.Models.Modifiers" || !typeof(ModifierModel).IsAssignableFrom(type)) return false;
        var entry = All.FirstOrDefault(e => e.TypeName == type.Name);
        if (entry == null) return false;
        emote = entry.Emote;
        return true;
    }

    /// <summary>Maps an existing modifier instance.</summary>
    public static bool TryGetEmote(ModifierModel? model, out EmoteType emote) => TryGetEmote(model?.GetType(), out emote);
}
