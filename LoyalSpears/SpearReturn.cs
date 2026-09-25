using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace LoyalSpears;

/// <summary>
/// Everything needed to get a thrown spear back into its owner's inventory, wherever it ended up.
/// </summary>
internal static class SpearReturn
{
    /// <summary>ZDO key holding the player ID of the player who threw the spear.</summary>
    public static readonly int OwnerHash = "LoyalSpears_Owner".GetStableHashCode();

    /// <summary>Minimum seconds between two "no room" messages.</summary>
    private const float NoRoomMessageInterval = 10f;

    private static readonly MethodInfo InventoryChanged = AccessTools.Method(typeof(Inventory), "Changed");

    private static float _nextNoRoomMessage;

    /// <summary>Player ID stored on the spear's ZDO, or 0 if it is not a loyal spear.</summary>
    public static long GetOwnerId(ItemDrop drop)
    {
        var nview = SpearPatches.GetNview(drop);
        return nview != null && nview.IsValid() ? nview.GetZDO().GetLong(OwnerHash) : 0L;
    }

    /// <summary>True if <paramref name="drop"/> is a loyal spear thrown by <paramref name="player"/>.</summary>
    public static bool IsOwnedBy(ItemDrop drop, Player player)
    {
        long ownerId = GetOwnerId(drop);
        return ownerId != 0L && ownerId == player.GetPlayerID();
    }

    /// <summary>
    /// Marks a freshly dropped spear as belonging to <paramref name="owner"/> and starts
    /// bringing it back. The mark is stored on the ZDO, so it survives unloading and relogs.
    /// </summary>
    public static LoyaltyComponent MakeLoyal(ItemDrop drop, Player owner, float? delay = null)
    {
        var nview = SpearPatches.GetNview(drop);

        if (nview != null && nview.IsValid() && nview.IsOwner())
        {
            nview.GetZDO().Set(OwnerHash, owner.GetPlayerID());
        }

        return AttachLoyalty(drop, owner, delay);
    }

    /// <summary>Adds (or resets) the <see cref="LoyaltyComponent"/> on a spear.</summary>
    public static LoyaltyComponent AttachLoyalty(ItemDrop drop, Player owner, float? delay = null)
    {
        if (!drop.TryGetComponent<LoyaltyComponent>(out var loyalty))
        {
            loyalty = drop.gameObject.AddComponent<LoyaltyComponent>();
        }

        loyalty.Setup(drop, owner, delay);
        return loyalty;
    }

    /// <summary>
    /// Tries to put a dropped spear back into <paramref name="player"/>'s inventory, always in the
    /// slot it was thrown from: whatever took that slot moves to another free slot, or is dropped
    /// if the inventory is full. Returns false if it has to wait (mid-teleport, dead, waiting on
    /// network ownership, slot held by an undroppable item); the caller retries later.
    /// </summary>
    public static bool TryReturn(ItemDrop drop, Player player)
    {
        if (drop == null || player == null || player.IsDead() || player.IsTeleporting())
        {
            return false;
        }

        var nview = SpearPatches.GetNview(drop);

        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        // someone else's client owns the ZDO (multiplayer); ask for it and retry next time
        if (!drop.CanPickup(autoPickupDelay: false))
        {
            drop.RequestOwn();
            return false;
        }

        drop.Load();
        var item = drop.m_itemData;
        var inventory = SpearPatches.GetInventory(player);

        if (PluginConfig.BlockReturnIfOverburdened.Value && item.GetWeight() + inventory.GetTotalWeight() > player.GetMaxCarryWeight())
        {
            return false;
        }

        // AddItem picks the first empty slot, so remember where the spear lived before the throw
        Vector2i originalSlot = item.m_gridPos;

        if (IsInside(inventory, originalSlot))
        {
            // the spear always gets its old slot back; whatever took it is moved or dropped
            var occupant = inventory.GetItemAt(originalSlot.x, originalSlot.y);

            if (occupant != null && !MakeRoom(player, inventory, occupant, item))
            {
                NotifyNoRoom(player, item);
                return false;
            }
        }
        else if (!inventory.CanAddItem(item))
        {
            NotifyNoRoom(player, item);
            return false;
        }

        if (!player.Pickup(drop.gameObject, autoequip: true, autoPickupDelay: false))
        {
            return false;
        }

        RestoreSlot(inventory, item, originalSlot);
        return true;
    }

    /// <summary>
    /// Brings back a spear that is still in flight. It is dropped at the owner's feet and picked
    /// up from there, so if the owner has no room it stays there as a loyal spear and keeps
    /// trying — it never just disappears.
    /// </summary>
    public static void ReturnInFlight(Player player, ItemDrop.ItemData item, Vector3 fallbackPosition)
    {
        Vector3 position = player != null ? player.transform.position + Vector3.up : fallbackPosition;
        var drop = ItemDrop.DropItem(item, 0, position, Quaternion.identity);

        if (player == null)
        {
            return;
        }

        var loyalty = MakeLoyal(drop, player, delay: 0f);

        if (!TryReturn(drop, player))
        {
            loyalty.ReturnNow();
        }
    }

    /// <summary>
    /// Pulls every one of <paramref name="player"/>'s spears back right now — in flight or on
    /// the ground. Used just before a teleport, while the spears are still loaded.
    /// </summary>
    public static void RecallAll(Player player)
    {
        foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            if (SpearPatches.GetProjectileOwner(projectile) == player)
            {
                RecallProjectile(projectile, player, destroyProjectile: true);
            }
        }

        foreach (var loyalty in Object.FindObjectsByType<LoyaltyComponent>(FindObjectsSortMode.None))
        {
            if (loyalty.Drop != null && IsOwnedBy(loyalty.Drop, player))
            {
                TryReturn(loyalty.Drop, player);
            }
        }
    }

    /// <summary>
    /// Takes the spear out of an in-flight projectile and returns it to <paramref name="player"/>.
    /// Does nothing if the projectile is not carrying one of the player's spears.
    /// </summary>
    public static void RecallProjectile(Projectile projectile, Player player, bool destroyProjectile)
    {
        var item = SpearPatches.GetSpawnItem(projectile);

        if (item == null || !SpearPatches.IsSpear(item))
        {
            return;
        }

        // clear it first so the projectile cannot also drop it when it hits or times out
        SpearPatches.SetSpawnItem(projectile, null);
        ReturnInFlight(player, item, projectile.transform.position);

        if (destroyProjectile)
        {
            ZNetScene.instance.Destroy(projectile.gameObject);
        }
    }

    /// <summary>Moves <paramref name="item"/> to <paramref name="slot"/> if that slot is empty.</summary>
    private static void RestoreSlot(Inventory inventory, ItemDrop.ItemData item, Vector2i slot)
    {
        if (item.m_gridPos == slot || !inventory.ContainsItem(item) || !IsInside(inventory, slot))
        {
            return;
        }

        if (inventory.GetItemAt(slot.x, slot.y) != null)
        {
            return;
        }

        item.m_gridPos = slot;
        InventoryChanged.Invoke(inventory, new object[] { false, false });
    }

    private static bool IsInside(Inventory inventory, Vector2i slot) =>
        slot.x >= 0 && slot.y >= 0 && slot.x < inventory.GetWidth() && slot.y < inventory.GetHeight();

    /// <summary>
    /// Clears the spear's old slot: moves <paramref name="occupant"/> to another empty slot, or,
    /// if the inventory is full, drops it at the player's feet. False if it can't be dropped
    /// (quest items).
    /// </summary>
    private static bool MakeRoom(Player player, Inventory inventory, ItemDrop.ItemData occupant, ItemDrop.ItemData spear)
    {
        for (int y = 0; y < inventory.GetHeight(); y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                if (inventory.GetItemAt(x, y) == null)
                {
                    occupant.m_gridPos = new Vector2i(x, y);
                    InventoryChanged.Invoke(inventory, new object[] { false, false });
                    return true;
                }
            }
        }

        if (!player.DropItem(inventory, occupant, occupant.m_stack))
        {
            return false;
        }

        player.Message(MessageHud.MessageType.Center, $"Dropped {occupant.m_shared.m_name} to make room for your {spear.m_shared.m_name}");
        return true;
    }

    private static void NotifyNoRoom(Player player, ItemDrop.ItemData item)
    {
        if (Time.time < _nextNoRoomMessage)
        {
            return;
        }

        _nextNoRoomMessage = Time.time + NoRoomMessageInterval;
        // MessageHud localizes the $token in the item name
        player.Message(MessageHud.MessageType.Center, $"No room for your {item.m_shared.m_name} - it will return when you free its slot");
    }
}
