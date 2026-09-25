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
    private static readonly FieldInfo FieldProjectileNview = AccessTools.Field(typeof(Projectile), "m_nview");

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

    /// <summary>Sets the spawned ItemData of a Projectile using reflection.</summary>
    public static void SetSpawnItem(Projectile projectile, ItemDrop.ItemData item) => FieldSpawnItem.SetValue(projectile, item);

    /// <summary>Gets the ZNetView from a Projectile using reflection.</summary>
    public static ZNetView GetNview(Projectile projectile) => (ZNetView)FieldProjectileNview.GetValue(projectile);

    /// <summary>
    /// Adds a LoyaltyComponent or WeightReserverComponent to a dropped spear item
    /// after it spawns on a surface. Called via transpiler on <see cref="ItemDrop.DropItem"/>.
    /// </summary>
    private static ItemDrop AddLoyaltySpearPickupComponent(ItemDrop item, Projectile projectile)
    {
        var owner = GetProjectileOwner(projectile);

        if (owner is Player player && player == Player.m_localPlayer && item != null && IsPotentiallyThrowable(item.m_itemData))
        {
            if (IsSpear(item.m_itemData))
            {
                // the drop now holds the spear; clearing it stops the projectile dropping a second
                // copy, and stops the destroy rescue below from returning one
                SetSpawnItem(projectile, null);
            }

            if (IsSpear(item.m_itemData) && PluginConfig.ReturnAfterSeconds.Value >= 0f)
            {
                SpearReturn.MakeLoyal(item, player);
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
    /// Postfix on <see cref="ItemDrop"/> Awake: a spear that was thrown earlier and is loaded
    /// again (walked back into range, relogged, respawned) picks up where it left off.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop), "Awake"), HarmonyPostfix]
    private static void ItemDrop_Awake_Postfix(ItemDrop __instance)
    {
        if (PluginConfig.ReturnAfterSeconds.Value < 0f || SpearReturn.GetOwnerId(__instance) == 0L)
        {
            return;
        }

        // the owner is checked on each attempt, since the local player may not exist yet
        SpearReturn.AttachLoyalty(__instance, owner: null);
    }

    /// <summary>
    /// Prefix on <see cref="ZNetScene.Destroy"/>: a spear projectile destroyed while still
    /// carrying its spear — its lifetime ran out flying into the sky, or it hit something vanilla
    /// does not drop items on — would take the spear with it. Return it to the owner instead.
    /// A projectile that already dropped its spear has it cleared, so this does nothing then.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy)), HarmonyPrefix]
    private static void ZNetScene_Destroy_Prefix(GameObject go)
    {
        if (go == null || !go.TryGetComponent<Projectile>(out var projectile))
        {
            return;
        }

        if (PluginConfig.ReturnAfterSeconds.Value < 0f || GetProjectileOwner(projectile) is not Player player || player != Player.m_localPlayer)
        {
            return;
        }

        var nview = GetNview(projectile);

        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            return;
        }

        SpearReturn.RecallProjectile(projectile, player, destroyProjectile: false);
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

        if (distSq > autoReturnDistance * autoReturnDistance && player == Player.m_localPlayer)
        {
            SpearReturn.RecallProjectile(__instance, (Player)player, destroyProjectile: true);
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
            long ownerId = SpearReturn.GetOwnerId(itemDrop);

            // a loyal spear: only its thrower may pick it up, even after the ZDO changed hands
            if (ownerId != 0L)
            {
                return !PluginConfig.EnableBlockAutoPickup.Value || ownerId == player.GetPlayerID();
            }

            var nview = GetNview(itemDrop);
            return !(nview != null && !nview.IsOwner());
        }

        return !BlockAutoPickupDueToReservedWeight(itemDrop, player) && !BlockAutoPickupDueToReservedSlot(itemDrop, player);
    }

    /// <summary>
    /// Checks if auto-pickup of an item should be blocked because it would take the last free
    /// inventory slot(s) that thrown spears still need to come back into.
    /// </summary>
    private static bool BlockAutoPickupDueToReservedSlot(ItemDrop itemDrop, Player player)
    {
        if (!PluginConfig.ReserveThrowSlot.Value || !player.TryGetComponent<PlayerWeightReserverTrackerComponent>(out var tracker))
        {
            return false;
        }

        int slotsToReserve = 0;

        foreach (var reserver in tracker.WeightReservers)
        {
            if (reserver != null && reserver.AttachedItemData != null)
            {
                slotsToReserve++;
            }
        }

        if (slotsToReserve == 0)
        {
            return false;
        }

        var inventory = GetInventory(player);
        var item = itemDrop.m_itemData;

        // fits entirely onto existing stacks, so it needs no new slot
        if (inventory.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) >= item.m_stack)
        {
            return false;
        }

        return inventory.GetEmptySlots() <= slotsToReserve;
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
    /// Portal and cave safety: just before the player teleports (portal, dungeon entrance or
    /// exit), pull every thrown spear back while it is still loaded. Once the player is gone
    /// the spear's area unloads, and before this a spear left behind was simply lost.
    /// Anything that cannot come back now (no room) stays a loyal spear on the ground and
    /// returns the next time it is loaded.
    /// </summary>
    [HarmonyPatch(typeof(Player), "TeleportTo"), HarmonyPrefix]
    private static void TeleportTo_Prefix(Player __instance)
    {
        if (__instance != Player.m_localPlayer || __instance.IsTeleporting() || PluginConfig.ReturnAfterSeconds.Value < 0f)
        {
            return;
        }

        SpearReturn.RecallAll(__instance);
    }

    /// <summary>
    /// Cleanup when the player object is destroyed (death, logout): releases plain weight
    /// reservations. Loyal spears are kept; they find the owner again by player ID.
    /// </summary>
    [HarmonyPatch(typeof(Player), "OnDestroy"), HarmonyPostfix]
    private static void OnDestroy_Postfix(Player __instance)
    {
        var reserverComponents = Object.FindObjectsByType<WeightReserverComponent>(FindObjectsSortMode.None);
        foreach (var reserver in reserverComponents)
        {
            if (reserver is not LoyaltyComponent && reserver.OriginalOwner == __instance)
            {
                Object.Destroy(reserver);
            }
        }
    }
}
