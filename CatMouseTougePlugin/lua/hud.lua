-- Random boilerplate code I copied from other projects
local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/static/CatMouseTougePlugin/"

local font = 'Segoe UI'
local fontBold = 'Segoe UI;Weight=Bold'

local sim = ac.getSim()
local screenSize = vec2(sim.windowWidth, sim.windowHeight)
if sim.isVRMode then
    screenSize = screenSize * 0.6
end

local screenOffset = ((vec2(sim.windowWidth, sim.windowHeight) - screenSize) * 0.5)

-- Example state of the rounds for testing
local roundResults = { nil, true, false }  -- First not played yet, second won, third lost

-- Position and size
local startX, startY = 100, 100
local rectWidth, rectHeight = 50, 30
local spacing = 10

-- Draw the HUD
ui.beginTransparentWindow('best_of_3_hud', vec2(startX, startY), vec2(rectWidth * 3 + spacing * 2, rectHeight))
for i = 1, 3 do
    -- Set color based on result
    if roundResults[i] == true then
        ui.setColor(rgbm(0, 1, 0, 1)) -- Green for win
    elseif roundResults[i] == false then
        ui.setColor(rgbm(1, 0, 0, 1)) -- Red for loss
    else
        ui.setColor(rgbm(1, 1, 1, 0.3)) -- Grey / transparent for not yet played
    end

    -- Calculate position
    local posX = (i - 1) * (rectWidth + spacing)

    -- Draw rectangle
    ui.rect(vec2(posX, 0), vec2(posX + rectWidth, rectHeight))
end
ui.endTransparentWindow()
