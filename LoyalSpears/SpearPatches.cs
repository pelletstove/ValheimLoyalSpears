using BepInEx.Bootstrap;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace LoyalSpears;

/// <summary>
/// Harmony patches implementing the loyal spears behavior:
/// blocking auto-pickup of thrown spears, returning them to owners after a delay,
/// and managing weight reservations for thrown spears.
/// </summary>
[HarmonyPatch]
internal class SpearPatches
{
    private static readonly FieldInfo FieldOwner = AccessTools.Field(typeof(Projectile), "m_owner");
    private static readonly FieldInfo FieldNview = AccessTools.Field(typeof(Character), "m_nview");
    private static readonly FieldInfo FieldInventory = AccessTools.Field(typeof(Humanoid), "m_inventory");
    private static readonly FieldInfo FieldPlayerProfile = AccessTools.Field(typeof(Game), "m_playerProfile");
    private static readonly FieldInfo FieldItemNview = AccessTools.Field(typeof(ItemDrop), "m_nview");
    private static readonly FieldInfo FieldSpawnItem = AccessTools.Field(typeof(Projectile), "m_spawnItem");

    /// <summary>
    /// Checks whether the given item is a spear (skill type is Spears).
    /// </summary>
    public static bool IsSpear(ItemDrop.ItemData itemData) =>
        itemData != null && itemData.m_shared != null && itemData.m_shared.m_skillType == Skills.SkillType.Spears;

    /// <summary>
    /// Checks whether the given item is a melee weapon (one or two-handed).
    /// </summary>
    public static bool IsWeapon(ItemDrop.ItemData itemData)
    {
        if (itemData == null || itemData.m_shared == null)
        {
            return false;
        }

        switch (itemData.m_shared.m_itemType)
        {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks if a compatible throwable weapon mod (EpicLoot) is installed.
    /// </summary>
    public static bool IsCompatibleThrowableWeaponModInstalled() =>
        Chainloader.PluginInfos.ContainsKey("randyknapp.mods.epicloot");

    /// <summary>
    /// Checks if an incompatible throwable weapon mod (Max Axe) is installed,
    /// which would conflict with Loyal Spears' behavior.
    /// </summary>
    private static bool IsIncompatibleThrowableWeaponModInstalled() =>
        Chainloader.PluginInfos.ContainsKey("neobotics.valheim_mod.maxaxe");

    /// <summary>
    /// Determines if the given item is potentially throwable by the loyal spears system.
    /// Spears are always throwable. Weapons are throwable if EpicLoot is installed and Max Axe is not.
    /// </summary>
    public static bool IsPotentiallyThrowable(ItemDrop.ItemData itemData)
    {
        if (IsSpear(itemData))
        {
            return true;
        }

        if (!IsIncompatibleThrowableWeaponModInstalled() && IsCompatibleThrowableWeaponModInstalled())
        {
            return IsWeapon(itemData);
        }

        return false;
    }

    /// <summary>Gets the ZNetView from a Character using reflection.</summary>
    public static ZNetView GetNview(Character character) => (ZNetView)FieldNview.GetValue(character);

    /// <summary>Gets the ZNetView from an ItemDrop using reflection.</summary>
    public static ZNetView GetNview(ItemDrop itemDrop) => (ZNetView)FieldItemNview.GetValue(itemDrop);

    /// <summary>Gets the Inventory from a Humanoid using reflection.</summary>
    public static Inventory GetInventory(Humanoid humanoid) => (Inventory)FieldInventory.GetValue(humanoid);

    /// <summary>Gets the PlayerProfile from a Game instance using reflection.</summary>
    public static PlayerProfile GetPlayerProfile(Game game) => (PlayerProfile)FieldPlayerProfile.GetValue(game);

    /// <summary>Gets the owning Character of a Projectile using reflection.</summary>
    public static Character GetProjectileOwner(Projectile projectile) => (Character)FieldOwner.GetValue(projectile);

    /// <summary>Gets the spawned ItemData from a Projectile using reflection.</summary>
    public static ItemDrop.ItemData GetSpawnItem(Projectile projectile) => (ItemDrop.ItemData)FieldSpawnItem.GetValue(projectile);

    /// <summary>
    /// Retrieves the local player's death count from the player profile.
    /// </summary>
    public static float GetPlayerDeathCount()
    {
        var profile = GetPlayerProfile(Game.instance);
        return profile.m_playerStats[0].m_stats[PlayerStatType.Deaths];
    }

    /// <summary>
    /// Registers the <c>RPC_PickupLoyaltySpear</c> RPC on player objects during Awake.
    /// </summary>
    [HarmonyPatch(typeof(Player), "Awake"), HarmonyPostfix]
    private static void AddLoyaltySpearRPC(Player __instance)
    {
        var nview = GetNview(__instance);
        nview.Register<ZDOID, float>("RPC_PickupLoyaltySpear",
            (player, item, deaths) => RPC_PickupLoyaltySpear(__instance, item, deaths));
    }

    /// <summary>
    /// Handles the RPC call that returns a spear to its owner.
    /// Validates inventory space, weight capacity, and death count before picking up.
    /// </summary>
    private static void RPC_PickupLoyaltySpear(Player player, ZDOID item, float deathCountOnThrow)
    {
        var instance = ZNetScene.instance.FindInstance(item);

        if (instance == null)
        {
            return;
        }

        var itemDrop = instance.GetComponent<ItemDrop>();

        if (itemDrop == null)
        {
            return;
        }

        var inventory = GetInventory(player);

        if (!inventory.CanAddItem(itemDrop.m_itemData))
        {
            return;
        }

        if (PluginConfig.BlockReturnIfOverburdened.Value)
        {
            if (itemDrop.m_itemData.GetWeight() + inventory.GetTotalWeight() > player.GetMaxCarryWeight())
            {
                return;
            }
        }

        if (deathCountOnThrow >= 0)
        {
            float currentDeathCount = GetPlayerDeathCount();

            // if you die before picking up your spear, the death count check prevents it
            // from teleporting across dimensions back into your inventory
            if (currentDeathCount > deathCountOnThrow)
            {
                return;
            }
        }

        itemDrop.Pickup(player);
    }

    /// <summary>
    /// Adds a LoyaltyComponent or WeightReserverComponent to a dropped spear item
    /// after it spawns on a surface. Called via transpiler on <see cref="ItemDrop.DropItem"/>.
    /// </summary>
    private static ItemDrop AddLoyaltySpearPickupComponent(ItemDrop item, Projectile projectile)
    {
        var owner = GetProjectileOwner(projectile);

        if (owner is Player player && player == Player.m_localPlayer && item != null && IsPotentiallyThrowable(item.m_itemData))
        {
            if (IsSpear(item.m_itemData) && PluginConfig.ReturnAfterSeconds.Value >= 0f)
            {
                if (item.gameObject.TryGetComponent<LoyaltyComponent>(out var loyalty))
                {
                    loyalty.StopTimer();
                }
                else
                {
                    loyalty = item.gameObject.AddComponent<LoyaltyComponent>();
                }

                float deathCount = GetPlayerDeathCount();

                loyalty.Setup(item, player, deathCount);
            }
            else if (PluginConfig.ReserveThrowWeight.Value && PluginConfig.MaxReservationSeconds.Value >= 0f)
            {
                if (item.gameObject.TryGetComponent<WeightReserverComponent>(out var weightReserver))
                {
                    weightReserver.StopTimer();
                }
                else
                {
                    weightReserver = item.gameObject.AddComponent<WeightReserverComponent>();
                }

                weightReserver.Setup(item.m_itemData, player);
            }
        }

        return item;
    }

    /// <summary>
    /// Postfix on <see cref="Projectile.Setup"/> to attach a WeightReserverComponent
    /// to the projectile itself, reserving weight for the thrown item.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "Setup"), HarmonyPostfix]
    public static void Projectile_Setup_Postfix(Projectile __instance)
    {
        var item = GetSpawnItem(__instance);

        if (!PluginConfig.ReserveThrowWeight.Value || PluginConfig.MaxReservationSeconds.Value < 0f)
        {
            return;
        }

        var owner = GetProjectileOwner(__instance);

        if (owner is Player player && player == Player.m_localPlayer && item != null && IsPotentiallyThrowable(item))
        {
            if (__instance.gameObject.TryGetComponent<WeightReserverComponent>(out var weightReserver))
            {
                weightReserver.StopTimer();
            }
            else
            {
                weightReserver = __instance.gameObject.AddComponent<WeightReserverComponent>();
            }

            weightReserver.Setup(item, player);
        }
    }

    /// <summary>
    /// Transpiler on <see cref="Projectile.SpawnOnHit"/> that inserts a call to
    /// <see cref="AddLoyaltySpearPickupComponent"/> after every <see cref="ItemDrop.DropItem"/> call,
    /// so that dropped spears get loyalty tracking components.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "SpawnOnHit"), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Projectile_SpawnOnHit_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo DropItemMethod = AccessTools.DeclaredMethod(typeof(ItemDrop), nameof(ItemDrop.DropItem));
        MethodInfo AddLoyaltyToSpearMethod = AccessTools.DeclaredMethod(typeof(SpearPatches), nameof(AddLoyaltySpearPickupComponent));

        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;

            if (instruction.opcode == OpCodes.Call && instruction.OperandIs(DropItemMethod))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AddLoyaltyToSpearMethod);
            }
        }
    }

    /// <summary>
    /// Postfix on <see cref="Projectile.LateUpdate"/> that auto-returns a spear
    /// if it has traveled too far from the player without hitting anything (e.g., thrown off the world).
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "LateUpdate"), HarmonyPostfix]
    public static void Projectile_LateUpdate_Postfix(Projectile __instance)
    {
        var item = GetSpawnItem(__instance);
        var player = GetProjectileOwner(__instance);

        if (player is not Player || item == null || !IsSpear(item))
        {
            return;
        }

        float autoReturnDistance = PluginConfig.FlightDistanceUntilAutoReturn.Value;

        if (autoReturnDistance < 0)
        {
            return;
        }

        Vector3 v = player.transform.position - __instance.transform.position;
        float distSq = v.sqrMagnitude;

        if (distSq > autoReturnDistance * autoReturnDistance)
        {
            var itemDrop = ItemDrop.DropItem(item, 0, __instance.transform.position, __instance.transform.rotation);
            var itemNview = GetNview(itemDrop);

            var playerNview = GetNview(player);
            playerNview.InvokeRPC("RPC_PickupLoyaltySpear", itemNview.GetZDO().m_uid, -1f);
            FieldSpawnItem.SetValue(__instance, null);
            ZNetScene.instance.Destroy(__instance.gameObject);
        }
    }

    /// <summary>
    /// Transpiler on <see cref="Player.AutoPickup"/> that inserts a call to
    /// <see cref="ShouldAutoPickup"/> to control whether an item should be auto-picked up,
    /// preventing other players from picking up thrown spears and respecting weight reservations.
    /// </summary>
    [HarmonyPatch(typeof(Player), "AutoPickup"), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AutoPickupTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo autoPickupField = AccessTools.DeclaredField(typeof(ItemDrop), nameof(ItemDrop.m_autoPickup));
        MethodInfo shouldAutoPickupMethod = AccessTools.DeclaredMethod(typeof(SpearPatches), nameof(ShouldAutoPickup));

        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(instruction => instruction.LoadsField(autoPickupField)))
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Dup)
            )
            .Advance(1)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0)
            )
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Call, shouldAutoPickupMethod)
            )
            .Instructions();
    }

    /// <summary>
    /// Determines whether an item should be auto-picked up by the player.
    /// Thrown spears are only picked up by their owner. Other items are blocked if reserved weight would cause overburden.
    /// </summary>
    public static bool ShouldAutoPickup(ItemDrop itemDrop, bool isAutoPickupable, Player player)
    {
        if (!itemDrop || itemDrop.m_itemData == null)
        {
            return isAutoPickupable;
        }

        // we never change 'false' to 'true', so we can stop here
        if (!isAutoPickupable)
        {
            return isAutoPickupable;
        }

        if (IsPotentiallyThrowable(itemDrop.m_itemData))
        {
            var nview = GetNview(itemDrop);
            return !(nview != null && !nview.IsOwner());
        }

        return !BlockAutoPickupDueToReservedWeight(itemDrop, player);
    }

    /// <summary>
    /// Checks if auto-pickup of a non-throwable item should be blocked due to reserved weight
    /// from a thrown spear that hasn't been picked up yet.
    /// </summary>
    private static bool BlockAutoPickupDueToReservedWeight(ItemDrop itemDrop, Player player)
    {
        if (!PluginConfig.ReserveThrowWeight.Value || PluginConfig.MaxReservationSeconds.Value < 0f)
        {
            return false;
        }

        float weightToReserve = 0f;

        if (player.TryGetComponent<PlayerWeightReserverTrackerComponent>(out var playerWeightReserverTracker))
        {
            for (int i = playerWeightReserverTracker.WeightReservers.Count - 1; i >= 0; i--)
            {
                WeightReserverComponent weightReserver = playerWeightReserverTracker.WeightReservers[i];
                if (weightReserver != null && weightReserver.AttachedItemData != null)
                {
                    weightToReserve += weightReserver.AttachedItemData.GetWeight();
                }
                else
                {
                    playerWeightReserverTracker.WeightReservers.RemoveAt(i);
                }
            }
        }

        if (weightToReserve <= 0f)
        {
            return false;
        }

        var inventory = GetInventory(player);
        return itemDrop.m_itemData.GetWeight() + inventory.GetTotalWeight() > player.GetMaxCarryWeight() - weightToReserve;
    }

    /// <summary>
    /// Portal safety: when a player teleports (goes through a portal), cleans up
    /// loyalty and weight-reserver components on active projectiles to prevent
    /// spears from getting stuck in another dimension trying to return to the player.
    /// </summary>
    [HarmonyPatch(typeof(Player), "TeleportTo"), HarmonyPostfix]
    private static void TeleportTo_Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
        {
            return;
        }

        var projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        foreach (var proj in projectiles)
        {
            var owner = GetProjectileOwner(proj);
            if (owner is Player p && p == __instance)
            {
                var loyalty = proj.GetComponent<LoyaltyComponent>();
                if (loyalty != null)
                {
                    Object.Destroy(loyalty);
                }

                var reserver = proj.GetComponent<WeightReserverComponent>();
                if (reserver != null)
                {
                    Object.Destroy(reserver);
                }
            }
        }

        // Also clean up any LoyaltyComponents that might try to return spears across dimensions
        var loyaltyComponents = Object.FindObjectsByType<LoyaltyComponent>(FindObjectsSortMode.None);
        foreach (var loyalty in loyaltyComponents)
        {
            if (loyalty.OriginalOwner == __instance)
            {
                Object.Destroy(loyalty);
            }
        }
    }

    /// <summary>
    /// Safety cleanup when the player object is destroyed, removing all
    /// loyalty and weight-reserver components associated with the player.
    /// </summary>
    [HarmonyPatch(typeof(Player), "OnDestroy"), HarmonyPostfix]
    private static void OnDestroy_Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
        {
            return;
        }

        var loyaltyComponents = Object.FindObjectsByType<LoyaltyComponent>(FindObjectsSortMode.None);
        foreach (var loyalty in loyaltyComponents)
        {
            if (loyalty.OriginalOwner == __instance)
            {
                Object.Destroy(loyalty);
            }
        }

        var reserverComponents = Object.FindObjectsByType<WeightReserverComponent>(FindObjectsSortMode.None);
        foreach (var reserver in reserverComponents)
        {
            if (reserver.OriginalOwner == __instance)
            {
                Object.Destroy(reserver);
            }
        }
    }
}
