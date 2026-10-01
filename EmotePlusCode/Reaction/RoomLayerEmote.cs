using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Reaction;

namespace EmotePlus.EmotePlusCode.Reaction;

// An emote sent from the base scene (Room), shown to a viewer who is in a child screen of the Room (map, player
// detail): it is drawn as part of the Room, i.e. UNDER the viewer's screen. It is hidden where that screen is
// opaque and shows through where it is not.
//
// The holder goes directly under NRun, right after RoomContainer (above the room, below GlobalUi which holds the
// map / overlays / capstones). It is deliberately not put inside RoomContainer: NSceneContainer.SetCurrentScene
// frees all of its children on every room change.
// Position is fixed at spawn (no tracking): the Room itself does not scroll. The emote's own drift is left alone.
public static class RoomLayerEmote
{
    public static bool TrySpawn(NReactionContainer container, ReactionType type, Vector2 position)
    {
        var run = NRun.Instance;
        var room = run?.GetNodeOrNull<Control>("%RoomContainer");
        if (run == null || room == null)
        {
            return false;
        }

        // Same size as the reaction container, see MapEmoteTracker (reaction.tscn anchors the emote to its parent).
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = container.Size };
        var emote = NReaction.Create(type);
        run.AddChildSafely(holder);
        run.MoveChildSafely(holder, room.GetIndex() + 1);
        holder.AddChildSafely(emote);

        // Center the emote on `position` (same convention as MapEmoteTracker: center = holder + Position + pivot).
        holder.GlobalPosition = position - emote.Position - emote.PivotOffset;
        emote.TreeExited += () =>
        {
            if (GodotObject.IsInstanceValid(holder))
            {
                holder.QueueFreeSafely();
            }
        };
        emote.BeginAnim();
        EmoteLog.Info($"room-layer emote type={type} pos={position} {EmoteLog.DescribeViewer()}");
        return true;
    }
}
