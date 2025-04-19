-- DONT CHANGE THIS STRUCT OR YOU NEED TO GET A NEW ID FOR THE PACKET.CS
local tagModeColorEvent = ac.OnlineEvent(
    { 
        ac.StructItem.key("tagModeColorPacket"), 
        R = ac.StructItem.byte(),
        G = ac.StructItem.byte(),
        B = ac.StructItem.byte(),
        Target = ac.StructItem.byte(),
        Disconnect = ac.StructItem.boolean()
    }, function(sender, message)
        if sender ~= nil then return end

        local car = ac.getCar.serverSlot(message.Target)
        
        local carNode = ac.findNodes('carRoot:' .. car.index)
        carNode:resetSkin()

        if not message.Disconnect then
            local color = rgbm(message.R/255, message.G/255, message.B/255, 1)
            ac.debug("tagModeColor", color)
            carNode -- this filter is probably extremely shit
                :findMeshes('{ shader:smCarPaint | material:?body? | material:?Body? | material:?BODY? | material:?car_paint? | material:?Car_Paint? | material:?CAR_PAINT? | material:?carpaint? | material:?CarPaint? | material:?carPaint? | material:?CARPAINT? }')
                :setMaterialTexture('txDiffuse', color)
                :setMaterialTexture('txEmissive', color)
                :storeCurrentTransformation()
        end
        
        ac.refreshCarColor(car.index)
    end)
