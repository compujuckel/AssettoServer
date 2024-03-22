-- Copyright 2024 <github.com/razaqq>

-- script allowing admins to teleport

local sim = ac.getSim()

local currentUiTargetPos = vec2(0, 0)
local targetPos = vec2(0.5, 0.5)

-- get track map
local mapFilename = ac.getFolder(ac.FolderID.ContentTracks)..'/'..ac.getTrackFullID('/')..'/map.png'

-- read map parameters
local mapParams = ac.INIConfig.load(ac.getFolder(ac.FolderID.ContentTracks)..'/'..ac.getTrackFullID('/')..'/data/map.ini'):mapSection('PARAMETERS', {
  X_OFFSET = 0,
  Z_OFFSET = 0,
  SCALE_FACTOR = 1,
  WIDTH = 600,
  HEIGHT = 600
})

-- And last, size of the map. We could calculate it each frame, but it’s nicer if done this way:
local mapSize = vec2(mapParams.WIDTH / mapParams.HEIGHT * 1000, 1000)

---@param pos vec2 Ui Position
---@param color rgbm
---@param text string
local function DrawCar(pos, color, text)
  ui.drawCircleFilled(pos, 3, color)
  ui.drawText(text, pos + vec2(0, -3), color)
end

---@param drawOrigin vec2 2D Origin Pos where the map is drawn from
---@param uiPos vec2
---@return vec2
local function UiPosToWorldPos(drawOrigin, uiPos)
  local p = (uiPos - drawOrigin) / mapSize
  local worldPosX = (p.x * mapParams.WIDTH * mapParams.SCALE_FACTOR) - mapParams.X_OFFSET
  local worldPosY = (p.y * mapParams.HEIGHT * mapParams.SCALE_FACTOR) - mapParams.Z_OFFSET
  return vec2(worldPosX, worldPosY)
end

---@param drawOrigin vec2 2D Origin Pos where the map is drawn from
---@param worldPos vec3
---@return vec2
local function WorldPosToUiPos(drawOrigin, worldPos)
  local relPosX = (worldPos.x + mapParams.X_OFFSET) / mapParams.WIDTH / mapParams.SCALE_FACTOR
  local relPosY = (worldPos.z + mapParams.Z_OFFSET) / mapParams.HEIGHT / mapParams.SCALE_FACTOR
  return drawOrigin + vec2(relPosX, relPosY) * mapSize
end

---@param worldPos vec3
---@return vec3
local function FixWorldPosHeight(worldPos)
  local trackPos = ac.worldCoordinateToTrack(worldPos)
  trackPos.y = 10
  return ac.trackCoordinateToWorld(trackPos)
end

local function TeleportHUD()
  ui.text('Select point on a map:')

  local drawOrigin = ui.getCursor()

  ui.drawImage(mapFilename, drawOrigin, drawOrigin + mapSize)

  -- draw all other cars with driver name, filter out traffic
  for i = 0, sim.carsCount - 1 do
    local car = ac.getCar(i)
    local driverName = ac.getDriverName(i)
    if car ~= nil and driverName ~= nil and car.isConnected and not car.isAIControlled and not string.find(driverName, "Traffic") then
      local color = i == 0 and rgbm.colors.red or rgbm.colors.blue
      DrawCar(WorldPosToUiPos(drawOrigin, car.position), color, driverName)
    end
  end

  -- All `ui.draw…` functions don’t actually move cursor, so we’re still where we were when started drawing stuff. Let’s move with
  -- map size, so that window size would extend and include map:
  ui.dummy(mapSize)

  if ui.itemClicked() then
    local mousePos = ui.mouseLocalPos();
    currentUiTargetPos = mousePos
    -- save world pos
    targetPos = UiPosToWorldPos(drawOrigin, mousePos)
  end

  ui.drawCircleFilled(currentUiTargetPos, 4, rgbm.colors.green)
end

local function TeleportHUDClosed(okClicked)
  if okClicked then
    physics.setCarVelocity(0, vec3(0, 0, 0))

    if ac.hasTrackSpline() then
      local finalPos = FixWorldPosHeight(vec3(targetPos.x, 0, targetPos.y))
      -- careful, the direction needs to be able to be normalized, otherwise the game crashes
      physics.setCarPosition(0, finalPos, vec3(1, 0, 0))
    else
      -- this is a bit hacky...
      local finalPos = vec3(targetPos.x, 5000, targetPos.y)
      local point = vec3(0, 0, 0)
      -- the normal appears to be buggy, maybe the ray bounce is influenced by vegetation?
      if physics.raycastTrack(finalPos, vec3(0, -1, 0), 10000, point, nil) > -1 then
        physics.setCarPosition(0, point + vec3(0, 1, 0), vec3(1, 0, 0))
      else
        ac.setMessage('Error', 'Failed to determine y coordinate of track.')
      end
    end
  end
end

ui.registerOnlineExtra(ui.Icons.FastForward, 'Teleport to Location', nil, TeleportHUD, TeleportHUDClosed, ui.OnlineExtraFlags.Admin)
