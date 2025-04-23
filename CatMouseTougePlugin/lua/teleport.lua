local supportAPI_physics = physics.setGentleStop ~= nil

function teleportExec(pos, rot)
    print("Teleporting car to position:", pos, "with direction:", rot)
    if supportAPI_physics then
        physics.setGentleStop(0, false)
    end
    physics.setCarPosition(0, pos, rot)
end

local teleportEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Teleport'),
        position = ac.StructItem.vec3(),
        direction = ac.StructItem.vec3()
    },
    function(sender, message)
        print("Received teleport packet")
        if sender ~= nil then
            print("Sender is nil")
            return
        end

        teleportExec(message.position, message.direction)
    end
)
