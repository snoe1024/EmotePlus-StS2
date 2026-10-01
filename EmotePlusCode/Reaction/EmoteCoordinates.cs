using System.Collections.Generic;
using EmotePlus.EmotePlusCode.Ui;
using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Reaction;

// Every node has its own coordinate system for the positions that go over the wire (see ContextEmoteMessage).
// Scrolling screens must use one that moves with their content, so that an emote sticks to what was under the
// cursor when the viewer (or the sender) scrolls:
//   Map                   NMapScreen's map-space net position
//   scrolling lists/grids the position inside the scrolled content, in units of 1000 px (UiTree.GetScrollSpace)
//   everything else       NetCursorHelper's normalized screen position
public static class EmoteCoordinates
{
    // Wide enough for scroll-content coordinates (a few thousand px / 1000); still sub-pixel precise at 16 bits.
    public static readonly QuantizeParams Quantize = new(-30f, 30f, 16);

    /// <summary>
    /// The position to put in an emote for a screen position: in the last node's coordinate system, or a plain
    /// normalized screen position if the emote was made on the header bar.
    /// </summary>
    public static Vector2 PositionFor(IReadOnlyList<UiNodeId> path, Vector2 screenPosition, NReactionContainer container,
        bool inHeader)
    {
        return inHeader
            ? NetCursorHelper.GetNormalizedPosition(screenPosition, container)
            : FromScreen(path, screenPosition, container);
    }

    /// <summary>Converts a screen position into the coordinate system of the last node of the path.</summary>
    public static Vector2 FromScreen(IReadOnlyList<UiNodeId> path, Vector2 screenPosition, NReactionContainer container)
    {
        var node = path[^1];
        if (node.Kind == UiNodeKind.Map && NMapScreen.Instance != null)
        {
            return NMapScreen.Instance.GetNetPositionFromScreenPosition(screenPosition);
        }

        var scroll = UiTree.GetScrollSpace(node);
        if (scroll != null)
        {
            return scroll.Value.ScreenToLocal(screenPosition) / ScrollUnit;
        }

        return NetCursorHelper.GetNormalizedPosition(screenPosition, container);
    }

    /// <summary>
    /// The reverse of <see cref="FromScreen"/> for the viewer's own screen (control space of the container), or
    /// null if the node's screen is not available.
    /// </summary>
    public static Vector2? ToScreen(UiNodeId node, Vector2 position, NReactionContainer container)
    {
        if (node.Kind == UiNodeKind.Map)
        {
            var map = NMapScreen.Instance;
            return map == null
                ? null
                : container.GetGlobalTransformWithCanvas() * map.GetScreenPositionFromNetPosition(position);
        }

        var scroll = UiTree.GetScrollSpace(node);
        if (scroll != null)
        {
            return container.GetGlobalTransformWithCanvas() * scroll.Value.LocalToScreen(position * ScrollUnit);
        }

        return NetCursorHelper.GetControlSpacePosition(position, container);
    }

    private const float ScrollUnit = 1000f;
}
