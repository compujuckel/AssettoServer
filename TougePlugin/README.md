# Cat Mouse Touge Plugin.

A plugin for Assetto Corsa servers that enables cat-and-mouse-style **touge** racing with an engaging tie-breaker ruleset. Designed for competitive head-to-head driving with a configurable format.

## Features

- **Cat & Mouse Touge Format**: Race head-to-head in alternating lead/chase roles.
- **Best-of-3 Logic**: Always runs 2 races. If tied (0–0 or 1–1), sudden-death rounds continue until the tie is broken.
- **Fully Configurable**: Tweak rules and behavior through a generated config file.
- **C# + Lua**: Powered by a server-side C# plugin and a client-side Lua UI integration.

## Installation

- **Download the plugin**  
    _(Download link will be added here soon)_
- **Extract it**  
    Place the contents into your server's `plugins` directory:
    `assettocorsa/server/plugins/`
- **Run your server once**  
    This will generate a configuration file inside the `cfg` folder.
- **Customize your ruleset**  
    Edit the generated `plugin_cat_mouse_touge_cfg.yml` to adjust setting to your liking.  
    _(A full list of settings will be documented soon.)_

## Tech Stack

- **Server-side**: C# (.NET)
- **Client-side**: Lua (for Content Manager + CSP)
- **Runs on**: AssettoServer 0.0.55 with Content Manager and CSP. (Might also work on older versions of AssettoServer, but hasn't been tested.)

## Notes

- Requires Content Manager with recent version of CSP enabled.
- This is a AssettoServer plugin.
- UI and other plugin features may evolve — stay tuned for updates.

