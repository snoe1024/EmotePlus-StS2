namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Single catalogue for identity and artwork routing. Search tags do not decide resource paths.</summary>
public static class EmoteCatalog
{
    /// <summary>Every emote exactly once, with stable settings names and explicit artwork categories.</summary>
    public static IReadOnlyList<EmoteDefinition> All { get; } = Array.AsReadOnly(Create());

    private static readonly IReadOnlyDictionary<EmoteType, EmoteDefinition> ByType = All.ToDictionary(d => d.Type);
    private static readonly IReadOnlyDictionary<string, EmoteDefinition> ByName = CreateNameIndex();

    /// <summary>Gets a definition by ID; unknown IDs use the question emote.</summary>
    public static EmoteDefinition Get(EmoteType type) => ByType.TryGetValue(type, out var definition)
        ? definition : ByType[EmoteType.QuestionMark];

    /// <summary>Looks up a saved key or an unambiguous short asset name in a single index.</summary>
    public static bool TryGet(string name, out EmoteDefinition definition) => ByName.TryGetValue(name, out definition!);

    private static EmoteDefinition[] Create()
    {
        var names = new Dictionary<EmoteType, string>
        {
            [EmoteType.Exclamation] = "exclaim",
            [EmoteType.Skull] = "skull",
            [EmoteType.ThumbDown] = "thumb_down",
            [EmoteType.SadSlime] = "slime_sad",
            [EmoteType.QuestionMark] = "question",
            [EmoteType.Heart] = "heart",
            [EmoteType.ThumbUp] = "thumb_up",
            [EmoteType.HappyCultist] = "happy_cultist",
            [EmoteType.Darv] = "darv",
            [EmoteType.Neow] = "neow",
            [EmoteType.Nonupeipe] = "nonupeipe",
            [EmoteType.Orobas] = "orobas",
            [EmoteType.Pael] = "pael",
            [EmoteType.Tanx] = "tanx",
            [EmoteType.Tezcatara] = "tezcatara",
            [EmoteType.Vakuu] = "vakuu",
            [EmoteType.Curious] = "curious",
            [EmoteType.Enrage] = "enrage",
            [EmoteType.HelloWorld] = "hello_world",
            [EmoteType.Vulnerable] = "vulnerable",
            [EmoteType.Weak] = "weak",
            [EmoteType.Gold] = "gold",
            [EmoteType.RIP] = "blasphemyrip",
            [EmoteType.Devotion] = "devotion",
        };
        var definitions = names.ToDictionary(p => p.Key,
            p => new EmoteDefinition(p.Key, p.Value, p.Value, EmoteCategory.None));
        foreach (var power in PowerEmotes.All)
        {
            var key = names.GetValueOrDefault(power.Emote, power.AssetName + "_power");
            definitions[power.Emote] = new(power.Emote, key, power.AssetName, EmoteCategory.Power);
        }
        // The original and power-specific HelloWorld IDs intentionally share the same artwork.
        definitions[EmoteType.HelloWorld] = definitions[EmoteType.HelloWorld] with { Category = EmoteCategory.Power };
        foreach (var ancient in AncientEmotes.All)
            definitions[ancient.Emote] = definitions[ancient.Emote] with { Category = EmoteCategory.Ancient };
        foreach (var relic in RelicEmotes.All)
            definitions.Add(relic.Emote, new(relic.Emote, relic.AssetName + "_relic", relic.AssetName, EmoteCategory.Relic));
        foreach (var enchantment in EnchantmentEmotes.All)
            definitions.Add(enchantment.Emote, new(enchantment.Emote, enchantment.AssetName + "_enchantment",
                enchantment.AssetName, EmoteCategory.Enchantment));
        foreach (var potion in PotionEmotes.All)
            definitions.Add(potion.Emote, new(potion.Emote, potion.AssetName + "_potion", potion.AssetName, EmoteCategory.Potion));
        return definitions.Values.ToArray();
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
