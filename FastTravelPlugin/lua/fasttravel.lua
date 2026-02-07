--c1xtz: original script writen by Tsuka1427
--c1xtz: comments are from Tsuka, unless they start with 'c1xtz:'
--thisguyStan: my comments start with 'thisguyStan:' :D

--thisguyStan: changed these paths to use the AssettoServer instance
--thisguyStan: moved a few settings into AssettoServer

local config = ac.configValues({
    disableCollisions = true,
    mapFixedTargetPosition = "", -- { -2100, 0, 3200 },
    mapZoomValues = "",          -- { 100, 1000, 4000, 15000 },
    mapMoveSpeeds = "",          -- { 1, 5, 20, 0 },
    showMapImg = true,
    useGroupInheritance = true,
    useGroupDrawMode = true,
    distanceModeRange = 100
})

local parsedTargetPos = JSON.parse(config.mapFixedTargetPosition)
local mapFixedTargetPosition = vec3(parsedTargetPos[1], parsedTargetPos[2], parsedTargetPos[3])
local mapZoomValue = JSON.parse(config.mapZoomValues)
local mapMoveSpeed = JSON.parse(config.mapMoveSpeeds)

local baseUrl = 'http://' .. ac.getServerIP() .. ':' .. ac.getServerPortHTTP() .. '/static/FastTravelPlugin/'

local supportAPI_physics = physics.setGentleStop ~= nil
local supportAPI_collision = physics.disableCarCollisions ~= nil
local supportAPI_matrix = ac.getPatchVersionCode() >= 3037
local trackCompassOffset = 24 -- for SRP

local font = 'Segoe UI'
local fontBold = 'Segoe UI;Weight=Bold'

--c1xtz: read teleports from server options, requires 'POINT_<num>_TYPE = PA/ST' to be added to the teleports in csp_extra_options.ini to not show up as the default.
--c1xtz: additional custom types can be created as long as a corresponding 'mapicon_<type>.png' is in the images folder. example: 'POINT_1_TYPE = GS' & 'mapicon_gs.png' for a gas station type.
--c1xtz: points inside of group inherite the type of the point before, meaning if 'POINT_1_TYPE = PA' and POINT_2 is not specifically given a type, it will inherit the PA type from point 1.
--c1xtz: this makes it possible to have multiple types per group, although only the first point per unique type is shown (see tpdrawing:)
local extraOptions = ac.INIConfig.onlineExtras()
local onlineTeleports, encounteredTypes = {}, {}
local defaultType = 'sp'

local function getTeleports()
    if not extraOptions then return end
    onlineTeleports, encounteredTypes = {}, {}
    for _, points in extraOptions:iterateValues('TELEPORT_DESTINATIONS', 'POINT') do
        if points:match('_POS$') then
            local pointName = extraOptions:get('TELEPORT_DESTINATIONS', points:gsub('_POS$', ''), '')
            local groupName = extraOptions:get('TELEPORT_DESTINATIONS', points:gsub('_POS$', '_GROUP'), '')
            local position = extraOptions:get('TELEPORT_DESTINATIONS', points, vec3())
            local heading = tonumber(extraOptions:get('TELEPORT_DESTINATIONS', points:gsub('_POS$', '_HEADING'), 0))
            local typeName = extraOptions:get('TELEPORT_DESTINATIONS', points:gsub('_POS$', '_TYPE'), ''):lower()
            if typeName == '' then
                typeName = (config.useGroupInheritance and encounteredTypes[groupName]) or defaultType
            else
                if config.useGroupInheritance then encounteredTypes[groupName] = typeName end
            end
            table.insert(onlineTeleports, { typeName, groupName, position, heading, pointName })
        end
    end
end

getTeleports()

local sim = ac.getSim()
local screenSize = vec2(sim.windowWidth, sim.windowHeight)
if sim.isVRMode then
    screenSize = screenSize * 0.6
end
local screenOffset = ((vec2(sim.windowWidth, sim.windowHeight) - screenSize) * 0.5)
local trackMapImage = ac.getFolder(ac.FolderID.ContentTracks) .. '/' .. ac.getTrackFullID('/') .. '/map.png' --c1xtz: local track image instead of 'map.png'
ui.decodeImage(trackMapImage)
local trackMapImageSize = vec2(981, 1440)
if ui.isImageReady(trackMapImage) then
    trackMapImageSize = ui.imageSize(trackMapImage)
end

local mapShot = ac.GeometryShot(ac.findNodes('trackRoot:yes'), screenSize, 1, false)
mapShot:setClippingPlanes(10, 30000)

local mapFullShot = ac.GeometryShot(ac.findNodes('sceneRoot:yes'), screenSize, 1, false)

local roadsNode = ac.findNodes('trackRoot:yes'):findMeshes('{ ?ROAD?, ?Road?, ?road?, ?ASPH?, ?Asph?, ?asph?, ?jnc_asp? }')
local roadsShot = ac.GeometryShot(roadsNode, screenSize, 1, false)
roadsShot:setShadersType(render.ShadersType.Simplified)
roadsShot:setAmbientColor(rgbm(100, 100, 100, 1))
roadsShot:setClippingPlanes(10, 30000)
ac.setExtraTrackLODMultiplier(10)

local roadsAABB_min, roadsAABB_max, meshCount = roadsNode:getStaticAABB()
local limitArea = vec4(roadsAABB_min.x, roadsAABB_min.z, roadsAABB_max.x, roadsAABB_max.z)

---@type ac.GrabbedCamera
local mapCamera = nil
local mapCameraOwn = 0
local mapMode = false
local mapZoom = 1
local mapFOV = 90
local mapMovePower = vec2()
local mapTargetPos = vec3()
local mapTargetEstimate = 0
local mouseThreshold = vec2(0.4, 0.4)
local lastPos = vec3()
local lastMp = vec2()
local lastCameraMode = 0
local lastPlayersPos = {}
local disabledCollision = false
local teleportEstimate = 0
local teleportAvailable = false
local map_opacity = 0
local apiWaiting = false

local hoverMark = -1
local hoverCID = -1
local hoverCSP = vec2()
local hoverDelay = 0

--c1xtz: debug variables
local debugPos = mapFixedTargetPosition:clone()
local debugOrigPos = mapFixedTargetPosition:clone()
local debugZoom, debugOrigZoom = table.clone(mapZoomValue), table.clone(mapZoomValue)
local debugSpeed, debugOrigSpeed = table.clone(mapMoveSpeed), table.clone(mapMoveSpeed)
local debugUseGroupInheritance, debugOrigUseGroupInheritance = config.useGroupInheritance, config.useGroupInheritance
local debugUseGroupDrawMode, debugOrigUseGroupDrawMode = config.useGroupDrawMode, config.useGroupDrawMode
local debugDistanceModeRange, debugOrigDistanceModeRange = config.distanceModeRange, config.distanceModeRange
local debugOnlineExtra = nil
local debugWindowOpen = false

local disabledCollisionEvent = ac.OnlineEvent({
        ac.StructItem.key('disabledCollisionEvent'),
        disabled = ac.StructItem.boolean()
    },
    function(sender, data)
        if sender == nil then return end
        if sender.index == 0 then return end
        if supportAPI_collision then
            ac.log(string.format('%s collision: [%d] %s', (data.disabled and 'Disabled' or 'Enabled'), sender.index, ac.getDriverName(sender.index)))
            physics.disableCarCollisions(sender.index, data.disabled)
            physics.disableCarCollisions(0, data.disabled)
        end
    end)

---@param mat mat4x4
---@param pos vec3
---@return vec2
local function posToViewSpace(mat, pos)
    local o = mat:transform(vec4(pos.x, pos.y, pos.z, 1))
    return vec2(o.x, -o.y) / o.w / 2 + 0.5
end

---@param screenPos vec2
---@param view mat4x4
---@param proj mat4x4
---@return vec3
local function screenToWorldDir(screenPos, view, proj)
    local p1 = proj:inverse():transformPoint(vec3(2 * screenPos.x - 1, 1 - 2 * screenPos.y, 0.5))
    return view:inverse():transformVector(p1):normalize()
end

local issueIgnoreFrames = 3
local issueHeightFrame = 0
local lastRealHeight = -9999
local function getTrackDistance(pos, dir)
    local d = physics.raycastTrack(pos, dir, 10000)
    if 10000 < d or d < 0.0 then
        d = -1
    end
    if d ~= -1 then
        issueHeightFrame = 0
        lastRealHeight = d
        return lastRealHeight
    else
        issueHeightFrame = issueHeightFrame + 1
        if issueHeightFrame < issueIgnoreFrames then
            return lastRealHeight
        end
    end
    lastRealHeight = -9999
    return nil
end

local function projectPoint(position)
    local screenPos = vec2()
    if supportAPI_matrix then
        local t = mapCamera.transform
        local view = mat4x4.look(t.position, t.look, t.up)
        local proj = mat4x4.perspective(math.rad(mapCamera.fov), screenSize.x / screenSize.y, 10, 30000)
        screenPos = posToViewSpace(view:mul(proj), position)
    else
        if ac.getPatchVersionCode() >= 2735 then --c1xtz: added this for compatibility with 0.2.0
            screenPos = render.projectPoint(position, render.ProjectFace.Center)
        else
            screenPos = render.projectPoint(position)
        end
    end
    return screenPos
end

--c1xtz: registerOnlineExtra debug window for plugin config params
local function window_FastTravelDebug()
    ui.textColored("Changes are only visible to you and are not saved\nWhile this window is open you cannot teleport", rgbm.colors.red)
    ui.tabBar('FastTravelDebugTabs', function()
        ui.text("Current Zoom Level: " .. mapZoom)
        ui.text('Shift+Drag sliders for fine control, Control+Click to type in values.')
        ui.separator()

        ui.tabItem('Map Image Position', function()
            if ui.checkbox("Show Map Image", config.showMapImg) then config.showMapImg = not config.showMapImg end
            if config.showMapImg then
                ui.separator()

                local changedPos = false
                local xSlider, movedX = ui.slider("##posX", debugPos.x, -25000, 25000, "X: %.0f")
                if movedX then
                    debugPos.x = xSlider
                    changedPos = true
                end
                local ySlider, movedY = ui.slider("##posY", debugPos.y, -25000, 25000, "Y: %.0f")
                if movedY then
                    debugPos.y = ySlider
                    changedPos = true
                end
                local zSlider, movedZ = ui.slider("##posZ", debugPos.z, -25000, 25000, "Z: %.0f")
                if movedZ then
                    debugPos.z = zSlider
                    changedPos = true
                end
                if changedPos then
                    mapFixedTargetPosition = debugPos:clone()
                    config.mapFixedTargetPosition = string.format('[ %.0f, %.0f, %.0f ]', debugPos.x, debugPos.y, debugPos.z)
                    if mapMode and mapZoom == #mapZoomValue then
                        mapTargetPos = mapFixedTargetPosition:clone()
                        mapTargetEstimate = 0
                    end
                end

                if ui.button("Reset") then
                    debugPos = debugOrigPos:clone()
                    mapFixedTargetPosition = debugOrigPos:clone()
                    if mapMode and mapZoom == #mapZoomValue then
                        mapTargetPos = mapFixedTargetPosition:clone()
                        mapTargetEstimate = 0
                    end
                end
            end
        end)

        ui.tabItem('Zoom & Move Speed Levels', function()
            local changedZoom, changedSpeed = false, false
            for i = 1, #mapZoomValue do
                ui.pushItemWidth(200)
                local value, moved = ui.slider("##zoom" .. i, debugZoom[i] or mapZoomValue[i], 0, 25000, "Zoom " .. i .. ": %.0f")
                ui.popItemWidth()
                if moved then
                    debugZoom[i] = value
                    changedZoom = true
                end
                ui.sameLine(0, 10)
                if i < #mapMoveSpeed then
                    ui.pushItemWidth(200)
                    local speedValue, speedMoved = ui.slider("##move" .. i, debugSpeed[i] or mapMoveSpeed[i], 0, 100, "Speed " .. i .. ": %.0f")
                    ui.popItemWidth()
                    if speedMoved then
                        debugSpeed[i] = speedValue
                        changedSpeed = true
                    end
                else
                    ui.text("Speed " .. i .. " has to be 0")
                    debugSpeed[i] = 0
                end
            end
            if changedZoom then
                for i = 1, #debugZoom do mapZoomValue[i] = debugZoom[i] end
                config.mapZoomValues = debugZoom
            end
            if changedSpeed then
                for i = 1, #debugSpeed do mapMoveSpeed[i] = debugSpeed[i] end
                config.mapMoveSpeeds = debugSpeed
            end

            ui.separator()

            if ui.button("Add Level") then
                local newZoom = debugZoom[#debugZoom] or 1000
                local newSpeed = debugSpeed[#debugSpeed - 1] or 1
                table.insert(debugZoom, newZoom)
                table.insert(mapZoomValue, newZoom)
                table.insert(debugSpeed, newSpeed)
                table.insert(mapMoveSpeed, newSpeed)
                debugSpeed[#debugSpeed], mapMoveSpeed[#mapMoveSpeed] = 0, 0
                config.mapZoomValues = debugZoom
                config.mapMoveSpeeds = debugSpeed
            end

            ui.sameLine()
            local removeDisabled = #mapZoomValue <= 1
            if ui.button("Remove Level", vec2(0, 0), removeDisabled and ui.ButtonFlags.Disabled or ui.ButtonFlags.None) then
                if not removeDisabled then
                    if mapZoom > #mapZoomValue - 1 then mapZoom = #mapZoomValue - 1 end
                    table.remove(debugZoom)
                    table.remove(mapZoomValue)
                    if #debugSpeed > 1 then table.remove(debugSpeed, #debugSpeed - 1) end
                    if #mapMoveSpeed > 1 then table.remove(mapMoveSpeed, #mapMoveSpeed - 1) end
                    debugSpeed[#debugSpeed], mapMoveSpeed[#mapMoveSpeed] = 0, 0
                    config.mapZoomValues = debugZoom
                    config.mapMoveSpeeds = debugSpeed
                end
            end

            if ui.button("Reset") then
                if mapZoom > #debugOrigZoom then mapZoom = #debugOrigZoom end
                debugZoom, debugSpeed, mapZoomValue, mapMoveSpeed = {}, {}, {}, {}
                for i = 1, #debugOrigZoom do
                    debugZoom[i] = debugOrigZoom[i]
                    mapZoomValue[i] = debugOrigZoom[i]
                end
                for i = 1, #debugOrigSpeed do
                    debugSpeed[i] = debugOrigSpeed[i]
                    mapMoveSpeed[i] = debugOrigSpeed[i]
                end
                debugSpeed[#debugSpeed], mapMoveSpeed[#mapMoveSpeed] = 0, 0
                config.mapZoomValues = debugZoom
                config.mapMoveSpeeds = debugSpeed
            end
        end)

        ui.tabItem("Point Display Settings", function()
            if ui.checkbox("Use Group Inheritance", debugUseGroupInheritance) then
                debugUseGroupInheritance = not debugUseGroupInheritance
                config.useGroupInheritance = debugUseGroupInheritance
                getTeleports()
            end

            if ui.checkbox("Use Group Draw Mode", debugUseGroupDrawMode) then
                debugUseGroupDrawMode = not debugUseGroupDrawMode
                config.useGroupDrawMode = debugUseGroupDrawMode
            end

            local rangeValue, rangeChanged = ui.slider("##distanceRange", debugDistanceModeRange, 1, 1000, "Distance Mode Range: %.0f")
            if rangeChanged then
                debugDistanceModeRange = rangeValue
                config.distanceModeRange = debugDistanceModeRange
            end

            ui.separator()
            if ui.button("Reset") then
                debugUseGroupInheritance = debugOrigUseGroupInheritance
                config.useGroupInheritance = debugOrigUseGroupInheritance
                debugUseGroupDrawMode = debugOrigUseGroupDrawMode
                config.useGroupDrawMode = debugOrigUseGroupDrawMode
                debugDistanceModeRange = debugOrigDistanceModeRange
                config.distanceModeRange = debugOrigDistanceModeRange
                getTeleports()
            end
        end)

        if ui.button("Copy Settings to Clipboard") then
            local exportConfig = {}
            exportConfig[#exportConfig + 1] = "MapZoomValues:"
            for i = 1, #mapZoomValue do
                exportConfig[#exportConfig + 1] = "- " .. math.round(mapZoomValue[i])
            end
            exportConfig[#exportConfig + 1] = "MapMoveSpeeds:"
            for i = 1, #mapMoveSpeed do
                exportConfig[#exportConfig + 1] = "- " .. math.round(mapMoveSpeed[i])
            end
            exportConfig[#exportConfig + 1] = "ShowMapImage: " .. tostring(config.showMapImg)
            exportConfig[#exportConfig + 1] = "MapFixedTargetPosition:"
            exportConfig[#exportConfig + 1] = "- " .. math.round(mapFixedTargetPosition.x)
            exportConfig[#exportConfig + 1] = "- " .. math.round(mapFixedTargetPosition.y)
            exportConfig[#exportConfig + 1] = "- " .. math.round(mapFixedTargetPosition.z)
            exportConfig[#exportConfig + 1] = "RequireCollisionDisable: " .. tostring(config.disableCollisions)
            exportConfig[#exportConfig + 1] = "UseGroupInheritance: " .. tostring(config.useGroupInheritance)
            exportConfig[#exportConfig + 1] = "UseGroupDrawMode: " .. tostring(config.useGroupDrawMode)
            exportConfig[#exportConfig + 1] = "DistanceModeRange: " .. math.round(config.distanceModeRange)
            ac.setClipboardText(table.concat(exportConfig, "\n"))
        end
    end)
end

function script.drawUI(dt)
    if dt == nil then dt = ac.getSim().dt end
    if mapMode then
        if mapCamera then
            mapCamera.transform.position.y = math.applyLag(mapCamera.transform.position.y, lastPos.y + mapZoomValue[mapZoom], 0.8, dt)
            if hoverCID >= 0 and mapZoom < #mapZoomValue then
                local hoveredCar = ac.getCar(hoverCID)
                local pos_diff = hoveredCar.position - lastPlayersPos[hoverCID]
                mapCamera.transform.position.x = mapCamera.transform.position.x + pos_diff.x
                mapCamera.transform.position.z = mapCamera.transform.position.z + pos_diff.z
            else
                hoverDelay = 0
            end
            if mapTargetEstimate < 0.3 then
                mapCamera.transform.position.x = math.applyLag(mapCamera.transform.position.x, math.max(limitArea.x, math.min(limitArea.z, mapTargetPos.x)), 0.8, dt)
                mapCamera.transform.position.z = math.applyLag(mapCamera.transform.position.z, math.max(limitArea.y, math.min(limitArea.w, mapTargetPos.z)), 0.8, dt)
            end
            mapCamera.transform.position.x = mapCamera.transform.position.x + (mapMovePower.x * mapMoveSpeed[mapZoom])
            mapCamera.transform.position.z = mapCamera.transform.position.z + (mapMovePower.y * mapMoveSpeed[mapZoom])
            mapCamera.transform.look = vec3(0, -1, 0)
            mapCamera.transform.up = vec3(0, 0, -1)

            if mapZoom == 1 then
                mapFullShot:update(mapCamera.transform.position, mapCamera.transform.look, mapCamera.transform.up, mapFOV)
            else
                mapShot:update(mapCamera.transform.position, mapCamera.transform.look, mapCamera.transform.up, mapFOV)
            end
            roadsShot:update(mapCamera.transform.position, mapCamera.transform.look, mapCamera.transform.up, mapFOV)
        end

        for i = 0, sim.carsCount - 1 do
            local carState = ac.getCar(i)
            lastPlayersPos[i] = carState.position:clone()
        end

        ui.transparentWindow('mapScreen', screenOffset, screenSize, function()
            local sim = ac.getSim()
            local mp = ui.mousePos() - screenOffset
            mapShot:setShadersType(render.ShadersType.Simplest)
            mapFullShot:setShadersType(render.ShadersType.Simplest)
            ui.drawRectFilled(vec2(), screenSize, rgbm(0, 0, 0, 0.5))
            if mapZoom == 1 then
                ui.drawImage(mapFullShot, vec2(), screenSize)
            else
                ui.drawImage(mapShot, vec2(), screenSize)
            end
            ui.drawImage(roadsShot, vec2(), screenSize, rgbm(0, 0.9, 1, 1))

            if mapZoom == #mapZoomValue and config.showMapImg then
                local map_mult = (screenSize.y - (screenSize.y * 0.1)) / trackMapImageSize.y
                local map_size = trackMapImageSize * map_mult
                local screen_center = screenSize / 2
                local map_offset = vec2(screen_center.x - map_size.x / 2, screen_center.y - map_size.y / 2)
                map_opacity = mapTargetEstimate > 0.2 and math.applyLag(map_opacity, 0.25, 0.8, dt) or 0
                ui.setShadingOffset(1, 1, 2, 0)    --c1xtz: this makes sure that the track image is actually white
                ui.beginOutline()                  --c1xtz: added outline to make text more readable
                ui.drawImage(trackMapImage, map_offset, map_size + map_offset, rgbm(1, 1, 1, map_opacity))
                ui.endOutline(rgbm(1, 1, 1, 1), 2) --c1xtz: added outline to make text more readable
                ui.resetShadingOffset()
            else
                map_opacity = 0
            end

            --c1xtz: tpdrawing: if config.useGroupDrawMode is true, only draws the first point of each unique type per group, otherwise only draw the first icon of each type based on config.distanceModeRange distance.
            hoverMark = -1
            local selected = {}
            local displayedPoints = {}

            for i = 1, #onlineTeleports do
                local teleport = onlineTeleports[i]
                local pointType = teleport[1]

                if not (pointType == defaultType and mapZoom == #mapZoomValue) then
                    local picked = false

                    if mapZoom > 2 then
                        if config.useGroupDrawMode then
                            local group = teleport[2]
                            displayedPoints[group] = displayedPoints[group] or {}
                            if not displayedPoints[group][pointType] then
                                displayedPoints[group][pointType] = true
                                picked = true
                            end
                        else
                            displayedPoints[pointType] = displayedPoints[pointType] or {}
                            local isTooClose = false
                            for _, drawnPos in ipairs(displayedPoints[pointType]) do
                                if teleport[3]:distance(drawnPos) < config.distanceModeRange then
                                    isTooClose = true
                                    break
                                end
                            end
                            if not isTooClose then
                                table.insert(displayedPoints[pointType], teleport[3])
                                picked = true
                            end
                        end
                    else
                        picked = true
                    end

                    if picked then
                        local screenPos = projectPoint(teleport[3])
                        if 0 < screenPos.x and screenPos.x < 1 and 0 < screenPos.y and screenPos.y < 1 then
                            screenPos = screenPos * screenSize
                            table.insert(selected, { index = i, teleport = teleport, screenPos = screenPos })
                        end
                    end
                end
            end

            --c1xtz: draw order stuff, making sure POINT_1 would be drawn ABOVE POINT_2
            for i = #selected, 1, -1 do
                local point = selected[i]
                ui.drawImage(baseUrl .. 'mapicon_' .. point.teleport[1] .. '.png', point.screenPos - vec2(40, 40), point.screenPos + vec2(40, 40))
            end

            --c1xtz: draw order stuff 2, making sure that when hovering over a teleport icon while zooming in the mouse keeps sticking to the hovered point
            for i = 1, #selected do
                local point = selected[i]
                if mp > point.screenPos - vec2(30, 30) and mp < point.screenPos + vec2(30, 30) then
                    hoverMark = point.index
                    hoverCID = -1
                    hoverCSP = point.screenPos
                    break
                end
            end

            hoverCID = -1
            for i = 0, sim.carsCount - 1 do
                local carState = ac.getCar(i)
                if carState.isActive and not ac.getCar(i).isHidingLabels then
                    local screenPos = projectPoint(carState.position)
                    if 0 < screenPos.x and screenPos.x < 1 and 0 < screenPos.y and screenPos.y < 1 then
                        screenPos = screenPos * screenSize
                        if mp > screenPos - vec2(30, 30) and mp < screenPos + vec2(30, 30) and hoverCID == -1 and hoverMark == -1 then
                            hoverMark = -1
                            hoverCID = i
                            hoverCSP = screenPos
                        end
                        ui.beginRotation()
                        ui.drawImage(baseUrl .. 'cursor_player.png', screenPos - vec2(40, 40), screenPos + vec2(40, 40))
                        ui.endRotation(90 - carState.compass + trackCompassOffset)
                    end
                end
            end

            ui.setMouseCursor(ui.MouseCursor.None)
            if hoverCID >= 0 or hoverMark >= 0 then
                local nametag
                local nametag_pos = hoverCSP + vec2(45, -16)
                if hoverCID >= 0 then
                    ac.focusCar(hoverCID)
                    nametag = ac.getDriverName(hoverCID)
                    ui.pushDWriteFont(font)
                    ui.beginOutline()                    --c1xtz: added outline to make text more readable
                    ui.dwriteDrawText(ac.getCarName(hoverCID, false), 18, nametag_pos + vec2(0, 30), rgbm(1, 1, 1, 1))
                    ui.endOutline(rgb.colors.black, 0.5) --c1xtz: added outline to make text more readable
                    ui.popDWriteFont()
                else
                    --c1xtz: added the name of the teleport point
                    nametag = onlineTeleports[hoverMark][2]
                    ui.pushDWriteFont(font)
                    ui.beginOutline()
                    ui.dwriteDrawText(onlineTeleports[hoverMark][5], 18, nametag_pos + vec2(0, 30), rgbm(1, 1, 1, 1))
                    ui.endOutline(rgb.colors.black, 0.5)
                    ui.popDWriteFont()
                end
                ui.pushDWriteFont(fontBold)
                ui.beginOutline()                  --c1xtz: added outline to make text more readable
                ui.dwriteDrawText(nametag, 20, nametag_pos, rgbm(1, 1, 1, 1))
                ui.endOutline(rgb.colors.black, 1) --c1xtz: added outline to make text more readable
                ui.popDWriteFont()
                ui.drawImage(baseUrl .. 'cursor_ch.png', hoverCSP - vec2(40, 40), hoverCSP + vec2(40, 40))
            else
                if teleportAvailable then
                    ui.drawImage(baseUrl .. 'cursor_std.png', mp - vec2(40, 40), mp + vec2(40, 40))
                else
                    ui.drawImage(baseUrl .. 'cursor_ng.png', mp - vec2(40, 40), mp + vec2(40, 40))
                end
            end
        end)
    elseif ac.getCar(0).speedKmh < 2 and sim.focusedCar == 0 and not ac.getUI().appsHidden then
        local opacity = math.sin(sim.gameTime * 5) / 2 + 0.5
        ui.pushDWriteFont(fontBold)
        ui.beginOutline()                  --c1xtz: added outline to make text more readable
        ui.dwriteDrawText('Press M key to FastTravel', 20, vec2(sim.windowWidth, sim.windowHeight) * vec2(0.1, 0.9), rgbm(1, 1, 1, opacity))
        ui.endOutline(rgb.colors.black, 1) --c1xtz: added outline to make text more readable
        ui.popDWriteFont()
    end
end

function script.update(dt)
    teleportEstimate = teleportEstimate + dt
    mapTargetEstimate = mapTargetEstimate + dt
    hoverDelay = hoverDelay + dt
    if not apiWaiting then inputCheck() end
    mapCameraOwn = math.applyLag(mapCameraOwn, mapMode and 1 or 0, mapMode and 0.9 or 0.8, dt)
    if mapCamera then
        if mapCameraOwn < 0.001 then
            mapCamera.ownShare = 0
            mapCamera:dispose()
            mapCamera = nil
        else
            mapCamera.ownShare = mapCameraOwn
        end
    end
    if mapMode then
        if supportAPI_physics then physics.setGentleStop(0, true) end
        if config.disableCollisions and not disabledCollision then
            if supportAPI_collision then
                ac.log('Disabled own collisions')
                physics.disableCarCollisions(0, true)
            end
            disabledCollisionEvent({ disabled = true })
            disabledCollision = true
        end
        teleportEstimate = 0
    elseif mapCamera and mapCamera.ownShare > 0 then
        ac.setCurrentCamera(lastCameraMode)
        ac.focusCar(0)
    end
    if teleportEstimate > 1 then
        if supportAPI_physics then physics.setGentleStop(0, false) end
    end
    if config.disableCollisions and disabledCollision and teleportEstimate > 5 then
        local closer = false
        for i = 1, sim.carsCount - 1 do
            local carState = ac.getCar(i)
            local dist = carState.position:distance(ac.getCar(0).position)
            if dist < (carState.aabbSize.z / 2) then
                closer = true
                teleportEstimate = teleportEstimate - 1
                break
            end
        end
        if not closer then
            if disabledCollision then
                if supportAPI_collision then
                    ac.log('Enabled own collisions')
                    physics.disableCarCollisions(0, false)
                end
                disabledCollisionEvent({ disabled = false })
                disabledCollision = false
            end
        end
    end

    --c1xtz: register admin debug onlineExtra
    if ac.getSim().isAdmin and debugOnlineExtra == nil then
        debugOnlineExtra = ui.registerOnlineExtra(
            ui.Icons.SettingsAlt,
            "FastTravelPlugin Debug",

            function()
                if not debugWindowOpen then debugWindowOpen = true end
                return true
            end,

            window_FastTravelDebug,

            function()
                if debugWindowOpen then debugWindowOpen = false end
                return false
            end,

            ui.OnlineExtraFlags.Tool
        )
    end
end

function teleportExec(pos, rot)
    hoverMark = -1
    hoverCID = -1
    apiWaiting = false
    if supportAPI_physics then physics.setGentleStop(0, false) end
    physics.setCarPosition(0, pos, rot)
    mapMode = false
end

local fastTravelEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_FastTravel'),
        position = ac.StructItem.vec3(),
        direction = ac.StructItem.vec3()
    }, function(sender, message)
        if sender ~= nil then
            --print('Failed to load: ' .. err)
            teleportExec(mapTargetPos, vec3(1, 0, 0))
            return
        end

        teleportExec(message.position, message.direction)
    end)

function inputCheck()
    local sim = ac.getSim()
    local carState = ac.getCar(0)
    if ui.keyboardButtonPressed(ui.KeyIndex.M, false) and not ui.anyItemFocused() and not ui.anyItemActive() and not sim.isPaused then
        mapMode = not mapMode
        if mapMode then
            if not mapCamera then
                mapCamera = ac.grabCamera('map camera')
            end
            mapZoom = 1
            lastPos = carState.position:clone()
            lastMp = ui.mousePos()
            mapCamera.transform.position = lastPos
            mapCamera.fov = mapFOV
            lastCameraMode = sim.cameraMode
        else
            hoverMark = -1
            hoverCID = -1
        end
    end

    if mapMode and mapCamera then
        if sim.isPaused then
            mapMode = false
            return
        end

        local mp = ui.mousePos()
        local mpScale = vec2(sim.windowWidth, sim.windowHeight) / screenSize
        local mw = ui.mouseWheel()

        local pos, dir
        if supportAPI_matrix then
            local view = mat4x4.look(mapCamera.transform.position, mapCamera.transform.look, mapCamera.transform.up)
            local proj = mat4x4.perspective(math.rad(mapCamera.fov), screenSize.x / screenSize.y, 10, 30000)
            pos = mapCamera.transform.position
            dir = screenToWorldDir((mp - screenOffset) / screenSize, view, proj)
        else
            local ray = render.createPointRay((mp - screenOffset) * mpScale)
            pos = ray.pos
            dir = ray.dir
        end

        local mpr = nil
        local distance = getTrackDistance(pos, dir)
        if distance then
            mpr = pos + dir * distance
        end

        local zoomed = false
        local lastMapZoom = mapZoom
        if mw < 0 and mapZoom < #mapZoomValue then
            mapZoom = mapZoom + 1
            zoomed = true
        elseif mw > 0 and mapZoom > 1 then
            mapZoom = mapZoom - 1
            zoomed = true
        end
        if zoomed then
            mapTargetEstimate = 0
            if mapZoom == #mapZoomValue then
                mapTargetPos = mapFixedTargetPosition
            elseif hoverCID >= 0 then
                mapTargetPos = ac.getCar(hoverCID).position
            elseif hoverMark >= 0 then
                mapTargetPos = onlineTeleports[hoverMark][3]
            elseif mpr ~= nil then
                mapTargetPos = mpr
            else
                mapTargetPos = pos + dir * mapZoomValue[lastMapZoom]
            end
        end

        mapMovePower = vec2()
        if hoverCID == -1 and hoverMark == -1 and mapZoom < #mapZoomValue and lastMp:distance(mp) > 10 then
            lastMp = vec2(-1, -1)
            if mp.x > sim.windowWidth * (1 - mouseThreshold.x) and limitArea.z > mapCamera.transform.position.x then
                mapMovePower.x = (mp.x - (sim.windowWidth * (1 - mouseThreshold.x)))
            elseif mp.x < sim.windowWidth * mouseThreshold.x and limitArea.x < mapCamera.transform.position.x then
                mapMovePower.x = -((sim.windowWidth * mouseThreshold.x) - mp.x)
            else
                mapMovePower.x = 0
            end
            if mp.y > sim.windowHeight * (1 - mouseThreshold.y) and limitArea.w > mapCamera.transform.position.z then
                mapMovePower.y = (mp.y - (sim.windowHeight * (1 - mouseThreshold.y)))
            elseif mp.y < sim.windowHeight * mouseThreshold.y and limitArea.y < mapCamera.transform.position.z then
                mapMovePower.y = -((sim.windowHeight * mouseThreshold.y) - mp.y)
            else
                mapMovePower.y = 0
            end
        end
        mapMovePower = mapMovePower * sim.dt

        local pos = vec3()
        local rot = vec3(1, 0, 0)
        teleportAvailable = false
        if hoverMark >= 0 then
            teleportAvailable = true
            pos = onlineTeleports[hoverMark][3]
            rot = mat4x4.rotation(math.rad(onlineTeleports[hoverMark][4] - 90 + trackCompassOffset), vec3(0, 1, 0)).side
        elseif ac.hasTrackSpline() and false then
            local splineNorm = ac.worldCoordinateToTrackProgress(mpr)
            local splinePos = ac.trackProgressToWorldCoordinate(splineNorm)
            local splinePos2 = ac.trackProgressToWorldCoordinate(splineNorm - 0.0001)
            rot = vec3(1, math.atan2(splinePos2.z - splinePos.z, splinePos2.x - splinePos.x), 0)
            pos = splinePos
            teleportAvailable = true
        elseif mpr ~= nil then
            teleportAvailable = true
            pos = mpr
            rot = vec3(1, 0, 0)
        end
        if teleportAvailable and not debugWindowOpen then
            if ui.mouseClicked(ui.MouseButton.Left) then
                apiWaiting = true
                mapTargetPos = pos
                mapTargetEstimate = 0
                mapMovePower = vec2()
                if hoverMark >= 0 then
                    teleportExec(mapTargetPos, rot)
                else
                    fastTravelEvent({ position = pos, direction = vec3(0, 0, 0) })
                end
            end
        end
    end
end
