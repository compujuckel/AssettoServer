# AssettoServer [![Build status](https://github.com/compujuckel/AssettoServer/actions/workflows/dotnet.yml/badge.svg)](https://github.com/compujuckel/AssettoServer/actions/workflows/dotnet.yml) [![Discord](https://discordapp.com/api/guilds/890676433746268231/widget.png?style=shield)](https://discord.gg/uXEXRcSkyz)

## About
AssettoServer is a custom game server for Assetto Corsa developed with freeroam in mind. It greatly improves upon the default game server by fixing various security issues and providing new features like AI traffic and dynamic weather.

This is a fork of https://github.com/Niewiarowski/AssettoServer.

## Installation

### Windows
* Install the ASP.NET 6 Runtime (select "Hosting Bundle"): https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime
* Download `assetto-server-win-x64.zip` from the [latest stable release](https://github.com/compujuckel/AssettoServer/releases) and extract it whereever you want.

### Linux
* Follow the ASP.NET 6 Runtime installation instructions for your distro: https://docs.microsoft.com/en-us/dotnet/core/install/linux
* Download `assetto-server-linux-x64.tar.gz` from the [latest stable release](https://github.com/compujuckel/AssettoServer/releases) and extract it whereever you want.

## Usage

The easiest way to get started is creating your server configuration with Content Manager.  
After that just click "Pack" to create an archive with all required configs and data files. Extract this archive into the server root folder.

## Features

Most features can be controlled via `extra_cfg.yml`. If this file does not exist it will be created at first server startup.

### Steam Ticket Validation

The default server implementation of Assetto Corsa does not use the Steam API to determine whether a connected players
account is actually the account they claim to be. This opens the door for SteamID spoofing, which means someone can
impersonate another player.

In this server the Steam Auth API is utilized, as documented
here: https://partner.steamgames.com/doc/features/auth

Since the player needs to get a Steam session ticket on client side that he has to transfer to the server upon joining,
a minimum CSP (Custom Shaders Patch) version of 0.1.75 or higher is required along with Content Manager v0.8.2297.38573 or higher for players to be able to join the server.

This feature must be enabled in `extra_cfg.yml`.

CSP can be found and downloaded here [https://acstuff.ru/patch/](https://acstuff.ru/patch/)  
CSP Discord: [https://discord.gg/KAbXE5Y](https://discord.gg/KAbXE5Y)

### Logging in as administrator via SteamID

It is possible to specify the SteamIDs of players that should be administrator on the server.

**Do not use this feature with Steam Auth disabled! Someone might be able to gain admin rights with SteamID spoofing.**

### AI Traffic

It is possible to load one or more AI splines to provide AI traffic. Place `fast_lane.ai` in the maps `ai/` folder and set `EnableAi` to `true` in `extra_cfg.yml`.  
The default AI settings have been tuned for Shutoko Revival Project, other maps will require different settings.

To allow AI to take a car slot you have to add a new parameter to the `entry_list.ini`, for example:
```ini
[CAR_0]
MODEL=ktyu_c8_lav_s1
SKIN=04_gunmetal_grey/ADAn
BALLAST=0
RESTRICTOR=0
AI=auto
```

Possible values for the `AI` parameter are
* `auto` - AI will take the slot when it is empty
* `fixed` - AI will always take the car slot. It won't be possible for players to join in this slot
* `none` - AI will never take the slot (default)

When using `AI=auto` slots it is highly recommended to specify a `MaxPlayerCount` in `extra_cfg.yml` to make sure there is always a minimum amount of AI cars available.


### Dynamic Weather

The server supports CSPs WeatherFX v1 which allows dynamic weather, smooth weather transitions and RainFX. CSP 0.1.76+ is required for this feature.

Two plugins are included that utilize dynamic weather:
* `LiveWeatherPlugin` for getting realtime weather from openweathermap.org
* `VotingWeatherPlugin` for letting players vote for weather changes

### Anti AFK system

Will kick players if they are not honking, braking, toggling headlights, moving steering wheel, using gas or sending
messages in chat. Can be adjusted by an admin by using the `/setafktime` command.

### Plugin Interface

There is an experimental plugin interface for adding functionality to the server. Take a look at one of the
included plugins to get started with developing your own plugin.

The API is still under development and might change in the future.

## Admin Commands

### Teleporting a player to pits

`/pit <id>`

| Parameter | Description                                                   |
| --------- | ------------------------------------------------------------- |
| id        | The car ID or name of the player to be teleported             |

### Kicking a player

`/kick <id> <reason>`  
`/kick_id <id> <reason>`

| Parameter | Description                                                   |
| --------- | ------------------------------------------------------------- |
| id        | The car ID or name of the player to be kicked                 |
| reason    | Optional, will display a reason on why the player was kicked. |

### Banning a player

`/ban <ID> <reason>`  
`/ban_id <ID> <reason>`

| Parameter | Description                                                   |
| --------- | ------------------------------------------------------------- |
| id        | The car ID or name of the player to be banned                 |
| reason    | Optional, will display a reason on why the player was banned. |

### Unbanning a player

`/unban <steamId>`

| Parameter | Description                                                   |
| --------- | ------------------------------------------------------------- |
| steamId   | The steamID64 (Dec) of the blacklisted player                 |

The server does not need to be restarted for the command to take effect.

### Setting the servers time

`/settime <secondOfDay>`

| Parameter | Description                                                   |
| --------- | ------------------------------------------------------------- |
| secondOfDay | 0 - 86400, second of day that the time is to be set to      |

### Setting the servers weather

`/setweather <weatherId>`

| Parameter | Description                                                   |
| --------- | ------------------------------------------------------------- |
| weatherId | The ID of the weather specified in the `server_cfg.ini`       |

### Setting the maximum afk time

`/setafktime <minutes>`

| Parameter | Description                                                      |
| --------- | ---------------------------------------------------------------- |
| minutes   | The time in minutes a player can be afk without getting kicked.  |

### Forcing headlights for a player

`/forcelights <on/off> <id>`

| Parameter | Description                                                             |
| --------- | ----------------------------------------------------------------------- |
| on/off    | On = Active forcing of headlights, Off = Disable forcing of headlights  |
| id        | The car ID or name of the player                                        |

Not specifying an id will enable/disable forced headlights for all players on the server.

**NOTE**: Forcing headlights for a player will still give him the opportunity to turn on/off his lights locally. His
lights will however appear turned on for all other players.
