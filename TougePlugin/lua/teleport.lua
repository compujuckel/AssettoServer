local supportAPI_physics = physics.setGentleStop ~= nil -- For disabling physics
local supportAPI_collision = physics.disableCarCollisions ~= nil

local teleportTimer = nil

function TeleportExec(pos, rot)
    if supportAPI_collision then physics.disableCarCollisions(0, true) end
    pos.y = FindGroundY(pos)  -- Adjust y-coordinate to ground level
    rot.y = 0 -- Make sure the car is right side up.
    physics.setCarPosition(0, pos, rot)
    
    -- Start teleport timer
    teleportTimer = 10
    
end

local teleportEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Teleport'),
        position = ac.StructItem.vec3(),
        direction = ac.StructItem.vec3()
    },
    function(sender, message)
        if sender ~= nil then
            print("Sender is nil")
            return
        end

        TeleportExec(message.position, message.direction)
    end
)

function FindGroundY(pos)
    local dir = vec3(0, -1, 0)  -- Direction: downward
    local maxDistance = 100.0    -- Maximum distance to check
    local distance = physics.raycastTrack(pos, dir, maxDistance)
    if distance >= 0 then
        return pos.y - distance
    else
        return pos.y  -- Fallback if no hit detected
    end
end

function script.update(dt)
    if teleportTimer then
        teleportTimer = teleportTimer - dt
        if teleportTimer <= 0 then
            teleportTimer = nil
            if supportAPI_collision then physics.disableCarCollisions(0, false) end
        end
    end
end
