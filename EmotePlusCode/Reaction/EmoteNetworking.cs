using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Flavor;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using STS2RitsuLib.Networking.Sidecar;
using EmotePlus.EmotePlusCode.Ui;

namespace EmotePlus.EmotePlusCode.Reaction;

internal static class EmoteNetworking
{
    private const ulong HoldMs = 100;
    private const ulong MatchMs = 1500;
    private const int MaxRecords = 512;
    private static RitsuLibSidecarEndpointHandle? _endpoint;
    private static INetGameService? _service;
    private static Action<NetErrorInfo>? _disconnect;
    private static int _generation;
    private static ulong _sequence;
    private static SceneTree? _tree;
    private static readonly List<Delivery> Deliveries = [];
    private static readonly Dictionary<(ulong, ulong), ulong> Seen = [];
    private readonly record struct Key(ulong Sender, ReactionType Type, Vector2 Screen);
    private sealed class Delivery
    {
        public required Key Key;
        public required ulong At;
        public bool Sidecar;
        public bool Matched;
        public NReactionContainer? Container;
        public ReactionMessage Vanilla;
        public NReaction? Fallback;
        public bool Displayed;
        public ulong Due;
    }

    public static void Initialize()
    {
        Register();
        RitsuLibSidecarSessionManager.SessionBound += change => Bind(change.NetService);
        RitsuLibSidecarSessionManager.SessionUnbound += _ => End();
        EnsurePump();
    }

    private static void Register()
    {
        var generation = _generation;
        _endpoint = RitsuLibSidecarEndpoints.Register(new RitsuLibSidecarEndpointDescriptor(
            ownerId: MainFile.ModId, name: "emotes", protocolVersion: 1, minimumCompatibleProtocolVersion: 1,
            deliveryProfile: RitsuLibSidecarDeliveryProfile.RealtimeDatagram,
            topology: RitsuLibSidecarEndpointTopology.RelayGroup,
            maxPayloadBytes: ContextEmoteMessage.MaxBytes,
            dispatchMode: RitsuLibSidecarEndpointDispatchMode.GodotMainLoop,
            maxOutboundPacketsPerSecond: 100, maxOutboundBytesPerSecond: 102400), message =>
            {
                if (generation == _generation) ReceiveSidecar(message);
            });
    }

    public static void Bind(INetGameService service)
    {
        EnsurePump();
        if (ReferenceEquals(service, _service)) return;
        End();
        if (service.Type == NetGameType.Singleplayer) return;
        _service = service;
        Register();
        _disconnect = _ => { if (ReferenceEquals(_service, service)) End(); };
        service.Disconnected += _disconnect;
    }

    private static void End()
    {
        if (_service != null && _disconnect != null) _service.Disconnected -= _disconnect;
        // Pending vanilla messages already received remain valid when a session changes.
        foreach (var d in Deliveries.Where(d => !d.Sidecar && !d.Displayed && !d.Matched)) ShowVanilla(d);
        Deliveries.Clear(); Seen.Clear();
        _service = null; _disconnect = null; _generation++;
        _endpoint?.Dispose(); _endpoint = null;
    }

    public static void Send(INetGameService service, EmoteType type, Vector2 screen, EmoteDrift drift)
    {
        Bind(service);
        if (service.Type == NetGameType.Singleplayer) return;
        var container = NGame.Instance?.ReactionContainer;
        if (container == null) return;
        var vanilla = new ReactionMessage
        {
            type = ContextEmoteMessage.ToVanilla(type),
            normalizedPosition = NetCursorHelper.GetNormalizedPosition(screen, container),
        };
        var path = UiTree.SenderPath(screen);
        var header = UiTree.ShowsHeader(UiTree.LocalPath(screen)) && UiTree.IsOnHeader(screen);
        var position = path.Length == 0 ? vanilla.normalizedPosition
            : EmoteCoordinates.PositionFor(path, screen, container, header);
        var enhanced = new ContextEmoteMessage(type, path, position, header, drift,
            ContextEmoteMessage.QuantizedScreen(vanilla), ++_sequence);
        try
        {
            var result = _endpoint?.Broadcast(enhanced.Encode());
            EmoteLog.Info($"sidecar send seq={enhanced.Sequence} status={result?.Status}");
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException) { MainFile.Logger.Warn($"Emote Sidecar skipped: {e.Message}"); }
        // Never let a missing route or payload failure prevent the safe vanilla fallback.
        service.SendMessage(vanilla);
    }

    public static void ReceiveVanilla(INetGameService service, NReactionContainer container,
        ReactionMessage message, ulong sender)
    {
        if ((int)message.type is < 1 or > 8) return;
        Bind(service); Pump();
        var now = Time.GetTicksMsec();
        var key = new Key(sender, message.type, ContextEmoteMessage.QuantizedScreen(message));
        var side = Deliveries.FirstOrDefault(d => d.Sidecar && !d.Matched && d.Key == key && now - d.At <= MatchMs);
        if (side != null) { side.Matched = true; return; }
        var delivery = new Delivery { Key = key, At = now, Container = container, Vanilla = message,
            Due = now + (_endpoint?.GetParticipantsSnapshot().Contains(sender) == true ? HoldMs : 0) };
        Deliveries.Add(delivery);
        if (delivery.Due == now) ShowVanilla(delivery);
        Trim(now);
    }

    private static void ReceiveSidecar(RitsuLibSidecarEndpointMessage incoming)
    {
        if (_service == null || incoming.NegotiatedProtocolVersion != 1 || incoming.OriginalSenderNetId == _service.NetId) return;
        if (!ContextEmoteMessage.TryDecode(incoming.Payload.Span, out var message) || message == null)
        { MainFile.Logger.Warn("Rejected invalid emote payload."); return; }
        Pump();
        var now = Time.GetTicksMsec(); var sender = incoming.OriginalSenderNetId;
        if (!Seen.TryAdd((sender, message.Sequence), now)) return;
        var key = new Key(sender, message.VanillaType, message.ScreenPosition);
        var vanilla = Deliveries.FirstOrDefault(d => !d.Sidecar && !d.Matched && d.Key == key && now - d.At <= MatchMs);
        if (vanilla != null)
        {
            vanilla.Matched = true;
            // A late enhanced message must not restart an emote whose vanilla animation already ended.
            if (vanilla.Displayed && !GodotObject.IsInstanceValid(vanilla.Fallback)) return;
            if (GodotObject.IsInstanceValid(vanilla.Fallback))
            {
                vanilla.Fallback!.Visible = false;
                vanilla.Fallback.QueueFree();
            }
        }
        else Deliveries.Add(new Delivery { Key = key, At = now, Sidecar = true });
        var container = NGame.Instance?.ReactionContainer;
        if (container == null) return;
        EmoteLog.Info($"sidecar receive sender={sender} seq={message.Sequence} matched={vanilla != null}");
        if (message.Path.Length == 0)
            LobbyEmote.Show(container, message.Type, sender,
                NetCursorHelper.GetControlSpacePosition(message.Position, container), message.Drift, vanilla == null ? 0 : now - vanilla.At);
        else EmoteTracker.Spawn(container, message.Type, sender, message.Path, message.Position, message.InHeader, message.Drift, vanilla == null ? 0 : now - vanilla.At);
        Trim(now);
    }

    private static void ShowVanilla(Delivery d)
    {
        d.Displayed = true;
        if (d.Container == null || !GodotObject.IsInstanceValid(d.Container) || !d.Container.IsInsideTree()) return;
        // Use the original rendering path: screen coordinates, top layer, no UI anchoring or scroll following.
        var before = d.Container.GetChildren().ToHashSet();
        d.Container.DoRemoteReaction(d.Vanilla.type,
            NetCursorHelper.GetControlSpacePosition(d.Vanilla.normalizedPosition, d.Container));
        d.Fallback = d.Container.GetChildren().OfType<NReaction>().FirstOrDefault(n => !before.Contains(n));
    }

    private static void EnsurePump()
    {
        if (Engine.GetMainLoop() is not SceneTree tree || ReferenceEquals(tree, _tree)) return;
        if (_tree != null && GodotObject.IsInstanceValid(_tree)) _tree.ProcessFrame -= Pump;
        _tree = tree; tree.ProcessFrame += Pump;
    }

    private static void Pump()
    {
        var now = Time.GetTicksMsec();
        foreach (var d in Deliveries.Where(d => !d.Sidecar && !d.Matched && !d.Displayed && now >= d.Due)) ShowVanilla(d);
        Trim(now);
    }

    private static void Trim(ulong now)
    {
        Deliveries.RemoveAll(d => now - d.At > MatchMs && (d.Displayed || d.Matched || d.Sidecar));
        // Preserve pending normal reactions even under a burst; never silently discard their fallback.
        while (Deliveries.Count > MaxRecords)
        {
            var d = Deliveries[0]; if (!d.Sidecar && !d.Matched && !d.Displayed) ShowVanilla(d);
            Deliveries.RemoveAt(0);
        }
        foreach (var key in Seen.Where(p => now - p.Value > 10000).Select(p => p.Key).ToArray()) Seen.Remove(key);
        while (Seen.Count > MaxRecords) Seen.Remove(Seen.First().Key);
    }
}
