local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/groupstreetracing/"

local StreetRaceStatus = {
    Challenging = 0,
    Starting = 1,
    Started = 2,
    Ended = 3,
    Cancelled = 4
}

local ownSessionId = ac.getCar(0).sessionID

local carsWithHazards = {}
local packetLen = 20

local packetLen = 20
local carsWithHazardsEvent = ac.OnlineEvent({
    ac.StructItem.key("groupStreetRacingHazardsPacket"),
    SessionIds = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    HealthOfCars = ac.StructItem.array(ac.StructItem.byte(), packetLen)        
}, function(sender, data)
    -- only accept packets from server
    if sender ~= nil then
        return
    end
    carsWithHazards = {}
    for i = 0, packetLen - 1 do
        
        local sessionId = data.SessionIds[i]
        if sessionId ~= 255 then
            carsWithHazards[i] = {
                SessionId = sessionId,
                PositionInRace = i + 1,
                Health = data.HealthOfCars[i]
            }
        else
            carsWithHazards[i] = {
                SessionId = 255,
                PositionInRace = -1,
                Health = -1
            }
        end
    end
    ac.debug("carsWithHazardsS5", carsWithHazards[5].SessionId)
    ac.debug("carsWithHazardsH5", carsWithHazards[5].Health)
    ac.debug("carsWithHazardsS", carsWithHazards[0].SessionId)
    ac.debug("carsWithHazardsH", carsWithHazards[0].Health)
end)
--   function GetOwnRanking(callback)
--     web.get(leaderboardUrl .. "/" .. ac.getUserSteamID(), function (err, response)
--       callback(stringify.parse(response.body))
--     end)
--   end

function GetDriverNameBySessionId(sessionId)
    local count = ac.getSim().carsCount
    for i = 0, count do
        local car = ac.getCar(i)
        if car.sessionID == sessionId then
            return ac.getDriverName(car.index)
        end
    end
end

function script.drawUI()
    -- DrawTextCentered("You lost the race.")
    RacePartyHUD()
end

function DrawTextCentered(text)
    local uiState = ac.getUI()

    ui.transparentWindow('raceText', vec2(uiState.windowSize.x / 2 - 250, uiState.windowSize.y / 2 - 250), vec2(500, 100)
        ,
        function()
            ui.pushFont(ui.Font.Huge)

            local size = ui.measureText(text)
            ui.setCursorX(ui.getCursorX() + ui.availableSpaceX() / 2 - (size.x / 2))
            ui.text(text)

            ui.popFont()
        end)
end

function PrintCarWithHazardsRow(name, position, health)
    ui.text(tostring(name))
    ui.nextColumn()
    ui.text(tostring(position))
    ui.nextColumn()
    ui.text(tostring(health))
    ui.nextColumn()
end

function RacePartyHUD()
    ui.childWindow('groupStreetRacingList', vec2(0, 275), true, ui.WindowFlags.None, function()
        if #carsWithHazards == 0 then
            ui.text("No cars with hazards yet")
            ac.debug("nocars", "yes")
        else
            ui.columns(3)
            ui.setColumnWidth(0, 200)
            ui.setColumnWidth(1, 200)
            ui.setColumnWidth(2, 200)

            PrintCarWithHazardsRow("Racer", "Pos", "Health")

            for i, carwithHaz in pairs(carsWithHazards) do
                if carwithHaz.SessionId ~= 255 then
                    PrintCarWithHazardsRow(GetDriverNameBySessionId(carwithHaz.SessionId), carwithHaz.PositionInRace, carwithHaz.Health)
                end
            end

            ui.columns()
            ac.debug("columns", "yes")
        end
        ui.offsetCursorY(ui.availableSpaceY() - 32)
        if ui.button("Close") then
            close = true
        end

        ac.debug("hasLoadedUI", "yes")
    end)
end

function RaceHudClosed()
end

ui.registerOnlineExtra(ui.Icons.LightThunderstorm, 'Cars With Hazards', nil, RacePartyHUD, RaceHudClosed,
    ui.OnlineExtraFlags.Tool)
-- function ui.registerOnlineExtra(iconID, title, availableCallback, uiCallback, closeCallback, flags) end

-- ui.registerOnlineExtra(ui.Icons.Leaderboard, "Cars With Hazards", function() return true end, function()
--     local close = false
--     ui.childWindow('groupStreetRacingList', vec2(0, 275), false, ui.WindowFlags.None, function()
--         if carsWithHazards == nil then
--             ui.text("No cars with hazards yet")
--             ac.debug("nocars", "yes")
--         else
--             ui.columns(2)
--             ui.setColumnWidth(0, 45)
--             ui.setColumnWidth(1, 200)

--             PrintCarWithHazardsRow("#", "Distance")

--             for i, sessionId in ipairs(carsWithHazards) do
--                 PrintCarWithHazardsRow(GetDriverNameBySessionId(sessionId), 0)
--             end

--             ui.columns()
--             ac.debug("columns", "yes")
--         end
--         ui.offsetCursorY(ui.availableSpaceY() - 32)
--         if ui.button("Close") then
--             close = true
--         end

--         ac.debug("hasLoadedUI", "yes")
--     end)

--     return close
-- end)
