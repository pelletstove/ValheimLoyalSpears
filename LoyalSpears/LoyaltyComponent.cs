using System.Collections;
using UnityEngine;

namespace LoyalSpears;

/// <summary>
/// Component attached to a dropped spear item that handles automatic return to the
/// original owner after a configurable delay. Inherits weight reservation behavior
/// from <see cref="WeightReserverComponent"/>.
/// </summary>
internal class LoyaltyComponent : WeightReserverComponent
{
    // ask IronGate why death count is a float
    /// <summary>Death count of the owner at the time the spear was thrown.</summary>
    private float _ownerDeathCountOnThrow;

    /// <summary>The ItemDrop representing the thrown spear.</summary>
    private ItemDrop _attachedItemDrop;

    /// <summary>
    /// Sets up the loyalty component with the item drop, original owner, and death count
    /// at the time of throwing. Also calls the base Setup to register weight reservation.
    /// </summary>
    public void Setup(ItemDrop attachedItemDrop, Player originalOwner, float ownerDeathCountOnThrow)
    {
        _attachedItemDrop = attachedItemDrop;
        _ownerDeathCountOnThrow = ownerDeathCountOnThrow;

        base.Setup(attachedItemDrop.m_itemData, originalOwner);
    }

    /// <summary>Starts the coroutine that waits before returning the spear.</summary>
    protected override void StartTimer()
    {
        // use string overload because we also use the string overload to potentially stop it:
        // https://docs.unity3d.com/ScriptReference/MonoBehaviour.StopCoroutine.html
        StartCoroutine(nameof(ReturnInABit));
    }

    /// <summary>Stops the return coroutine if it is still running.</summary>
    public override void StopTimer()
    {
        StopCoroutine(nameof(ReturnInABit));
    }

    /// <summary>
    /// Coroutine that waits for <see cref="PluginConfig.ReturnAfterSeconds"/> and then
    /// invokes the RPC to return the spear to the owner. The death count check prevents
    /// returning a spear if the owner has died since throwing it.
    /// </summary>
    private IEnumerator ReturnInABit()
    {
        float seconds = PluginConfig.ReturnAfterSeconds.Value;

        if (seconds > 0)
        {
            yield return new WaitForSeconds(seconds);
        }

        if (OriginalOwner != null && _attachedItemDrop != null && _attachedItemDrop.CanPickup())
        {
            var playerNview = SpearPatches.GetNview(OriginalOwner);
            var itemNview = SpearPatches.GetNview(_attachedItemDrop);
            playerNview.InvokeRPC("RPC_PickupLoyaltySpear", itemNview.GetZDO().m_uid, _ownerDeathCountOnThrow);
        }

        // we only try to return once
        Destroy(this);
    }
}
