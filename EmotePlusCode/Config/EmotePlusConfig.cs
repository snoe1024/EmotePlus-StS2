using BaseLib.Config;
using BaseLib.Config.UI;
using EmotePlus.EmotePlusCode.Reaction;
using EmotePlus.EmotePlusCode.Extensions;
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

    /// <summary>Let the hidden cursor move naturally during selection instead of warping it back to the opening point.</summary>
    public static bool PreserveCursorPosition { get; set; } = false;

    /// <summary>Add a final wheel page containing the latest eight distinct activity emotes. The saved key is retained.</summary>
    public static bool ShowRecentActivity { get; set; } = true;

    /// <summary>Append a page sampled again whenever it is opened.</summary>
    public static bool ShowRandomWheel { get; set; } = false;

    /// <summary>Allow right-click favorites and append their dedicated pages.</summary>
    public static bool ShowFavoriteWheel { get; set; } = true;

    /// <summary>Favorite stable catalogue names, including temporarily unavailable content.</summary>
    [ConfigHideInUI]
    public static string FavoriteEmotes { get; set; } = "";

    /// <summary>Persist wheel edits through the same path as settings edits.</summary>
    public void SaveWheelFavorites()
    {
        SaveDebounced();
        Changed();
    }

    /// <summary>Persistent migration marker for the old exclusion-list setting.</summary>
    [ConfigHideInUI]
    public static int EmoteVisibilityVersion { get; set; }

    /// <summary>Explicitly selected emote names; unknown future additions remain unselected.</summary>
    [ConfigHideInUI]
    public static string EnabledEmotes { get; set; } = EmoteImages.InitialSelection;

    /// <summary>Saved catalogue snapshot used by the all-hidden fallback.</summary>
    [ConfigHideInUI]
    public static string EmptySelectionFallback { get; set; } = EmoteImages.InitialSelection;

    /// <summary>
    /// The emotes hidden from the wheel (see <see cref="EmoteImages.IsHidden"/>): their names, comma separated.
    /// Read once for migration; the grid now edits EnabledEmotes.
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
        var grid = new VirtualEmoteGrid();
        var gridArea = new VBoxContainer { CustomMinimumSize = new Vector2(VirtualEmoteGrid.GridWidth, 0f) };
        var noResults = CreateRawLabelControl(new LocString("settings_ui", "EMOTEPLUS-NO_FILTER_RESULT").GetFormattedText(), 24);
        noResults.HorizontalAlignment = HorizontalAlignment.Center;
        noResults.VerticalAlignment = VerticalAlignment.Center;
        noResults.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        noResults.CustomMinimumSize = new Vector2(0f, 72f);
        noResults.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        noResults.MouseFilter = Control.MouseFilterEnum.Ignore;
        noResults.Visible = false;
        gridArea.AddChild(noResults);
        gridArea.AddChild(grid);
        const int maxGridItems = VirtualEmoteGrid.Columns * 50;
        var truncatedResults = CreateRawLabelControl(GetLabelText("FilterResultLimit"), 22);
        truncatedResults.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        truncatedResults.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        truncatedResults.MouseFilter = Control.MouseFilterEnum.Ignore;
        truncatedResults.Visible = false;
        gridArea.AddChild(truncatedResults);
        var browser = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        browser.AddThemeConstantOverride("separation", 20);
        var filters = new VBoxContainer { CustomMinimumSize = new Vector2(180f, 0f) };
        var search = new LineEdit
        {
            PlaceholderText = GetLabelText("SearchPlaceholder"),
            CustomMinimumSize = new Vector2(180f, 44f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        AttachTip(search, "EmoteSearch");
        filters.AddChild(new Label { Text = GetLabelText("EmoteSearch") });
        filters.AddChild(search);
        var activeTags = EmoteTag.None;
        var tagButtons = new List<CheckButton>();
        var tagFlow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var count = new Label();
        filters.AddChild(count);
        filters.AddChild(tagFlow);
        var contentMods = ModdedEmotes.Groups
            .Where(g => g.Entries.Any(e => ModdedEmotes.IsAvailable((EmoteType)e.Id)))
            .OrderBy(g => g.LocalizedName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var contentFilter = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(180f, 44f),
        };
        contentFilter.AddItem(GetLabelText("AllContent"));
        contentFilter.AddItem(GetLabelText("VanillaContent"));
        foreach (var mod in contentMods) contentFilter.AddItem(mod.LocalizedName);
        contentFilter.Select(0);
        AttachTip(contentFilter, "ContentModFilter");
        filters.AddChild(new Label { Text = GetLabelText("ContentModFilter") });
        filters.AddChild(contentFilter);
        browser.AddChild(filters);
        browser.AddChild(gridArea);
        box.AddChild(browser);

        // Keep the eight-column grid intact on narrower settings panels.
        box.Resized += () =>
        {
            var narrow = box.Size.X < filters.GetCombinedMinimumSize().X + VirtualEmoteGrid.GridWidth + 20f;
            if (narrow && filters.GetParent() == browser)
            {
                browser.RemoveChild(filters);
                box.AddChild(filters);
                box.MoveChild(filters, browser.GetIndex());
            }
            else if (!narrow && filters.GetParent() == box)
            {
                box.RemoveChild(filters);
                browser.AddChild(filters);
                browser.MoveChild(filters, 0);
            }
        };
        var notice = CreateRawLabelControl(GetLabelText("FilterNotice"), 20);
        notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        notice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.AddChild(notice);

        var swapMode = false;
        int? selected = null; // swap mode: the first of the two emotes to swap
        var metadata = EmoteImages.WheelSlots.ToDictionary(t => t, EmoteSearch.Describe);
        var contentGroups = metadata.Keys.ToDictionary(t => t,
            t => ModdedEmotes.TryDescribe(t, out var group, out _) ? group.Key : null);
        EmoteType[] matchingEmotes = [];

        void UpdateCell(int index, Control cell)
        {
            var type = EmoteImages.WheelSlots[index];
            var name = EmoteImages.NameOf(type);
            var button = (TextureButton)cell;
            var entry = metadata[type];
            var hidden = EmoteImages.IsHidden(name);
            // Keep UI-only textures out of the gameplay's persistent texture cache.
            button.TextureNormal = ResourceLoader.Load<Texture2D>(type.GetEmotePath());
            button.TooltipText = entry.Title + "\n" + name + "\n" + string.Join(", ",
                EmoteSearch.Tags.Where(entry.HasTag).Select(t => GetLabelText("Tag" + t)));
            button.GetNode<Control>("HiddenMark").Visible = hidden;
            button.SelfModulate = hidden ? new Color(1f, 1f, 1f, 0.45f) : Colors.White;
            button.Modulate = selected == index ? new Color(1.5f, 1.4f, 0.5f) : Colors.White;
        }
        grid.UpdateCell = UpdateCell;

        void Refresh()
        {
            var order = EmoteImages.WheelSlots;
            var selection = contentFilter.Selected;
            var query = EmoteSearch.PrepareQuery(search.Text, activeTags);
            var matches = Enumerable.Range(0, order.Count)
                .Where(i => metadata[order[i]].Matches(query) &&
                    (selection == 0 || (selection == 1 ? EmoteCatalog.Get(order[i]).IsVanillaContent
                        : contentGroups[order[i]] == contentMods[selection - 2].Key))).ToArray();
            matchingEmotes = matches.Select(i => order[i]).ToArray();
            // Keep bulk actions on the full result; only materialized rows are limited.
            grid.SetItems(matches.Take(maxGridItems).ToArray());
            truncatedResults.Visible = matches.Length > maxGridItems;
            grid.Visible = matches.Length > 0;
            noResults.Visible = matches.Length == 0;
            count.Text = $"{matches.Length} / {order.Count}";
        }
        search.TextChanged += _ => { selected = null; Refresh(); };
        contentFilter.ItemSelected += _ => { selected = null; Refresh(); };
        foreach (var tag in EmoteSearch.Tags)
        {
            var toggle = new CheckButton { Text = GetLabelText("Tag" + tag) };
            AttachTip(toggle, "Tag" + tag);
            toggle.Toggled += on =>
            {
                if (on) activeTags |= tag; else activeTags &= ~tag;
                selected = null;
                Refresh();
            };
            tagButtons.Add(toggle);
            tagFlow.AddChild(toggle);
        }
        filters.AddChild(CreateButtonWithTip("ClearFilters", () =>
        {
            search.Text = "";
            activeTags = EmoteTag.None;
            foreach (var toggle in tagButtons) toggle.SetPressedNoSignal(false);
            contentFilter.Select(0);
            selected = null;
            Refresh();
        }));

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
            if (a == b || a < 0 || b < 0 || a >= order.Count || b >= order.Count)
            {
                return;
            }

            (order[a], order[b]) = (order[b], order[a]);
            SetOrder(order);
        }

        grid.CreateCell = index =>
        {
            var button = new TextureButton
            {
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(72f, 72f),
                FocusMode = Control.FocusModeEnum.All,
            };
            var mark = CreateHiddenMark();
            mark.Name = "HiddenMark";
            button.AddChild(mark);
            mark.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            // Dragging an emote onto another swaps them.
            button.SetDragForwarding(
                Callable.From<Vector2, Variant>(_ =>
                {
                    grid.PinDragSource(index);
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
            return button;
        };

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
            EmoteImages.SetHidden(matchingEmotes, false);

            Edited();
        }));
        buttons.AddChild(CreateButtonWithTip("HideAllEmotes", () =>
        {
            EmoteImages.SetHidden(matchingEmotes, true);

            Edited();
        }));

        // "Restore defaults" changes the properties from outside.
        void OnReloaded() { EmoteImages.MigrateVisibility(); Refresh(); }
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
