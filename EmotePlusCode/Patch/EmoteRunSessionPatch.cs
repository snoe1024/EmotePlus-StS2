using EmotePlus.EmotePlusCode.Reaction;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace EmotePlus.EmotePlusCode.Patch;

// Some run entry paths replace the lobby connection without creating a new wheel.
[HarmonyPatch(typeof(RunManager), "InitializeShared")]
public static class EmoteRunSessionPatch
{
    [HarmonyPostfix]
    public static void Postfix(RunManager __instance) => EmoteNetworking.Bind(__instance.NetService);
}
