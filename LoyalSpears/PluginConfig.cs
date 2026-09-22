using BepInEx;
using BepInEx.Configuration;
using Jotunn.Extensions;

namespace LoyalSpears;

/// <summary>
/// Provides synchronized configuration entries for the Loyal Spears mod.
/// All config entries are synced from server to client via Jotunn's config sync system.
/// </summary>
public static class PluginConfig
{
    /// <summary>Prevents other players from auto-picking up thrown spears.</summary>
    public static ConfigEntry<bool> EnableBlockAutoPickup { get; private set; } = null!;

    /// <summary>Seconds until a thrown spear returns to its owner after hitting the ground. 0 or negative disables.</summary>
    public static ConfigEntry<float> ReturnAfterSeconds { get; private set; } = null!;

    /// <summary>Skips auto-return if picking the spear back up would overburden the owner.</summary>
    public static ConfigEntry<bool> BlockReturnIfOverburdened { get; private set; } = null!;

    /// <summary>Counts thrown spear weight as reserved during auto-pickup checks for other items.</summary>
    public static ConfigEntry<bool> ReserveThrowWeight { get; private set; } = null!;

    /// <summary>Safety cap so a lost spear doesn't reserve weight forever.</summary>
    public static ConfigEntry<float> MaxReservationSeconds { get; private set; } = null!;

    /// <summary>If a thrown spear travels further than this distance without hitting ground, it auto-returns. Negative disables.</summary>
    public static ConfigEntry<float> FlightDistanceUntilAutoReturn { get; private set; } = null!;

    /// <summary>
    /// Initializes all configuration entries with Jotunn's BindConfig for automatic server-client sync.
    /// </summary>
    /// <param name="plugin">The plugin instance owning the config file.</param>
    public static void Init(BaseUnityPlugin plugin)
    {
        EnableBlockAutoPickup = plugin.Config.BindConfig("General", "EnableBlockAutoPickup", defaultValue: true,
            description: "Prevent other players from auto-picking up your thrown spears.", synced: true);

        ReturnAfterSeconds = plugin.Config.BindConfig("General", "ReturnAfterSeconds", defaultValue: 5f,
            description: "Seconds until a thrown spear returns to its owner after hitting the ground. 0 or negative disables.", synced: true);

        BlockReturnIfOverburdened = plugin.Config.BindConfig("General", "BlockReturnIfOverburdened", defaultValue: false,
            description: "Skip auto-return if picking it back up would overburden the owner.", synced: true);

        ReserveThrowWeight = plugin.Config.BindConfig("General", "ReserveThrowWeight", defaultValue: true,
            description: "Count thrown spear weight as reserved during auto-pickup checks.", synced: true);

        MaxReservationSeconds = plugin.Config.BindConfig("General", "MaxReservationSeconds", defaultValue: 60f,
            description: "Safety cap so a lost spear doesn't reserve weight forever.", synced: true);

        FlightDistanceUntilAutoReturn = plugin.Config.BindConfig("General", "FlightDistanceUntilAutoReturn", defaultValue: -1f,
            description: "If a thrown spear travels further than this distance from the owner without hitting the ground, it auto-returns. Negative disables.", synced: true);
    }
}
