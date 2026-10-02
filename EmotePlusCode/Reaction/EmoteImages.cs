using EmotePlus.EmotePlusCode.Config;
using EmotePlus.EmotePlusCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>
/// The emotes and their images. An emote has an icon and an outline (tinted separately, see
/// <see cref="StringExtensions"/>); it is identified by name = the file name under res://EmotePlus/ui/emote/
/// (without ".png" / "_outline.png").
///
/// To add an emote: put the two images there, add an <see cref="EmoteType"/> value (a new number), add it to
/// <see cref="Names"/>, and put it in <see cref="WheelLayout"/> where it should be. Images of any size work: they
/// are fitted into the same box.
/// </summary>
public static class EmoteImages
{
    /// <summary>The image name of every emote. The order does not matter here.</summary>
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

    /// <summary>
    /// Where the emotes are on the wheel, in order: 8 per page, each page clockwise from the right (right, lower
    /// right, down, lower left, left, upper left, up, upper right). <see cref="EmoteType.None"/> leaves a slot empty
    /// (to keep the next emote at a given wedge, or to end a page early). Emotes missing here are added at the end,
    /// so a forgotten one is never unreachable.
    /// </summary>
    private static readonly EmoteType[] WheelLayout =
    [
        // page 1
        EmoteType.Exclamation,
        EmoteType.QuestionMark,
        EmoteType.ThumbDown,
        EmoteType.ThumbUp,
        EmoteType.HelloWorld,
        EmoteType.Gold,
        EmoteType.Vulnerable,
        EmoteType.Weak,
        
        // page 2
        EmoteType.Skull,
        EmoteType.SadSlime,
        EmoteType.Heart,
        EmoteType.HappyCultist,
        EmoteType.Enrage,
        EmoteType.Curious,
        EmoteType.Devotion,
        EmoteType.RIP,
        
        // page 3
        EmoteType.Darv,
        EmoteType.Neow,
        EmoteType.Nonupeipe,
        EmoteType.Orobas,
        EmoteType.Pael,
        EmoteType.Tanx,
        EmoteType.Tezcatara,
        EmoteType.Vakuu,
    ];

    /// <summary>The slots of the wheel, page after page (<see cref="EmoteType.None"/> = empty slot).</summary>
    public static readonly IReadOnlyList<EmoteType> WheelSlots =
        WheelLayout.Concat(Names.Keys.Except(WheelLayout)).ToList();

    /// <summary>Every emote with its image name, in the order of <see cref="WheelSlots"/>.</summary>
    public static IEnumerable<(EmoteType Type, string Name)> AllEmotes =>
        WheelSlots.Where(t => t != EmoteType.None).Select(t => (t, NameOf(t)));

    // The emotes the player hid (EmotePlusConfig.HiddenEmotes), parsed again whenever the text changes.
    private static string _hiddenText = "";
    private static HashSet<string> _hidden = [];

    private static HashSet<string> Hidden
    {
        get
        {
            var text = EmotePlusConfig.HiddenEmotes ?? "";
            if (text != _hiddenText)
            {
                _hiddenText = text;
                _hidden = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            }

            return _hidden;
        }
    }

    public static bool IsHidden(string name) => Hidden.Contains(name);

    public static void SetHidden(string name, bool hidden)
    {
        var set = new HashSet<string>(Hidden);
        if (hidden ? !set.Add(name) : !set.Remove(name))
        {
            return;
        }

        EmotePlusConfig.HiddenEmotes = string.Join(",", set.Order());
    }

    /// <summary>
    /// <see cref="WheelSlots"/> without the emotes the player hid, so the wheel closes the gaps. Empty slots
    /// (<see cref="EmoteType.None"/>) stay. If every emote is hidden, nothing is hidden.
    /// </summary>
    public static IReadOnlyList<EmoteType> VisibleSlots()
    {
        var hidden = Hidden;
        if (hidden.Count == 0 || AllEmotes.All(e => hidden.Contains(e.Name)))
        {
            return WheelSlots;
        }

        return WheelSlots.Where(t => t == EmoteType.None || !hidden.Contains(NameOf(t))).ToList();
    }

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
