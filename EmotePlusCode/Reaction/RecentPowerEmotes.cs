using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Local recent-power page. Repeated icons move to the front instead of occupying multiple slots.</summary>
public static class RecentPowerEmotes
{
    private static readonly List<EmoteType> Recent = [];
    /// <summary>Changes whenever the local recent page changes.</summary>
    public static ulong Revision { get; private set; }
    /// <summary>Latest first, padded to exactly eight slots.</summary>
    public static IReadOnlyList<EmoteType> Snapshot() => Recent.Concat(Enumerable.Repeat(EmoteType.None, 8 - Recent.Count)).ToArray();
    internal static void Clear() { Recent.Clear(); Revision++; }

    internal static void Record(Creature owner, PowerModel power)
    {
        if (owner.CombatState == null || TemporaryPowerContext.IsInternal(owner, power.GetType()) ||
            !PowerEmotes.TryGetEmote(power, out var emote)) return;
        Recent.Remove(emote);
        Recent.Insert(0, emote);
        if (Recent.Count > 8) Recent.RemoveAt(8);
        Revision++;
    }
}

// BeforeApplied can await while applying an internal Strength/Dexterity/Focus power. A thread-static flag
// would leak across concurrent game actions; AsyncLocal follows that operation's execution context instead.
internal static class TemporaryPowerContext
{
    internal sealed record Scope(Creature Owner, Type InternalType, Scope? Parent);
    internal static readonly AsyncLocal<Scope?> Current = new();
    internal static bool IsInternal(Creature owner, Type powerType)
    {
        for (var scope = Current.Value; scope != null; scope = scope.Parent)
            if (ReferenceEquals(owner, scope.Owner) && powerType == scope.InternalType) return true;
        return false;
    }
}
