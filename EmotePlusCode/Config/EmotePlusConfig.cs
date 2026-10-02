using BaseLib.Config;

namespace EmotePlus.EmotePlusCode.Config;

// The namespace's first segment gives the localization key prefix ("EMOTEPLUS-", see settings_ui.json).
[ConfigHoverTipsByDefault]
public sealed class EmotePlusConfig : SimpleModConfig
{
    /// <summary>How long every emote stays on screen (appearing + waiting + fading out; the game's own is 1.1).</summary>
    [ConfigSlider(0.5, 5.0, 0.1, Format = "{0:0.0}sec")]
    public static float EmoteDisplayTime { get; set; } = 1.1f;

    /// <summary>
    /// While the emote wheel is open, the left click goes to the previous page and the right click to the next one;
    /// with this on, the other way round.
    /// </summary>
    public static bool ReversePageClicks { get; set; } = false;
}
