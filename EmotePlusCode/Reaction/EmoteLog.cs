using Godot;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace EmotePlus.EmotePlusCode.Reaction;

// Diagnostics for tracing where an emote ends up. Everything goes to godot.log with the "[EmotePlus][emote]" tag.
public static class EmoteLog
{
    public static void Info(string message)
    {
        MainFile.Logger.Info($"[emote] {message}");
    }

    public static string DescribeContainer(NReactionContainer? container)
    {
        if (container == null || !GodotObject.IsInstanceValid(container))
        {
            return "container=<invalid>";
        }

        return $"container(inTree={container.IsInsideTree()}, visibleInTree={container.IsVisibleInTree()}, " +
               $"size={container.Size}, scale={container.Scale}, childCount={container.GetChildCount()}, " +
               $"index={container.GetIndex()}, parent={container.GetParent()?.Name}, " +
               $"canvasXform={container.GetGlobalTransformWithCanvas().Origin})";
    }

    public static string DescribeMap()
    {
        var map = NMapScreen.Instance;
        if (map == null || !GodotObject.IsInstanceValid(map))
        {
            return "map=<none>";
        }

        return $"map(isOpen={map.IsOpen}, visible={map.Visible}, visibleInTree={map.IsVisibleInTree()}, " +
               $"globalPos={map.GlobalPosition}, size={map.Size}, index={map.GetIndex()})";
    }

    public static string DescribeViewer()
    {
        return $"viewer={ViewerContext.Current}, {DescribeMap()}";
    }
}
