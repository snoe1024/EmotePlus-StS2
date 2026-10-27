using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Local, in-memory activity history. Entries are unique emote IDs, newest first.</summary>
public static class RecentActivityEmotes
{
    /// <summary>Public API version for reading and recording local activity.</summary>
    public const int ApiVersion = 2;
    private static readonly List<EmoteType> Recent = [];
    private static readonly HashSet<ModelId> Flashed = [];
    private static ICombatState? _combat;
    private static int _round;
    private static object? _side;

    /// <summary>Changes only when the visible eight-slot history changes.</summary>
    public static ulong Revision { get; private set; }
    internal static ulong Generation { get; private set; }
    /// <summary>Returns eight slots, padded with None; never persisted to the run save.</summary>
    public static IReadOnlyList<EmoteType> Snapshot() => Recent.Concat(Enumerable.Repeat(EmoteType.None, 8 - Recent.Count)).ToArray();

    internal static void Clear()
    {
        Generation++;
        if (Recent.Count != 0) { Recent.Clear(); Revision++; }
        ResetCombat();
    }

    internal static void StartRun(RunState state)
    {
        Clear();
        // Launch has assigned the local NetId and both new/saved players already contain their relics.
        var player = LocalContext.GetMe(state);
        if (player == null) return;
        foreach (var modifier in state.Modifiers) Record(modifier);
        foreach (var relic in player.Relics) Record(relic);
    }

    internal static void ResetCombat() { Flashed.Clear(); _combat = null; _side = null; }

    /// <summary>
    /// Records an explicitly selected emote locally. Call on the game main thread during a run.
    /// Returns false for None, unregistered IDs or outside a run; true when accepted, including an unchanged front entry.
    /// Moves duplicates to the front, keeps at most eight entries, and never sends or persists activity.
    /// Unlike automatic model observation, explicit activity accepts every registered emote, including hidden ones.
    /// </summary>
    public static bool TryRecord(EmoteType type)
    {
        if (!RunManager.Instance.IsInProgress || type == EmoteType.None || EmoteCatalog.Get(type).Type != type ||
            !ModdedEmotes.IsAvailable(type)) return false;
        if (Recent.Count > 0 && Recent[0] == type) return true;
        Recent.Remove(type);
        Recent.Insert(0, type);
        if (Recent.Count > 8) Recent.RemoveAt(8);
        Revision++;
        return true;
    }

    internal static void Record(EmoteType type)
    {
        if (CreatureEmotes.TracksActivity(type)) TryRecord(type);
    }

    internal static void Record(AbstractModel? model)
    {
        EmoteType type;
        var found = model switch
        {
            RelicModel relic => RelicEmotes.TryGetEmote(relic, out type),
            PotionModel potion => PotionEmotes.TryGetEmote(potion, out type),
            PowerModel power => PowerEmotes.TryGetEmote(power, out type),
            EnchantmentModel enchantment => EnchantmentEmotes.TryGetEmote(enchantment, out type),
            AncientEventModel ancient => AncientEmotes.TryGetEmote(ancient, out type),
            CharacterModel character => CharacterEmotes.TryGetEmote(character, out type),
            MonsterModel monster => CreatureEmotes.TryGetEmote(monster, out type),
            EncounterModel encounter => CreatureEmotes.TryGetEmote(encounter, out type),
            ModifierModel modifier => ModifierEmotes.TryGetEmote(modifier, out type),
            _ => NoMatch(out type),
        };
        if (found) Record(type);
    }

    private static bool NoMatch(out EmoteType type) { type = EmoteType.None; return false; }

    internal static void Record(Creature? creature)
    {
        if (CreatureEmotes.TryGetEmote(creature, out var type)) Record(type);
    }

    internal static void RecordPower(PowerModel power, decimal amount, Creature? applier)
    {
        if (power.Owner.CombatState == null || (!LocalContext.IsMe(power.Owner) && !LocalContext.IsMe(applier)) ||
            !(amount > 0 || amount < 0 && power.AllowNegative && power.Amount < 0) ||
            TemporaryPowerContext.IsInternal(power.Owner, power.GetType())) return;
        Record(power);
    }

    internal static void RecordFlash(RelicModel relic)
    {
        if (relic.IsCanonical || !RelicEmotes.TryGetEmote(relic, out var type)) return;
        var state = relic.Owner?.Creature.CombatState;
        if (state != null)
        {
            if (!ReferenceEquals(_combat, state) || _round != state.RoundNumber || !Equals(_side, state.CurrentSide))
            {
                Flashed.Clear(); _combat = state; _round = state.RoundNumber; _side = state.CurrentSide;
            }
            if (!Flashed.Add(relic.Id)) return; // Copies held by different players share a model ID.
        }
        Record(type);
    }
}
