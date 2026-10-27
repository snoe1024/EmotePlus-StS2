namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Single catalogue for identity and artwork routing. Search tags do not decide resource paths.</summary>
public static class EmoteCatalog
{
    // Preserve the existing catalogue order without using placeholder definitions for the ancients.
    private static readonly EmoteType[] InitialCatalogueOrder =
    [
        EmoteType.Exclamation, EmoteType.Skull, EmoteType.ThumbDown, EmoteType.SadSlime,
        EmoteType.QuestionMark, EmoteType.Heart, EmoteType.ThumbUp, EmoteType.HappyCultist,
        EmoteType.AncientDarv, EmoteType.AncientNeow, EmoteType.AncientNonupeipe, EmoteType.AncientOrobas,
        EmoteType.AncientPael, EmoteType.AncientTanx, EmoteType.AncientTezcatara, EmoteType.AncientVakuu,
        EmoteType.Gold,
    ];

    /// <summary>Every emote exactly once, with stable settings names and explicit artwork categories.</summary>
    public static IReadOnlyList<EmoteDefinition> All { get; } = Array.AsReadOnly(Create());

    private static readonly IReadOnlyDictionary<EmoteType, EmoteDefinition> ByType = All.ToDictionary(d => d.Type);
    private static readonly IReadOnlyDictionary<string, EmoteDefinition> ByName = CreateNameIndex();

    /// <summary>Gets a definition by ID; unknown IDs use the question emote.</summary>
    public static EmoteDefinition Get(EmoteType type) => ByType.TryGetValue(type, out var definition)
        ? definition : ByType[EmoteType.QuestionMark];

    /// <summary>Whether the ID has a real definition rather than only an enum reservation.</summary>
    public static bool IsRegistered(EmoteType type) => ByType.ContainsKey(type);

    /// <summary>
    /// Resolves fixed artwork references without Godot or skin selection. Type and Name keep the requested
    /// identity; Category and AssetName identify the physical pair. Suitable for offline catalogue consumers.
    /// </summary>
    public static EmoteDefinition ResolveArtwork(EmoteType type)
    {
        var definition = Get(type);
        if (definition.ArtworkEmoteId is not { } sourceId) return definition;
        var source = Get((EmoteType)sourceId);
        return definition with { Category = source.Category, AssetName = source.AssetName, ArtworkEmoteId = null };
    }

    /// <summary>Looks up a saved key or an unambiguous short asset name in a single index.</summary>
    public static bool TryGet(string name, out EmoteDefinition definition) => ByName.TryGetValue(name, out definition!);

    private static EmoteDefinition[] Create()
    {
        var definitions = NoneEmotes.All.ToDictionary(d => d.Type);
        foreach (var ancient in AncientEmotes.All)
            definitions.Add(ancient.Emote, new(ancient.Emote, ancient.AssetName, ancient.AssetName, EmoteCategory.Ancient));
        foreach (var power in PowerEmotes.All)
        {
            definitions.Add(power.Emote, new(power.Emote, power.AssetName + "_power", power.AssetName, EmoteCategory.Power));
        }
        foreach (var relic in RelicEmotes.All)
            definitions.Add(relic.Emote, new(relic.Emote, relic.AssetName + "_relic", relic.AssetName, EmoteCategory.Relic));
        foreach (var enchantment in EnchantmentEmotes.All)
            definitions.Add(enchantment.Emote, new(enchantment.Emote, enchantment.AssetName + "_enchantment",
                enchantment.AssetName, EmoteCategory.Enchantment));
        foreach (var potion in PotionEmotes.All)
            definitions.Add(potion.Emote, new(potion.Emote, potion.AssetName + "_potion", potion.AssetName, EmoteCategory.Potion));
        foreach (var character in CharacterEmotes.All)
            definitions.Add(character.Emote, new(character.Emote, character.AssetName + "_character", character.AssetName, EmoteCategory.Character));
        foreach (var creature in CreatureEmotes.All)
            definitions.Add(creature.Emote, new(creature.Emote, creature.AssetName + "_creature", creature.AssetName, EmoteCategory.Creature));
        foreach (var modifier in ModifierEmotes.All)
            definitions.Add(modifier.Emote, new(modifier.Emote, modifier.AssetName + "_modifier", modifier.AssetName, EmoteCategory.Modifier));
        // Classify provenance before adding custom artwork and optional-mod definitions.
        foreach (var type in definitions.Keys.ToArray())
            definitions[type] = definitions[type] with { IsVanillaContent = true };
        foreach (var meme in MemeEmotes.All)
            definitions.Add(meme.Type, meme);
        foreach (var group in ModdedEmotes.Groups)
            foreach (var entry in group.Entries)
                definitions.Add((EmoteType)entry.Id, new((EmoteType)entry.Id, entry.Name, entry.AssetName, entry.Category)
                    { ArtworkEmoteId = entry.ArtworkEmoteId });
        if (definitions.Values.Where(d => !d.ArtworkEmoteId.HasValue).GroupBy(d => (d.Category, d.AssetName)).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Duplicate emote artwork: map model variants to a single emote ID.");
        foreach (var definition in definitions.Values)
            if (!EmoteIdLayout.MatchesCategory((int)definition.Type, definition.Category))
                throw new InvalidOperationException($"Emote ID outside category range: {definition.Name}");
        var initial = InitialCatalogueOrder.ToHashSet();
        return InitialCatalogueOrder.Select(t => definitions[t])
            .Concat(definitions.Values.Where(d => !initial.Contains(d.Type))).ToArray();
    }

    private static Dictionary<string, EmoteDefinition> CreateNameIndex()
    {
        var index = All.ToDictionary(d => d.Name, StringComparer.Ordinal);
        // Short aliases are safe only when every matching entry points at the same artwork.
        foreach (var group in All.GroupBy(d => d.AssetName))
            if (group.Select(d => d.Category).Distinct().Count() == 1)
                index.TryAdd(group.Key, group.First());
        return index;
    }
}
