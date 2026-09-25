# Loyal Spears

Spears that are loyal — they return to you after being thrown and prevent other players from auto-picking them up.

This is a Valheim 1.0+ port of the original [Loyal Spears](https://github.com/Goldenrevolver/ValheimSpearModsAndFriends/tree/main/LoyalSpears) by Goldenrevolver, upgraded to use Jotunn's built-in config sync instead of ServerSync.

## Features

- **Auto-Return**: Thrown spears automatically return to their owner after landing, or after traveling too far.
- **Owner-Only Pickup**: Prevent other players from auto-picking up your thrown spears.
- **Never Lost**: A spear that can't come back yet (mid-teleport, out of range) keeps retrying instead of giving up. Its owner is stored on the item itself, so it also comes back after you walk back into range, relog, or die and respawn.
- **Original Slot**: A returning spear always goes back to the inventory slot it was thrown from. Whatever took that slot is moved to another free slot, or dropped at your feet if your inventory is full.
- **Weight and Slot Reservation**: Throws reserve carrying capacity and an inventory slot, so auto-pickup of loot can't overburden you or fill the slot your spear needs.
- **Portal and Cave Safety**: Entering a portal or a dungeon/cave entrance pulls your thrown spears back first, while they are still loaded.
- **Lifetime Rescue**: A spear that flies until it expires (or hits something vanilla won't drop it on) is returned instead of vanishing.

## Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx) and [Jotunn](https://github.com/Valheim-Modding/Jotunn) into your Valheim game.
2. Download the latest release `LoyalSpears.zip`.
3. Extract the contents directly into your Valheim game folder.
   - This will place `LoyalSpears.dll` in `BepInEx/plugins/`
   - The `manifest.json` and `README.md` are for mod manager compatibility.
4. Launch Valheim. The mod will be active.

## Uninstall

1. Delete `BepInEx/plugins/LoyalSpears.dll` from your Valheim game folder.

## Configuration

All settings are server-synced. The server's configuration is authoritative.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableBlockAutoPickup` | bool | `true` | Prevent other players from auto-picking up your thrown spears. |
| `ReturnAfterSeconds` | float | `5` | Seconds until a thrown spear returns to its owner after hitting the ground. `0` returns immediately, negative disables returning. |
| `BlockReturnIfOverburdened` | bool | `false` | Skip auto-return if picking it back up would overburden the owner. |
| `ReserveThrowWeight` | bool | `true` | Count thrown spear weight as reserved during auto-pickup checks. |
| `ReserveThrowSlot` | bool | `true` | Keep a free inventory slot for each thrown spear by not auto-picking up items that would fill it. |
| `MaxReservationSeconds` | float | `60` | Safety cap so a lost spear doesn't reserve weight forever. |
| `FlightDistanceUntilAutoReturn` | float | `-1` | If a thrown spear travels further than this distance from the owner without hitting the ground, it auto-returns. Negative disables. |

Find the config file at `BepInEx/config/com.loyalspears.mod.cfg`.

## Dependencies

- [BepInExPack for Valheim](https://github.com/AzumattDev/BepInEx)
- [Jotunn](https://github.com/Valheim-Modding/Jotunn) (latest)

## License

This project is licensed under the MIT License - see [LICENSE](https://github.com/jorstar/ValheimLoyalSpears/blob/master/LICENSE) for details.

Original code from [Goldenrevolver/ValheimSpearModsAndFriends](https://github.com/Goldenrevolver/ValheimSpearModsAndFriends) (MIT, Copyright 2023 Goldenrevolver).
