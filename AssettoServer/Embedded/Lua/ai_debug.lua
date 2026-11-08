local carsBySessionId = {}
local debugInfoBySessionId = {}

for i = 0, sim.carsCount - 1 do
    local c = ac.getCar(i)
    carsBySessionId[c.sessionID] = c
    debugInfoBySessionId[c.sessionID] = {
        CurrentSpeed = -1,
        TargetSpeed = -1,
        MaxSpeed = -1,
        ClosestAiObstacle = -1,
    }
end

local packetLen = 20
local debugEvent = ac.OnlineEvent({
    ac.StructItem.key("ai_debug"),
    SessionIds = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    CurrentSpeeds = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    TargetSpeeds = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    MaxSpeeds = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    ClosestAiObstacles = ac.StructItem.array(ac.StructItem.int16(), packetLen),
}, function (sender, message)
    for i = 0, packetLen - 1 do
        local sessionId = message.SessionIds[i]
        if sessionId ~= 255 then
            debugInfoBySessionId[sessionId].CurrentSpeed = message.CurrentSpeeds[i]
            debugInfoBySessionId[sessionId].TargetSpeed = message.TargetSpeeds[i]
            debugInfoBySessionId[sessionId].MaxSpeed = message.MaxSpeeds[i]
            debugInfoBySessionId[sessionId].ClosestAiObstacle = message.ClosestAiObstacles[i]
        end
    end
end)

function script.draw3D()
    for sessionID, debugInfo in pairs(debugInfoBySessionId) do
        local car = carsBySessionId[sessionID]
        if car.position:closerToThan(ac.getCameraPosition(), 200) then
            local message = "S:" .. debugInfo.CurrentSpeed .. "\nT:" .. debugInfo.TargetSpeed .. "\nM:" .. debugInfo.MaxSpeed .. "\nAO:" .. debugInfo.ClosestAiObstacle
            render.debugText(car.position, message, rgbm.colors.white, 1.5, render.FontAlign.Left)
        end
    end
end

-- debugEvent({
--     SessionIds = {9,8,7},
--     CurrentSpeeds = {1, 2, 3},
--     TargetSpeeds = {50, 60, 70},
--     MaxSpeeds = {13,14,15},
--     ClosestAiObstacles = {100, 200, 300},
-- }, false)
