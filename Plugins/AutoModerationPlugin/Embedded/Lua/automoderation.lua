local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/static/AutoModerationPlugin/"

local flags = {
    NO_LIGHTS = 1,
    NO_PARKING = 2,
    WRONG_WAY = 4
}

local flagImages = {
    [flags.NO_LIGHTS] = baseUrl .. "no_lights.png",
    [flags.NO_PARKING] = baseUrl .. "no_parking.png",
    [flags.WRONG_WAY] = baseUrl .. "wrong_way.png"
}

local flagsToDraw = {}
local autoModerationFlagEvent = ac.OnlineEvent({
    ac.StructItem.key("autoModerationFlag"),
    flags = ac.StructItem.byte()
}, function (sender, message)
    if sender ~= nil then return end

    flagsToDraw = {}

    for flag, imagePath in pairs(flagImages) do
        if bit.band(message.flags, flag) ~= 0 then
            table.insert(flagsToDraw, imagePath)
        end
    end
end)

local color = rgbm(255, 255, 255, 0.8)
local flagSize = vec2(128, 128)
local centerPos = nil
function script.drawUI()
    if #flagsToDraw > 0 then
        if centerPos == nil then
            centerPos = vec2(ac.getUI().windowSize.x / 2, 100)
        end

        local p1 = vec2(centerPos.x - (flagSize.x + 10) / 2 * #flagsToDraw, centerPos.y)

        for i, path in ipairs(flagsToDraw) do
            ui.drawImage(path, p1, p1 + flagSize, color)
            p1.x = p1.x + flagSize.x + 10
        end
    end
end
