# Loyal Spears

Spears that are loyal — they return to you after being thrown and prevent other players from auto-picking them up.

This is a Valheim 1.0+ port of the original [Loyal Spears](https://github.com/Goldenrevolver/ValheimSpearModsAndFriends/tree/main/LoyalSpears) by Goldenrevolver, upgraded to use Jotunn's built-in config sync instead of ServerSync.

## Features

- **Auto-Return**: Thrown spears automatically return to their owner after landing, or after traveling too far.
- **Owner-Only Pickup**: Prevent other players from auto-picking up your thrown spears.
- **Weight Reservation**: Throws reserve a portion of your carrying capacity so you don't accidentally overburden yourself picking up loot before retrieving your spear.
- **Portal Safety**: Spear tracking components are cleaned up when teleporting through portals, preventing spears from getting stuck across dimensions.

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
| `ReturnAfterSeconds` | float | `5` | Seconds until a thrown spear returns to its owner after hitting the ground. `0` or negative disables. |
| `BlockReturnIfOverburdened` | bool | `false` | Skip auto-return if picking it back up would overburden the owner. |
| `ReserveThrowWeight` | bool | `true` | Count thrown spear weight as reserved during auto-pickup checks. |
| `MaxReservationSeconds` | float | `60` | Safety cap so a lost spear doesn't reserve weight forever. |
| `FlightDistanceUntilAutoReturn` | float | `-1` | If a thrown spear travels further than this distance from the owner without hitting the ground, it auto-returns. Negative disables. |

Find the config file at `BepInEx/config/com.loyalspears.mod.cfg`.

## Dependencies

- [BepInExPack for Valheim](https://github.com/AzumattDev/BepInEx)
- [Jotunn](https://github.com/Valheim-Modding/Jotunn) (latest)

## License

This project is licensed under the MIT License - see [LICENSE](https://github.com/jorstar/ValheimLoyalSpears/blob/master/LICENSE) for details.

Original code from [Goldenrevolver/ValheimSpearModsAndFriends](https://github.com/Goldenrevolver/ValheimSpearModsAndFriends) (MIT, Copyright 2023 Goldenrevolver).
