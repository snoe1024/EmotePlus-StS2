using EmotePlus.EmotePlusCode.Config;
using EmotePlus.EmotePlusCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>
/// The emotes and their images. An emote has an icon and an outline (tinted separately, see
/// <see cref="StringExtensions"/>). Stored keys are independent of filenames and the explicit artwork category
/// in <see cref="EmoteCatalog"/>. Icons and outlines use matching category subdirectories.
///
/// To add an emote: put the two images there, add an <see cref="EmoteType"/> value (a new number), add it to
/// <see cref="EmoteCatalog"/>, and put it in <see cref="WheelLayout"/> where it should be by default. Images of any size
/// work: they are fitted into the same box. The player can rearrange the emotes in the mod settings
/// (<see cref="WheelSlots"/>).
/// </summary>
public static class EmoteImages
{
    /// <summary>The persistent settings key of every emote. The order does not matter here.</summary>
    private static readonly Dictionary<EmoteType, string> Names =
        EmoteCatalog.All.ToDictionary(d => d.Type, d => d.Name);

    /// <summary>The pre-1.0 selection. Future additions must not change this migration/default baseline.</summary>
    public const string InitialSelection = "exclaim,skull,thumb_down,slime_sad,question,heart,thumb_up,happy_cultist,darv,neow,nonupeipe,orobas,pael,tanx,tezcatara,vakuu,curious,enrage,hello_world,vulnerable,weak,gold,blasphemyrip,devotion";

    /// <summary>Migrates the old exclusion list, preserving its effective 24-emote selection.</summary>
    public static bool MigrateVisibility()
    {
        if (EmotePlusConfig.EmoteVisibilityVersion >= 1) return false;
        var hidden = ParseNames(EmotePlusConfig.HiddenEmotes);
        EmotePlusConfig.EnabledEmotes = string.Join(",", ParseNames(InitialSelection).Where(n => !hidden.Contains(n)));
        EmotePlusConfig.EmptySelectionFallback = InitialSelection;
        EmotePlusConfig.EmoteVisibilityVersion = 1;
        return true;
    }

    /// <summary>
    /// The default order of the emotes on the wheel: 8 per page, each page clockwise from the right (right, lower
    /// right, down, lower left, left, upper left, up, upper right). <see cref="EmoteType.None"/> leaves a slot empty
    /// (to keep the next emote at a given wedge, or to end a page early). Emotes missing here are added at the end,
    /// so a forgotten one is never unreachable.
    /// </summary>
    private static readonly EmoteType[] WheelLayout =
    [
        // page 1
        EmoteType.Exclamation,
        EmoteType.Vulnerable,
        EmoteType.ThumbDown,
        EmoteType.Weak,
        EmoteType.QuestionMark,
        EmoteType.Gold,
        EmoteType.ThumbUp,
        EmoteType.HelloWorld,
        
        // page 2
        EmoteType.Enrage,
        EmoteType.Skull,
        EmoteType.RIP,
        EmoteType.SadSlime,
        EmoteType.Devotion,
        EmoteType.HappyCultist,
        EmoteType.Curious,
        EmoteType.Heart,
        
        // page 3
        EmoteType.Neow,
        EmoteType.Orobas,
        EmoteType.Tezcatara,
        EmoteType.Pael,
        EmoteType.Darv,
        EmoteType.Tanx,
        EmoteType.Nonupeipe,
        EmoteType.Vakuu,
    ];

    /// <summary>The default order, complete (no placeholders; every emote exactly once).</summary>
    public static readonly IReadOnlyList<EmoteType> DefaultOrder =
        WheelLayout.Where(t => t != EmoteType.None).Concat(Names.Keys.Except(WheelLayout)).ToList();

    /// <summary>
    /// The order of the emotes on the wheel, page after page: the player's own order from the settings
    /// (<see cref="EmotePlusConfig.EmoteOrder"/>) or else <see cref="DefaultOrder"/>. Always complete: unknown names
    /// are ignored, and emotes the saved order does not mention (added by an update) are put at the end.
    /// </summary>
    public static IReadOnlyList<EmoteType> WheelSlots
    {
        get
        {
            var text = EmotePlusConfig.EmoteOrder ?? "";
            if (text != _orderText)
            {
                _orderText = text;
                _order = ParseOrder(text);
            }

            return _order;
        }
    }

    private static string? _orderText;
    private static IReadOnlyList<EmoteType> _order = DefaultOrder;

    private static IReadOnlyList<EmoteType> ParseOrder(string text)
    {
        if (text.Length == 0)
        {
            return DefaultOrder;
        }

        var byName = Names.ToDictionary(e => e.Value, e => e.Key);
        var order = new List<EmoteType>();
        foreach (var name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (byName.TryGetValue(name, out var type) && !order.Contains(type))
            {
                order.Add(type);
            }
        }

        order.AddRange(DefaultOrder.Except(order));
        return order;
    }

    /// <summary>Saves the order (an empty text stands for the default order).</summary>
    public static void SetOrder(IEnumerable<EmoteType> order)
    {
        var list = order.ToList();
        EmotePlusConfig.EmoteOrder = list.SequenceEqual(DefaultOrder) ? "" : string.Join(",", list.Select(NameOf));
    }

    /// <summary>The order of the original game's wheel (its 8 emotes) first, then the others as they are now.</summary>
    public static List<EmoteType> VanillaFirst()
    {
        var vanilla = Enumerable.Range(1, 8).Select(i => (EmoteType)i).ToList();
        return vanilla.Concat(WheelSlots.Except(vanilla)).ToList();
    }

    /// <summary>The emotes that are shown on the wheel first (in their order), the hidden ones after them.</summary>
    public static List<EmoteType> HiddenLast()
    {
        return WheelSlots.Where(t => !IsHidden(NameOf(t))).Concat(WheelSlots.Where(t => IsHidden(NameOf(t)))).ToList();
    }

    /// <summary>Every emote with its image name, in the order of <see cref="WheelSlots"/>.</summary>
    public static IEnumerable<(EmoteType Type, string Name)> AllEmotes =>
        WheelSlots.Where(t => t != EmoteType.None).Select(t => (t, NameOf(t)));

    private static string? _enabledText;
    private static HashSet<string> _enabled = [];

    private static HashSet<string> Enabled
    {
        get
        {
            var text = EmotePlusConfig.EnabledEmotes ?? "";
            if (text != _enabledText)
            {
                _enabledText = text;
                _enabled = ParseNames(text);
            }

            return _enabled;
        }
    }

    private static HashSet<string> ParseNames(string? text) => (text ?? "").Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);

    public static bool IsHidden(string name) => !Enabled.Contains(name);

    public static void SetHidden(string name, bool hidden)
    {
        var set = new HashSet<string>(Enabled);
        if (hidden ? !set.Remove(name) : !set.Add(name))
        {
            return;
        }

        EmotePlusConfig.EnabledEmotes = string.Join(",", set.Order());
        // Explicit user edits may include today's catalogue in the all-hidden fallback. Future additions are
        // excluded because this snapshot is saved, rather than recalculated when loading a newer mod version.
        EmotePlusConfig.EmptySelectionFallback = string.Join(",", Names.Values);
    }

    /// <summary>Explicitly selects or hides the current catalogue without opting into future additions.</summary>
    public static void SetAllHidden(bool hidden)
    {
        EmotePlusConfig.EnabledEmotes = hidden ? "" : string.Join(",", Names.Values);
        EmotePlusConfig.EmptySelectionFallback = string.Join(",", Names.Values);
    }

    /// <summary>
    /// <see cref="WheelSlots"/> without the emotes the player hid, so the wheel closes the gaps. Empty slots
    /// (<see cref="EmoteType.None"/>) stay. If every emote is hidden, nothing is hidden.
    /// </summary>
    public static IReadOnlyList<EmoteType> VisibleSlots()
    {
        var selected = Enabled;
        if (!Names.Values.Any(selected.Contains)) selected = ParseNames(EmotePlusConfig.EmptySelectionFallback);
        var visible = WheelSlots.Where(t => t != EmoteType.None && selected.Contains(NameOf(t))).ToList();
        if (visible.Count > 0) return visible;
        var baseline = ParseNames(InitialSelection);
        return WheelSlots.Where(t => baseline.Contains(NameOf(t))).ToList();
    }

    private static readonly Dictionary<string, Texture2D> Cache = new();

    /// <summary>The vanilla emote's id: same numbers, see <see cref="EmoteType"/>.</summary>
    public static EmoteType FromVanilla(ReactionType type) => (EmoteType)(int)type;

    public static string NameOf(EmoteType type) => Names.TryGetValue(type, out var name) ? name : "question";

    public static Texture2D Icon(string name) => Load(name.GetEmotePath());

    public static Texture2D Outline(string name) => Load(name.GetEmoteOutlinePath());

    /// <summary>Loads the icon through its explicit artwork category.</summary>
    public static Texture2D Icon(EmoteType type) => Load(type.GetEmotePath());

    /// <summary>Loads the outline through its explicit artwork category.</summary>
    public static Texture2D Outline(EmoteType type) => Load(type.GetEmoteOutlinePath());

    private static Texture2D Load(string path)
    {
        if (!Cache.TryGetValue(path, out var texture))
        {
            Cache[path] = texture = ResourceLoader.Load<Texture2D>(path);
        }

        return texture;
    }
}
