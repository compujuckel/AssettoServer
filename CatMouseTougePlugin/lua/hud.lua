-- Random boilerplate code I copied from other projects
local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/static/CatMouseTougePlugin/"

local font = 'Segoe UI'
local fontBold = 'Segoe UI;Weight=Bold'

local sim = ac.getSim()
local screenSize = vec2(sim.windowWidth, sim.windowHeight)
if sim.isVRMode then
    screenSize = screenSize * 0.6
end

-- Example state of the rounds for testing
local standings = { 0, 0, 0 }  -- Default, no rounds have been completed.
local isHudOn = false;

local standingEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Standing'),
        result1 = ac.StructItem.int32(),
        result2 = ac.StructItem.int32(),
        result3 = ac.StructItem.int32(),
        isHudOn = ac.StructItem.boolean()
    }, function (sender, message)
        print("Received standings packet.")
        if sender ~= nil then
            print("Sender is nil.")
            return
        end

        standings[1] = message.result1
        standings[2] = message.result2
        standings[3] = message.result3
        isHudOn = message.isHudOn


    end)

local elo = -1

local eloEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Elo'),
        elo = ac.StructItem.int32()
    }, function (sender, message)
        if sender ~= nil then
            print("Sender is nil.")
            return
        end       

        elo = message.elo

    end)

eloEvent({elo = elo});

-- Global variables for access across functions
local sim = ac.getSim()

function script.drawUI()

    -- Get updated window dimensions each frame
    local windowWidth = sim.windowWidth
    local windowHeight = sim.windowHeight

    if isHudOn then    
        ui.transparentWindow("scoreWindow", vec2(windowWidth/96, windowHeight/2), vec2(1000, 1000), function()

            -- Draw text
            ui.dwriteTextAligned("Standings:", 32, ui.Alignment.Start, ui.Alignment.Center, vec2(1000, 100), false, rgbm(1,1,1,1))

            
            -- Explicit loop through fixed indices
            for i = 1, 3 do
                local result = standings[i]
                
                -- Calculate position for each circle (horizontally centered)
                local circleRadius = 20
                local spacing = 20
                local totalWidth = (3 * (circleRadius * 2)) + (2 * spacing)
                local startX = (200 - totalWidth) / 2
                local xPos = startX + (i - 1) * (circleRadius * 2 + spacing) + circleRadius
                
                -- Set color based on result
                local color
                if result == 0 then
                    color = rgbm(0.5, 0.5, 0.5, 0.1) -- Gray for not played
                elseif result == 1 then
                    color = rgbm(0.561, 0.651, 0.235, 1) -- Green for won
                else
                    color = rgbm(0.349, 0.0078, 0.0078, 1) -- Red for lost
                end
                
                -- Draw circle with appropriate color
                ui.drawCircleFilled(vec2(xPos, 100), circleRadius, color)
            end
        end)
    end

    -- Draw elo hud element
        if elo ~= -1 then
            ui.transparentWindow("eloWindow", vec2(windowWidth/96, windowHeight/4), vec2(1000,1000), function ()
                ui.dwriteDrawText(string.format("Elo: %i", elo), 32, 1)
        
            end)
        end
end


