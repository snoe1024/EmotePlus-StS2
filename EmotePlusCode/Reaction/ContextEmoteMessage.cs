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
internal struct ContextEmoteMessage : INetMessage, IPacketSerializable
{
    public ReactionType type;

    public UiNodeId[]? path;

    public Vector2 position;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteEnum(type);
        UiPath.Write(writer, path);
        writer.WriteVector2(position, EmoteCoordinates.Quantize, EmoteCoordinates.Quantize);
    }

    public void Deserialize(PacketReader reader)
    {
        type = reader.ReadEnum<ReactionType>();
        path = UiPath.Read(reader);
        position = reader.ReadVector2(EmoteCoordinates.Quantize, EmoteCoordinates.Quantize);
    }
}
