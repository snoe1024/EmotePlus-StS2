using BaseLib.Config;
using EmotePlus.EmotePlusCode.Reaction;
using Godot;

namespace EmotePlus.EmotePlusCode.Config;

// The namespace's first segment gives the localization key prefix ("EMOTEPLUS-", see settings_ui.json).
[ConfigHoverTipsByDefault]
public sealed class EmotePlusConfig : SimpleModConfig
{
    /// <summary>How long every emote stays on screen (appearing + waiting + fading out; the game's own is 1.1).</summary>
    [ConfigSlider(0.5, 5.0, 0.1, Format = "{0:0.0}sec")]
    public static float EmoteDisplayTime { get; set; } = 1.1f;

    /// <summary>
    /// While the emote wheel is open, the left click goes to the previous page and the right click to the next one;
    /// with this on, the other way round.
    /// </summary>
    public static bool ReversePageClicks { get; set; } = false;

    /// <summary>
    /// The emotes hidden from the wheel (see <see cref="EmoteImages.IsHidden"/>): their names, comma separated.
    /// Edited by the grid below the other options, not by a row of its own.
    /// </summary>
    [ConfigHideInUI]
    public static string HiddenEmotes { get; set; } = "";

    public override void SetupConfigUI(Control optionContainer)
    {
        base.SetupConfigUI(optionContainer);

        // The grid of emotes goes above the "restore defaults" button the base class put last.
        var section = CreateCollapsibleSection("EmoteVisibility");
        section.ContentContainer.AddChild(CreateVisibilityGrid());
        optionContainer.AddChild(section);
        var reset = optionContainer.GetNodeOrNull("ResetDefaultsButtonContainer");
        if (reset != null)
        {
            optionContainer.MoveChild(section, reset.GetIndex());
        }

        SetupFocusNeighbors(optionContainer);
    }

    private Control CreateVisibilityGrid()
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12);

        var hint = CreateRawLabelControl(GetLabelText("EmoteVisibilityHint"), 22);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(760f, 0f);
        box.AddChild(hint);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 16);
        box.AddChild(buttons);

        // A flow container wraps at whatever width the settings screen gives it (as many per row as fit).
        var grid = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(grid);

        var cells = new List<(string Name, TextureButton Button, Control Mark)>();

        void Refresh()
        {
            foreach (var (name, button, mark) in cells)
            {
                var hidden = EmoteImages.IsHidden(name);
                mark.Visible = hidden;
                button.SelfModulate = hidden ? new Color(1f, 1f, 1f, 0.45f) : Colors.White;
            }
        }

        void Edited()
        {
            SaveDebounced();
            Changed();
            Refresh();
        }

        foreach (var (_, name) in EmoteImages.AllEmotes)
        {
            var button = new TextureButton
            {
                TextureNormal = EmoteImages.Icon(name),
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(72f, 72f),
                TooltipText = name,
                FocusMode = Control.FocusModeEnum.All,
            };
            var mark = CreateHiddenMark();
            button.AddChild(mark);
            mark.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var captured = name;
            button.Pressed += () =>
            {
                EmoteImages.SetHidden(captured, !EmoteImages.IsHidden(captured));
                Edited();
            };
            grid.AddChild(button);
            cells.Add((name, button, mark));
        }

        buttons.AddChild(CreateRawButtonControl(GetLabelText("ShowAllEmotes"), () =>
        {
            foreach (var (_, name) in EmoteImages.AllEmotes)
            {
                EmoteImages.SetHidden(name, false);
            }

            Edited();
        }));
        buttons.AddChild(CreateRawButtonControl(GetLabelText("HideAllEmotes"), () =>
        {
            foreach (var (_, name) in EmoteImages.AllEmotes)
            {
                EmoteImages.SetHidden(name, true);
            }

            Edited();
        }));

        // "Restore defaults" changes the property from outside.
        void OnReloaded() => Refresh();
        OnConfigReloaded += OnReloaded;
        box.TreeExiting += () => OnConfigReloaded -= OnReloaded;

        Refresh();
        return box;
    }

    // The mark over a hidden emote: the red cross image, or (if that file is missing) a red x.
    private static Control CreateHiddenMark()
    {
        const string path = "res://EmotePlus/ui/settings/mark_hidden.png";
        if (ResourceLoader.Exists(path))
        {
            return new TextureRect
            {
                Texture = ResourceLoader.Load<Texture2D>(path),
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
