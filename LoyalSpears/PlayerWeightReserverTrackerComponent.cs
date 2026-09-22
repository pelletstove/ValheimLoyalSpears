using System.Collections.Generic;
using UnityEngine;

namespace LoyalSpears;

/// <summary>
/// Tracker component attached to a player that maintains a list of all active
/// <see cref="WeightReserverComponent"/> instances for that player.
/// Used by <see cref="SpearPatches.BlockAutoPickupDueToReservedWeight"/> to calculate
/// total reserved weight when checking auto-pickup eligibility.
/// </summary>
internal class PlayerWeightReserverTrackerComponent : MonoBehaviour
{
    /// <summary>List of active weight reserver components for this player.</summary>
    public readonly List<WeightReserverComponent> WeightReservers = new();
}
