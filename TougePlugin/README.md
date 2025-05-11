# Cat Mouse Touge Plugin.

A plugin for Assetto Corsa servers that enables cat-and-mouse-style **touge** racing with an engaging tie-breaker ruleset. Designed for competitive head-to-head driving with a configurable format. Find some live example servers running the plugin [here](https://assetto.scratchedclan.nl/servers).

---

## Features

- **Touge Format**: Race head-to-head in alternating lead/chase roles.
- **Best-of-3 Logic**: Always runs 2 races. If tied (0–0 or 1–1), sudden-death rounds continue until the tie is broken.
- **Fully Configurable**: Tweak rules and behavior through a generated config file.
- **C# + Lua**: Powered by a server-side C# plugin and a client-side Lua UI integration.

---
## Installation

- **Download the plugin**  
    _(Download link will be added here soon)_
- **Extract it**  
    Place the contents into your server's `plugins` directory.
- **Run your server once**  
    This will generate a configuration file inside the `cfg` folder.
- **Customize your ruleset**  
    Edit the generated `plugin_cat_mouse_touge_cfg.yml` to adjust setting to your liking.  

---

## Configuration


### Elo Configuration

### `CarPerformanceRatings`
**Type:** `Dictionary<string, int>`  
**Description:** Specifies performance ratings for different car models.  
**Usage:** Each key represents the car's internal model name (e.g., `ks_mazda_miata`) and the value is a performance score between **1** and **1000**.  
**Purpose:** Used in player elo calculations to improve fairness. Winning in a faster car against a slower car will award less elo gain than beating a fast car using a slower one.  
**Example:**
```yaml
CarPerformanceRatings:
  ks_mazda_miata: 125
  ks_toyota_ae86: 131
```

---

### `MaxEloGain`
**Type:** `int`  
**Description:** The maximum amount of elo rating a player can gain (or lose) in a single race.  
**Constraints:** Must be a **positive integer**.  
**Purpose:** Gives control over the volatility of the rating system.

---

### `ProvisionalRaces`
**Type:** `int`  
**Description:** The number of initial races a player is considered "provisional" in the elo system.  
**Constraints:** Must be **greater than 0**.  
**Purpose:** Allows for slightly larger elo changes than configured in MaxEloGain while a player's skill is still being established.

---

### `MaxEloGainProvisional`
**Type:** `int`  
**Description:** The maximum elo gain/loss while a player is still provisional.  
**Constraints:** Must be **greater than 0**.  
**Purpose:** Allows for a faster elo adjustment during provisional matches compared to regular ones.

---

## Race Setup

### `StartingPositions`
**Type:** `Dictionary<string, Vector3>[][]`  
**Description:** A two-dimensional array of starting position pairs.  
Each inner array contains two dictionaries, one for each car, and each dictionary contains:
- `"Position"`: a 3D vector (X, Y, Z) indicating the starting location.
- `"Direction"`: a normalized 3D vector indicating the direction the car faces at start.

**Constraints:**
- At least one pair of starting positions must be defined.
- Each dictionary must include both `"Position"` and `"Direction"` keys.

**Purpose:** Defines the spawn and orientation of cars at the start of a touge race.

**Example:**
```yaml
StartingPositions:
  - 
    - Position: { X: -204.4, Y: 468.34, Z: -93.87 }
      Direction: { X: 0.0998, Y: 0.992, Z: 0.0784 }
    - Position: { X: -198.89, Y: 468.01, Z: -88.14 }
      Direction: { X: 0.0919, Y: 0.992, Z: 0.0832 }
```

---

### `isRollingStart`
**Type:** `bool`  
**Description:** Enables or disables rolling starts.  
**Usage:**  
- `true`: Cars start moving at the beginning of the race.  
- `false`: Cars are stationary at the start.

---

### `outrunTime`
**Type:** `int`  
**Description:** The number of seconds the **chase car** has to cross the finish line after the **lead car** finishes.  
**Constraints:** Must be between **1 and 60 seconds**.  
**Purpose:** Used to determine if the lead car successfully outran the chase car.

---

## Database

### `isDbLocalMode`
**Type:** `bool`  
**Description:** Whether the system should use a local in-memory or file-based database instead of a PostgreSQL server.  
**Usage:**
- `true`: No external DB needed; local data only.
- `false`: Requires valid PostgreSQL connection string.

---

### `postgresqlConnectionString`
**Type:** `string?`  
**Description:** Connection string used to connect to a PostgreSQL database.  
**Constraints:**  
- Must be non-empty **only if** `isDbLocalMode` is `false`.  
**Purpose:** Provides data persistence and multi-server support in non-local setups.

**Example:**
```yaml
postgresqlConnectionString: "Host=localhost;Port=5432;Database=touge_db;Username=user;Password=pass"
```

---

## Notes

- Requires Content Manager with recent version of CSP enabled.
- This is a AssettoServer plugin.
	- Tested on [v0.0.55-pre25](https://github.com/compujuckel/AssettoServer/releases/tag/v0.0.55-pre25)
- UI and other plugin features may evolve — stay tuned for updates.

