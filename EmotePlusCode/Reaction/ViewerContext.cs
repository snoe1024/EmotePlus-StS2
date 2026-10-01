using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Reaction;

public enum UiContext
{
    /// <summary>Not in a run (no run UI to anchor to).</summary>
    None,

    /// <summary>The base scene: no capstone and no map open (overlays are still treated as the base scene).</summary>
    Room,

    Map,

    /// <summary>
    /// The player detail screen (NMultiplayerPlayerExpandedState, opened by clicking a remote player's widget).
    /// A capstone, so a direct child of the base scene.
    /// </summary>
    PlayerDetail,

    /// <summary>Some other capstone (deck view, pause menu, ...). Not supported yet.</summary>
    Other,
}

// Where the local player currently is in the UI tree. Only the cases needed so far (Room / Map / PlayerDetail).
// Mirrors ActiveScreenContext's priority: capstone > map > the rest.
public static class ViewerContext
{
    public static UiContext Current
    {
        get
        {
            if (NRun.Instance == null)
            {
                return UiContext.None;
            }

            var capstone = NCapstoneContainer.Instance;
            if (capstone?.InUse == true)
            {
                return capstone.CurrentCapstoneScreen is NMultiplayerPlayerExpandedState
                    ? UiContext.PlayerDetail
                    : UiContext.Other;
            }

            return NMapScreen.Instance?.IsOpen == true ? UiContext.Map : UiContext.Room;
        }
    }
}
