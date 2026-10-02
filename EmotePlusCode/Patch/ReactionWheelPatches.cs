using EmotePlus.EmotePlusCode.Config;
using EmotePlus.EmotePlusCode.Reaction;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Reaction;

namespace EmotePlus.EmotePlusCode.Patch;

/// <summary>
/// The emote wheel with pages. Replaces NReactionWheel._Input, which still has the 8 wedges (the slots of a page):
/// - the emotes of the current page are put on the wedges (a wedge without an emote on the last page is hidden);
/// - released with the mouse (almost) not moved: no emote;
/// - while the wheel is open the mouse buttons turn the page (left: back, right: next, see the config) and do
///   nothing else;
/// - the wheel appears and disappears quickly.
/// </summary>
[HarmonyPatch(typeof(NReactionWheel), nameof(NReactionWheel._Input))]
public static class ReactionWheelInputPatch
{
    /// <summary>
    /// The marker must be moved at least this far from the center (in the wheel's own units, its maximum is 70) to
    /// select an emote. Closer than this is "not moved".
    /// </summary>
    private const float MinSelectDistance = 10f;

    /// <summary>Show the outline behind the emote previews on the wheel? (Tinted with the local character's color.)</summary>
    private const bool PreviewWithOutline = false;

    private const int SlotCount = 8;
    private const float MarkerRadius = 70f;
    private const float WheelScale = 0.75f;
    private const float AppearScale = 0.6f;
    private const double AppearTime = 0.06;
    private const double DisappearTime = 0.05;

    private static readonly StringName ReactWheel = new("react_wheel");

    private static readonly AccessTools.FieldRef<NReactionWheel, TextureRect> MarkerRef =
        AccessTools.FieldRefAccess<NReactionWheel, TextureRect>("_marker");

    private static readonly AccessTools.FieldRef<NReactionWheel, bool> IgnoreNextRef =
        AccessTools.FieldRefAccess<NReactionWheel, bool>("_ignoreNextMouseInput");

    private static readonly AccessTools.FieldRef<NReactionWheel, Vector2> CenterRef =
        AccessTools.FieldRefAccess<NReactionWheel, Vector2>("_centerPosition");

    private static readonly AccessTools.FieldRef<NReactionWheel, NReactionWheelWedge?> SelectedRef =
        AccessTools.FieldRefAccess<NReactionWheel, NReactionWheelWedge?>("_selectedWedge");

    private static readonly AccessTools.FieldRef<NReactionWheel, Player?> PlayerRef =
        AccessTools.FieldRefAccess<NReactionWheel, Player?>("_localPlayer");

    private static readonly AccessTools.FieldRef<NReactionWheelWedge, TextureRect> WedgeIconRef =
        AccessTools.FieldRefAccess<NReactionWheelWedge, TextureRect>("_textureRect");

    // The wedges in the order of the angle (see NReactionWheel.GetSelectedWedge): right, then clockwise.
    private static readonly string[] WedgeFields =
    [
        "_rightWedge", "_downRightWedge", "_downWedge", "_downLeftWedge",
        "_leftWedge", "_upLeftWedge", "_upWedge", "_upRightWedge",
    ];

    private static NReactionWheel? _wheel;
    private static NReactionWheelWedge[] _wedges = [];
    private static readonly EmoteType?[] Slots = new EmoteType?[SlotCount];
    private static Label? _pageLabel;
    private static Tween? _tween;
    private static int _page;
    private static bool _closing;

    private static int PageCount => (EmoteImages.WheelSlots.Count + SlotCount - 1) / SlotCount;

    [HarmonyPrefix]
    public static bool Prefix(NReactionWheel __instance, InputEvent inputEvent)
    {
        if (!ReferenceEquals(_wheel, __instance))
        {
            Setup(__instance);
        }

        var focus = __instance.GetViewport().GuiGetFocusOwner();
        var typing = focus is TextEdit or LineEdit;
        var open = __instance.Visible && !_closing;

        if (!NGame.Instance.ReactionContainer.InMultiplayer)
        {
            if (open)
            {
                Close(__instance);
            }
        }
        else if (inputEvent is InputEventMouseMotion motion)
        {
            if (IgnoreNextRef(__instance))
            {
                IgnoreNextRef(__instance) = false;
            }
            else if (open)
            {
                MoveMarker(__instance, motion.Relative);
                IgnoreNextRef(__instance) = true;
                WarpMouseBack(__instance);
            }
        }
        else if (inputEvent is InputEventMouseButton button && open)
        {
            // Clicks are not for the game while the wheel is open (both press and release are swallowed).
            if (button.Pressed && button.ButtonIndex is MouseButton.Left or MouseButton.Right)
            {
                TurnPage(__instance, next: (button.ButtonIndex == MouseButton.Right) != EmotePlusConfig.ReversePageClicks);
            }

            __instance.GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.IsActionPressed(ReactWheel) && !typing)
        {
            Open(__instance);
        }
        else if (inputEvent.IsActionReleased(ReactWheel) && open)
        {
            Release(__instance);
        }

        return false;
    }

    private static void Setup(NReactionWheel wheel)
    {
        _wheel = wheel;
        _wedges = WedgeFields
            .Select(name => (NReactionWheelWedge)AccessTools.Field(typeof(NReactionWheel), name).GetValue(wheel)!)
            .ToArray();
        _tween = null;
        _closing = false;
        _page = 0;

        // Scaled around its center, so that the appearing animation does not move it.
        wheel.PivotOffset = wheel.Size / 2f;

        // The page number in the middle of the ring, over the marker.
        _pageLabel = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _pageLabel.AddThemeFontSizeOverride("font_size", 36);
        _pageLabel.AddThemeConstantOverride("outline_size", 8);
        _pageLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        if (ResourceLoader.Load("res://themes/kreon_bold_glyph_space_two.tres") is Font font)
        {
            _pageLabel.AddThemeFontOverride("font", font);
        }

        wheel.AddChild(_pageLabel);
        _pageLabel.CustomMinimumSize = new Vector2(120f, 40f);
        _pageLabel.Size = _pageLabel.CustomMinimumSize;
        _pageLabel.Position = (wheel.Size - _pageLabel.Size) / 2f;

        foreach (var wedge in _wedges)
        {
            PrepareIcon(WedgeIconRef(wedge));
        }

        ShowPage();
    }

    // The icon rect of a wedge shows any image size alike: fitted into its box (instead of at the image's own size).
    private static void PrepareIcon(TextureRect icon)
    {
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }

    // Puts the emotes of the current page on the wedges.
    private static void ShowPage()
    {
        var color = EmoteTracker.OutlineColorOf(_wheel != null ? PlayerRef(_wheel)?.Character : null);
        for (var i = 0; i < SlotCount; i++)
        {
            var index = _page * SlotCount + i;
            var wedge = _wedges[i];
            if (index >= EmoteImages.WheelSlots.Count || EmoteImages.WheelSlots[index] == EmoteType.None)
            {
                Slots[i] = null;
                wedge.Visible = false;
                continue;
            }

            var type = EmoteImages.WheelSlots[index];
            var name = EmoteImages.NameOf(type);
            Slots[i] = type;
            wedge.Visible = true;
            var icon = WedgeIconRef(wedge);
            foreach (var child in icon.GetChildren())
            {
                child.QueueFree();
            }

            if (PreviewWithOutline)
            {
                // The outline is the rect's own image (tinted), the emote is on top of it as a child.
                icon.Texture = EmoteImages.Outline(name);
                icon.SelfModulate = color;
                var top = new TextureRect
                {
                    Texture = EmoteImages.Icon(name),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                PrepareIcon(top);
                icon.AddChild(top);
                top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            else
            {
                icon.Texture = EmoteImages.Icon(name);
                icon.SelfModulate = Colors.White;
            }
        }

        if (_pageLabel != null)
        {
            _pageLabel.Visible = PageCount >= 2;
            _pageLabel.Text = $"{_page + 1}/{PageCount}";
        }
    }

    private static void Open(NReactionWheel wheel)
    {
        _tween?.Kill();
        _closing = false;

        var marker = MarkerRef(wheel);
        if (PlayerRef(wheel) is { } player)
        {
            marker.Texture = player.Character.MapMarker;
        }

        // A selection left over from the last time would be picked by a release without moving.
        SelectedRef(wheel)?.OnDeselected();
        SelectedRef(wheel) = null;

        ShowPage();
        CenterRef(wheel) = wheel.GetViewport().GetMousePosition();
        marker.Position = (wheel.Size - marker.Size) * 0.5f;
        marker.Rotation = 0f;
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        wheel.Visible = true;
        wheel.Modulate = new Color(1f, 1f, 1f, 0f);
        wheel.Scale = Vector2.One * AppearScale;

        // The wheel is scaled around its center (the pivot), and GlobalPosition is the transform's origin, which the
        // scale moves by pivot * (1 - scale): compensate, so that the wheel's center is exactly at the mouse.
        wheel.GlobalPosition = CenterRef(wheel) - wheel.Size / 2f + wheel.PivotOffset * (1f - AppearScale);
        _tween = wheel.CreateTween().SetParallel();
        _tween.TweenProperty(wheel, "modulate:a", 1f, AppearTime);
        _tween.TweenProperty(wheel, "scale", Vector2.One * WheelScale, AppearTime)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
    }

    private static void Release(NReactionWheel wheel)
    {
        var marker = MarkerRef(wheel);
        var distance = (marker.Position - (wheel.Size - marker.Size) * 0.5f).Length();
        var center = CenterRef(wheel);
        var picked = SelectedRef(wheel) is { } wedge ? SlotOf(wedge) : null;

        Close(wheel);

        if (distance >= MinSelectDistance && picked is { } type)
        {
            LocalEmote.Do(NGame.Instance.ReactionContainer, type, center);
        }
    }

    private static void TurnPage(NReactionWheel wheel, bool next)
    {
        if (PageCount < 2)
        {
            return;
        }

        _page = (_page + (next ? 1 : PageCount - 1)) % PageCount;
        ShowPage();
        MoveMarker(wheel, Vector2.Zero); // the selection may now be on an empty slot, or another emote
    }

    private static EmoteType? SlotOf(NReactionWheelWedge wedge)
    {
        var index = Array.IndexOf(_wedges, wedge);
        return index >= 0 ? Slots[index] : null;
    }

    private static void Close(NReactionWheel wheel)
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        WarpMouseBack(wheel);

        _tween?.Kill();
        _closing = true;
        _tween = wheel.CreateTween().SetParallel();
        _tween.TweenProperty(wheel, "modulate:a", 0f, DisappearTime);
        _tween.TweenProperty(wheel, "scale", Vector2.One * AppearScale, DisappearTime)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        _tween.Chain().TweenCallback(Callable.From(() =>
        {
            wheel.Visible = false;
            wheel.Scale = Vector2.One * WheelScale;
            wheel.Modulate = Colors.White;
            _closing = false;
        }));
    }

    private static void WarpMouseBack(NReactionWheel wheel)
    {
        Input.WarpMouse(wheel.GetViewportTransform() * CenterRef(wheel));
    }

    private static void MoveMarker(NReactionWheel wheel, Vector2 relative)
    {
        var marker = MarkerRef(wheel);
        var origin = (wheel.Size - marker.Size) * 0.5f;
        var offset = (marker.Position - origin + relative).LimitLength(MarkerRadius);
        marker.Position = origin + offset;
        var angle = Mathf.Atan2(offset.Y, offset.X);
        marker.Rotation = angle - Mathf.Pi / 2f;

        // Like the vanilla wheel, but only once the marker is out of the center, and never an empty slot.
        NReactionWheelWedge? selected = null;
        if (offset.Length() >= MinSelectDistance)
        {
            var index = (int)(Mathf.Wrap(angle + Mathf.Pi / 8f, 0f, Mathf.Pi * 2f) / (Mathf.Pi / 4f));
            if (Slots[index] != null)
            {
                selected = _wedges[index];
            }
        }

        if (SelectedRef(wheel) != selected)
        {
            SelectedRef(wheel)?.OnDeselected();
            SelectedRef(wheel) = selected;
            selected?.OnSelected();
        }
    }
}

// The wedge goes back to its place slower (0.2s) in vanilla, which makes quick flicks between wedges look sluggish.
[HarmonyPatch(typeof(NReactionWheelWedge), nameof(NReactionWheelWedge.OnDeselected))]
public static class WedgeDeselectPatch
{
    private static readonly Color DefaultColor = new("e0f9ff40");

    private static readonly AccessTools.FieldRef<NReactionWheelWedge, Tween?> TweenRef =
        AccessTools.FieldRefAccess<NReactionWheelWedge, Tween?>("_tween");

    private static readonly AccessTools.FieldRef<NReactionWheelWedge, Vector2> DefaultPositionRef =
        AccessTools.FieldRefAccess<NReactionWheelWedge, Vector2>("_defaultPosition");

    [HarmonyPrefix]
    public static bool Prefix(NReactionWheelWedge __instance)
    {
        TweenRef(__instance)?.Kill();
        var tween = __instance.CreateTween();
        tween.SetParallel();
        tween.TweenProperty(__instance, "position", DefaultPositionRef(__instance), 0.08)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tween.TweenProperty(__instance, "self_modulate", DefaultColor, 0.08)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        TweenRef(__instance) = tween;
        return false;
    }
}
