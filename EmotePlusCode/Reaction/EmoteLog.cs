namespace EmotePlus.EmotePlusCode.Reaction;

// Diagnostics for tracing where an emote ends up. Everything goes to godot.log with the "[EmotePlus] [emote]" tag.
public static class EmoteLog
{
    public static void Info(string message)
    {
        MainFile.Logger.Info($"[emote] {message}");
    }
}
