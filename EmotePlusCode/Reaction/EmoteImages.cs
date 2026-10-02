using EmotePlus.EmotePlusCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>
/// The images of an emote: the icon and its outline (tinted separately, see <see cref="StringExtensions"/>).
/// Emotes are identified by name = the file name under res://EmotePlus/ui/emote/ (without ".png" / "_outline.png"),
/// so adding an emote is: drop the two images there and add the name to <see cref="Names"/>.
/// </summary>
public static class EmoteImages
{
    private static readonly Dictionary<EmoteType, string> Names = new()
    {
        [EmoteType.Exclamation] = "exclaim",
        [EmoteType.Skull] = "skull",
        [EmoteType.ThumbDown] = "thumb_down",
        [EmoteType.SadSlime] = "slime_sad",
        [EmoteType.QuestionMark] = "question",
        [EmoteType.Heart] = "heart",
        [EmoteType.ThumbUp] = "thumb_up",
        [EmoteType.HappyCultist] = "happy_cultist",
    };

    private static readonly Dictionary<string, Texture2D> Cache = new();

    /// <summary>The vanilla emote's id: same numbers, see <see cref="EmoteType"/>.</summary>
    public static EmoteType FromVanilla(ReactionType type) => (EmoteType)(int)type;

    public static string NameOf(EmoteType type) => Names.TryGetValue(type, out var name) ? name : "question";

    public static Texture2D Icon(string name) => Load(name.GetEmotePath());

    public static Texture2D Outline(string name) => Load(name.GetEmoteOutlinePath());

    private static Texture2D Load(string path)
    {
        if (!Cache.TryGetValue(path, out var texture))
        {
            Cache[path] = texture = ResourceLoader.Load<Texture2D>(path);
        }

        return texture;
    }
}
