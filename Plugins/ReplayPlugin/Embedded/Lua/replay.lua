
local uploadDataEvent = ac.OnlineEvent({
    ac.StructItem.key("ReplayPlugin_uploadData"),
    sessionID = ac.StructItem.byte(),
    wheelPositions = ac.StructItem.array(ac.StructItem.vec3(), 4)
})

local alreadyUploaded = {}

local function uploadReplayDataForCar(index)
    if alreadyUploaded[index] == true then
        return
    else
        alreadyUploaded[index] = true
    end

    local car = ac.getCar(index)
    -- we need physics transform, so we can't use car.worldToLocal here
    local worldToLocal = car.transform:inverse()

    uploadDataEvent({
        sessionID = car.sessionID,
        wheelPositions = {
            worldToLocal:transformPoint(car.wheels[0].position),
            worldToLocal:transformPoint(car.wheels[1].position),
            worldToLocal:transformPoint(car.wheels[2].position),
            worldToLocal:transformPoint(car.wheels[3].position),
        }
    })
end

uploadReplayDataForCar(0);

setTimeout(function ()
    for i, car in ac.iterateCars.ordered() do
        uploadReplayDataForCar(car.index)
    end
end, 5)

ac.onClientConnected(function (index, sessionID)
    uploadReplayDataForCar(index)
end)
