using EmotePlus.EmotePlusCode.Reaction;
using EmotePlus.EmotePlusCode.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Patch;

// Vanilla sends a ReactionMessage with a viewport-normalized position. Inside a run we send a ContextEmoteMessage
// instead (the sender's UI path + a position in that node's coordinate system) and display it with EmoteTracker.
// Outside a run (lobbies) the vanilla message is left alone.
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

        EmoteTracker.Spawn(container, message.type, message.path, message.position);
    }
}

// The local player's own emote goes through the same tracker as everybody else's (as an emote whose sender is the
// local player), so that it behaves the same way: e.g. on the scrolling map it sticks to the map instead of to the
// screen. Vanilla's DoLocalReaction would draw it at the screen position and then send it; we do both ourselves.
[HarmonyPatch(typeof(NReactionContainer), nameof(NReactionContainer.DoLocalReaction))]
public static class DoLocalReactionPatch
{
    private static readonly System.Func<Texture2D, ReactionType> TextureToType =
        AccessTools.MethodDelegate<System.Func<Texture2D, ReactionType>>(AccessTools.Method(typeof(NReaction), "TextureToType"));

    private static readonly AccessTools.FieldRef<NReactionContainer, ReactionSynchronizer?> Synchronizer =
        AccessTools.FieldRefAccess<NReactionContainer, ReactionSynchronizer?>("_synchronizer");

    [HarmonyPrefix]
    public static bool Prefix(NReactionContainer __instance, Texture2D tex, Vector2 position)
    {
        var path = UiTree.LocalPath(position);
        if (path.Length == 0)
        {
            return true; // not in a run: vanilla
        }

        var type = TextureToType(tex);
        EmoteTracker.Spawn(__instance, type, path, EmoteCoordinates.FromScreen(path, position, __instance));
        Synchronizer(__instance)?.SendLocalReaction(type, position);
        return false;
    }
}

[HarmonyPatch(typeof(ReactionSynchronizer), nameof(ReactionSynchronizer.SendLocalReaction))]
public static class SendLocalReactionPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ReactionSynchronizer __instance, ReactionType type, Vector2 mouseScreenPos)
    {
        var path = UiTree.LocalPath(mouseScreenPos);
        var container = NGame.Instance?.ReactionContainer;
        if (path.Length == 0 || container == null)
        {
            EmoteLog.Info($"send vanilla ReactionMessage type={type} (not in a run)");
            return true;
        }

        // The position is in the coordinate system of the last node of the path (see ContextEmoteMessage).
        var position = EmoteCoordinates.FromScreen(path, mouseScreenPos, container);
        EmoteLog.Info($"send type={type} sender={UiPath.Describe(path)} screenPos={mouseScreenPos} pos={position}");
        __instance.NetService.SendMessage(new ContextEmoteMessage { type = type, path = path, position = position });
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
