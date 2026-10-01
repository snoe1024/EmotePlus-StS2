using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace EmotePlus.EmotePlusCode;

//You're recommended but not required to keep all your code in this package and all your assets in the EmotePlus folder.
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "EmotePlus"; //At the moment, this is used only for the Logger and harmony names.

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        //If you want to use scripts defined in your mod for Godot scenes, uncomment the following line.
        //Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        Harmony harmony = new(ModId);

        harmony.PatchAll(assembly);

        // Log what actually got patched, so a silently-unapplied patch shows up in godot.log right away.
        var patchedMethods = harmony.GetPatchedMethods().ToList();
        Logger.Info($"{ModId}: Harmony patched {patchedMethods.Count} method(s) on startup:");
        foreach (var method in patchedMethods)
        {
            Logger.Info($"{ModId}:   - {method.DeclaringType?.FullName}.{method.Name}");
        }
    }
}