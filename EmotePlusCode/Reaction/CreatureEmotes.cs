using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Artwork may represent multiple monster parts and their shared encounter.</summary>
public sealed record CreatureEmoteDefinition(IReadOnlyList<string> TypeNames, string? EncounterTypeName,
    EmoteType Emote, string AssetName, bool Unused, bool TrackActivity = true);

/// <summary>Official monster/encounter lookup with stable artwork IDs, including retired artwork.</summary>
public static class CreatureEmotes
{
    /// <summary>Public creature lookup API version.</summary>
    public const int ApiVersion = 1;
    private static readonly CreatureEmoteDefinition[] Definitions =
    [
        new(Array.AsReadOnly<string>(["Aeonglass"]), "AeonglassBoss", EmoteType.CreatureAeonglass, "aeonglass", false),
        new(Array.AsReadOnly<string>(["CeremonialBeast"]), "CeremonialBeastBoss", EmoteType.CreatureCeremonialBeast, "ceremonial_beast", false),
        new(Array.AsReadOnly<string>(["Doormaker"]), "DoormakerBoss", EmoteType.CreatureDoormaker, "doormaker", true),
        new(Array.AsReadOnly<string>(["Crusher", "Rocket"]), "KaiserCrabBoss", EmoteType.CreatureKaiserCrab, "kaiser_crab", false),
        new(Array.AsReadOnly<string>(["KnowledgeDemon"]), "KnowledgeDemonBoss", EmoteType.CreatureKnowledgeDemon, "knowledge_demon", false),
        new(Array.AsReadOnly<string>(["LagavulinMatriarch"]), "LagavulinMatriarchBoss", EmoteType.CreatureLagavulinMatriarch, "lagavulin_matriarch", false),
        new(Array.AsReadOnly<string>([]), null, EmoteType.CreatureMerchant, "merchant", false),
        new(Array.AsReadOnly<string>(["PaelsLegion"]), null, EmoteType.CreaturePaelsRegion, "paels_region", false, false),
        new(Array.AsReadOnly<string>(["Queen"]), "QueenBoss", EmoteType.CreatureQueen, "queen", false),
        new(Array.AsReadOnly<string>(["SoulFysh"]), "SoulFyshBoss", EmoteType.CreatureSoulFysh, "soul_fysh", false),
        new(Array.AsReadOnly<string>(["TestSubject"]), "TestSubjectBoss", EmoteType.CreatureTestSubject, "test_subject", false),
        new(Array.AsReadOnly<string>(["TheInsatiable"]), "TheInsatiableBoss", EmoteType.CreatureTheInsatiable, "the_insatiable", false),
        new(Array.AsReadOnly<string>(["KinPriest", "KinFollower"]), "TheKinBoss", EmoteType.CreatureTheKin, "the_kin", false),
        new(Array.AsReadOnly<string>(["Vantom"]), "VantomBoss", EmoteType.CreatureVantom, "vantom", false),
        new(Array.AsReadOnly<string>(["WaterfallGiant"]), "WaterfallGiantBoss", EmoteType.CreatureWaterfallGiant, "waterfall_giant", false),
    ];
    private static readonly IReadOnlyDictionary<string, CreatureEmoteDefinition> ByMonster = Definitions
        .SelectMany(d => d.TypeNames.Select(name => (name, definition: d)))
        .ToDictionary(p => p.name, p => p.definition, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, CreatureEmoteDefinition> ByEncounter =
        Definitions.Where(d => d.EncounterTypeName != null).ToDictionary(d => d.EncounterTypeName!, StringComparer.Ordinal);
    private static readonly HashSet<EmoteType> ActivityExcluded = Definitions.Where(d => !d.TrackActivity).Select(d => d.Emote).ToHashSet();

    internal static bool TracksActivity(EmoteType type) => !ActivityExcluded.Contains(type);
    /// <summary>All artwork. Doormaker is explicitly marked unused even if an older model type is supplied.</summary>
    public static IReadOnlyList<CreatureEmoteDefinition> All { get; } = Array.AsReadOnly(Definitions);
    /// <summary>Maps an exact official monster or encounter type; no static dependency on retired classes.</summary>
    public static bool TryGetEmote(Type? type, out EmoteType emote)
    {
        emote = EmoteType.None;
        if (type == null || type.Assembly != typeof(MonsterModel).Assembly) return false;
        CreatureEmoteDefinition? entry;
        if (type.Namespace == "MegaCrit.Sts2.Core.Models.Monsters" && typeof(MonsterModel).IsAssignableFrom(type))
            ByMonster.TryGetValue(type.Name, out entry);
        else if (type.Namespace == "MegaCrit.Sts2.Core.Models.Encounters" && typeof(EncounterModel).IsAssignableFrom(type))
            ByEncounter.TryGetValue(type.Name, out entry);
        else return false;
        if (entry == null) return false;
        emote = entry.Emote;
        return true;
    }
    /// <summary>Maps an existing monster model.</summary>
    public static bool TryGetEmote(MonsterModel? model, out EmoteType emote) => TryGetEmote(model?.GetType(), out emote);
    /// <summary>Maps an existing encounter, including multipart bosses.</summary>
    public static bool TryGetEmote(EncounterModel? model, out EmoteType emote) => TryGetEmote(model?.GetType(), out emote);
    /// <summary>Maps a battle entity to monster artwork, or character artwork for a player.</summary>
    public static bool TryGetEmote(Creature? creature, out EmoteType emote)
    {
        if (creature?.Player is { } player) return CharacterEmotes.TryGetEmote(player.Character, out emote);
        return TryGetEmote(creature?.Monster, out emote);
    }
}
