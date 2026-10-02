using EmotePlus.EmotePlusCode.Ui;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace EmotePlus.EmotePlusCode.Reaction;

// The only emote message EmotePlus sends inside a run (vanilla's ReactionMessage is replaced). EmotePlus is
// affects_gameplay=true, so every peer has this type and the auto-assigned message ids line up (see
// sts2_dev_knowledge multiplayer-networking.md).
//
// path     where the sender was in the UI tree (UiTree.LocalPath)
// position in the coordinate system of the LAST node of path (see EmoteCoordinates)
// drift    where the emote drifts to while it appears (see EmoteDrift)
// inHeader the sender made the emote on the header bar (only possible on a screen that shows it, see
//          UiTree.ShowsHeader). Then position is a normalized SCREEN position instead: viewers who also see the
//          header show the emote right there, everybody else resolves it from the path as usual.
internal struct ContextEmoteMessage : INetMessage, IPacketSerializable
{
    public EmoteType type;

    public UiNodeId[]? path;

    public Vector2 position;

    public bool inHeader;

    public EmoteDrift drift;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteByte((byte)type);
        UiPath.Write(writer, path);
        writer.WriteVector2(position, EmoteCoordinates.Quantize, EmoteCoordinates.Quantize);
        writer.WriteBool(inHeader);
        writer.WriteByte(drift.Angle);
        writer.WriteByte(drift.Distance);
    }

    public void Deserialize(PacketReader reader)
    {
        type = (EmoteType)reader.ReadByte();
        path = UiPath.Read(reader);
        position = reader.ReadVector2(EmoteCoordinates.Quantize, EmoteCoordinates.Quantize);
        inHeader = reader.ReadBool();
        drift = new EmoteDrift(reader.ReadByte(), reader.ReadByte());
    }
}
