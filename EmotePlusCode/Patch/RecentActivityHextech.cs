using System.Reflection;
using System.Runtime.CompilerServices;
using EmotePlus.EmotePlusCode.Reaction;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace EmotePlus.EmotePlusCode.Patch;

// Optional UI integration uses already loaded types only. No assembly dependency, texture readback, polling,
// or model creation is needed: Hextech supplies the exact icon relic to its focus/hover wiring method.
internal static class RecentActivityHextech
{
    private sealed class Binding
    {
        internal RelicModel? Model;
        internal bool Mouse, Focus;
        internal ulong Generation;
        internal RelicModel? Recorded;
    }
    private static readonly ConditionalWeakTable<Control, Binding> Bindings = new();
    private static readonly HashSet<MethodBase> Patched = [];
    private static FieldInfo? CollapseButton;
    private static MethodInfo? IconRelic;

    internal static void Install()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "HextechRunes" &&
            a.GetType("HextechRunes.HextechRuneSelectionScreen", false) != null);
        if (assembly == null) return;
        var method = assembly.GetType("HextechRunes.HextechRuneSelectionScreen")?.GetMethod("AttachRelicHoverTips",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method != null && !Patched.Contains(method))
        {
            var args = method.GetParameters();
            if (args.Length == 3 && args[0].ParameterType == typeof(Control) && args[1].ParameterType == typeof(RelicModel))
                Patch(method, nameof(Attached));
        }
        var collapse = assembly.GetType("HextechRunes.HextechEnemyHexCollapseView", false);
        var update = collapse?.GetMethod("UpdateButtonIcon", BindingFlags.NonPublic | BindingFlags.Static);
        CollapseButton = collapse?.GetField("_button", BindingFlags.NonPublic | BindingFlags.Static);
        IconRelic = assembly.GetType("HextechRunes.MonsterHexCatalog", false)?.GetMethod("GetIconRelicForMonsterHex",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (update != null && !Patched.Contains(update) && CollapseButton != null && IconRelic != null &&
            update.GetParameters() is [{ ParameterType.IsEnum: true }] &&
            IconRelic.GetParameters() is [var kind] && kind.ParameterType == update.GetParameters()[0].ParameterType &&
            typeof(RelicModel).IsAssignableFrom(IconRelic.ReturnType)) Patch(update, nameof(CollapseUpdated));
    }

    private static void Patch(MethodInfo method, string postfix)
    {
        new Harmony(MainFile.ModId + ".Activity.Hextech").Patch(method,
            postfix: new HarmonyMethod(typeof(RecentActivityHextech), postfix));
        Patched.Add(method);
    }

    private static void CollapseUpdated(object __0)
    {
        if (CollapseButton?.GetValue(null) is Control holder && IconRelic?.Invoke(null, [__0]) is RelicModel model)
            Bind(holder, model);
    }

    private static void Attached(Control __0, RelicModel __1) => Bind(__0, __1);

    internal static void Bind(Control holder, RelicModel model)
    {
        if (!Bindings.TryGetValue(holder, out var binding))
        {
            binding = new Binding();
            Bindings.Add(holder, binding);
            holder.MouseEntered += () => { binding.Mouse = true; Record(holder, binding); };
            holder.FocusEntered += () => { binding.Focus = true; Record(holder, binding); };
            holder.MouseExited += () => { binding.Mouse = false; Reset(binding); };
            holder.FocusExited += () => { binding.Focus = false; Reset(binding); };
        }
        binding.Model = model;
    }

    private static void Reset(Binding binding)
    {
        if (!binding.Mouse && !binding.Focus) binding.Recorded = null;
    }

    private static void Record(Control holder, Binding binding)
    {
        if (!RunManager.Instance.IsInProgress || !holder.IsVisibleInTree() || binding.Model == null ||
            ReferenceEquals(binding.Recorded, binding.Model) && binding.Generation == RecentActivityEmotes.Generation) return;
        binding.Recorded = binding.Model;
        binding.Generation = RecentActivityEmotes.Generation;
        RecentActivityEmotes.Record(binding.Model);
    }
}
