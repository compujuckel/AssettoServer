local requestResetCarEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_RequestResetCar"),
    dummy = ac.StructItem.byte(),
}, function (sender, message)
    if sender ~= nil then return end
    ac.debug("request_reset_car", message.dummy)
end)

local resetCarControl = ac.ControlButton('__EXT_CMD_RESET', nil)
resetCarControl:onPressed(function() requestResetCarEvent({}) end)
