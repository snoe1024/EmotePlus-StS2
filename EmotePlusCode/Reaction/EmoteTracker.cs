using System.Collections.Generic;
using System.Linq;
using EmotePlus.EmotePlusCode.Ui;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Reaction;

// Displays a remote emote according to where its sender and the local player each are in the UI tree (UiNodeId),
// and keeps re-resolving that every frame while the emote is alive. With S = the sender's path and V = the
// viewer's path:
//
//   S == V               same node: at the sender's position. On the map that is the map-space position
//                        (re-projected with the viewer's own scroll, clamped inside the screen below the header).
//   V is an ancestor     the viewer is above the sender: pinned on the button that opens the sender's branch.
//   S is an ancestor     the viewer is below the sender: drawn as part of the sender's scene (if it is drawn),
//                        at the sender's screen position.
//   otherwise            the nearest common ancestor A: pinned on A's button towards the sender's branch, drawn
//                        as part of A's scene.
//   (no path / no button / scene not alive)  not drawn.
//
// Layers: where the emote node lives decides what it is drawn above/below. An emote is created lazily in the layer
// its current placement asks for (and kept, hidden, when the placement moves on):
//   Top         child of the ReactionContainer: above all run UI
//   Room        child of NRun right after RoomContainer: above the room, below the map/overlays/capstones
//   UnderHeader child of NGlobalUi right after the MapScreen: above the map, below the header bar
//   PlayerList  child of NGlobalUi right after the player list (the widgets that open the player detail screens):
//               the same depth as those widgets, i.e. above the map and capstones
//   Scene       child of that screen's own node (hidden/covered exactly like the screen)
// A layer needed later than LateLayerMs after the spawn is not created (its animation would restart).
//
// The emote's own drift: NReaction.DoAnim tweens the emote node's `position` by 40-60px in a random direction.
// The emote sits inside a holder Control and every frame the holder is moved so that the emote's *final* center
// lands where the placement says (pinned: drift cancelled; otherwise the drift is kept, and clamped if needed).
//
// The holder MUST be the same size as the reaction container: reaction.tscn anchors the emote to its parent's
// full rect with offsets (-40,-40,-1880,-1040), i.e. it is only 80x80 when the parent is 1920x1080. Under a
// zero-sized holder the emote collapses to 0x0 and is invisible.
public static class EmoteTracker
{
    // The map is clamped like every other scrolling view (see ScrollSpace.ClampInset).
    private const float ClampMargin = ScrollSpace.ClampInset;
    private const ulong MaxLifeMs = 2000;
    private const ulong LateLayerMs = 500;

    private enum LayerKind
    {
        Top,
        Room,
        UnderHeader,
        PlayerList,
        Scene,
    }

    private readonly record struct LayerSpec(LayerKind Kind, UiNodeId Scene = default)
    {
        public override string ToString() => Kind == LayerKind.Scene ? $"Scene({Scene})" : Kind.ToString();
    }

    private enum Mode
    {
        Pin,
        Drift,
    }

    // Bounds: if set, the emote's final center is clamped into this global rect.
    private readonly record struct Placement(LayerSpec Layer, Vector2 Target, Mode Mode, Rect2? Bounds = null);

    private sealed class Layer
    {
        public required LayerSpec Spec;
        public required Control Holder;
        public required NReaction Emote;
        public required Vector2 BasePosition;
        public bool Started;
    }

    private sealed class Entry
    {
        public required NReactionContainer Container;
        public required ReactionType Type;
        public required UiNodeId[] SenderPath;
        public required Vector2 Position;
        public required ulong SpawnedMs;
        public readonly List<Layer> Layers = new();
        public LayerSpec? Active;
    }

    private static readonly List<Entry> Entries = new();
    private static SceneTree? _subscribedTree;

    public static void Spawn(NReactionContainer container, ReactionType type, UiNodeId[] senderPath, Vector2 position)
    {
        if (!GodotObject.IsInstanceValid(container) || !container.IsInsideTree())
        {
            return;
        }

        var entry = new Entry
        {
            Container = container,
            Type = type,
            SenderPath = senderPath,
            Position = position,
            SpawnedMs = Time.GetTicksMsec(),
        };
        Entries.Add(entry);
        var viewer = UiTree.LocalPath();
        EmoteLog.Info($"spawn type={type} sender={UiPath.Describe(senderPath)} viewer={UiPath.Describe(viewer)} pos={position}");
        Update(entry, viewer, entry.SpawnedMs);
        Subscribe(container.GetTree());
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

    // Called every frame from SceneTree.ProcessFrame (the map is closed -> NMapScreen does not process), and again
    // right after NMapScreen._Process (see MapScreenProcessPatch) so the emote uses the map's scroll position of
    // the *same* frame (ProcessFrame alone would lag one frame behind it).
    public static void UpdateAll()
    {
        if (Entries.Count > 0)
        {
            var viewer = UiTree.LocalPath();
            var now = Time.GetTicksMsec();
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var entry = Entries[i];
                if (!GodotObject.IsInstanceValid(entry.Container) || now - entry.SpawnedMs > MaxLifeMs)
                {
                    // Layers free themselves when their emote's animation ends (or with their parent).
                    Entries.RemoveAt(i);
                    continue;
                }

                Update(entry, viewer, now);
            }
        }

        if (Entries.Count == 0)
        {
            Unsubscribe();
        }
    }

    private static void Update(Entry entry, UiNodeId[] viewer, ulong now)
    {
        var placement = Resolve(entry, viewer);
        var spec = placement?.Layer;
        if (spec != entry.Active)
        {
            EmoteLog.Info($"placement {entry.Active?.ToString() ?? "<hidden>"} -> {spec?.ToString() ?? "<hidden>"}" +
                          $"{(placement.HasValue ? $" target={placement.Value.Target} mode={placement.Value.Mode}" : "")}" +
                          $" viewer={UiPath.Describe(viewer)}");
            entry.Active = spec;
        }

        entry.Layers.RemoveAll(l => !GodotObject.IsInstanceValid(l.Holder) || !GodotObject.IsInstanceValid(l.Emote));
        if (placement.HasValue && !entry.Layers.Exists(l => l.Spec == placement.Value.Layer) &&
            now - entry.SpawnedMs <= LateLayerMs)
        {
            CreateLayer(entry, placement.Value.Layer);
        }

        foreach (var layer in entry.Layers)
        {
            var shown = placement.HasValue && layer.Spec == placement.Value.Layer;
            layer.Holder.Visible = shown;
            if (shown)
            {
                PositionLayer(entry, layer, placement!.Value);
            }
        }
    }

    private static void PositionLayer(Entry entry, Layer layer, Placement placement)
    {
        layer.Holder.Size = entry.Container.Size;
        var drift = layer.Emote.Position - layer.BasePosition;
        var center = placement.Mode == Mode.Pin ? placement.Target : placement.Target + drift;
        if (placement.Bounds is { } bounds)
        {
            center = new Vector2(
                Mathf.Clamp(center.X, bounds.Position.X, Mathf.Max(bounds.Position.X, bounds.End.X)),
                Mathf.Clamp(center.Y, bounds.Position.Y, Mathf.Max(bounds.Position.Y, bounds.End.Y)));
        }

        // emote center = holder origin + emote.Position + pivot (the scene's pivot is the 80x80 emote's center)
        layer.Holder.GlobalPosition = center - layer.Emote.Position - layer.Emote.PivotOffset;
        if (!layer.Started)
        {
            layer.Started = true;
            layer.Emote.BeginAnim();
        }
    }

    private static void CreateLayer(Entry entry, LayerSpec spec)
    {
        Node? parent = null;
        Node? placeAfter = null;
        switch (spec.Kind)
        {
            case LayerKind.Top:
                parent = entry.Container;
                break;
            case LayerKind.Room:
                parent = NRun.Instance;
                placeAfter = NRun.Instance?.GetNodeOrNull<Control>("%RoomContainer");
                break;
            case LayerKind.UnderHeader:
            {
                var globalUi = NRun.Instance?.GlobalUi;
                var map = NMapScreen.Instance;
                if (globalUi != null && map != null && map.GetParent() == globalUi)
                {
                    parent = globalUi;
                    placeAfter = map;
                }

                break;
            }
            case LayerKind.PlayerList:
            {
                var globalUi = NRun.Instance?.GlobalUi;
                var playerList = globalUi?.MultiplayerPlayerContainer;
                if (globalUi != null && playerList != null && playerList.GetParent() == globalUi)
                {
                    parent = globalUi;
                    placeAfter = playerList;
                }

                break;
            }
            case LayerKind.Scene:
                parent = UiTree.FindSceneNode(spec.Scene);
                break;
        }

        if (parent == null || !GodotObject.IsInstanceValid(parent))
        {
            EmoteLog.Info($"cannot create layer {spec}: parent is not available");
            return;
        }

        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = entry.Container.Size };
        var emote = NReaction.Create(entry.Type);
        parent.AddChildSafely(holder);
        if (placeAfter != null && placeAfter.GetParent() == parent)
        {
            parent.MoveChildSafely(holder, placeAfter.GetIndex() + 1);
        }

        holder.AddChildSafely(emote);
        emote.TreeExited += () =>
        {
            if (GodotObject.IsInstanceValid(holder))
            {
                holder.QueueFreeSafely();
            }
        };
        entry.Layers.Add(new Layer { Spec = spec, Holder = holder, Emote = emote, BasePosition = emote.Position });
        EmoteLog.Info($"created layer {spec} under {parent.Name}");
    }

    private static Placement? Resolve(Entry entry, UiNodeId[] viewer)
    {
        var sender = entry.SenderPath;
        var common = UiPath.CommonLength(sender, viewer);
        if (common == 0)
        {
            return null; // not in a run on one of the sides
        }

        var owner = SharedRegionOwner(sender[^1].Kind);
        if (owner != null)
        {
            // An emote made on a region that belongs to a whole screen instead of to one of its pages (the card
            // library's control panel, the bestiary's monster list): it looks the same on every page, so anyone
            // inside that screen (any page, or an enlarged card) sees it in that region.
            var screen = viewer.ToList().FindIndex(n => n.Kind == owner);
            if (screen >= 0)
            {
                var inRegion = EmoteCoordinates.ToScreen(sender[^1], entry.Position, entry.Container);
                return inRegion == null
                    ? null
                    : new Placement(SceneLayerOf(viewer[screen]), inRegion.Value, Mode.Drift,
                        UiTree.GetScrollSpace(sender[^1])?.Bounds);
            }
        }

        if (common == sender.Length && common == viewer.Length)
        {
            // Same node: at the sender's position, which on a scrolling screen moves with the content.
            var node = sender[^1];
            var onScreen = EmoteCoordinates.ToScreen(node, entry.Position, entry.Container);
            if (onScreen == null)
            {
                return null;
            }

            if (node.Kind == UiNodeKind.Map)
            {
                return new Placement(new LayerSpec(LayerKind.UnderHeader), onScreen.Value, Mode.Drift,
                    MapBounds(entry.Container.Size));
            }

            var scroll = UiTree.GetScrollSpace(node);
            return scroll != null
                ? new Placement(SceneLayerOf(node), onScreen.Value, Mode.Drift, scroll.Value.Bounds)
                : new Placement(new LayerSpec(LayerKind.Top), onScreen.Value, Mode.Drift);
        }

        if (common == viewer.Length)
        {
            // The viewer is above the sender: pin on the button that opens the sender's branch.
            return PinOnButton(new LayerSpec(LayerKind.Top), viewer[^1], sender[common]);
        }

        if (common == sender.Length)
        {
            // The viewer is below the sender: part of the sender's scene, at the sender's position there.
            var node = sender[^1];
            var onScreen = EmoteCoordinates.ToScreen(node, entry.Position, entry.Container);
            return onScreen == null
                ? null
                : new Placement(SceneLayerOf(node), onScreen.Value, Mode.Drift, UiTree.GetScrollSpace(node)?.Bounds);
        }

        // Different branches: the nearest common ancestor draws it, pinned on its button towards the sender.
        var ancestor = sender[common - 1];
        var button = UiTree.FindButton(ancestor, sender[common]);
        if (button == null)
        {
            return null;
        }

        // The base scene's buttons (top bar, player list) are drawn above the map, so a viewer on the map must see
        // the emote at the button's own depth. Under a capstone they are hidden anyway: the Room layer, which the
        // capstone covers, is right.
        var layer = ancestor.Kind == UiNodeKind.Base && viewer[^1].Kind == UiNodeKind.Map
            ? new LayerSpec(UiTree.IsInPlayerList(button.Value.Control) ? LayerKind.PlayerList : LayerKind.Top)
            : SceneLayerOf(ancestor);
        return PinAt(layer, button.Value);
    }

    // The screen that a "shared region" node (see UiNode.cs) belongs to.
    private static UiNodeKind? SharedRegionOwner(UiNodeKind kind)
    {
        return kind switch
        {
            UiNodeKind.CardLibraryPanel => UiNodeKind.CardLibrary,
            UiNodeKind.BestiaryList => UiNodeKind.Bestiary,
            _ => null,
        };
    }

    private static Placement? PinOnButton(LayerSpec layer, UiNodeId parent, UiNodeId child)
    {
        var button = UiTree.FindButton(parent, child);
        return button == null ? null : PinAt(layer, button.Value);
    }

    private static Placement PinAt(LayerSpec layer, ButtonTarget button)
    {
        return new Placement(layer, button.Point(), Mode.Pin, button.Bounds);
    }

    private static LayerSpec SceneLayerOf(UiNodeId node)
    {
        return node.Kind switch
        {
            UiNodeKind.Base => new LayerSpec(LayerKind.Room),
            UiNodeKind.Map => new LayerSpec(LayerKind.UnderHeader),
            _ => new LayerSpec(LayerKind.Scene, node),
        };
    }

    // The map fills the screen: keep the emote's center inside it, and below the header bar so it never looks like
    // it was sent to the header itself.
    private static Rect2 MapBounds(Vector2 screenSize)
    {
        var top = ClampMargin;
        var bg = NRun.Instance?.GlobalUi.TopBar.GetNodeOrNull<Control>("BgImage");
        if (bg != null && GodotObject.IsInstanceValid(bg))
        {
            top = Mathf.Max(top, (bg.GetGlobalTransformWithCanvas() * new Vector2(0f, bg.Size.Y)).Y + ClampMargin);
        }

        return new Rect2(ClampMargin, top, screenSize.X - 2 * ClampMargin, screenSize.Y - ClampMargin - top);
    }
}
