-- DONT CHANGE THIS STRUCT OR YOU NEED TO GET A NEW ID FOR THE PACKET.CS
local tagModeColorEvent = ac.OnlineEvent(
    { 
        ac.StructItem.key("tagModeColorPacket"), 
        Color = ac.StructItem.rgbm(),
        Disconnect = ac.StructItem.boolean()
    }, function(sender, message)
        local carNode = ac.findNodes('carRoot:' .. sender.index)
        carNode:resetSkin()

        if not message.Disconnect then
            ac.debug("tagModeColor", message.Color)
            carNode -- this filter is probably extremely shit
                :findMeshes('{ shader:smCarPaint | material:?body? | material:?Body? | material:?BODY? | material:?car_paint? | material:?Car_Paint? | material:?CAR_PAINT? | material:?carpaint? | material:?CarPaint? | material:?carPaint? | material:?CARPAINT? }')
                :setMaterialTexture('txDiffuse', message.Color)
                :setMaterialTexture('txEmissive', message.Color)
        end
        
        ac.refreshCarColor(sender.index)
    end)
