using EmotePlus.EmotePlusCode.Config;
using EmotePlus.EmotePlusCode.Ui;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>The source space, independent of the viewer's current screen.</summary>
public enum ExternalEmoteSpace { Room, Map, Lobby }

/// <summary>Why an external presentation must stop.</summary>
public enum ExternalEmoteEnd { Released, Expired, Replaced, ContainerRemoved, SessionEnded, SceneRemoved }

/// <summary>
/// Optional local presentation handoff, API version 3. All calls and callbacks run on the Godot main thread.
/// Claiming replaces direct presentation; substitute-position navigation hints remain managed by EmotePlus.
/// </summary>
public static class ExternalEmotes
{
    /// <summary>Version of this local API; unrelated to the network protocol.</summary>
    public const int ApiVersion = 4;
    /// <summary>
    /// Raised synchronously before drawing, once per eligible source revision. Call TryClaim during the callback.
    /// A late Sidecar replacement cancels the old claim before offering the corrected source with the same Id.
    /// </summary>
    public static event Action<ExternalEmoteOffer>? Offered;
    private static ulong _nextId;
    private static readonly List<ExternalEmoteClaim> Claims = [];
    private static SceneTree? _tree;

    internal static ulong NewId() => ++_nextId;

    internal static ExternalEmoteClaim? Offer(NReactionContainer container, EmoteType type, ulong sender,
        UiNodeId[] path, Vector2 position, bool header, bool vanilla, ulong elapsedMs, ulong id = 0)
    {
        var lobby = NRun.Instance == null ? UiTree.ActiveLobby() : null;
        if (header || (NRun.Instance == null && lobby == null) || (!vanilla &&
            !(path.Length == 0 && lobby != null) &&
            !(path.Length == 1 && path[0].Kind == UiNodeKind.Base) &&
            !(path.Length == 2 && path[0].Kind == UiNodeKind.Base && path[1].Kind == UiNodeKind.Map))) return null;
        if (NRun.Instance == null && path.Length != 0) return null;
        if (NRun.Instance != null && !vanilla && path.Length == 0) return null;
        if (Offered == null) return null;
        var space = lobby != null ? ExternalEmoteSpace.Lobby :
            !vanilla && path[^1].Kind == UiNodeKind.Map ? ExternalEmoteSpace.Map : ExternalEmoteSpace.Room;
        if (space == ExternalEmoteSpace.Map && NMapScreen.Instance == null) return null;
        var offer = new ExternalEmoteOffer(id == 0 ? NewId() : id, type, sender, container, space,
            position, vanilla, elapsedMs, space == ExternalEmoteSpace.Map
                ? EmotePlusConfig.MapEmoteDisplayTime : EmotePlusConfig.EmoteDisplayTime, lobby);
        foreach (Action<ExternalEmoteOffer> callback in Offered.GetInvocationList())
        {
            try { callback(offer); }
            catch (Exception e)
            {
                offer.AbortClaim();
                MainFile.Logger.Warn($"External emote subscriber failed: {e}");
            }
            if (offer.Claim != null) break;
        }
        offer.Close();
        if (offer.Claim == null) return null;
        Claims.Add(offer.Claim);
        if (_tree == null)
        {
            _tree = container.GetTree();
            _tree.ProcessFrame += Pump;
        }
        return offer.Claim;
    }

    private static void Pump()
    {
        foreach (var claim in Claims.ToArray())
        {
            if (!claim.IsActive) Claims.Remove(claim);
            else if (!GodotObject.IsInstanceValid(claim.Source.Container) || !claim.Source.Container.IsInsideTree())
                claim.End(ExternalEmoteEnd.ContainerRemoved);
            else if (!GodotObject.IsInstanceValid(claim.Source.SceneRoot) || !claim.Source.SceneRoot.IsInsideTree())
                claim.End(ExternalEmoteEnd.SceneRemoved);
            else if (!claim.Source.IsSceneCurrent) claim.End(ExternalEmoteEnd.SceneRemoved);
            else if (!claim.ManagesLifetime && claim.Source.ElapsedSeconds >= claim.Source.LifetimeSeconds)
                claim.End(ExternalEmoteEnd.Expired);
        }
        if (Claims.Count == 0 && _tree != null)
        {
            _tree.ProcessFrame -= Pump;
            _tree = null;
        }
    }

    internal static void EndSession()
    {
        foreach (var claim in Claims.ToArray()) claim.End(ExternalEmoteEnd.SessionEnded);
    }
}

/// <summary>Immutable source data and a synchronous opportunity to replace its presentation.</summary>
public sealed class ExternalEmoteOffer
{
    private bool _open = true;
    private readonly ulong _started;
    private readonly double _initialElapsed;
    private readonly UiTree.LobbyContext? _lobby;
    internal ExternalEmoteClaim? Claim { get; private set; }
    internal ExternalEmoteOffer(ulong id, EmoteType type, ulong sender, NReactionContainer container,
        ExternalEmoteSpace space, Vector2 position, bool vanilla, ulong elapsedMs, float lifetime,
        UiTree.LobbyContext? lobby = null)
    {
        Id = id; Type = type; SenderNetId = sender; Container = container; Space = space;
        Position = position; IsVanilla = vanilla; LifetimeSeconds = lifetime;
        _lobby = lobby;
        SceneRoot = lobby?.Root ?? (space == ExternalEmoteSpace.Map ? NMapScreen.Instance! :
            NRun.Instance!.GetNode<NSceneContainer>("%RoomContainer").CurrentScene ?? NRun.Instance);
        _started = Time.GetTicksMsec(); _initialElapsed = elapsedMs / 1000.0;
    }
    /// <summary>Process-local logical ID. A corrected late delivery reuses this ID.</summary>
    public ulong Id { get; }
    /// <summary>Emote asset identifier.</summary>
    public EmoteType Type { get; }
    /// <summary>Authenticated transport sender, or local player's ID.</summary>
    public ulong SenderNetId { get; }
    /// <summary>Container supplying the game UI screen coordinate system, including lobbies.</summary>
    public NReactionContainer Container { get; }
    /// <summary>The actual source map, room or lobby screen; hiding the map does not end ownership.</summary>
    public Node SceneRoot { get; }
    /// <summary>Explicit source space; this is never a substitute button.</summary>
    public ExternalEmoteSpace Space { get; }
    /// <summary>Map net coordinates for Map; NetCursorHelper normalized screen coordinates for Room and Lobby.</summary>
    public Vector2 Position { get; }
    /// <summary>True for a vanilla delivery, which carries no source UI information and is treated as Room.</summary>
    public bool IsVanilla { get; }
    /// <summary>Total local lifetime, captured from settings at the time of handoff.</summary>
    public float LifetimeSeconds { get; }
    /// <summary>Elapsed local seconds, including time before late Sidecar replacement.</summary>
    public double ElapsedSeconds => _initialElapsed + (Time.GetTicksMsec() - _started) / 1000.0;
    /// <summary>Default visual size in game UI pixels.</summary>
    public Vector2 Size => new(80, 80);
    /// <summary>Converts original coordinates to the current viewer's screen, without clamping or drift.</summary>
    public Vector2? ScreenPosition => Space == ExternalEmoteSpace.Lobby
        ? MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput.NetCursorHelper.GetControlSpacePosition(Position, Container)
        : EmoteCoordinates.ToScreen(
        new UiNodeId(Space == ExternalEmoteSpace.Map ? UiNodeKind.Map : UiNodeKind.Base), Position, Container);

    internal bool IsSceneCurrent => _lobby is not { } source ||
        UiTree.ActiveLobby() is { } current && ReferenceEquals(source.Identity, current.Identity) &&
        ReferenceEquals(source.Root, current.Root) && ReferenceEquals(source.Service, current.Service);

    /// <summary>
    /// Transfers direct drawing and lifetime animation to the caller. First claimant wins; only valid during Offered.
    /// The cancellation callback must discard the simulation body. Dispose ends ownership, without replaying the emote.
    /// </summary>
    public ExternalEmoteClaim? TryClaim(Action<ExternalEmoteEnd> cancelled)
        => TryClaim(cancelled, manageLifetime: false);

    /// <summary>
    /// Optionally delegates expiration to the consumer, which must Dispose the claim when retention ends.
    /// With manageLifetime=true even an aged source revision can be claimed. Scene, container, session and
    /// replacement cancellation remain mandatory; the original local LifetimeSeconds and age are unchanged.
    /// </summary>
    public ExternalEmoteClaim? TryClaim(Action<ExternalEmoteEnd> cancelled, bool manageLifetime)
    {
        ArgumentNullException.ThrowIfNull(cancelled);
        if (!_open || Claim != null || (!manageLifetime && ElapsedSeconds >= LifetimeSeconds)) return null;
        return Claim = new ExternalEmoteClaim(this, cancelled, manageLifetime);
    }
    internal void Close() => _open = false;
    internal void AbortClaim()
    {
        Claim?.End(ExternalEmoteEnd.Released);
        Claim = null;
    }
}

/// <summary>Exclusive local presentation ownership; EmotePlus never positions or animates its optional visual.</summary>
public sealed class ExternalEmoteClaim : IDisposable
{
    private readonly Action<ExternalEmoteEnd> _cancelled;
    private NReaction? _visual;
    private readonly EmoteDefinition? _artwork;
    internal ExternalEmoteClaim(ExternalEmoteOffer source, Action<ExternalEmoteEnd> cancelled, bool manageLifetime)
    {
        Source = source; _cancelled = cancelled; ManagesLifetime = manageLifetime;
        if (!source.IsVanilla) _artwork = EmoteArtworkOverrides.Resolve(EmoteCatalog.Get(source.Type), source.SenderNetId);
    }
    /// <summary>Stable source and lifetime information.</summary>
    public ExternalEmoteOffer Source { get; }
    /// <summary>
    /// The immutable artwork snapshot used by CreateVisual, including any sender-specific replacement.
    /// Null for vanilla presentation, which uses the game's original ReactionType texture instead.
    /// Read Category and AssetName directly; resolving Type again can select a different skin.
    /// </summary>
    public EmoteDefinition? Artwork => _artwork;
    /// <summary>True when the consumer is responsible for expiration and must eventually Dispose this claim.</summary>
    public bool ManagesLifetime { get; }
    /// <summary>False after expiry, replacement, session end, or disposal.</summary>
    public bool IsActive { get; private set; } = true;
    /// <summary>
    /// Creates at most one detached, unanimated 80x80 visual. Add it to your own parent and control position,
    /// visibility, alpha and simulation. Do not call BeginAnim. EmotePlus frees it when the claim ends.
    /// </summary>
    public NReaction CreateVisual()
    {
        if (!IsActive) throw new InvalidOperationException("The emote claim has ended.");
        if (GodotObject.IsInstanceValid(_visual)) return _visual!;
        _visual = Source.IsVanilla
            ? NReaction.Create((MegaCrit.Sts2.Core.Entities.Multiplayer.ReactionType)(int)Source.Type)
            : EmoteTracker.CreateEmote(_artwork!, EmoteTracker.OutlineColorOf(Source.Space == ExternalEmoteSpace.Lobby
                ? UiTree.LobbyCharacter(Source.SenderNetId)
                : RunManager.Instance.DebugOnlyGetState()?.GetPlayer(Source.SenderNetId)?.Character));
        _visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _visual.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _visual.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _visual.Size = Source.Size;
        _visual.PivotOffset = Source.Size / 2;
        _visual.Modulate = Colors.White;
        return _visual;
    }
    /// <summary>Stops external presentation permanently; does not restart ordinary drawing.</summary>
    public void Dispose() => End(ExternalEmoteEnd.Released);
    internal void End(ExternalEmoteEnd reason)
    {
        if (!IsActive) return;
        IsActive = false;
        if (GodotObject.IsInstanceValid(_visual)) { _visual!.Hide(); _visual.QueueFree(); }
        try { _cancelled(reason); }
        catch (Exception e) { MainFile.Logger.Warn($"External emote cancellation failed: {e}"); }
    }
}
