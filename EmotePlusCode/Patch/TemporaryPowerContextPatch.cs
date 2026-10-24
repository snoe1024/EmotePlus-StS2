using System.Reflection;
using EmotePlus.EmotePlusCode.Reaction;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Patch;

[HarmonyPatch]
internal static class TemporaryPowerContextPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(PowerCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => !m.IsGenericMethod && (m.Name is "Apply" or "ModifyAmount") &&
            m.GetParameters().Any(p => p.ParameterType == typeof(PowerModel)));

    [HarmonyPrefix]
    private static void Prefix(MethodBase __originalMethod, object[] __args, out TemporaryPowerContext.Scope? __state)
    {
        __state = TemporaryPowerContext.Current.Value;
        if (__args.OfType<PowerModel>().FirstOrDefault() is not ITemporaryPower temporary) return;
        var power = (PowerModel)temporary;
        var owner = __originalMethod.Name == "Apply" ? __args.OfType<Creature>().FirstOrDefault() : power.Owner;
        if (owner != null)
            TemporaryPowerContext.Current.Value = new(owner, temporary.InternallyAppliedPower.GetType(), __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(TemporaryPowerContext.Scope? __state)
    {
        // The original async method has captured the scope for its continuations. Restore only its caller.
        TemporaryPowerContext.Current.Value = __state;
    }
}
