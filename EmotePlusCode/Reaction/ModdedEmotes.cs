using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>A fixed optional-mod model mapping. Model identities never depend on runtime registration order.</summary>
public sealed record ModdedEmoteDefinition(int Id, string Name, string AssetName, EmoteCategory Category,
    string Assembly, string TypeName, string Title, string[] Aliases, string? LocTable, string? LocKey,
    string[]? Tags = null, bool CharacterCookie = false)
{
    /// <summary>Source Workshop item; required when the group spans multiple Workshop items.</summary>
    public string? WorkshopId { get; init; }
}

/// <summary>An installable content group; multiple manifest IDs can identify bundled characters.</summary>
public sealed record ContentModDefinition(string Key, string[] ModIds, string Name, string? WorkshopId,
    string[] Aliases, ModdedEmoteDefinition[] Entries)
{
    /// <summary>Additional Workshop sources; the existing singular source remains supported.</summary>
    public string[] WorkshopIds { get; init; } = [];

    /// <summary>Distinct source items, including the optional singular source.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> AllWorkshopIds => (WorkshopIds ?? [])
        .Concat(string.IsNullOrWhiteSpace(WorkshopId) ? [] : new[] { WorkshopId })
        .Distinct(StringComparer.Ordinal).ToArray();
}

/// <summary>Optional content support, without loading assemblies or constructing third-party models.</summary>
public static class ModdedEmotes
{
    /// <summary>Public content lookup API version.</summary>
    public const int ApiVersion = 2;
    private static readonly IReadOnlyDictionary<EmoteCategory, Type> BaseTypes = new Dictionary<EmoteCategory, Type>
    {
        [EmoteCategory.Character] = typeof(CharacterModel), [EmoteCategory.Creature] = typeof(MonsterModel),
        [EmoteCategory.Relic] = typeof(RelicModel), [EmoteCategory.Power] = typeof(PowerModel),
        [EmoteCategory.Potion] = typeof(PotionModel), [EmoteCategory.Enchantment] = typeof(EnchantmentModel),
        [EmoteCategory.Ancient] = typeof(AncientEventModel),
    };

    /// <summary>All declared groups, including ones not installed locally.</summary>
    public static IReadOnlyList<ContentModDefinition> Groups { get; } = Array.AsReadOnly(Load());
    private static readonly Dictionary<EmoteType, (ContentModDefinition Group, ModdedEmoteDefinition Entry)> Entries =
        Groups.SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .ToDictionary(p => (EmoteType)p.Entry.Id);
    private static Dictionary<EmoteType, Type>? _available;
    private static Dictionary<EmoteType, Type>? _mappingSource;
    private static Dictionary<(Type, EmoteCategory), EmoteType> _byModel = [];
    private static Dictionary<Type, EmoteType> _byCookieCharacter = [];

    private static ContentModDefinition[] Load()
    {
        var assembly = typeof(ModdedEmotes).Assembly;
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var groups = assembly.GetManifestResourceNames().Where(n => n.Contains(".ContentMods.") && n.EndsWith(".json"))
            .Order(StringComparer.Ordinal).Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                return JsonSerializer.Deserialize<ContentModDefinition>(stream, options)
                    ?? throw new InvalidDataException($"Empty content catalogue: {name}");
            }).ToArray();
        foreach (var group in groups)
        {
            if (group.ModIds.Length == 0 || string.IsNullOrWhiteSpace(group.Key))
                throw new InvalidDataException("Content groups require a key and manifest IDs.");
            var workshopIds = group.AllWorkshopIds;
            if (workshopIds.Count == 0 || workshopIds.Any(id => !ulong.TryParse(id, out var value) || value == 0 || id.Any(c => c is < '0' or > '9')))
                throw new InvalidDataException($"Invalid Workshop sources: {group.Key}");
            foreach (var entry in group.Entries)
                if (entry.Id < 10000 || !Enum.IsDefined((EmoteType)entry.Id) ||
                    !EmoteIdLayout.MatchesCategory(entry.Id, entry.Category) ||
                    !entry.AssetName.StartsWith("mods/" + group.Key + "/", StringComparison.Ordinal) ||
                    entry.AssetName.Contains("..") || entry.AssetName.Contains('\\') ||
                    !BaseTypes.ContainsKey(entry.Category) || entry.CharacterCookie && entry.Category != EmoteCategory.Relic ||
                    (entry.WorkshopId != null && !workshopIds.Contains(entry.WorkshopId, StringComparer.Ordinal)) ||
                    (workshopIds.Count > 1 && entry.WorkshopId == null))
                    throw new InvalidDataException($"Invalid content entry: {group.Key}/{entry.Name}");
            foreach (var character in group.Entries.Where(e => e.Category == EmoteCategory.Character))
                if (!group.Entries.Any(e => e.CharacterCookie && e.Assembly == character.Assembly && e.TypeName == character.TypeName))
                    throw new InvalidDataException($"Character requires cookie artwork: {group.Key}/{character.Name}");
        }
        return groups;
    }

    private static Dictionary<EmoteType, Type> Available()
    {
        if (_available != null) return _available;
        var available = new Dictionary<EmoteType, Type>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var group in Groups)
        {
            if (!ModManager.Mods.Any(m => m.state == ModLoadState.Loaded && m.manifest?.affectsGameplay == true &&
                    group.ModIds.Contains(m.manifest.id, StringComparer.Ordinal))) continue;
            foreach (var entry in group.Entries)
            {
                var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == entry.Assembly);
                Type? type;
                try { type = assembly?.GetType(entry.TypeName, throwOnError: false); }
                catch (Exception e) when (e is TypeLoadException or FileNotFoundException or FileLoadException) { continue; }
                if (type != null && !type.IsAbstract && (entry.CharacterCookie ? typeof(CharacterModel) : BaseTypes[entry.Category]).IsAssignableFrom(type))
                    available.Add((EmoteType)entry.Id, type);
            }
        }
        // Mod order is not fixed: do not freeze a negative result during another mod's initializer.
        if (ModManager.Mods.Count > 0 && ModManager.Mods.All(m => m.state != ModLoadState.None)) _available = available;
        return available;
    }

    /// <summary>Whether the required content and exact model type are loaded; official emotes are always available.</summary>
    public static bool IsAvailable(EmoteType type) => type != EmoteType.None && Enum.IsDefined(type) &&
        (!Entries.ContainsKey(type) || Available().ContainsKey(type));

    /// <summary>Maps an exact loaded third-party type in the requested category.</summary>
    public static bool TryGetEmote(Type? type, EmoteCategory category, out EmoteType emote)
    {
        emote = EmoteType.None;
        if (type == null || type.Assembly == typeof(AbstractModel).Assembly) return false;
        RefreshMappings();
        return _byModel.TryGetValue((type, category), out emote);
    }

    private static void RefreshMappings()
    {
        var available = Available();
        if (!ReferenceEquals(available, _mappingSource))
        {
            _byModel = available.Where(p => !Entries[p.Key].Entry.CharacterCookie)
                .ToDictionary(p => (p.Value, Entries[p.Key].Entry.Category), p => p.Key);
            _byCookieCharacter = available.Where(p => Entries[p.Key].Entry.CharacterCookie)
                .ToDictionary(p => p.Value, p => p.Key);
            _mappingSource = available;
        }
    }

    /// <summary>Maps a supported mod character to its own YummyCookie artwork without defaulting to another character.</summary>
    public static bool TryGetCookieEmote(CharacterModel? character, out EmoteType emote)
    {
        emote = EmoteType.None;
        if (character == null) return false;
        RefreshMappings();
        return _byCookieCharacter.TryGetValue(character.GetType(), out emote);
    }

    internal static bool TryDescribe(EmoteType type, out ContentModDefinition group, out ModdedEmoteDefinition entry)
    {
        if (Entries.TryGetValue(type, out var pair)) { group = pair.Group; entry = pair.Entry; return true; }
        group = null!; entry = null!; return false;
    }
}
