local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/randomdynamictraffic/"

local traffic_density = {
    LOW = 1,
    CASUAL = 2,
    PEAK = 3,
    ACCIDENT = 4
}

local trafficImages = {
    [traffic_density.LOW] = baseUrl .. "low.png",
    [traffic_density.CASUAL] = baseUrl .. "casual.png",
    [traffic_density.PEAK] = baseUrl .. "peak.png",
    [traffic_density.ACCIDENT] = baseUrl .. "accident.png"
}


local trafficImageToDraw = nil
local imagePath = nil

local randomDynamicTrafficEvent = ac.OnlineEvent({
    ac.StructItem.key("randomDynamicTrafficIconPacket"),
    TrafficState = ac.StructItem.byte()
}, function (sender, data)
    if sender ~= nil then return end
    ac.debug("TrafficState", data.TrafficState)

    imagePath = trafficImages[data.TrafficState]
    ac.debug("imagePath", imagePath)
end)

local color = rgbm(255, 255, 255, 0.8)
local iconSize = vec2(128, 59)
local centerPos = nil
function script.drawUI()
    DrawTrafficHud()
end


function DrawTrafficHud() 
    local uiState = ac.getUI()    
    if imagePath ~= nil then
        if centerPos == nil then
            --centerPos = vec2(ac.getUI().windowSize.x / 2, 50)
            centerPos = vec2(150, 50)
        end
        local p1 = vec2(centerPos.x - (iconSize.x + 10) / 2 * 1, centerPos.y)
        ui.drawImage(imagePath, p1, p1 + iconSize, color)            
        ac.debug("lastdraw", imagePath)
    end
end
