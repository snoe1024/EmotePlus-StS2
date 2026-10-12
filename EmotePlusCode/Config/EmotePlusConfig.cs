using BaseLib.Config;
using BaseLib.Config.UI;
using EmotePlus.EmotePlusCode.Reaction;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace EmotePlus.EmotePlusCode.Config;

// The namespace's first segment gives the localization key prefix ("EMOTEPLUS-", see settings_ui.json).
[ConfigHoverTipsByDefault]
public sealed class EmotePlusConfig : SimpleModConfig
{
    private const string OrderImagePath = "res://EmotePlus/ui/settings/wheel_order.png";
    private const string HiddenMarkPath = "res://EmotePlus/ui/settings/mark_hidden.png";

    /// <summary>How long every emote stays on screen (appearing + waiting + fading out; the game's own is 1.1).</summary>
    [ConfigSlider(0.5, 10.0, 0.1, Format = "{0:0.0}sec")]
    public static float EmoteDisplayTime { get; set; } = 1.1f;

    /// <summary>
    /// The same for emotes made while the map is open: they point at a place, so they stay longer.
    /// </summary>
    [ConfigSlider(0.5, 10.0, 0.1, Format = "{0:0.0}sec")]
    public static float MapEmoteDisplayTime { get; set; } = 4.5f;

    /// <summary>
    /// Releasing the emote key in the middle of the wheel (the mouse hardly moved) repeats the last emote, if the key
    /// was held shorter than this many seconds; held longer, nothing is sent. 0 turns the repeating off.
    /// </summary>
    [ConfigSlider(0.0, 5.0, 0.1, Format = "{0:0.0}sec")]
    public static float RepeatEmoteTime { get; set; } = 2.5f;

    /// <summary>Use the emote wheel in singleplayer too (the emotes are only shown to the player themself).</summary>
    public static bool EnableInSingleplayer { get; set; } = true;

    /// <summary>
    /// While the emote wheel is open, scrolling down goes to the next page and scrolling up to the previous one;
    /// with this on, the other way round.
    /// </summary>
    public static bool ReverseScrollPaging { get; set; } = false;

    /// <summary>
    /// The emotes hidden from the wheel (see <see cref="EmoteImages.IsHidden"/>): their names, comma separated.
    /// Edited by the grid below the other options, not by a row of its own.
    /// </summary>
    [ConfigHideInUI]
    public static string HiddenEmotes { get; set; } = "";

    /// <summary>
    /// The order of the emotes on the wheel (see <see cref="EmoteImages.WheelSlots"/>): their names, comma separated;
    /// empty = the default order. Edited by the same grid.
    /// </summary>
    [ConfigHideInUI]
    public static string EmoteOrder { get; set; } = "";

    public override void SetupConfigUI(Control optionContainer)
    {
        base.SetupConfigUI(optionContainer);

        // The grid of emotes goes above the "restore defaults" button the base class put last.
        var section = CreateCollapsibleSection("EmoteVisibility");
        section.ContentContainer.AddChild(CreateEmoteGrid());
        optionContainer.AddChild(section);
        var reset = optionContainer.GetNodeOrNull("ResetDefaultsButtonContainer");
        if (reset != null)
        {
            optionContainer.MoveChild(section, reset.GetIndex());
        }

        SetupFocusNeighbors(optionContainer);
    }

    private Control CreateEmoteGrid()
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12);

        // The picture explaining the order of the wheel (made by hand; skipped while the file is missing).
        if (ResourceLoader.Exists(OrderImagePath))
        {
            var texture = ResourceLoader.Load<Texture2D>(OrderImagePath);
            var picture = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                // Pass: it must see the mouse for its hover tip, and still hand the scrolling on to the screen.
                MouseFilter = Control.MouseFilterEnum.Pass,
            };
            AttachTip(picture, "WheelOrderImage");

            // As wide as the settings screen gives it, as high as its proportions need. (The built-in
            // FitWidthProportional mode left the height at 0 here: the height is asked for before the width is known.)
            var ratio = texture.GetHeight() / (float)Math.Max(1, texture.GetWidth());
            picture.Resized += () =>
            {
                var height = picture.Size.X * ratio;
                if (Math.Abs(picture.CustomMinimumSize.Y - height) > 0.5f)
                {
                    picture.CustomMinimumSize = new Vector2(0f, height);
                }
            };
            box.AddChild(picture);
        }

        foreach (var key in new[] { "EmoteOrderHint", "EmoteVisibilityHint" })
        {
            var hint = CreateRawLabelControl("[center]" + GetLabelText(key) + "[/center]", 22);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hint.CustomMinimumSize = new Vector2(760f, 0f);
            box.AddChild(hint);
        }

        var buttons = new HFlowContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = FlowContainer.AlignmentMode.Center,
        };
        buttons.AddThemeConstantOverride("h_separation", 16);
        buttons.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(buttons);

        // 8 emotes to a row: one row is one page of the wheel (when the hidden emotes are packed at the end).
        var grid = new GridContainer { Columns = 8, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 14);
        box.AddChild(grid);

        var cells = new List<(TextureButton Button, Control Mark)>();
        var swapMode = false;
        int? selected = null; // swap mode: the first of the two emotes to swap

        void Refresh()
        {
            var order = EmoteImages.WheelSlots;
            for (var i = 0; i < cells.Count && i < order.Count; i++)
            {
                var name = EmoteImages.NameOf(order[i]);
                var (button, mark) = cells[i];
                var hidden = EmoteImages.IsHidden(name);
                button.TextureNormal = EmoteImages.Icon(name);
                button.TooltipText = name;
                mark.Visible = hidden;
                button.SelfModulate = hidden ? new Color(1f, 1f, 1f, 0.45f) : Colors.White;
                button.Modulate = selected == i ? new Color(1.5f, 1.4f, 0.5f) : Colors.White;
            }
        }

        void Edited()
        {
            SaveDebounced();
            Changed();
            Refresh();
        }

        void SetOrder(IEnumerable<EmoteType> order)
        {
            EmoteImages.SetOrder(order);
            Edited();
        }

        void Swap(int a, int b)
        {
            var order = EmoteImages.WheelSlots.ToList();
            if (a == b || a >= order.Count || b >= order.Count)
            {
                return;
            }

            (order[a], order[b]) = (order[b], order[a]);
            SetOrder(order);
        }

        for (var i = 0; i < EmoteImages.WheelSlots.Count; i++)
        {
            var index = i;
            var button = new TextureButton
            {
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(72f, 72f),
                FocusMode = Control.FocusModeEnum.All,
            };
            var mark = CreateHiddenMark();
            button.AddChild(mark);
            mark.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            // Dragging an emote onto another swaps them.
            button.SetDragForwarding(
                Callable.From<Vector2, Variant>(_ =>
                {
                    button.SetDragPreview(new TextureRect
                    {
                        Texture = button.TextureNormal,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        CustomMinimumSize = new Vector2(72f, 72f),
                        Size = new Vector2(72f, 72f),
                        Modulate = new Color(1f, 1f, 1f, 0.8f),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    });
                    return Variant.From(index);
                }),
                Callable.From<Vector2, Variant, bool>((_, data) => data.VariantType == Variant.Type.Int),
                Callable.From<Vector2, Variant>((_, data) => Swap(data.AsInt32(), index)));

            button.Pressed += () =>
            {
                if (!swapMode)
                {
                    // A click hides or shows the emote.
                    var name = EmoteImages.NameOf(EmoteImages.WheelSlots[index]);
                    EmoteImages.SetHidden(name, !EmoteImages.IsHidden(name));
                    Edited();
                }
                else if (selected == null)
                {
                    selected = index;
                    Refresh();
                }
                else
                {
                    var first = selected.Value;
                    selected = null;
                    Swap(first, index);
                    Refresh();
                }
            };
            grid.AddChild(button);
            cells.Add((button, mark));
        }

        // Swap mode: for those who cannot drag (a controller), and as a second way. Its button is lit while on.
        NConfigButton? swapButton = null;
        swapButton = CreateButtonWithTip("SwapMode", () =>
        {
            swapMode = !swapMode;
            selected = null;
            swapButton!.SetColor(Color.FromHtml(swapMode ? "#c9822b" : NConfigButton.DefaultColor));
            Refresh();
        });
        buttons.AddChild(swapButton);

        buttons.AddChild(CreateButtonWithTip("DefaultOrder", () => SetOrder(EmoteImages.DefaultOrder)));
        buttons.AddChild(CreateButtonWithTip("VanillaOrder", () => SetOrder(EmoteImages.VanillaFirst())));
        buttons.AddChild(CreateButtonWithTip("PackHiddenEmotes", () => SetOrder(EmoteImages.HiddenLast())));
        buttons.AddChild(CreateButtonWithTip("ShowAllEmotes", () =>
        {
            foreach (var (_, name) in EmoteImages.AllEmotes)
            {
                EmoteImages.SetHidden(name, false);
            }

            Edited();
        }));
        buttons.AddChild(CreateButtonWithTip("HideAllEmotes", () =>
        {
            foreach (var (_, name) in EmoteImages.AllEmotes)
            {
                EmoteImages.SetHidden(name, true);
            }

            Edited();
        }));

        // "Restore defaults" changes the properties from outside.
        void OnReloaded() => Refresh();
        OnConfigReloaded += OnReloaded;
        box.TreeExiting += () => OnConfigReloaded -= OnReloaded;

        Refresh();
        return box;
    }

    // A settings button with its label and a hover tip (the mouse-over text): the keys are the name's
    // EMOTEPLUS-<NAME>.title and .hover.desc in settings_ui.json. A missing .hover.desc just means no tip.
    private NConfigButton CreateButtonWithTip(string name, Action onPressed)
    {
        var button = CreateRawButtonControl(GetLabelText(name), onPressed);
        AttachTip(button, name);
        return button;
    }

    // Shows the hover tip of the given name while the mouse is over the control.
    private void AttachTip(Control control, string name)
    {
        var key = ModPrefix + StringHelper.Slugify(name);
        if (!LocString.Exists("settings_ui", key + ".hover.desc"))
        {
            return;
        }

        var tip = new HoverTip(new LocString("settings_ui", key + ".title"), new LocString("settings_ui", key + ".hover.desc"));
        control.MouseEntered += () => NHoverTipSet.CreateAndShow(control, tip, HoverTipAlignment.Center);
        control.MouseExited += () => NHoverTipSet.Remove(control);
        control.TreeExiting += () => NHoverTipSet.Remove(control);
    }

    // The mark over a hidden emote: the red cross image, or (if that file is missing) a red x.
    private static Control CreateHiddenMark()
    {
        if (ResourceLoader.Exists(HiddenMarkPath))
        {
            return new TextureRect
            {
                Texture = ResourceLoader.Load<Texture2D>(HiddenMarkPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
        }

        var label = new Label
        {
            Text = "×",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 64);
        label.AddThemeColorOverride("font_color", new Color(0.9f, 0.1f, 0.1f));
        return label;
    }
}
