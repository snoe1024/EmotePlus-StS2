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

// Vanilla sends a ReactionMessage with a viewport-normalized position. We always send a ContextEmoteMessage instead:
// inside a run the sender's UI path + a position in that node's coordinate system (displayed by EmoteTracker),
// outside a run (lobbies) an empty path + a normalized screen position (displayed by LobbyEmote).
internal static class ContextEmoteHandler
{
    public static void Handle(ContextEmoteMessage message, ulong senderId)
    {
        var container = NGame.Instance?.ReactionContainer;
        EmoteLog.Info($"received from {senderId}: type={message.type} sender={UiPath.Describe(message.path ?? [])}");
        if (container == null || message.path == null)
        {
            return;
        }

        if (message.path.Length == 0)
        {
            LobbyEmote.Show(container, message.type, senderId,
                NetCursorHelper.GetControlSpacePosition(message.position, container), message.drift);
            return;
        }

        EmoteTracker.Spawn(container, message.type, senderId, message.path, message.position, message.inHeader,
            message.drift);
    }
}

[HarmonyPatch(typeof(ReactionSynchronizer), nameof(ReactionSynchronizer.SendLocalReaction))]
public static class SendLocalReactionPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ReactionSynchronizer __instance, ReactionType type, Vector2 mouseScreenPos)
    {
        var path = UiTree.SenderPath(mouseScreenPos);
        var container = NGame.Instance?.ReactionContainer;
        if (container == null)
        {
            return true;
        }

        if (path.Length == 0)
        {
            // Not in a run (a lobby): just the screen position.
            __instance.NetService.SendMessage(new ContextEmoteMessage
            {
                type = EmoteImages.FromVanilla(type), path = [], drift = LocalEmote.TakeDrift(),
                position = NetCursorHelper.GetNormalizedPosition(mouseScreenPos, container),
            });
            return false;
        }

        // The position is in the coordinate system of the last node of the path (see ContextEmoteMessage), unless
        // the emote was made on the header bar (then it is a screen position).
        var inHeader = UiTree.ShowsHeader(UiTree.LocalPath(mouseScreenPos)) && UiTree.IsOnHeader(mouseScreenPos);
        var position = EmoteCoordinates.PositionFor(path, mouseScreenPos, container, inHeader);
        EmoteLog.Info($"send type={type} sender={UiPath.Describe(path)} screenPos={mouseScreenPos} pos={position} inHeader={inHeader}");
        __instance.NetService.SendMessage(new ContextEmoteMessage
        {
            type = EmoteImages.FromVanilla(type), path = path, position = position, inHeader = inHeader,
            drift = LocalEmote.TakeDrift(),
        });
        return false;
    }
}

[HarmonyPatch(typeof(ReactionSynchronizer), MethodType.Constructor,
    typeof(INetGameService), typeof(NReactionContainer))]
public static class ReactionSynchronizerCtorPatch
{
    [HarmonyPostfix]
    public static void Postfix(ReactionSynchronizer __instance)
    {
        __instance.NetService.RegisterMessageHandler<ContextEmoteMessage>(ContextEmoteHandler.Handle);
    }
}

[HarmonyPatch(typeof(ReactionSynchronizer), nameof(ReactionSynchronizer.Dispose))]
public static class ReactionSynchronizerDisposePatch
{
    [HarmonyPrefix]
    public static void Prefix(ReactionSynchronizer __instance)
    {
        __instance.NetService.UnregisterMessageHandler<ContextEmoteMessage>(ContextEmoteHandler.Handle);
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
