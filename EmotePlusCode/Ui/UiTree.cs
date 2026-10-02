using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.StatsScreen;

namespace EmotePlus.EmotePlusCode.Ui;

// Looks up the live game nodes behind the UiNodeId tree: where the local player currently is, the button that
// leads from a node to one of its children, and the scene node that "contains" a node.
/// <summary>Where, inside a button, an emote pinned on it is centered.</summary>
public enum ButtonAnchor
{
    Center,

    /// <summary>The left end, vertically centered (wide buttons with centered text).</summary>
    LeftEdge,

    TopLeftCorner,

    TopRightCorner,

    /// <summary>Just outside the left edge, vertically centered (next to a name in a list).</summary>
    LeftOutside,
}

/// <param name="Bounds">If set, the pinned point is clamped into this global rect (for buttons inside a scroll view).</param>
public readonly record struct ButtonTarget(Control Control, ButtonAnchor Anchor, Rect2? Bounds = null)
{
    // How far inside the button the emote's center sits for the edge/corner anchors (the emote is 80x80).
    private const float Inset = 40f;

    public Vector2 Point()
    {
        var transform = Control.GetGlobalTransformWithCanvas();
        var size = Control.Size;
        return Anchor switch
        {
            ButtonAnchor.LeftEdge => transform * new Vector2(0f, size.Y * 0.5f) + new Vector2(Inset, 0f),
            ButtonAnchor.TopLeftCorner => transform * Vector2.Zero + new Vector2(Inset, Inset),
            ButtonAnchor.TopRightCorner => transform * new Vector2(size.X, 0f) + new Vector2(-Inset, Inset),
            ButtonAnchor.LeftOutside => transform * new Vector2(0f, size.Y * 0.5f) + new Vector2(-Inset, 0f),
            _ => transform * (size * 0.5f),
        };
    }
}

/// <summary>A scrolling view: the content that moves, and the (fixed) area it is seen through.</summary>
public readonly record struct ScrollSpace(Control Content, Control Viewport)
{
    public Vector2 ScreenToLocal(Vector2 screen) => Content.GetGlobalTransformWithCanvas().AffineInverse() * screen;

    public Vector2 LocalToScreen(Vector2 local) => Content.GetGlobalTransformWithCanvas() * local;

    /// <summary>
    /// How far inside the edge of a scrolling view an emote's center may go. The emote is 80px wide, so its center
    /// 13px inside the edge lets about a third (27px) overhang: an emote that was clamped is visibly cut off, which
    /// hints that the real spot is out of view and invites the player to scroll.
    /// </summary>
    public const float ClampInset = 13f;

    /// <summary>Where emotes may be shown (their centers): the viewport, nearly to its edge.</summary>
    public Rect2 Bounds => Viewport.GetGlobalRect().Grow(-ClampInset);

    /// <summary>Like <see cref="Bounds"/> but without limits to the left/right (for a list whose names are drawn
    /// next to, not inside, the scroll view).</summary>
    public Rect2 VerticalBounds
    {
        get
        {
            var bounds = Bounds;
            return new Rect2(-100000f, bounds.Position.Y, 200000f, bounds.Size.Y);
        }
    }
}

public static class UiTree
{
    private static readonly AccessTools.FieldRef<NMultiplayerPlayerExpandedState, Player> DetailPlayer =
        AccessTools.FieldRefAccess<NMultiplayerPlayerExpandedState, Player>("_player");

    private static readonly AccessTools.FieldRef<NCardGrid, Control> GridScrollContainer =
        AccessTools.FieldRefAccess<NCardGrid, Control>("_scrollContainer");

    private static readonly AccessTools.FieldRef<NInspectCardScreen, List<CardModel>?> InspectCards =
        AccessTools.FieldRefAccess<NInspectCardScreen, List<CardModel>?>("_cards");

    private static readonly AccessTools.FieldRef<NInspectCardScreen, int> InspectIndex =
        AccessTools.FieldRefAccess<NInspectCardScreen, int>("_index");

    private static readonly AccessTools.FieldRef<NCardsViewScreen, NCardGrid> DeckGrid =
        AccessTools.FieldRefAccess<NCardsViewScreen, NCardGrid>("_grid");

    private static readonly AccessTools.FieldRef<NBestiary, NBestiaryEntry?> BestiarySelected =
        AccessTools.FieldRefAccess<NBestiary, NBestiaryEntry?>("_selectedEntry");

    private static readonly AccessTools.FieldRef<NInspectRelicScreen, IReadOnlyList<RelicModel>?> InspectRelics =
        AccessTools.FieldRefAccess<NInspectRelicScreen, IReadOnlyList<RelicModel>?>("_relics");

    private static readonly AccessTools.FieldRef<NInspectRelicScreen, int> InspectRelicIndex =
        AccessTools.FieldRefAccess<NInspectRelicScreen, int>("_index");

    // The card pool filters of the card library, in a fixed order (the pool index of CardLibraryPool).
    private static readonly string[] PoolFilterPaths =
    {
        "%IroncladPool", "%SilentPool", "%DefectPool", "%RegentPool",
        "%NecrobinderPool", "%ColorlessPool", "%AncientsPool", "%MiscPool",
    };

    private const byte NoPool = 255;

    private static readonly AccessTools.FieldRef<NScrollableContainer, Control?> ScrollableContent =
        AccessTools.FieldRefAccess<NScrollableContainer, Control?>("_content");

    // The Enchantment Compendium mod (workshop 3750879024) is not referenced: it adds a button named
    // "EnchantmentCompendiumButton" to the compendium's top row and, when pressed, an overlay named
    // "EnchantmentCompendiumOverlay" (child of the compendium) holding a scrollable "ScreenContents". Everything is
    // found by node name, so without the mod these simply resolve to null.
    private const string EnchantmentButtonPath = "MarginContainer/VBoxContainer/TopRow/EnchantmentCompendiumButton";
    private const string EnchantmentOverlayName = "EnchantmentCompendiumOverlay";

    private static readonly UiNodeId Base = new(UiNodeKind.Base);
    private static readonly UiNodeId Pause = new(UiNodeKind.PauseMenu);
    private static readonly UiNodeId Compendium = new(UiNodeKind.Compendium);

    /// <summary>
    /// The path an emote made at <paramref name="mousePosition"/> is SENT with: the local player's path
    /// (<see cref="LocalPath(Vector2)"/>), except that the local player's own deck screen is disguised as their
    /// player detail screen. A viewer's deck is their own, so a deck screen is meaningless to everyone else; the
    /// screen that shows this player's deck to others is the player detail screen. The card the emote refers to
    /// (the enlarged one, or else the one under the emote) selects the matching card of that screen; with no card
    /// it is the detail screen itself. The position stays a screen position.
    /// </summary>
    public static UiNodeId[] SenderPath(Vector2 mousePosition)
    {
        var path = LocalPath(mousePosition);
        if (path.Length != 2 || path[1].Kind != UiNodeKind.DeckView || LocalContext.NetId is not { } me)
        {
            return path;
        }

        return PlayerDetailPath(me, ViewedCard() ?? (IsOnHeader(mousePosition) ? null : CardUnder(mousePosition)));
    }

    private static UiNodeId[] PlayerDetailPath(ulong netId, CardModel? card)
    {
        var detail = new UiNodeId(UiNodeKind.PlayerDetail, netId);
        return card == null
            ? new[] { Base, detail }
            : new[] { Base, detail, new UiNodeId(UiNodeKind.PlayerDetailCard, 0, DetailCardKey(card)) };
    }

    /// <summary>The screens that show the header bar (top bar): the base scene, the map and the deck screen.</summary>
    public static bool ShowsHeader(IReadOnlyList<UiNodeId> path)
    {
        return path.Count == 1 ||
               (path.Count == 2 && path[1].Kind is UiNodeKind.Map or UiNodeKind.DeckView);
    }

    /// <summary>True if <paramref name="position"/> is on the header bar. Only meaningful where it is shown.</summary>
    public static bool IsOnHeader(Vector2 position)
    {
        var topBar = NRun.Instance?.GlobalUi.TopBar;
        var bg = topBar?.GetNodeOrNull<Control>("BgImage");
        if (topBar == null || bg == null || !topBar.IsVisibleInTree())
        {
            return false;
        }

        return position.Y <= (bg.GetGlobalTransformWithCanvas() * new Vector2(0f, bg.Size.Y)).Y;
    }

    /// <summary>
    /// Identifies a card the way the player detail screen groups its deck (id, upgrade level, enchantment), so the
    /// same card is found on every client.
    /// </summary>
    public static string DetailCardKey(CardModel card)
    {
        return $"{card.Id}|{card.CurrentUpgradeLevel}|{card.Enchantment?.Id}:{card.Enchantment?.Amount}";
    }

    private static bool HitboxContains(Control hitbox, Vector2 point)
    {
        return new Rect2(Vector2.Zero, hitbox.Size).HasPoint(hitbox.GetGlobalTransformWithCanvas().AffineInverse() * point);
    }

    // The card of the deck screen's grid under the given position, if any (the topmost one).
    private static CardModel? CardUnder(Vector2 position)
    {
        if (NCapstoneContainer.Instance?.CurrentCapstoneScreen is not NDeckViewScreen deck)
        {
            return null;
        }

        // A grid card holder is a zero-size Control placed at the card's CENTER (and scaled): the card's area is its
        // Hitbox child. Only cards inside the grid's own area count (the rest is scrolled out from under the header).
        var grid = DeckGrid(deck);
        if (!grid.GetGlobalRect().HasPoint(position))
        {
            return null;
        }

        return grid.CurrentlyDisplayedCardHolders
            .LastOrDefault(h => GodotObject.IsInstanceValid(h) && h.IsVisibleInTree() && HitboxContains(h.Hitbox, position))
            ?.CardModel;
    }

    /// <summary>
    /// The local player's path for an emote made at <paramref name="mousePosition"/>: like <see cref="LocalPath()"/>,
    /// except that on the card library's fixed control panel the page is replaced by the panel.
    /// </summary>
    public static UiNodeId[] LocalPath(Vector2 mousePosition)
    {
        var path = LocalPath();
        if (path.Length == 0)
        {
            return path;
        }

        // Regions that belong to the screen as a whole instead of to the page being shown (the enlarged card
        // being open is a deeper node, which is not affected).
        switch (path[^1].Kind)
        {
            case UiNodeKind.CardLibraryPool:
                if (Contains(Submenu<NCardLibrary>()?.GetNodeOrNull<Control>("Sidebar"), mousePosition))
                {
                    path[^1] = new UiNodeId(UiNodeKind.CardLibraryPanel);
                }

                break;
            case UiNodeKind.BestiaryMonster:
                if (Contains(Submenu<NBestiary>()?.GetNodeOrNull<Control>("%Sidebar"), mousePosition))
                {
                    path[^1] = new UiNodeId(UiNodeKind.BestiaryList);
                }

                break;
        }

        return path;
    }

    /// <summary>The local player's path in the tree, root first. Empty when not in a run.</summary>
    public static UiNodeId[] LocalPath()
    {
        if (NRun.Instance == null)
        {
            return Array.Empty<UiNodeId>();
        }

        var capstone = NCapstoneContainer.Instance;
        if (capstone?.InUse == true)
        {
            return capstone.CurrentCapstoneScreen switch
            {
                NMultiplayerPlayerExpandedState detail => PlayerDetailPath(DetailPlayer(detail).NetId, ViewedCard()),
                NDeckViewScreen => new[] { Base, new UiNodeId(UiNodeKind.DeckView) },
                NCapstoneSubmenuStack stack => SubmenuPath(stack.Stack.Peek()),
                _ => new[] { Base, new UiNodeId(UiNodeKind.OtherCapstone) },
            };
        }

        return NMapScreen.Instance?.IsOpen == true ? new[] { Base, new UiNodeId(UiNodeKind.Map) } : new[] { Base };
    }

    private static UiNodeId[] SubmenuPath(NSubmenu? submenu)
    {
        return submenu switch
        {
            NPauseMenu => new[] { Base, Pause },
            NSettingsScreen => new[] { Base, Pause, new UiNodeId(UiNodeKind.Settings) },
            NCompendiumSubmenu compendium => CompendiumPath(compendium),
            NCardLibrary library => CardLibraryPath(library),
            NRelicCollection => RelicCollectionPath(),
            NPotionLab => new[] { Base, Pause, Compendium, new UiNodeId(UiNodeKind.PotionLab) },
            NBestiary bestiary => BestiaryPath(bestiary),
            NStatsScreen => new[] { Base, Pause, Compendium, new UiNodeId(UiNodeKind.Stats) },
            NRunHistory => new[] { Base, Pause, Compendium, new UiNodeId(UiNodeKind.RunHistory) },
            _ => new[] { Base, new UiNodeId(UiNodeKind.OtherCapstone) },
        };
    }

    private static UiNodeId[] CompendiumPath(NCompendiumSubmenu compendium)
    {
        return EnchantmentOverlay(compendium) != null
            ? new[] { Base, Pause, Compendium, new UiNodeId(UiNodeKind.EnchantmentCompendium) }
            : new[] { Base, Pause, Compendium };
    }

    private static Control? EnchantmentOverlay(Node? compendium)
    {
        var overlay = compendium?.GetNodeOrNull<Control>(EnchantmentOverlayName);
        return overlay != null && GodotObject.IsInstanceValid(overlay) && !overlay.IsQueuedForDeletion() ? overlay : null;
    }

    private static bool Contains(Control? control, Vector2 point)
    {
        return control != null && GodotObject.IsInstanceValid(control) && control.GetGlobalRect().HasPoint(point);
    }

    private static UiNodeId[] BestiaryPath(NBestiary bestiary)
    {
        var path = new List<UiNodeId> { Base, Pause, Compendium, new(UiNodeKind.Bestiary) };
        var selected = BestiarySelected(bestiary);
        if (selected != null && GodotObject.IsInstanceValid(selected))
        {
            path.Add(new UiNodeId(UiNodeKind.BestiaryMonster, 0, BestiaryKey(selected)));
        }

        return path.ToArray();
    }

    // A monster can be listed under several acts/encounters: the pair identifies the list entry.
    private static string BestiaryKey(NBestiaryEntry entry)
    {
        return $"{entry.Entry.monsterModel?.Id}@{entry.Entry.encounterModel.Id}";
    }

    private static UiNodeId[] RelicCollectionPath()
    {
        var collection = new UiNodeId(UiNodeKind.RelicCollection);
        var relic = ViewedRelicKey();
        return relic == null
            ? new[] { Base, Pause, Compendium, collection }
            : new[] { Base, Pause, Compendium, collection, new UiNodeId(UiNodeKind.RelicDetail, 0, relic) };
    }

    /// <summary>The relic shown enlarged on the relic inspect screen, if it is open.</summary>
    private static string? ViewedRelicKey()
    {
        var inspect = NGame.Instance?.InspectRelicScreen;
        if (inspect == null || !GodotObject.IsInstanceValid(inspect) || !inspect.Visible)
        {
            return null;
        }

        var relics = InspectRelics(inspect);
        var index = InspectRelicIndex(inspect);
        return relics != null && index >= 0 && index < relics.Count ? relics[index].Id.ToString() : null;
    }

    private static UiNodeId[] CardLibraryPath(NCardLibrary library)
    {
        var path = new List<UiNodeId>
        {
            Base, Pause, Compendium,
            new(UiNodeKind.CardLibrary),
            new(UiNodeKind.CardLibraryPool, SelectedPool(library)),
        };
        var detail = ViewedCardKey();
        if (detail != null)
        {
            path.Add(new UiNodeId(UiNodeKind.CardDetail, 0, detail));
        }

        return path.ToArray();
    }

    private static byte SelectedPool(NCardLibrary library)
    {
        for (var i = 0; i < PoolFilterPaths.Length; i++)
        {
            if (library.GetNodeOrNull<NCardPoolFilter>(PoolFilterPaths[i])?.IsSelected == true)
            {
                return (byte)i;
            }
        }

        return NoPool;
    }

    private static string? ViewedCardKey() => ViewedCard()?.Id.ToString();

    /// <summary>The card shown enlarged on the card inspect screen, if it is open.</summary>
    private static CardModel? ViewedCard()
    {
        var inspect = NGame.Instance?.InspectCardScreen;
        if (inspect == null || !GodotObject.IsInstanceValid(inspect) || !inspect.Visible)
        {
            return null;
        }

        var cards = InspectCards(inspect);
        var index = InspectIndex(inspect);
        return cards != null && index >= 0 && index < cards.Count ? cards[index] : null;
    }

    /// <summary>The scrolling content/viewport of a node's screen, or null if the node does not scroll.</summary>
    public static ScrollSpace? GetScrollSpace(UiNodeId node)
    {
        if (node.Kind == UiNodeKind.DeckView)
        {
            return NCapstoneContainer.Instance?.CurrentCapstoneScreen is NDeckViewScreen deck ? ScrollOfGrid(DeckGrid(deck)) : null;
        }

        if (node.Kind == UiNodeKind.EnchantmentCompendium)
        {
            return ScrollOf(EnchantmentOverlay(Submenu<NCompendiumSubmenu>())?.GetNodeOrNull<NScrollableContainer>("ScreenContents"));
        }

        // The collection screens that are one plain scroll view.
        if (node.Kind == UiNodeKind.BestiaryList)
        {
            return ScrollOf(Submenu<NBestiary>()?.GetNodeOrNull<NScrollableContainer>("%Sidebar"));
        }

        if (node.Kind == UiNodeKind.PotionLab)
        {
            return ScrollOf(Submenu<NPotionLab>()?.GetNodeOrNull<NScrollableContainer>("ScreenContents"));
        }

        if (node.Kind == UiNodeKind.RelicCollection)
        {
            return ScrollOf(Submenu<NRelicCollection>()?.GetNodeOrNull<NScrollableContainer>("ScreenContents"));
        }

        if (node.Kind != UiNodeKind.CardLibraryPool)
        {
            return null;
        }

        return ScrollOfGrid(Submenu<NCardLibrary>()?.GetNodeOrNull<NCardGrid>("%CardGrid"));
    }

    private static ScrollSpace? ScrollOfGrid(NCardGrid? grid)
    {
        if (grid == null || !GodotObject.IsInstanceValid(grid))
        {
            return null;
        }

        var content = GridScrollContainer(grid);
        return GodotObject.IsInstanceValid(content) ? new ScrollSpace(content, grid) : null;
    }

    // The game's generic scrolling container moves its "Content" child; that is what emotes must stick to.
    private static ScrollSpace? ScrollOf(NScrollableContainer? container)
    {
        if (container == null || !GodotObject.IsInstanceValid(container))
        {
            return null;
        }

        var content = ScrollableContent(container);
        return content != null && GodotObject.IsInstanceValid(content) ? new ScrollSpace(content, container) : null;
    }

    /// <summary>The UI button (or widget) in <paramref name="parent"/>'s scene that opens <paramref name="child"/>.</summary>
    public static ButtonTarget? FindButton(UiNodeId parent, UiNodeId child)
    {
        var button = parent.Kind switch
        {
            UiNodeKind.Base => FindBaseButton(child),
            // Wide buttons with centered text: the emote goes at the left end.
            UiNodeKind.PauseMenu => child.Kind switch
            {
                UiNodeKind.Settings => Wide(Submenu<NPauseMenu>(), "%ButtonContainer/Settings"),
                UiNodeKind.Compendium => Wide(Submenu<NPauseMenu>(), "%ButtonContainer/Compendium"),
                _ => null,
            },
            UiNodeKind.CardLibrary when child.Kind == UiNodeKind.CardLibraryPool => PoolButton(child),
            UiNodeKind.CardLibraryPool when child.Kind == UiNodeKind.CardDetail => CardHolderOrPoolButton(parent, child),
            UiNodeKind.RelicCollection when child.Kind == UiNodeKind.RelicDetail => RelicEntryButton(parent, child),
            UiNodeKind.Bestiary when child.Kind == UiNodeKind.BestiaryMonster => BestiaryEntryButton(child),
            UiNodeKind.PlayerDetail when child.Kind == UiNodeKind.PlayerDetailCard => DetailCardButton(parent, child),
            UiNodeKind.Compendium => child.Kind switch
            {
                // Big near-square buttons with a centered picture: the emote goes at the top-left corner.
                UiNodeKind.CardLibrary => Square(Submenu<NCompendiumSubmenu>(), "%CardLibraryButton"),
                UiNodeKind.RelicCollection => Square(Submenu<NCompendiumSubmenu>(), "%RelicCollectionButton"),
                UiNodeKind.PotionLab => Square(Submenu<NCompendiumSubmenu>(), "%PotionLabButton"),
                UiNodeKind.Bestiary => Square(Submenu<NCompendiumSubmenu>(), "%BestiaryButton"),
                UiNodeKind.EnchantmentCompendium => Square(Submenu<NCompendiumSubmenu>(), EnchantmentButtonPath),
                // The bottom row is near-square as well.
                UiNodeKind.Stats => Square(Submenu<NCompendiumSubmenu>(), "%StatisticsButton"),
                UiNodeKind.RunHistory => Square(Submenu<NCompendiumSubmenu>(), "%RunHistoryButton"),
                _ => null,
            },
            _ => null,
        };
        return button.HasValue && GodotObject.IsInstanceValid(button.Value.Control) ? button : null;
    }

    private static ButtonTarget? PoolButton(UiNodeId pool)
    {
        if (pool.Arg >= (ulong)PoolFilterPaths.Length)
        {
            return null;
        }

        return Target(Submenu<NCardLibrary>(), PoolFilterPaths[pool.Arg], ButtonAnchor.Center);
    }

    // The enlarged card was opened from a card of the grid: its holder's top-right corner (the top-left shows the
    // cost). If the grid does not show that card (filters/search), the viewer is on the right page but the card is
    // not available to them: the pool's own button instead.
    private static ButtonTarget? CardHolderOrPoolButton(UiNodeId pool, UiNodeId detail)
    {
        var scroll = GetScrollSpace(pool);
        var grid = scroll?.Viewport as NCardGrid;
        var holder = grid?.CurrentlyDisplayedCardHolders.FirstOrDefault(h => h.CardModel?.Id.ToString() == detail.Key);
        if (holder != null && GodotObject.IsInstanceValid(holder))
        {
            return new ButtonTarget(holder.Hitbox, ButtonAnchor.TopRightCorner, scroll!.Value.Bounds);
        }

        return PoolButton(pool);
    }

    // The enlarged relic was opened from an entry of the collection: that entry (kept inside the scroll view).
    private static ButtonTarget? RelicEntryButton(UiNodeId collection, UiNodeId detail)
    {
        var scroll = GetScrollSpace(collection);
        var entry = FindRelicEntry(Submenu<NRelicCollection>(), detail.Key);
        return entry == null ? null : new ButtonTarget(entry, ButtonAnchor.Center, scroll?.Bounds);
    }

    // The monster page was opened from an entry of the list on the right: just left of that name (the list is not
    // clipped, so vertically it is kept on screen).
    private static ButtonTarget? BestiaryEntryButton(UiNodeId monster)
    {
        var bestiary = Submenu<NBestiary>();
        var list = bestiary?.GetNodeOrNull<Control>("%BestiaryList");
        var entry = list?.GetChildren().OfType<NBestiaryEntry>().FirstOrDefault(e => BestiaryKey(e) == monster.Key);
        if (entry == null)
        {
            return null;
        }

        return new ButtonTarget(entry, ButtonAnchor.LeftOutside,
            GetScrollSpace(new UiNodeId(UiNodeKind.BestiaryList))?.VerticalBounds);
    }

    // The card of the player detail screen that matches (see DetailCardKey): where the emote shows which card it
    // was about.
    private static ButtonTarget? DetailCardButton(UiNodeId player, UiNodeId card)
    {
        if (FindSceneNode(player) is not NMultiplayerPlayerExpandedState detail || DetailPlayer(detail).NetId != player.Arg)
        {
            return null;
        }

        var entry = FindDescendant<NDeckHistoryEntry>(detail, e => DetailCardKey(e.Card) == card.Key);
        // A wide text button (card name and count): the emote goes at its left end.
        return entry == null ? null : new ButtonTarget(entry, ButtonAnchor.LeftEdge);
    }

    private static T? FindDescendant<T>(Node node, Func<T, bool> match) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T candidate && match(candidate))
            {
                return candidate;
            }

            var found = FindDescendant(child, match);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Control? FindRelicEntry(Node? node, string? key)
    {
        if (node == null)
        {
            return null;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is NRelicCollectionEntry entry)
            {
                if (entry.relic?.Id.ToString() == key)
                {
                    return entry;
                }

                continue; // an entry's own children are its icon, not other entries
            }

            var found = FindRelicEntry(child, key);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static ButtonTarget? Wide(Node? scene, string path) => Target(scene, path, ButtonAnchor.LeftEdge);

    private static ButtonTarget? Square(Node? scene, string path) => Target(scene, path, ButtonAnchor.TopLeftCorner);

    private static ButtonTarget? Target(Node? scene, string path, ButtonAnchor anchor)
    {
        var control = scene?.GetNodeOrNull<Control>(path);
        return control == null ? null : new ButtonTarget(control, anchor);
    }

    private static ButtonTarget? FindBaseButton(UiNodeId child)
    {
        var globalUi = NRun.Instance?.GlobalUi;
        if (globalUi == null)
        {
            return null;
        }

        Control? control = child.Kind switch
        {
            UiNodeKind.Map => globalUi.TopBar.Map,
            UiNodeKind.DeckView => globalUi.TopBar.Deck,
            UiNodeKind.PauseMenu => globalUi.TopBar.Pause,
            UiNodeKind.PlayerDetail => globalUi.MultiplayerPlayerContainer.GetChildren()
                .OfType<NMultiplayerPlayerState>()
                .FirstOrDefault(widget => widget.Player.NetId == child.Arg),
            _ => null,
        };
        // The player widgets are wide and show the (possibly long) player name in the middle: the emote goes at the
        // left end, over the character icon. The top bar buttons are small icons: centered.
        var anchor = child.Kind == UiNodeKind.PlayerDetail ? ButtonAnchor.LeftEdge : ButtonAnchor.Center;
        return control == null ? null : new ButtonTarget(control, anchor);
    }

    /// <summary>True if the node is part of the player list (the widgets that open the player detail screens).</summary>
    public static bool IsInPlayerList(Node node)
    {
        var container = NRun.Instance?.GlobalUi.MultiplayerPlayerContainer;
        return container != null && container.IsAncestorOf(node);
    }

    /// <summary>
    /// The scene node whose children are "inside" the given node, or null if the node has no scene of its own
    /// (Base and Map get dedicated layers, see EmoteTracker) or it is not currently alive.
    /// </summary>
    public static Control? FindSceneNode(UiNodeId node)
    {
        Control? scene = node.Kind switch
        {
            UiNodeKind.PauseMenu => Submenu<NPauseMenu>(),
            UiNodeKind.Settings => Submenu<NSettingsScreen>(),
            UiNodeKind.Compendium => Submenu<NCompendiumSubmenu>(),
            UiNodeKind.CardLibrary or UiNodeKind.CardLibraryPool => Submenu<NCardLibrary>(),
            UiNodeKind.EnchantmentCompendium => EnchantmentOverlay(Submenu<NCompendiumSubmenu>()),
            UiNodeKind.RelicCollection => Submenu<NRelicCollection>(),
            UiNodeKind.PotionLab => Submenu<NPotionLab>(),
            UiNodeKind.Bestiary or UiNodeKind.BestiaryMonster or UiNodeKind.BestiaryList => Submenu<NBestiary>(),
            UiNodeKind.Stats => Submenu<NStatsScreen>(),
            UiNodeKind.RunHistory => Submenu<NRunHistory>(),
            UiNodeKind.PlayerDetail or UiNodeKind.DeckView or UiNodeKind.OtherCapstone =>
                NCapstoneContainer.Instance?.CurrentCapstoneScreen as Control,
            _ => null,
        };
        return scene != null && GodotObject.IsInstanceValid(scene) ? scene : null;
    }

    /// <summary>
    /// The character a player picked in the (new run) lobby, or null when there is no such lobby or the player
    /// chose the random character (which has no color of its own).
    /// </summary>
    public static CharacterModel? LobbyCharacter(ulong netId)
    {
        var players = Submenu<NCharacterSelectScreen>()?.Lobby?.Players;
        var character = players?.Find(p => p.id == netId).character;
        if (character != null)
        {
            return character is RandomCharacter ? null : character;
        }

        // The lobby for continuing a saved run: every player's character is fixed by the save.
        var load = Submenu<NMultiplayerLoadGameScreen>() is { } screen ? LoadLobby(screen) : null;
        load ??= Submenu<NCustomRunLoadScreen>() is { } custom ? CustomLoadLobby(custom) : null;
        var saved = load?.Run.Players.FirstOrDefault(p => p.NetId == netId);
        return saved == null ? null : ModelDb.GetById<CharacterModel>(saved.CharacterId);
    }

    private static readonly AccessTools.FieldRef<NMultiplayerLoadGameScreen, LoadRunLobby> LoadLobby =
        AccessTools.FieldRefAccess<NMultiplayerLoadGameScreen, LoadRunLobby>("_runLobby");

    private static readonly AccessTools.FieldRef<NCustomRunLoadScreen, LoadRunLobby> CustomLoadLobby =
        AccessTools.FieldRefAccess<NCustomRunLoadScreen, LoadRunLobby>("_lobby");

    // Submenus are owned by the capstone submenu stack, and are only reachable while that capstone is open.
    private static T? Submenu<T>() where T : NSubmenu
    {
        return (NCapstoneContainer.Instance?.CurrentCapstoneScreen as NCapstoneSubmenuStack)?.Stack.GetSubmenuType<T>();
    }
}
