using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>An additional exact model sharing an entry's artwork, optionally from another source assembly.</summary>
public sealed record EmoteModelAlias(string Assembly, string TypeName)
{
    /// <summary>Optional source override; otherwise the entry's Workshop source is inherited.</summary>
    public string? WorkshopId { get; init; }
}

/// <summary>A fixed optional-mod model mapping. Model identities never depend on runtime registration order.</summary>
public sealed record ModdedEmoteDefinition(int Id, string Name, string AssetName, EmoteCategory Category,
    string Assembly, string TypeName, string Title, string[] Aliases, string? LocTable, string? LocKey,
    string[]? Tags = null, bool CharacterCookie = false)
{
    /// <summary>Source Workshop item; required when the group spans multiple Workshop items.</summary>
    public string? WorkshopId { get; init; }

    /// <summary>Additional exact model names in the same assembly sharing this fixed artwork.</summary>
    public string[] TypeAliases { get; init; } = [];

    /// <summary>Additional exact types from explicitly named assemblies sharing the same artwork.</summary>
    public EmoteModelAlias[] ModelAliases { get; init; } = [];

    /// <summary>All exact model identities, including the primary type and same-assembly aliases.</summary>
    [JsonIgnore]
    public IReadOnlyList<EmoteModelAlias> AllModels => AllTypeNames.Select(n => new EmoteModelAlias(Assembly, n)
        { WorkshopId = WorkshopId }).Concat(ModelAliases ?? []).ToArray();

    /// <summary>Explicit reuse of an official YummyCookie emote for this character; no new cookie artwork is registered.</summary>
    public int? CookieEmoteId { get; init; }

    /// <summary>Model lookup category, independent of uncategorized artwork. Omitted values use Category.</summary>
    public EmoteCategory? BindingCategory { get; init; }

    /// <summary>The category used to validate model bases and dispatch activity lookups.</summary>
    [JsonIgnore]
    public EmoteCategory ResolvedBindingCategory => BindingCategory ?? Category;

    /// <summary>Primary and additional model names; these are separate from human search aliases.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> AllTypeNames => new[] { TypeName }.Concat(TypeAliases ?? []).ToArray();
}

/// <summary>An exact third-party model reusing existing official artwork without adding a catalogue entry.</summary>
public sealed record FixedEmoteRoute(int TargetId, EmoteCategory Category, string Assembly, string TypeName)
{
    /// <summary>Source Workshop item; required for a multi-source content group.</summary>
    public string? WorkshopId { get; init; }

    /// <summary>Source model category when it differs from the official artwork category.</summary>
    public EmoteCategory? BindingCategory { get; init; }

    /// <summary>Model category used for exact type validation and lookup.</summary>
    [JsonIgnore]
    public EmoteCategory ResolvedBindingCategory => BindingCategory ?? Category;

}

/// <summary>An installable content group; multiple manifest IDs can identify bundled characters.</summary>
public sealed record ContentModDefinition(string Key, string[] ModIds, string Name, string? WorkshopId,
    string[] Aliases, ModdedEmoteDefinition[] Entries)
{
    /// <summary>Exact optional-model routes to existing official IDs; no duplicate artwork is registered.</summary>
    public FixedEmoteRoute[] FixedRoutes { get; init; } = [];

    /// <summary>Settings localization prefix for this stable group key.</summary>
    [JsonIgnore]
    public string LocalizationKey => "EMOTEPLUS-CONTENT_MOD_" + Key.ToUpperInvariant();

    /// <summary>Localized display name, falling back to the catalogue name when no translation exists.</summary>
    [JsonIgnore]
    public string LocalizedName => Localized(".title", Name);

    /// <summary>Additional current-language search aliases; invariant aliases remain searchable separately.</summary>
    [JsonIgnore]
    public string LocalizedAliases => Localized(".aliases", "");

    private string Localized(string suffix, string fallback) => LocString.Exists("settings_ui", LocalizationKey + suffix)
        ? new LocString("settings_ui", LocalizationKey + suffix).GetFormattedText() : fallback;

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
    public const int ApiVersion = 7;
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
    private static Dictionary<EmoteType, Type[]>? _available;
    private static Dictionary<EmoteType, Type[]>? _mappingSource;
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
        var modelMappings = new HashSet<(string, string, EmoteCategory, bool)>();
        foreach (var group in groups)
        {
            if (group.ModIds.Length == 0 || string.IsNullOrWhiteSpace(group.Key))
                throw new InvalidDataException("Content groups require a key and manifest IDs.");
            var workshopIds = group.AllWorkshopIds;
            if (workshopIds.Count == 0 || workshopIds.Any(id => !ulong.TryParse(id, out var value) || value == 0 || id.Any(c => c is < '0' or > '9')))
                throw new InvalidDataException($"Invalid Workshop sources: {group.Key}");
            foreach (var entry in group.Entries)
            {
                if (entry.Id < 10000 || !Enum.IsDefined((EmoteType)entry.Id) ||
                    !EmoteIdLayout.MatchesCategory(entry.Id, entry.Category) ||
                    !entry.AssetName.StartsWith("mods/" + group.Key + "/", StringComparison.Ordinal) ||
                    entry.AssetName.Contains("..") || entry.AssetName.Contains('\\') ||
                    (entry.Category != EmoteCategory.None && !BaseTypes.ContainsKey(entry.Category)) ||
                    !BaseTypes.ContainsKey(entry.ResolvedBindingCategory) ||
                    (entry.Category != EmoteCategory.None && entry.ResolvedBindingCategory != entry.Category) ||
                    (entry.ResolvedBindingCategory == EmoteCategory.Character && entry.Category != EmoteCategory.Character) ||
                    entry.CharacterCookie && entry.Category != EmoteCategory.Relic ||
                    (entry.WorkshopId != null && !workshopIds.Contains(entry.WorkshopId, StringComparer.Ordinal)) ||
                    (workshopIds.Count > 1 && entry.WorkshopId == null))
                    throw new InvalidDataException($"Invalid content entry: {group.Key}/{entry.Name}");
                if (((entry.TypeAliases?.Length ?? 0) > 0 || (entry.ModelAliases?.Length ?? 0) > 0) && (entry.CharacterCookie || entry.Category == EmoteCategory.Character))
                    throw new InvalidDataException($"Character/cookie aliases require separate artwork definitions: {entry.Name}");
                if (entry.CookieEmoteId is { } cookie && (entry.Category != EmoteCategory.Character || !IsOfficialCookie((EmoteType)cookie)))
                    throw new InvalidDataException($"Invalid cookie reuse target: {entry.Name}");
                foreach (var model in entry.AllModels)
                    if (string.IsNullOrWhiteSpace(model.Assembly) || string.IsNullOrWhiteSpace(model.TypeName) ||
                        (model.WorkshopId != null && !workshopIds.Contains(model.WorkshopId, StringComparer.Ordinal)) ||
                        !modelMappings.Add((model.Assembly, model.TypeName, entry.ResolvedBindingCategory, entry.CharacterCookie)))
                        throw new InvalidDataException($"Duplicate or invalid model mapping: {entry.Name}/{model.TypeName}");
            }
            foreach (var route in group.FixedRoutes ?? [])
            {
                if (!IsOfficialRouteTarget(route) || !BaseTypes.ContainsKey(route.ResolvedBindingCategory) ||
                    route.ResolvedBindingCategory == EmoteCategory.Character || string.IsNullOrWhiteSpace(route.Assembly) ||
                    string.IsNullOrWhiteSpace(route.TypeName) ||
                    (route.WorkshopId != null && !workshopIds.Contains(route.WorkshopId, StringComparer.Ordinal)) ||
                    (workshopIds.Count > 1 && route.WorkshopId == null) ||
                    !modelMappings.Add((route.Assembly, route.TypeName, route.ResolvedBindingCategory, false)))
                    throw new InvalidDataException($"Invalid or duplicate fixed route: {group.Key}/{route.TypeName}");
            }
            foreach (var character in group.Entries.Where(e => e.Category == EmoteCategory.Character))
            {
                var dedicated = group.Entries.Any(e => e.CharacterCookie && e.Assembly == character.Assembly && e.TypeName == character.TypeName);
                if (dedicated == character.CookieEmoteId.HasValue)
                    throw new InvalidDataException($"Character requires exactly one cookie definition or reuse route: {group.Key}/{character.Name}");
            }
        }
        return groups;
    }

    private static Dictionary<EmoteType, Type[]> Available()
    {
        if (_available != null) return _available;
        var available = new Dictionary<EmoteType, Type[]>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var group in Groups)
        {
            if (!ModManager.Mods.Any(m => m.state == ModLoadState.Loaded && m.manifest?.affectsGameplay == true &&
                    group.ModIds.Contains(m.manifest.id, StringComparer.Ordinal))) continue;
            foreach (var entry in group.Entries)
            {
                var types = new List<Type>();
                foreach (var model in entry.AllModels)
                {
                    var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == model.Assembly);
                    Type? type;
                    try { type = assembly?.GetType(model.TypeName, throwOnError: false); }
                    catch (Exception e) when (e is TypeLoadException or FileNotFoundException or FileLoadException) { continue; }
                    if (type != null && type.Assembly == assembly && !type.IsAbstract &&
                        MatchesModelBase(type, entry.CharacterCookie ? EmoteCategory.Character : entry.ResolvedBindingCategory))
                        types.Add(type);
                }
                if (types.Count > 0) available.Add((EmoteType)entry.Id, types.ToArray());
            }
        }
        // Mod order is not fixed: do not freeze a negative result during another mod's initializer.
        if (ModManager.Mods.Count > 0 && ModManager.Mods.All(m => m.state != ModLoadState.None)) _available = available;
        return available;
    }

    /// <summary>Whether the required content and exact model type are loaded; official emotes are always available.</summary>
    public static bool IsAvailable(EmoteType type) => type != EmoteType.None && EmoteCatalog.IsRegistered(type) &&
        (!Entries.ContainsKey(type) || Available().ContainsKey(type));

    /// <summary>Maps an exact loaded third-party type in the requested model binding category.</summary>
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
                .SelectMany(p => p.Value.Select(t => (Type: t, Category: Entries[p.Key].Entry.ResolvedBindingCategory, Emote: p.Key)))
                .ToDictionary(p => (p.Type, p.Category), p => p.Emote);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var group in Groups.Where(IsGroupLoaded))
                foreach (var route in group.FixedRoutes ?? [])
                {
                    var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == route.Assembly);
                    Type? type;
                    try { type = assembly?.GetType(route.TypeName, throwOnError: false); }
                    catch (Exception e) when (e is TypeLoadException or FileNotFoundException or FileLoadException) { continue; }
                    if (type != null && type.Assembly == assembly && type.Assembly != typeof(AbstractModel).Assembly && !type.IsAbstract &&
                        MatchesModelBase(type, route.ResolvedBindingCategory))
                        _byModel.Add((type, route.ResolvedBindingCategory), (EmoteType)route.TargetId);
                }
            _byCookieCharacter = available.Where(p => Entries[p.Key].Entry.CharacterCookie)
                .Select(p => (Type: p.Value[0], Emote: p.Key))
                .Concat(available.Where(p => Entries[p.Key].Entry.CookieEmoteId.HasValue)
                    .Select(p => (Type: p.Value[0], Emote: (EmoteType)Entries[p.Key].Entry.CookieEmoteId!.Value)))
                .ToDictionary(p => p.Type, p => p.Emote);
            _mappingSource = available;
        }
    }

    // Creature artwork represents both live monster parts and the encounter shown in map/history headers.
    private static bool MatchesModelBase(Type type, EmoteCategory category) =>
        BaseTypes[category].IsAssignableFrom(type) ||
        category == EmoteCategory.Creature && typeof(EncounterModel).IsAssignableFrom(type);

    private static bool IsGroupLoaded(ContentModDefinition group) => ModManager.Mods.Any(m =>
        m.state == ModLoadState.Loaded && m.manifest?.affectsGameplay == true &&
        group.ModIds.Contains(m.manifest.id, StringComparer.Ordinal));

    // Use the official definition lists directly: EmoteCatalog itself depends on Groups during static initialization.
    private static bool IsOfficialRouteTarget(FixedEmoteRoute route)
    {
        if (route.TargetId <= 0 || route.TargetId >= 10000) return false;
        var target = (EmoteType)route.TargetId;
        return route.Category switch
        {
            EmoteCategory.Power => PowerEmotes.All.Any(e => e.Emote == target),
            EmoteCategory.Relic => RelicEmotes.All.Any(e => e.Emote == target),
            EmoteCategory.Potion => PotionEmotes.All.Any(e => e.Emote == target),
            EmoteCategory.Enchantment => EnchantmentEmotes.All.Any(e => e.Emote == target),
            EmoteCategory.Creature => CreatureEmotes.All.Any(e => e.Emote == target),
            EmoteCategory.Ancient => AncientEmotes.All.Any(e => e.Emote == target),
            _ => false,
        };
    }

    private static bool IsOfficialCookie(EmoteType type) => type is EmoteType.RelicYummyCookieDefect
        or EmoteType.RelicYummyCookieIronclad or EmoteType.RelicYummyCookieNecro
        or EmoteType.RelicYummyCookieRegent or EmoteType.RelicYummyCookieSilent;

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
