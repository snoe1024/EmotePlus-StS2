using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Reaction;

// Displays a remote emote that was sent from the map screen, and keeps re-resolving where it should be drawn
// every frame while it is alive:
//   viewer on the map          -> the emote's map-space position, re-projected with the current scroll offset,
//                                  clamped to the area below the header bar; drawn above the map but BELOW the
//                                  header (so it cannot look like an emote on the header itself)
//   viewer in the base scene   -> exactly on the top bar's map button; drawn above everything
//   viewer anywhere else       -> hidden
//
// Layers: one emote is spawned as two copies ("layers"), because the two cases need different draw orders and
// reparenting a node that is mid-tween is fragile:
//   top layer          child of the ReactionContainer (above all run UI)
//   under-header layer child of NGlobalUi, right after the MapScreen (above the map, below the top bar)
// Only the layer matching the viewer's current context is visible.
//
// The emote's own drift: NReaction.DoAnim tweens the emote node's `position` by 40-60px in a random direction.
// The emote sits inside a holder Control and every frame the holder is moved so that the emote's *final* center
// lands on the resolved point (the drift is cancelled for the map button, and included in -- and so limited by --
// the clamp for the map).
//
// The holder MUST be the same size as the reaction container: reaction.tscn anchors the emote to its parent's
// full rect with offsets (-40,-40,-1880,-1040), i.e. it is only 80x80 when the parent is 1920x1080. Under a
// zero-sized holder the emote collapses to 0x0 and is invisible.
public static class MapEmoteTracker
{
    private const float ClampMargin = 48f;

    private sealed class Layer
    {
        public required Control Holder;
        public required NReaction Emote;
        public required Vector2 BasePosition;
        public required bool UnderHeader;
    }

    private sealed class Entry
    {
        public required NReactionContainer Container;
        public required Vector2 NetPosition;
        public readonly List<Layer> Layers = new();
        public int Frame;
    }

    private static readonly List<Entry> Entries = new();
    private static SceneTree? _subscribedTree;

    public static void Spawn(NReactionContainer container, ReactionType type, Vector2 netPosition)
    {
        if (!GodotObject.IsInstanceValid(container) || !container.IsInsideTree())
        {
            return;
        }

        var entry = new Entry { Container = container, NetPosition = netPosition };
        Entries.Add(entry);
        CreateLayer(entry, type, container, null, underHeader: false);

        var globalUi = NRun.Instance?.GlobalUi;
        var map = NMapScreen.Instance;
        if (globalUi != null && map != null && map.GetParent() == globalUi)
        {
            CreateLayer(entry, type, globalUi, map, underHeader: true);
        }

        EmoteLog.Info($"spawn type={type} net={netPosition} layers={entry.Layers.Count} {EmoteLog.DescribeViewer()}");
        Update(entry);
        Subscribe(container.GetTree());
        foreach (var layer in entry.Layers)
        {
            layer.Emote.BeginAnim();
        }
    }

    private static void CreateLayer(Entry entry, ReactionType type, Node parent, Node? placeAfter, bool underHeader)
    {
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = entry.Container.Size };
        var emote = NReaction.Create(type);
        parent.AddChildSafely(holder);
        if (placeAfter != null)
        {
            parent.MoveChildSafely(holder, placeAfter.GetIndex() + 1);
        }

        holder.AddChildSafely(emote);

        var layer = new Layer { Holder = holder, Emote = emote, BasePosition = emote.Position, UnderHeader = underHeader };
        entry.Layers.Add(layer);
        emote.TreeExited += () => OnLayerExited(entry, layer);
    }

    private static void OnLayerExited(Entry entry, Layer layer)
    {
        if (GodotObject.IsInstanceValid(layer.Holder))
        {
            layer.Holder.QueueFreeSafely();
        }

        entry.Layers.Remove(layer);
        if (entry.Layers.Count == 0)
        {
            EmoteLog.Info($"emote finished (frames alive={entry.Frame})");
            Entries.Remove(entry);
            if (Entries.Count == 0)
            {
                Unsubscribe();
            }
        }
    }

    private static void Subscribe(SceneTree tree)
    {
        if (_subscribedTree != null)
        {
            return;
        }

        _subscribedTree = tree;
        tree.ProcessFrame += UpdateAll;
    }

    private static void Unsubscribe()
    {
        if (_subscribedTree == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_subscribedTree))
        {
            _subscribedTree.ProcessFrame -= UpdateAll;
        }

        _subscribedTree = null;
    }

    // Called every frame from SceneTree.ProcessFrame, and again right after NMapScreen._Process (see
    // MapScreenProcessPatch) so the emote uses the map's scroll position of the *same* frame (no 1-frame lag).
    public static void UpdateAll()
    {
        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            var entry = Entries[i];
            if (!GodotObject.IsInstanceValid(entry.Container))
            {
                // Everything is being torn down; the layers free themselves with their parents.
                EmoteLog.Info($"container gone, dropping entry (frames alive={entry.Frame})");
                Entries.RemoveAt(i);
                continue;
            }

            Update(entry);
        }

        if (Entries.Count == 0)
        {
            Unsubscribe();
        }
    }

    private static void Update(Entry entry)
    {
        var context = ViewerContext.Current;
        var target = ResolveTarget(entry, context);

        foreach (var layer in entry.Layers)
        {
            if (!GodotObject.IsInstanceValid(layer.Holder) || !GodotObject.IsInstanceValid(layer.Emote))
            {
                continue;
            }

            var shown = target.HasValue && (context == UiContext.Map ? layer.UnderHeader : !layer.UnderHeader);
            layer.Holder.Visible = shown;
            if (!shown)
            {
                continue;
            }

            layer.Holder.Size = entry.Container.Size;
            var drift = layer.Emote.Position - layer.BasePosition;
            Vector2 center;
            if (context == UiContext.Map)
            {
                center = ClampToMapArea(target!.Value + drift, entry.Container.Size);
            }
            else
            {
                center = target!.Value; // pinned on the button: the drift is cancelled out below
            }

            // emote center = holder origin + emote.Position + pivot (the scene's pivot is the 80x80 emote's center)
            layer.Holder.GlobalPosition = center - layer.Emote.Position - layer.Emote.PivotOffset;
        }

        if (entry.Frame++ % 30 == 0)
        {
            EmoteLog.Info($"frame={entry.Frame - 1} viewer={context} target={target?.ToString() ?? "<hidden>"} layers=" +
                          string.Join(", ", entry.Layers.ConvertAll(DescribeLayer)));
        }
    }

    private static string DescribeLayer(Layer layer)
    {
        if (!GodotObject.IsInstanceValid(layer.Holder) || !GodotObject.IsInstanceValid(layer.Emote))
        {
            return "<freed>";
        }

        return $"{(layer.UnderHeader ? "underHeader" : "top")}(visible={layer.Holder.IsVisibleInTree()}, " +
               $"center={layer.Holder.GlobalPosition + layer.Emote.Position + layer.Emote.PivotOffset}, " +
               $"emoteSize={layer.Emote.Size}, alpha={layer.Emote.Modulate.A})";
    }

    // The point the emote should be centered on, before the drift/clamp are applied. Null = don't draw.
    private static Vector2? ResolveTarget(Entry entry, UiContext context)
    {
        switch (context)
        {
            case UiContext.Map:
            {
                var map = NMapScreen.Instance;
                if (map == null)
                {
                    return null;
                }

                var screenPosition = map.GetScreenPositionFromNetPosition(entry.NetPosition);
                return entry.Container.GetGlobalTransformWithCanvas() * screenPosition;
            }
            case UiContext.Room:
            {
                var button = NRun.Instance?.GlobalUi.TopBar.Map;
                if (button == null || !GodotObject.IsInstanceValid(button))
                {
                    return null;
                }

                return button.GlobalPosition + button.Size * 0.5f;
            }
            default:
                return null;
        }
    }

    // Keeps the emote's center inside the screen, and below the header bar so it never looks like it was sent to
    // the header itself.
    private static Vector2 ClampToMapArea(Vector2 center, Vector2 screenSize)
    {
        var top = ClampMargin;
        var bg = NRun.Instance?.GlobalUi.TopBar.GetNodeOrNull<Control>("BgImage");
        if (bg != null && GodotObject.IsInstanceValid(bg))
        {
            top = Mathf.Max(top, (bg.GetGlobalTransformWithCanvas() * new Vector2(0f, bg.Size.Y)).Y + ClampMargin * 0.5f);
        }

        return new Vector2(
            Mathf.Clamp(center.X, ClampMargin, screenSize.X - ClampMargin),
            Mathf.Clamp(center.Y, top, screenSize.Y - ClampMargin));
    }
}
