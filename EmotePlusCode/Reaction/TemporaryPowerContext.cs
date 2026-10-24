using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Reaction;

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
