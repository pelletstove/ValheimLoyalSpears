using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace LoyalSpears;

/// <summary>
/// Main plugin entry point for Loyal Spears. Handles initialization of config and Harmony patches.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInDependency(Jotunn.Main.ModGuid)]
internal class LoyalSpearsPlugin : BaseUnityPlugin
{
    /// <summary>Unique identifier for this mod.</summary>
    private const string Guid = "com.loyalspears.mod";

    /// <summary>Display name of this mod.</summary>
    private const string Name = "Loyal Spears";

    /// <summary>Version of this mod.</summary>
    private const string Version = "1.1.0";

    private void Awake()
    {
        PluginConfig.Init(this);
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Logger.LogInfo($"{Name} v{Version} loaded");
    }
}
