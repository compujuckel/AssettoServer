local requestResetCarEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_RequestResetCar"),
    dummy = ac.StructItem.byte(),
}, function (sender, message)
    if sender ~= nil then return end
    ac.debug("request_reset_car", message.spicy)
end)

local resetCarControl = ac.ControlButton('__EXT_CMD_RESET', nil)
resetCarControl:onPressed(function() requestResetCarEvent({spicy=0}) end)

local drawWindow = true
local centerPos = vec2(ac.getUI().windowSize.x / 2, ac.getUI().windowSize.y / 4)
local size = vec2(500, 250)
local position = vec2(centerPos.x - (size.x / 2), centerPos.y - (size.y / 2))

setTimeout(function ()
    drawWindow = false
end, 30)

function script.drawUI()
    if drawWindow then
        ui.beginTransparentWindow('resetCarWindow', position, size)
        ui.beginOutline()
        
        if resetCarControl:disabled() or not resetCarControl:configured() then
            ui.pushFont(ui.Font.Huge)
            
            ui.text("Reset button not bound")
            
            ui.popFont()

            ui.text("Please bind a button in Content Manager")
            ui.text("Settings -> Assetto Corsa -> Controls -> Patch -> Reset car")
            ui.text("In the meantime you can use /resetcar in the chat")
        else
            ui.pushFont(ui.Font.Huge)
            
            ui.text("Reset button: " .. resetCarControl:boundTo())
            
            ui.popFont()
        end
        
        ui.endOutline(rgbm.colors.black, 1)
        ui.endTransparentWindow()
    end
end
