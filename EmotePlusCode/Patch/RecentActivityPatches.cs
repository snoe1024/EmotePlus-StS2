using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using EmotePlus.EmotePlusCode.Reaction;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace EmotePlus.EmotePlusCode.Patch;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
internal static class RecentActivityPowerPatch
{
    // Both fresh applications and stacks reach this after the actual amount is changed, with the current applier.
    [HarmonyPrefix]
    private static void Prefix(PowerModel power, decimal amount, Creature? applier) =>
        RecentActivityEmotes.RecordPower(power, amount, applier);
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
internal static class RecentActivityCombatPatch
{
    [HarmonyPrefix]
    private static void Prefix() => RecentActivityEmotes.ResetCombat();
    [HarmonyPostfix]
    private static void Postfix(CombatState state) => RecentActivityEmotes.Record(state.Encounter);
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AddCreature))]
internal static class RecentActivityCreatureAddedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature creature)
    {
        if (creature.Monster != null) RecentActivityEmotes.Record(creature);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class RecentActivityCardTargetPatch
{
    [HarmonyPrefix]
    private static void Prefix(CardPlay cardPlay) => RecentActivityEmotes.Record(cardPlay.Target);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class RecentActivityEndRunPatch
{
    [HarmonyPostfix]
    private static void Postfix() => RecentActivityEmotes.Clear();
}

[HarmonyPatch(typeof(Player), nameof(Player.AddRelicInternal))]
internal static class RecentActivityRelicObtainedPatch
{
    [HarmonyPostfix]
    private static void Postfix(RelicModel relic, bool silent)
    {
        if (!silent) RecentActivityEmotes.Record(relic);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.AddPotionInternal))]
internal static class RecentActivityPotionObtainedPatch
{
    [HarmonyPostfix]
    private static void Postfix(PotionModel potion, bool silent, PotionProcureResult __result)
    {
        if (!silent && __result.success) RecentActivityEmotes.Record(potion);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforePotionUsed))]
internal static class RecentActivityPotionUsedPatch
{
    // This is the committed use path (also used by automatic potions), not the cancelable targeting UI.
    [HarmonyPrefix]
    private static void Prefix(PotionModel potion) => RecentActivityEmotes.Record(potion);
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Flash), [typeof(IEnumerable<Creature>)])]
internal static class RecentActivityRelicFlashPatch
{
    [HarmonyPrefix]
    private static void Prefix(RelicModel __instance) => RecentActivityEmotes.RecordFlash(__instance);
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom._Ready))]
internal static class RecentActivityAncientPatch
{
    [HarmonyPostfix]
    private static void Postfix(EventModel ____event, bool ____isPreFinished)
    {
        if (!____isPreFinished && ____event is AncientEventModel ancient) RecentActivityEmotes.Record(ancient);
    }
}

// Explicit focus entry points avoid attributing tooltip refreshes, unknown cards, or every power in a creature tooltip.
internal static class RecentActivityHover
{
    private sealed class HoverState { internal AbstractModel? Model; internal ulong Generation; internal bool Merchant; }
    private static readonly ConditionalWeakTable<Control, HoverState> Focused = new();
    private static readonly AccessTools.FieldRef<NLabPotionHolder, ModelVisibility> LabVisibility =
        AccessTools.FieldRefAccess<NLabPotionHolder, ModelVisibility>("_visibility");
    private static readonly AccessTools.FieldRef<NLabPotionHolder, PotionModel> LabModel =
        AccessTools.FieldRefAccess<NLabPotionHolder, PotionModel>("_model");
    private static readonly AccessTools.FieldRef<NMapPoint, IRunState> MapRunState =
        AccessTools.FieldRefAccess<NMapPoint, IRunState>("_runState");
    private static readonly AccessTools.FieldRef<NTopBarModifier, ModifierModel> TopModifier =
        AccessTools.FieldRefAccess<NTopBarModifier, ModifierModel>("_modifier");

    internal static readonly Type[] FocusTypes =
    [
        typeof(NRelicInventoryHolder), typeof(NRelicBasicHolder), typeof(NPotionHolder),
        typeof(NCardHolder), typeof(NPreviewCardHolder), typeof(NMerchantSlot),
        typeof(NRelicCollectionEntry), typeof(NLabPotionHolder),
        typeof(NTreasureRoomRelicHolder), typeof(NRewardButton),
        typeof(NCreature), typeof(NMultiplayerPlayerState),
        typeof(NMerchantButton),
        typeof(NAncientMapPoint), typeof(NBossMapPoint),
        typeof(NTopBarPortraitTip), typeof(NTopBarModifier),
    ];

    internal static IEnumerable<MethodBase> Methods(bool enter)
    {
        foreach (var type in FocusTypes)
            yield return type.GetMethod(enter ? "OnFocus" : "OnUnfocus",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
        yield return AccessTools.Method(typeof(NPower), enter ? "OnHovered" : "OnUnhovered");
    }

    internal static void Enter(Control control)
    {
        if (!RunManager.Instance.IsInProgress || !control.IsVisibleInTree()) return;
        if (control is NMerchantButton)
        {
            // The shop NPC has no MonsterModel; focus is the local interaction with this artwork.
            var merchant = Focused.GetOrCreateValue(control);
            if (merchant.Merchant && merchant.Generation == RecentActivityEmotes.Generation) return;
            merchant.Merchant = true;
            merchant.Generation = RecentActivityEmotes.Generation;
            RecentActivityEmotes.Record(EmoteType.CreatureMerchant);
            return;
        }
        AbstractModel? model = control switch
        {
            NAncientMapPoint point => MapRunState(point).Act.Ancient,
            NBossMapPoint point => point.Point == MapRunState(point).Map.SecondBossMapPoint
                ? MapRunState(point).Act.SecondBossEncounter : MapRunState(point).Act.BossEncounter,
            NRelicInventoryHolder holder => holder.Relic.Model,
            NRelicBasicHolder holder => holder.Relic.Model,
            NPotionHolder holder => holder.Potion?.Model,
            NPower power => power.Model,
            NCardHolder { CardNode.Visibility: ModelVisibility.Visible } holder => holder.CardModel?.Enchantment,
            NMerchantSlot { Entry: MerchantRelicEntry entry } => entry.Model,
            NMerchantSlot { Entry: MerchantPotionEntry entry } => entry.Model,
            NRelicCollectionEntry { ModelVisibility: ModelVisibility.Visible } entry => entry.relic,
            NLabPotionHolder holder when LabVisibility(holder) == ModelVisibility.Visible => LabModel(holder),
            NTreasureRoomRelicHolder holder => holder.Relic.Model,
            NRewardButton { Reward: RelicReward reward } => reward.Relic,
            NRewardButton { Reward: PotionReward reward } => reward.Potion,
            NCreature creature when creature.IsFocused => (AbstractModel?)creature.Entity.Player?.Character ?? creature.Entity.Monster,
            NMultiplayerPlayerState player => player.Player.Character,
            NTopBarPortraitTip => RunManager.Instance.DebugOnlyGetState() is { } run ? LocalContext.GetMe(run)?.Character : null,
            NTopBarModifier modifier => TopModifier(modifier),
            _ => null,
        };
        if (model == null) return;
        var state = Focused.GetOrCreateValue(control);
        if (ReferenceEquals(state.Model, model) && state.Generation == RecentActivityEmotes.Generation) return;
        state.Model = model;
        state.Generation = RecentActivityEmotes.Generation;
        RecentActivityEmotes.Record(model);
    }

    internal static void Exit(Control control) => Focused.Remove(control);
}

[HarmonyPatch]
internal static class RecentActivityFocusPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => RecentActivityHover.Methods(true);
    [HarmonyPostfix]
    private static void Postfix(Control __instance) => RecentActivityHover.Enter(__instance);
}

[HarmonyPatch]
internal static class RecentActivityUnfocusPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => RecentActivityHover.Methods(false);
    [HarmonyPostfix]
    private static void Postfix(Control __instance) => RecentActivityHover.Exit(__instance);
}
