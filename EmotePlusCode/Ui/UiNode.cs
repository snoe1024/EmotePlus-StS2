using System.Collections.Generic;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace EmotePlus.EmotePlusCode.Ui;

// The UI screens EmotePlus knows about, as a tree (a node is identified by its path from the root):
//
//   Base                          the room (overlays such as rewards are part of it)
//     Map
//     PlayerDetail(netId)         NMultiplayerPlayerExpandedState, one node per inspected player
//     DeckView
//     PauseMenu
//       Settings
//       Compendium
//         CardLibrary
//           CardLibraryPool(index)   the card pool page being viewed (character / colorless / ...); scrolls
//             CardDetail(card id)    the enlarged card (NInspectCardScreen opened from the library)
//           CardLibraryPanel         the fixed control panel on the left: shared by all pages, never scrolls
//                                    (only ever the sender's node: an emote made on the panel)
//         EnchantmentCompendium       the overlay added by the Enchantment Compendium mod (if installed); scrolls
//         RelicCollection             scrolls
//           RelicDetail(relic id)     the enlarged relic (NInspectRelicScreen opened from the collection)
//         PotionLab                   scrolls (it has no detail screen, only hover tips)
//         Bestiary
//           BestiaryMonster(key)      the monster page: the centered picture etc., fixed on screen
//           BestiaryList              the scrolling monster list on the right, shared by all monster pages
//                                     (only ever the sender's node: an emote made on the list)
//         Stats / RunHistory
//     OtherCapstone               any capstone we do not model yet (feedback, card pile, ...)
public enum UiNodeKind : byte
{
    Base,
    Map,
    PlayerDetail,
    DeckView,
    PauseMenu,
    Settings,
    Compendium,
    CardLibrary,
    RelicCollection,
    PotionLab,
    Bestiary,
    Stats,
    RunHistory,
    OtherCapstone,
    CardLibraryPool,
    CardDetail,
    EnchantmentCompendium,
    CardLibraryPanel,
    RelicDetail,
    BestiaryMonster,
    BestiaryList,
}

/// <param name="Arg">PlayerDetail: the inspected player's net id. CardLibraryPool: the pool index.</param>
/// <param name="Key">CardDetail / RelicDetail / BestiaryMonster: the card's / relic's / monster entry's id.</param>
public readonly record struct UiNodeId(UiNodeKind Kind, ulong Arg = 0, string? Key = null)
{
    public override string ToString() => Key != null ? $"{Kind}({Key})" : Arg == 0 ? Kind.ToString() : $"{Kind}({Arg})";
}

public static class UiPath
{
    public static int CommonLength(IReadOnlyList<UiNodeId> a, IReadOnlyList<UiNodeId> b)
    {
        var length = 0;
        while (length < a.Count && length < b.Count && a[length] == b[length])
        {
            length++;
        }

        return length;
    }

    public static string Describe(IReadOnlyList<UiNodeId> path)
    {
        return path.Count == 0 ? "<none>" : string.Join(" > ", path);
    }

    public static void Write(PacketWriter writer, IReadOnlyList<UiNodeId>? path)
    {
        var count = path?.Count ?? 0;
        writer.WriteByte((byte)count);
        for (var i = 0; i < count; i++)
        {
            var node = path![i];
            writer.WriteByte((byte)node.Kind);
            switch (node.Kind)
            {
                case UiNodeKind.PlayerDetail:
                    writer.WriteULong(node.Arg);
                    break;
                case UiNodeKind.CardLibraryPool:
                    writer.WriteByte((byte)node.Arg);
                    break;
                case UiNodeKind.CardDetail:
                case UiNodeKind.RelicDetail:
                case UiNodeKind.BestiaryMonster:
                    writer.WriteString(node.Key ?? "");
                    break;
            }
        }
    }

    public static UiNodeId[] Read(PacketReader reader)
    {
        var path = new UiNodeId[reader.ReadByte()];
        for (var i = 0; i < path.Length; i++)
        {
            var kind = (UiNodeKind)reader.ReadByte();
            path[i] = kind switch
            {
                UiNodeKind.PlayerDetail => new UiNodeId(kind, reader.ReadULong()),
                UiNodeKind.CardLibraryPool => new UiNodeId(kind, reader.ReadByte()),
                UiNodeKind.CardDetail or UiNodeKind.RelicDetail or UiNodeKind.BestiaryMonster =>
                    new UiNodeId(kind, 0, reader.ReadString()),
                _ => new UiNodeId(kind),
            };
        }

        return path;
    }
}
