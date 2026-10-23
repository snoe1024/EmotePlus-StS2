using System.Text;
using Godot;
using EmotePlus.EmotePlusCode.Ui;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Flavor;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace EmotePlus.EmotePlusCode.Reaction;

// This is an application payload, deliberately not an INetMessage (game IDs must stay unchanged).
internal sealed record ContextEmoteMessage(EmoteType Type, UiNodeId[] Path, Vector2 Position,
    bool InHeader, EmoteDrift Drift, Vector2 ScreenPosition, ulong Sequence)
{
    public const int MaxBytes = 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public ReactionType VanillaType => ToVanilla(Type);
    public static ReactionType ToVanilla(EmoteType type) => (int)type is >= 1 and <= 8
        ? (ReactionType)(int)type : ReactionType.QuestionMark;

    public byte[] Encode()
    {
        if (UiTree.IsLocalOnlyPath(Path)) throw new InvalidDataException("Private overlay emotes must stay local");
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream, Utf8);
        w.Write(Sequence); w.Write(checked((ushort)Type)); w.Write(Position.X); w.Write(Position.Y);
        w.Write(ScreenPosition.X); w.Write(ScreenPosition.Y); w.Write(InHeader);
        w.Write(Drift.Angle); w.Write(Drift.Distance);
        if (Path.Length > 8) throw new InvalidDataException("UI path too long");
        w.Write((byte)Path.Length);
        foreach (var n in Path)
        {
            w.Write((byte)n.Kind); w.Write(n.Arg);
            var key = Utf8.GetBytes(n.Key ?? "");
            if (key.Length > 256) throw new InvalidDataException("UI key too long");
            w.Write((ushort)key.Length); w.Write(key);
        }
        if (stream.Length > MaxBytes) throw new InvalidDataException("Emote payload too large");
        return stream.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> bytes, out ContextEmoteMessage? message)
    {
        message = null;
        if (bytes.Length > MaxBytes) return false;
        try
        {
            using var stream = new MemoryStream(bytes.ToArray());
            using var r = new BinaryReader(stream, Utf8);
            var seq = r.ReadUInt64(); var type = (EmoteType)r.ReadUInt16();
            if (type == EmoteType.None || !Enum.IsDefined(type)) return false;
            var pos = new Vector2(r.ReadSingle(), r.ReadSingle());
            var screen = new Vector2(r.ReadSingle(), r.ReadSingle());
            if (!Valid(pos, 30) || !Valid(screen, 3)) return false;
            var headerByte = r.ReadByte(); if (headerByte > 1) return false;
            var drift = new EmoteDrift(r.ReadByte(), r.ReadByte());
            var count = r.ReadByte(); if (count > 8) return false;
            var path = new UiNodeId[count];
            for (var i = 0; i < count; i++)
            {
                var kind = (UiNodeKind)r.ReadByte(); if (!Enum.IsDefined(kind)) return false;
                if (kind == UiNodeKind.PrivateOverlay) return false;
                var arg = r.ReadUInt64(); var length = r.ReadUInt16();
                if (length > 256 || length > stream.Length - stream.Position) return false;
                var keyed = kind is UiNodeKind.CardDetail or UiNodeKind.RelicDetail
                    or UiNodeKind.BestiaryMonster or UiNodeKind.PlayerDetailCard;
                if (keyed != (length > 0) || kind != UiNodeKind.PlayerDetail && kind != UiNodeKind.CardLibraryPool && arg != 0
                    || kind == UiNodeKind.CardLibraryPool && arg > 255) return false;
                var key = Utf8.GetString(r.ReadBytes(length));
                if (key.Any(char.IsControl)) return false;
                path[i] = new UiNodeId(kind, arg, key.Length == 0 ? null : key);
            }
            if (count > 0 && path[0].Kind != UiNodeKind.Base || stream.Position != stream.Length) return false;
            message = new(type, path, pos, headerByte != 0, drift, screen, seq);
            return true;
        }
        catch (Exception e) when (e is IOException or ArgumentException) { return false; }
    }

    private static bool Valid(Vector2 v, float max) => float.IsFinite(v.X) && float.IsFinite(v.Y)
        && Math.Abs(v.X) <= max && Math.Abs(v.Y) <= max;

    // Both routes use exactly the game's quantized coordinates for matching, including boundary clamping.
    public static Vector2 QuantizedScreen(ReactionMessage message)
    {
        var w = new PacketWriter(); message.Serialize(w);
        var r = new PacketReader(); r.Reset(w.Buffer[..w.BytePosition]);
        var decoded = new ReactionMessage(); decoded.Deserialize(r);
        return decoded.normalizedPosition;
    }
}
