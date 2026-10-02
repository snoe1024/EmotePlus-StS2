using EmotePlus.EmotePlusCode.Config;
using EmotePlus.EmotePlusCode.Reaction;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Reaction;

namespace EmotePlus.EmotePlusCode.Patch;

/// <summary>
/// NReaction.DoAnim with a configurable total time (vanilla: 0.3s appearing + 0.6s waiting + 0.2s fading out = 1.1s).
/// Appearing and fading stay as they are; the waiting part is what grows or shrinks.
/// </summary>
[HarmonyPatch(typeof(NReaction), nameof(NReaction.BeginAnim))]
public static class ReactionAnimPatch
{
    /// <summary>Meta of an emote whose animation starts late: the seconds that have already passed (a double).</summary>
    public static readonly StringName ElapsedMeta = new("emoteplus_elapsed");

    /// <summary>Meta of an emote that must drift a given way (a Vector2 offset); without it the drift is random.</summary>
    public static readonly StringName DriftMeta = new("emoteplus_drift");

    /// <summary>Meta of an emote with its own display time in seconds (a float); else the general setting.</summary>
    public static readonly StringName DisplaySecondsMeta = new("emoteplus_display_seconds");

    public const float AppearTime = 0.3f;
    public const float FadeTime = 0.2f;

    [HarmonyPrefix]
    public static bool Prefix(NReaction __instance)
    {
        var seconds = __instance.HasMeta(DisplaySecondsMeta)
            ? (float)__instance.GetMeta(DisplaySecondsMeta).AsDouble()
            : EmotePlusConfig.EmoteDisplayTime;
        var hold = Mathf.Max(0f, seconds - AppearTime - FadeTime);
        var color = __instance.Modulate;
        color.A = 0f;
        __instance.Modulate = color;

        var drift = __instance.HasMeta(DriftMeta) ? __instance.GetMeta(DriftMeta).AsVector2() : EmoteDrift.Random().Offset;
        var target = __instance.Position + drift;

        var tween = __instance.CreateTween();
        tween.SetParallel();
        tween.TweenProperty(__instance, "position", target, AppearTime).SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        tween.TweenProperty(__instance, "modulate:a", 1f, AppearTime).SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        tween.SetParallel(false);
        tween.TweenProperty(__instance, "modulate:a", 0f, FadeTime).SetDelay(hold).SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Expo);
        tween.TweenCallback(Callable.From(() => __instance.QueueFreeSafely()));

        var elapsed = __instance.GetMeta(ElapsedMeta, 0.0).AsDouble();
        if (elapsed > 0.0)
        {
            tween.CustomStep(elapsed);
        }

        return false;
    }
}
