using Godot;

namespace EmotePlus.EmotePlusCode.Extensions;

// Utilities to get asset paths.
public static class StringExtensions
{
    // Emote name used when the requested one has no image (a typo or a half-added emote).
    private const string FallbackEmote = "question";

    /// <summary>
    /// The path of the emote image (the icon without its outline) for an emote name like "heart":
    /// res://EmotePlus/ui/emote/heart.png. Falls back to a default image if it is missing.
    /// </summary>
    public static string GetEmotePath(this string name)
    {
        return Existing(EmotePath(name), EmotePath(FallbackEmote));
    }

    /// <summary>
    /// The path of the emote's outline image (a single-color silhouette, tinted by the sender's color):
    /// res://EmotePlus/ui/emote/heart_outline.png.
    /// </summary>
    public static string GetEmoteOutlinePath(this string name)
    {
        return Existing(EmoteOutlinePath(name), EmoteOutlinePath(FallbackEmote));
    }

    private static string EmotePath(string name) => $"{MainFile.ResPath}/ui/emote/{name}.png";

    private static string EmoteOutlinePath(string name) => $"{MainFile.ResPath}/ui/emote/{name}_outline.png";

    private static string Existing(string path, string fallback)
    {
        if (ResourceLoader.Exists(path))
        {
            return path;
        }

        MainFile.Logger.Info($"Could not find emote image path: {path}");
        return fallback;
    }
}
