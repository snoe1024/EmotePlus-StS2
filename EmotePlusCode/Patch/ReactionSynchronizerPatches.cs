using EmotePlus.EmotePlusCode.Reaction;
using EmotePlus.EmotePlusCode.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Patch;

// Enhanced messages bypass the game's automatically assigned message IDs.
[HarmonyPatch(typeof(ReactionSynchronizer), nameof(ReactionSynchronizer.SendLocalReaction))]
public static class SendLocalReactionPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ReactionSynchronizer __instance, ReactionType type, Vector2 mouseScreenPos)
    {
        EmoteNetworking.Send(__instance.NetService, EmoteImages.FromVanilla(type), mouseScreenPos, LocalEmote.TakeDrift());
        return false;
    }
}

[HarmonyPatch(typeof(ReactionSynchronizer), MethodType.Constructor, typeof(INetGameService), typeof(NReactionContainer))]
public static class ReactionSynchronizerCtorPatch
{
    [HarmonyPostfix]
    public static void Postfix(ReactionSynchronizer __instance) => EmoteNetworking.Bind(__instance.NetService);
}

[HarmonyPatch(typeof(ReactionSynchronizer), "HandleReactionMessage")]
public static class VanillaReactionReceivePatch
{
    private static readonly AccessTools.FieldRef<ReactionSynchronizer, NReactionContainer> Container =
        AccessTools.FieldRefAccess<ReactionSynchronizer, NReactionContainer>("_container");

    [HarmonyPrefix]
    public static bool Prefix(ReactionSynchronizer __instance,
        MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Flavor.ReactionMessage message, ulong senderId)
    {
        EmoteNetworking.ReceiveVanilla(__instance.NetService, Container(__instance), message, senderId);
        return false;
    }
}

// NCardGrid._Process scrolls the card library (and the other card grids): same reasoning as for the map below.
[HarmonyPatch(typeof(NCardGrid), nameof(NCardGrid._Process))]
public static class CardGridProcessPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        EmoteTracker.UpdateAll();
    }
}

// NScrollableContainer._Process scrolls most other screens (settings, collections, the Enchantment Compendium...).
[HarmonyPatch(typeof(NScrollableContainer), nameof(NScrollableContainer._Process))]
public static class ScrollableContainerProcessPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        EmoteTracker.UpdateAll();
    }
}

// NMapScreen._Process moves the map (scroll). Updating the emotes right after it uses the same frame's scroll
// position; SceneTree.ProcessFrame (which EmoteTracker also uses, for when the map is closed and NMapScreen does
// not process at all) runs before it and would lag one frame behind.
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Process))]
public static class MapScreenProcessPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        EmoteTracker.UpdateAll();
    }
}
