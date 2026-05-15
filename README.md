![](banner.png)

[![AGPL-3.0 License](https://img.shields.io/static/v1?label=Licence&message=AGPL-3.0&color=green)](https://opensource.org/licenses/AGPL-3.0) [![GitHub Release](https://img.shields.io/static/v1?label=Version&message=1.0.0&color=blue)]() [![Patreon](https://img.shields.io/badge/Patreon-FFFFFF)](https://patreon.com/FrykesFiddlings)

This is the repository for Keys, coded by Fryke (fryke) on Discord.

Keys is a V Rising dedicated server mod that adds a clan key management system. Clan leaders can register their clan, issue invite keys to players, and those players can use their keys to join the clan directly, bypassing the game's standard clan invite flow and member limits.

## Features

- **Clan Registration** - Clan leaders register their clan with the keys system to start issuing keys.
- **Key Issuance** - Issue keys to individual players, all current clan members, or everyone within a radius.
- **Bypass Keys** - Special keys that ignore the configurable clan member limit when joining.
- **Ownership Transfer** - Transfer clan ownership to another player (with admin override support).
- **Key Revocation** - Revoke individual keys, all non-owner keys for a clan, or deregister a clan entirely.
- **Clan Joining via Keys** - Players use `.keys use "Clan Name"` to join a registered clan they hold a key for.
- **Discord Webhook Integration** - Configurable webhook notifications for key join events.
- **Configurable Clan Member Limit** - Server-wide clan member cap, enforced (or bypassed) by the keys system.
- **Persistent Data** - All player and key data is saved to a local JSON file and survives server restarts.

## Commands

All commands use the `.keys` prefix.

| Command | Description | Admin Only |
|---|---|---|
| `.keys register` | Register your clan with the keys system | No |
| `.keys owner <player>` | Transfer clan ownership to another player | No |
| `.keys owner <origOwner> <newOwner>` | Transfer ownership between any two players | Yes |
| `.keys give <player>` | Issue a key to a player for your clan | No |
| `.keys give <player> <clan>` | Issue a key to a player for a specific clan | Yes |
| `.keys give <player> <clan> bypass` | Issue a bypass key (ignores member limit) | Yes |
| `.keys giveall <clan>` | Issue keys to all members of a clan | Yes |
| `.keys giveall <clan> bypass` | Issue bypass keys to all members | Yes |
| `.keys giveradius <clan> <radius>` | Issue keys to players within radius | Yes |
| `.keys giveradius <clan> <radius> bypass` | Issue bypass keys within radius | Yes |
| `.keys list mine` | List all keys you hold | No |
| `.keys list clan` | List all keys for your clan (owner only) | No |
| `.keys list all` | List all registered clans and key counts | Yes |
| `.keys list <clan>` | List keys for a specific clan | Yes |
| `.keys remove <player>` | Revoke a player's key for your clan | No |
| `.keys remove clan` | Revoke all non-owner keys for your clan | No |
| `.keys remove <player> <clan>` | Revoke a specific player's key for any clan | Yes |
| `.keys deregister <clan>` | Deregister a clan and revoke all keys | Yes |
| `.keys use "Clan Name"` | Join a clan using your key | No |

## Data Storage

All player and key data is stored locally on the server as a JSON file at `BepInEx/config/Keys/keys_player_data.json`. Configuration is stored in `BepInEx/config/io.vrising.Keys.cfg`.

Data persists through server restarts and is saved automatically when changes are made.

## Installation

1. Install [BepInEx](https://docs.bepinex.dev/) and [VampireCommandFramework](https://github.com/decaprisan/VampireCommandFramework) on your V Rising dedicated server.
2. Place `Keys.dll` in your `BepInEx/plugins/` directory.
3. Start the server, the mod will generate its config file automatically.
4. Configure `ClanMemberLimit`, Discord webhook URLs, and other settings in `BepInEx/config/io.vrising.Keys.cfg`.

## Attribution

This project is licensed under AGPL-3.0.

Portions of code and design patterns in this project were inspired by or adapted from the following projects:

  - KindredCommands <https://github.com/decaprisan/KindredCommands>
    Licensed under AGPL-3.0

This is an independent project with its own purpose and functionality. It is not a fork, modification, or derivative of any of the above projects. Some utility code and patterns were referenced during development.
