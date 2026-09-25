# Changelog

## 1.1.0

- Returning spears no longer give up after one try. If the inventory is full (or you are mid-teleport) the spear waits and retries every second, with a "no room" message, instead of being left on the ground for good.
- A returning spear goes back to the slot it was thrown from when that slot is free.
- New `ReserveThrowSlot` option (default on): auto-pickup won't take the last free slot(s) your thrown spears need.
- Entering a portal or a dungeon/cave entrance recalls thrown spears first. Previously the spear was abandoned.
- The spear's owner is stored on the dropped item, so it returns after you walk back into range, relog, or die and respawn. The old death-count check that blocked returns after dying is removed.
- A spear projectile destroyed before it could drop (lifetime expired, or a hit vanilla doesn't drop on) is returned instead of vanishing.
- `EnableBlockAutoPickup` is now honoured: loyal spears can only be auto-picked up by the player who threw them.
- `ValheimDir` and `DeployPath` can be overridden on the command line; `-p:SkipPackage=true` skips the zip step.

## 1.0.0

- Initial Valheim 1.0 port.
