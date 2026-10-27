using ArtworkPaths = EmotePlus.EmotePlusCode.Extensions.StringExtensions;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Alternative artwork for an existing ID, guarded by mounted bytes or the final displayed texture.</summary>
public sealed record EmoteImageOverride(int TargetId, EmoteCategory Category, string AssetName,
    string SourceResource, string SourceSha256)
{
    /// <summary>ResourceFile hashes mounted bytes; CharacterIcon hashes the final displayed RGBA8 pixels.</summary>
    public string Match { get; init; } = "ResourceFile";
    /// <summary>Native source dimensions, required for CharacterIcon matches.</summary>
    public int Width { get; init; }
    /// <summary>Native source dimensions, required for CharacterIcon matches.</summary>
    public int Height { get; init; }
}

/// <summary>Resolves local artwork without changing catalogue identity, selection or wire IDs.</summary>
public static class EmoteArtworkOverrides
{
    /// <summary>Public artwork-resolution API version.</summary>
    public const int ApiVersion = 1;

    /// <summary>
    /// Resolves an artwork snapshot on the Godot main thread. Null sender uses the local selection; pass the
    /// original sender for synthetic remote visuals. Unknown/reserved IDs resolve to the question emote.
    /// Unrecognized skins or incomplete replacement pairs retain the original artwork. This resolves custom
    /// artwork only, not the game's unmodified vanilla ReactionMessage presentation.
    /// </summary>
    public static EmoteDefinition Resolve(EmoteType type, ulong? senderNetId = null)
        => Resolve(EmoteCatalog.Get(type), senderNetId);

    private static readonly Lazy<IReadOnlyDictionary<EmoteType, (ContentModDefinition Group, EmoteImageOverride Rule)[]>> Rules =
        new(() => ModdedEmotes.Groups.SelectMany(g => (g.ImageOverrides ?? []).Select(r => (Group: g, Rule: r)))
            .GroupBy(p => (EmoteType)p.Rule.TargetId).ToDictionary(g => g.Key, g => g.ToArray()));

    internal static void Validate(IReadOnlyList<ContentModDefinition> groups)
    {
        // EmoteCatalog depends on Groups during initialization; consult definition lists without entering that cycle.
        var registered = NoneEmotes.All.Select(e => (int)e.Type)
            .Concat(PowerEmotes.All.Select(e => (int)e.Emote))
            .Concat(RelicEmotes.All.Select(e => (int)e.Emote))
            .Concat(PotionEmotes.All.Select(e => (int)e.Emote))
            .Concat(EnchantmentEmotes.All.Select(e => (int)e.Emote))
            .Concat(CharacterEmotes.All.Select(e => (int)e.Emote))
            .Concat(CreatureEmotes.All.Select(e => (int)e.Emote))
            .Concat(AncientEmotes.All.Select(e => (int)e.Emote))
            .Concat(ModifierEmotes.All.Select(e => (int)e.Emote))
            .Concat(MemeEmotes.All.Select(e => (int)e.Type))
            .Concat(groups.SelectMany(g => g.Entries.Select(e => e.Id))).ToHashSet();
        var guards = new HashSet<(int, string, string)>();
        foreach (var group in groups)
            foreach (var rule in group.ImageOverrides ?? [])
            {
                if (!registered.Contains(rule.TargetId) || !EmoteIdLayout.MatchesCategory(rule.TargetId, rule.Category) ||
                    string.IsNullOrEmpty(rule.AssetName) || string.IsNullOrEmpty(rule.SourceResource) || string.IsNullOrEmpty(rule.SourceSha256) ||
                    !rule.AssetName.StartsWith("mods/" + group.Key + "/", StringComparison.Ordinal) ||
                    rule.AssetName.Contains("..") || rule.AssetName.Contains('\\') || rule.AssetName.Contains(':') ||
                    !rule.SourceResource.StartsWith("res://", StringComparison.Ordinal) ||
                    rule.SourceResource.Contains("..") || rule.SourceResource.Contains('\\') ||
                    (rule.Match != "ResourceFile" && rule.Match != "CharacterIcon") ||
                    (rule.Match == "CharacterIcon" && (rule.Category != EmoteCategory.Character || rule.Width <= 0 || rule.Height <= 0)) ||
                    rule.SourceSha256.Length != 64 || !rule.SourceSha256.All(Uri.IsHexDigit) ||
                    !guards.Add((rule.TargetId, rule.SourceResource, rule.SourceSha256.ToLowerInvariant())))
                    throw new InvalidDataException($"Invalid or duplicate artwork override: {group.Key}/{rule.TargetId}");
            }
    }

    internal static EmoteDefinition Resolve(EmoteDefinition original, ulong? senderNetId = null)
    {
        if (original.ArtworkEmoteId.HasValue) original = EmoteCatalog.ResolveArtwork(original.Type);
        if (!Rules.Value.TryGetValue(original.Type, out var candidates)) return original;
        // Resolve once per pair. An observed but unknown icon must not fall back to a different installed skin.
        var icon = candidates.Any(c => c.Rule.Match == "CharacterIcon")
            ? CharacterArtwork.CurrentIcon(original.Type, senderNetId) : null;
        foreach (var (group, rule) in candidates)
        {
            if (!ModManager.Mods.Any(m => m.state == ModLoadState.Loaded &&
                    group.ModIds.Contains(m.manifest?.id, StringComparer.Ordinal))) continue;
            // Presence alone is insufficient: inactive skins or another later PCK must not select this artwork.
            if (rule.Match == "CharacterIcon"
                ? !CharacterArtwork.Matches(icon, rule)
                : !Godot.FileAccess.FileExists(rule.SourceResource) ||
                  !string.Equals(Godot.FileAccess.GetSha256(rule.SourceResource), rule.SourceSha256, StringComparison.OrdinalIgnoreCase))
                continue;
            var replacement = original with { AssetName = rule.AssetName };
            // A partially supplied pair must retain the original artwork instead of showing a question mark.
            if (ResourceLoader.Exists(ArtworkPaths.ArtworkPath(replacement, false)) &&
                ResourceLoader.Exists(ArtworkPaths.ArtworkPath(replacement, true))) return replacement;
        }
        return original;
    }
}
