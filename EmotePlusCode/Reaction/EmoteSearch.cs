using System.Globalization;
using System.Text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Search metadata independent of wheel order, visibility and texture loading.</summary>
internal static class EmoteSearch
{
    internal static readonly string[] Tags = ["Power", "Buff", "Debuff", "Relic", "Enchantment", "Potion", "Unused", "Ancient", "Character", "Communicate", "Meme"];

    internal sealed record Entry(string Title, string Text, HashSet<string> Tags)
    {
        internal bool Matches(string query, IReadOnlySet<string> tags) =>
            (tags.Count == 0 || tags.Overlaps(Tags)) &&
            Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).All(Text.Contains);
    }

    internal static Entry Describe(EmoteType type)
    {
        var name = EmoteImages.NameOf(type);
        var key = "EMOTEPLUS-EMOTE_" + name.ToUpperInvariant();
        var title = Localized(key + ".title", name);
        var words = new StringBuilder(name.Replace('_', ' ') + " " + name + " " + title);
        words.Append(' ').Append(Localized(key + ".aliases", ""));
        var tags = new HashSet<string>(EmoteTags.ByEmote.GetValueOrDefault(type) ?? []);
        foreach (var power in PowerEmotes.All.Where(p => p.Emote == type))
        {
            tags.Add("Power");
            if (PowerEmoteTags.Buffs.Contains(power.TypeName)) tags.Add("Buff");
            if (PowerEmoteTags.Debuffs.Contains(power.TypeName)) tags.Add("Debuff");
            var powerKey = StringHelper.Slugify(power.TypeName) + ".title";
            var powerTitle = LocString.Exists("powers", powerKey)
                ? new LocString("powers", powerKey).GetFormattedText() : power.TypeName;
            if ((int)type > 24 && !LocString.Exists("settings_ui", key + ".title")) title = powerTitle;
            words.Append(' ').Append(power.TypeName).Append(' ').Append(powerTitle);
        }
        foreach (var relic in RelicEmotes.All.Where(r => r.Emote == type))
        {
            tags.Add("Relic");
            if (relic.Unused) tags.Add("Unused");
            var relicKey = relic.TypeName == null ? null : StringHelper.Slugify(relic.TypeName) + ".title";
            var relicTitle = relicKey != null && LocString.Exists("relics", relicKey)
                ? new LocString("relics", relicKey).GetFormattedText() : relic.AssetName;
            if (!LocString.Exists("settings_ui", key + ".title")) title = relicTitle;
            words.Append(' ').Append(relic.AssetName).Append(' ').Append(relic.TypeName).Append(' ').Append(relicTitle);
        }
        void AddModel(string tag, string table, string? typeName, string assetName, bool unused)
        {
            tags.Add(tag);
            if (unused) tags.Add("Unused");
            var modelKey = typeName == null ? null : StringHelper.Slugify(typeName) + ".title";
            var modelTitle = modelKey != null && LocString.Exists(table, modelKey)
                ? new LocString(table, modelKey).GetFormattedText() : assetName;
            if (!LocString.Exists("settings_ui", key + ".title")) title = modelTitle;
            words.Append(' ').Append(assetName).Append(' ').Append(typeName).Append(' ').Append(modelTitle);
        }
        foreach (var enchantment in EnchantmentEmotes.All.Where(e => e.Emote == type))
            AddModel("Enchantment", "enchantments", enchantment.TypeName, enchantment.AssetName, enchantment.Unused);
        foreach (var potion in PotionEmotes.All.Where(p => p.Emote == type))
            AddModel("Potion", "potions", potion.TypeName, potion.AssetName, potion.Unused);
        return new Entry(title, Normalize(words.ToString()), tags);
    }

    private static string Localized(string key, string fallback) => LocString.Exists("settings_ui", key)
        ? new LocString("settings_ui", key).GetFormattedText() : fallback;

    // Decomposition makes accented Latin aliases searchable without requiring the accent marks.
    internal static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormKD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        .ToLowerInvariant().Normalize(NormalizationForm.FormKC);
}
