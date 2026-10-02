using EmotePlus.EmotePlusCode.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Reaction;
using MegaCrit.Sts2.Core.Random;

namespace EmotePlus.EmotePlusCode.Patch;

/// <summary>
/// NReaction.DoAnim with a configurable total time (vanilla: 0.3s appearing + 0.6s waiting + 0.2s fading out = 1.1s).
/// Appearing and fading stay as they are; the waiting part is what grows or shrinks.
/// </summary>
[HarmonyPatch(typeof(NReaction), nameof(NReaction.BeginAnim))]
public static class ReactionAnimPatch
{
    public const float AppearTime = 0.3f;
    public const float FadeTime = 0.2f;

    [HarmonyPrefix]
    public static bool Prefix(NReaction __instance)
    {
        var hold = Mathf.Max(0f, EmotePlusConfig.EmoteDisplayTime - AppearTime - FadeTime);
        var color = __instance.Modulate;
        color.A = 0f;
        __instance.Modulate = color;

        // The same random drift as vanilla.
        var distance = Rng.Chaotic.NextFloat(40f, 60f);
        var degrees = Rng.Chaotic.NextFloat(-30f, 30f);
        var target = __instance.Position + Vector2.Up.Rotated(Mathf.DegToRad(degrees)) * distance;

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
        return false;
    }
}
