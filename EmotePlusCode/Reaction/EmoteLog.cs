using System.Diagnostics;

namespace EmotePlus.EmotePlusCode.Reaction;

// Opt-in build diagnostics: omitted calls also avoid formatting per-emote strings in ordinary builds.
public static class EmoteLog
{
    [Conditional("EMOTEPLUS_DIAGNOSTICS")]
    public static void Info(string message)
    {
        MainFile.Logger.Info($"[emote] {message}");
    }
}
