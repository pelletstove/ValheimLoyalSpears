using UnityEngine;

namespace LoyalSpears;

/// <summary>
/// Component attached to a dropped spear that belongs to a player. It keeps trying to return
/// the spear to its owner until it succeeds, so a full inventory or a teleport only delays the
/// return instead of cancelling it.
///
/// Ownership is stored on the item's ZDO (see <see cref="SpearReturn.OwnerHash"/>), so the
/// component is re-attached whenever the spear is loaded again — after walking back to it,
/// after a relog, or after dying and respawning.
/// </summary>
internal class LoyaltyComponent : WeightReserverComponent
{
    /// <summary>How often a failed return is retried.</summary>
    private const float RetryIntervalSeconds = 1f;

    /// <summary>The ItemDrop representing the thrown spear.</summary>
    private ItemDrop _attachedItemDrop;

    /// <summary>Time.time at which the next return attempt is made.</summary>
    private float _nextAttempt;

    /// <summary>The spear this component is bringing back.</summary>
    public ItemDrop Drop => _attachedItemDrop;

    /// <summary>
    /// Sets up the loyalty component. The first return attempt happens after
    /// <see cref="PluginConfig.ReturnAfterSeconds"/>, or after <paramref name="delay"/> if given.
    /// <paramref name="owner"/> may be null when the owner is not known yet (spear loaded from
    /// the world); the reservation is then made on the first return attempt.
    /// </summary>
    public void Setup(ItemDrop attachedItemDrop, Player owner, float? delay = null)
    {
        _attachedItemDrop = attachedItemDrop;
        _nextAttempt = Time.time + Mathf.Max(0f, delay ?? PluginConfig.ReturnAfterSeconds.Value);

        if (owner != null && owner != OriginalOwner)
        {
            ReserveFor(owner);
        }
    }

    /// <summary>A loyal spear reserves weight for as long as it is away, so there is no timeout.</summary>
    protected override void StartTimer()
    {
    }

    /// <inheritdoc cref="StartTimer"/>
    public override void StopTimer()
    {
    }

    /// <summary>Makes the next <see cref="Update"/> attempt the return straight away.</summary>
    public void ReturnNow() => _nextAttempt = 0f;

    private void Update()
    {
        if (_attachedItemDrop == null || Time.time < _nextAttempt)
        {
            return;
        }

        _nextAttempt = Time.time + RetryIntervalSeconds;

        if (PluginConfig.ReturnAfterSeconds.Value < 0f)
        {
            return;
        }

        var player = Player.m_localPlayer;

        if (player == null || !SpearReturn.IsOwnedBy(_attachedItemDrop, player))
        {
            return;
        }

        // the owner may have died and respawned as a new Player object since this was set up
        if (OriginalOwner != player)
        {
            ReserveFor(player);
        }

        // on success the drop is destroyed, and this component with it
        SpearReturn.TryReturn(_attachedItemDrop, player);
    }

    /// <summary>Moves this component's weight/slot reservation onto <paramref name="player"/>.</summary>
    private void ReserveFor(Player player)
    {
        Unregister();
        base.Setup(_attachedItemDrop.m_itemData, player);
    }
}
