using Godot;
using EmotePlus.EmotePlusCode.Reaction;

namespace EmotePlus.EmotePlusCode.Extensions;

// Utilities to get asset paths.
public static class StringExtensions
{
    // Emote name used when the requested one has no image (a typo or a half-added emote).
    private const string FallbackEmote = "question";
    private static readonly HashSet<string> ReportedMissing = [];

    /// <summary>
    /// The path of the emote image (the icon without its outline) for an emote name like "heart":
    /// res://EmotePlus/ui/emote/icons/heart.png. Falls back to a default image if it is missing.
    /// </summary>
    public static string GetEmotePath(this string name)
    {
        return Existing(ImagePath(Definition(name), false), FallbackPath(false));
    }

    /// <summary>
    /// The path of the emote's outline image (a single-color silhouette, tinted by the sender's color):
    /// res://EmotePlus/ui/emote/outlines/heart.png.
    /// </summary>
    public static string GetEmoteOutlinePath(this string name)
    {
        return Existing(ImagePath(Definition(name), true), FallbackPath(true));
    }

    /// <summary>Gets the icon path directly from an emote ID and its catalogue category.</summary>
    public static string GetEmotePath(this EmoteType type) =>
        Existing(ImagePath(EmoteCatalog.Get(type), false), FallbackPath(false));

    /// <summary>Gets the outline path directly from an emote ID and its catalogue category.</summary>
    public static string GetEmoteOutlinePath(this EmoteType type) =>
        Existing(ImagePath(EmoteCatalog.Get(type), true), FallbackPath(true));

    /// <summary>Resolves a short asset name in an explicitly selected directory, without catalogue name ambiguity.</summary>
    public static string GetEmotePath(this string assetName, EmoteCategory category) =>
        Existing(ImagePath(new(EmoteType.None, assetName, assetName, category), false), FallbackPath(false));

    /// <summary>Resolves an outline in an explicitly selected directory.</summary>
    public static string GetEmoteOutlinePath(this string assetName, EmoteCategory category) =>
        Existing(ImagePath(new(EmoteType.None, assetName, assetName, category), true), FallbackPath(true));

    private static EmoteDefinition Definition(string name) => EmoteCatalog.TryGet(name, out var definition)
        ? definition : new(EmoteType.None, name, name, EmoteCategory.None);

    private static string FallbackPath(bool outline) =>
        $"{MainFile.ResPath}/ui/emote/{(outline ? "outlines" : "icons")}/{FallbackEmote}.png";

    private static string ImagePath(EmoteDefinition definition, bool outline)
    {
        var folder = definition.Category switch
        {
            EmoteCategory.None => "",
            EmoteCategory.Power => "powers/",
            EmoteCategory.Ancient => "ancients/",
            EmoteCategory.Relic => "relics/",
            EmoteCategory.Enchantment => "enchantments/",
            EmoteCategory.Potion => "potions/",
            _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.Category, "Unknown artwork category"),
        };
        return $"{MainFile.ResPath}/ui/emote/{(outline ? "outlines" : "icons")}/{folder}{definition.AssetName}.png";
    }

    private static string Existing(string path, string fallback)
    {
        if (ResourceLoader.Exists(path))
        {
            return path;
        }

        if (ReportedMissing.Add(path)) MainFile.Logger.Info($"Could not find emote image path: {path}");
        return fallback;
    }
}
