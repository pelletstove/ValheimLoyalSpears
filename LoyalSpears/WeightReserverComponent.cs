using System.Collections;
using UnityEngine;

namespace LoyalSpears;

/// <summary>
/// Component attached to items/projectiles that reserves a portion of the owner's
/// carrying capacity while the item is unaccounted for (e.g., a thrown spear in flight).
/// This prevents auto-pickup of other items that would cause overburden when the
/// thrown spear is eventually recovered.
/// </summary>
internal class WeightReserverComponent : MonoBehaviour
{
    /// <summary>The item data whose weight is being reserved.</summary>
    public ItemDrop.ItemData AttachedItemData => _attachedItemData;

    /// <summary>The original owner who threw the item.</summary>
    public Player OriginalOwner => _originalOwner;

    /// <summary>The item data whose weight is being reserved.</summary>
    protected ItemDrop.ItemData _attachedItemData;

    /// <summary>The original owner who threw the item.</summary>
    protected Player _originalOwner;

    /// <summary>
    /// Initializes the weight reserver with the item data and owner.
    /// Registers itself with the owner's <see cref="PlayerWeightReserverTrackerComponent"/>.
    /// If the owner is null, the component is immediately destroyed.
    /// </summary>
    public void Setup(ItemDrop.ItemData attachedItemData, Player originalOwner)
    {
        if (originalOwner == null)
        {
            Destroy(this);
            return;
        }

        _attachedItemData = attachedItemData;
        _originalOwner = originalOwner;

        if (!originalOwner.TryGetComponent<PlayerWeightReserverTrackerComponent>(out var playerWeightReserverTracker))
        {
            playerWeightReserverTracker = originalOwner.gameObject.AddComponent<PlayerWeightReserverTrackerComponent>();
        }

        playerWeightReserverTracker.WeightReservers.Add(this);

        StartTimer();
    }

    /// <summary>Starts the coroutine that ends the reservation after a timeout.</summary>
    protected virtual void StartTimer()
    {
        // use string overload because we also use the string overload to potentially stop it:
        // https://docs.unity3d.com/ScriptReference/MonoBehaviour.StopCoroutine.html
        StartCoroutine(nameof(UnreserveInABit));
    }

    /// <summary>Stops the reservation timeout coroutine.</summary>
    public virtual void StopTimer()
    {
        StopCoroutine(nameof(UnreserveInABit));
    }

    /// <summary>
    /// Coroutine that waits for <see cref="PluginConfig.MaxReservationSeconds"/> and then
    /// destroys this component, releasing the reserved weight.
    /// </summary>
    private IEnumerator UnreserveInABit()
    {
        float seconds = PluginConfig.MaxReservationSeconds.Value;

        if (seconds > 0)
        {
            yield return new WaitForSeconds(seconds);
        }

        Destroy(this);
    }

    /// <summary>
    /// Removes this component from the owner's tracker when destroyed.
    /// </summary>
    public void OnDestroy()
    {
        if (_originalOwner.TryGetComponent<PlayerWeightReserverTrackerComponent>(out var playerWeightReserverTracker))
        {
            playerWeightReserverTracker.WeightReservers.Remove(this);
        }
    }
}
