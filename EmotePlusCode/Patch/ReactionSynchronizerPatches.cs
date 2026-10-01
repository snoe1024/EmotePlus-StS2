using EmotePlus.EmotePlusCode.Reaction;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Patch;

// Vanilla sends a ReactionMessage with a viewport-normalized position. While the local player has the map open
// we send a MapEmoteMessage with a map-space position instead, and handle it on the receiving side.
// All other cases still go through the vanilla message untouched.
internal static class MapEmoteHandler
{
    public static void Handle(MapEmoteMessage message, ulong senderId)
    {
        var container = NGame.Instance?.ReactionContainer;
        EmoteLog.Info($"received MapEmoteMessage from {senderId}: type={message.type} net={message.netPosition} " +
                      EmoteLog.DescribeViewer());
        if (container == null)
        {
            EmoteLog.Info("NGame.Instance.ReactionContainer is null, dropping");
            return;
        }

        MapEmoteTracker.Spawn(container, message.type, message.netPosition);
    }
}

[HarmonyPatch(typeof(ReactionSynchronizer), nameof(ReactionSynchronizer.SendLocalReaction))]
public static class SendLocalReactionPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ReactionSynchronizer __instance, ReactionType type, Vector2 mouseScreenPos)
    {
        if (ViewerContext.Current != UiContext.Map || NMapScreen.Instance == null)
        {
            EmoteLog.Info($"send vanilla ReactionMessage type={type} screenPos={mouseScreenPos} {EmoteLog.DescribeViewer()}");
            return true;
        }

        var netPosition = NMapScreen.Instance.GetNetPositionFromScreenPosition(mouseScreenPos);
        EmoteLog.Info($"send MapEmoteMessage type={type} screenPos={mouseScreenPos} net={netPosition} {EmoteLog.DescribeViewer()}");
        __instance.NetService.SendMessage(new MapEmoteMessage
        {
            type = type,
            netPosition = netPosition,
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
        __instance.NetService.RegisterMessageHandler<MapEmoteMessage>(MapEmoteHandler.Handle);
    }
}

[HarmonyPatch(typeof(ReactionSynchronizer), nameof(ReactionSynchronizer.Dispose))]
public static class ReactionSynchronizerDisposePatch
{
    [HarmonyPrefix]
    public static void Prefix(ReactionSynchronizer __instance)
    {
        __instance.NetService.UnregisterMessageHandler<MapEmoteMessage>(MapEmoteHandler.Handle);
    }
}

// Remote emotes that arrive through the vanilla ReactionMessage were sent from the base scene (Room), at a
// screen position. A viewer who is in a child screen of the Room (map, player detail) sees them as a child of the
// Room, i.e. under that screen: see RoomLayerEmote. Every other viewer keeps the vanilla display.
[HarmonyPatch(typeof(NReactionContainer), nameof(NReactionContainer.DoRemoteReaction))]
public static class DoRemoteReactionPatch
{
    [HarmonyPrefix]
    public static bool Prefix(NReactionContainer __instance, ReactionType type, Vector2 position)
    {
        var context = ViewerContext.Current;
        EmoteLog.Info($"vanilla DoRemoteReaction type={type} controlSpacePos={position} {EmoteLog.DescribeViewer()}");
        if (context is not (UiContext.Map or UiContext.PlayerDetail))
        {
            return true;
        }

        return !RoomLayerEmote.TrySpawn(__instance, type, position);
    }
}

// NMapScreen._Process moves the map (scroll). Updating the emotes right after it uses the same frame's scroll
// position; SceneTree.ProcessFrame alone runs before it and would lag one frame behind.
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Process))]
public static class MapScreenProcessPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        MapEmoteTracker.UpdateAll();
    }
}
