using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using EmotePlus.EmotePlusCode.Ui;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace EmotePlus.EmotePlusCode.Reaction;

// Observe the final texture, after resource replacement and all skin patches. No skin-manager dependency or
// load-order guessing: two players using the same character can still have different rendered icons.
internal static class CharacterArtwork
{
    private sealed class Fingerprint
    {
        internal bool Dirty = true;
        internal int Width, Height;
        internal string? Hash;
    }

    private static readonly ConditionalWeakTable<Texture2D, Fingerprint> Fingerprints = new();

    internal static Texture2D? CurrentIcon(EmoteType target, ulong? senderNetId)
    {
        var run = RunManager.Instance.DebugOnlyGetState();
        var lobby = UiTree.ActiveLobby();
        var localId = LocalContext.NetId ?? lobby?.Service.NetId;
        var playerId = senderNetId ?? localId;
        var character = playerId is { } id
            ? run?.GetPlayer(id)?.Character ?? UiTree.LobbyCharacter(id) : null;
        if (character == null && senderNetId != null && senderNetId != localId) return null;
        if (character != null && CharacterEmotes.TryGetEmote(character, out var current) && current == target)
        {
            var root = (Node?)NRun.Instance?.GlobalUi.MultiplayerPlayerContainer ?? lobby?.Root;
            if (root != null && FindIcon(root, playerId!.Value) is { } observed) return observed;
            // Outside the remote player's selection scope, a model getter can return our own selected skin.
            if (senderNetId != null && senderNetId != localId) return null;
            return character.IconTexture;
        }
        // A different character in the picker has no sender-specific scope; use its final model getter.
        return ModelDb.AllCharacters.FirstOrDefault(c => CharacterEmotes.TryGetEmote(c, out var type) && type == target)?.IconTexture;
    }

    private static Texture2D? FindIcon(Node node, ulong playerId)
    {
        if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion()) return null;
        if ((node is NMultiplayerPlayerState state && state.Player?.NetId == playerId) ||
            (node is NRemoteLobbyPlayer remote && remote.PlayerId == playerId))
            return node.GetNodeOrNull<TextureRect>("%CharacterIcon")?.Texture;
        foreach (var child in node.GetChildren())
            if (FindIcon(child, playerId) is { } texture) return texture;
        return null;
    }

    internal static bool Matches(Texture2D? texture, EmoteImageOverride rule)
    {
        if (!GodotObject.IsInstanceValid(texture)) return false;
        var fingerprint = Fingerprints.GetValue(texture!, t =>
        {
            var entry = new Fingerprint();
            t.Changed += () => entry.Dirty = true;
            return entry;
        });
        if (fingerprint.Dirty)
        {
            fingerprint.Dirty = false;
            fingerprint.Hash = null;
            // Read pixels only on first use or Resource.Changed, never once per frame or for every catalogue icon.
            using var image = texture!.GetImage();
            if (image == null || image.IsEmpty()) return false;
            if (image.IsCompressed() && image.Decompress() != Error.Ok) return false;
            image.ClearMipmaps();
            image.Convert(Image.Format.Rgba8);
            fingerprint.Width = image.GetWidth();
            fingerprint.Height = image.GetHeight();
            fingerprint.Hash = Convert.ToHexString(SHA256.HashData(image.GetData()));
        }
        return fingerprint.Width == rule.Width && fingerprint.Height == rule.Height &&
            string.Equals(fingerprint.Hash, rule.SourceSha256, StringComparison.OrdinalIgnoreCase);
    }
}
