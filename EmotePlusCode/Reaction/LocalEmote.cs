using EmotePlus.EmotePlusCode.Patch;
using EmotePlus.EmotePlusCode.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Reaction;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>What happens when the local player picks an emote on the wheel.</summary>
public static class LocalEmote
{
    private static readonly AccessTools.FieldRef<NReactionContainer, ReactionSynchronizer?> Synchronizer =
        AccessTools.FieldRefAccess<NReactionContainer, ReactionSynchronizer?>("_synchronizer");

    private static EmoteDrift? _pendingDrift;

    /// <summary>The drift of the emote being sent (made by <see cref="Do"/>).</summary>
    public static EmoteDrift TakeDrift()
    {
        var drift = _pendingDrift ?? EmoteDrift.Random();
        _pendingDrift = null;
        return drift;
    }

    /// <summary>
    /// Shows the emote for the local player and sends it. The local player's own emote goes through the same
    /// tracker as everybody else's (as an emote whose sender is the local player), so that it behaves the same way:
    /// e.g. on the scrolling map it sticks to the map instead of to the screen.
    /// </summary>
    public static void Do(NReactionContainer container, EmoteType type, Vector2 screenPosition)
    {
        // Decided here, for the emote shown here and the one sent (SendLocalReactionPatch takes it from _pendingDrift).
        var drift = EmoteDrift.Random();
        _pendingDrift = drift;
        var path = UiTree.LocalPath(screenPosition);
        // LocalContext.NetId is only set while a run is going: in a lobby, ask the connection.
        var me = Synchronizer(container)?.NetService.NetId ?? LocalContext.NetId ?? 0UL;
        if (path.Length == 0)
        {
            LobbyEmote.Show(container, type, me, screenPosition, drift);
        }
        else
        {
            var inHeader = UiTree.ShowsHeader(path) && UiTree.IsOnHeader(screenPosition);
            EmoteTracker.Spawn(container, type, me, path,
                EmoteCoordinates.PositionFor(path, screenPosition, container, inHeader), inHeader, drift);
        }

        // The synchronizer's parameter is the vanilla enum, but SendLocalReactionPatch takes over and only reads
        // the number (see EmoteType).
        Synchronizer(container)?.SendLocalReaction((ReactionType)(int)type, screenPosition);
    }
}

/// <summary>
/// An emote outside a run (a lobby): drawn at the screen position, with the outline in the color of the sender's
/// picked character (<see cref="EmoteTracker.NeutralOutline"/> for the random character or no lobby character).
/// </summary>
public static class LobbyEmote
{
    public static void Show(NReactionContainer container, EmoteType type, ulong senderId, Vector2 position,
        EmoteDrift drift)
    {
        var character = UiTree.LobbyCharacter(senderId);
        var emote = EmoteTracker.CreateEmote(type, EmoteTracker.OutlineColorOf(character));
        emote.SetMeta(ReactionAnimPatch.DriftMeta, drift.Offset);
        container.AddChildSafely(emote);
        emote.GlobalPosition = position - emote.Size / 2f;
        emote.BeginAnim();
    }
}
