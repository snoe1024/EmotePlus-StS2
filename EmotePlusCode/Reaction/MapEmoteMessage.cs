using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace EmotePlus.EmotePlusCode.Reaction;

// Sent instead of vanilla's ReactionMessage when the sender emotes while the map screen is open.
// netPosition is NMapScreen.GetNetPositionFromScreenPosition's output (map-space, scroll independent), so the
// receiver can re-project it with its own scroll offset. EmotePlus is affects_gameplay=true, so every peer has
// this type and the auto-assigned message ids line up (see sts2_dev_knowledge multiplayer-networking.md).
internal struct MapEmoteMessage : INetMessage, IPacketSerializable
{
    public ReactionType type;

    public Vector2 netPosition;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteEnum(type);
        writer.WriteVector2(netPosition, NetCursorHelper.quantizeParams, NetCursorHelper.quantizeParams);
    }

    public void Deserialize(PacketReader reader)
    {
        type = reader.ReadEnum<ReactionType>();
        netPosition = reader.ReadVector2(NetCursorHelper.quantizeParams, NetCursorHelper.quantizeParams);
    }
}
