-- Copyright 2024 <github.com/razaqq>

local sim = ac.getSim()

local selectedCar = nil ---@type ac.StateCar

local function TeleportHUD()
  ui.text('Select car to teleport to:')

  ui.childWindow('##drivers', vec2(ui.availableSpaceX(), 120), function ()
    for i = 1, sim.carsCount - 1 do
      local car = ac.getCar(i)

      local driverName = ac.getDriverName(i)

      if car.isConnected and not car.isAIControlled and not string.find(driverName, "Traffic") then
        if ui.selectable(driverName, selectedCar == car) then
          selectedCar = car
        end

      end
    end
  end)
end

local function TeleportHUDClosed(okClicked)
  if okClicked and selectedCar then
    local dir = selectedCar.look;
    physics.setCarVelocity(0, vec3(0, 0, 0))
    -- spawn 8 meters behind, add 0.1 meter height to avoid falling through the map
    physics.setCarPosition(0, selectedCar.position + vec3(0, 0.1, 0) - dir * 8, -dir)
  end
end

ui.registerOnlineExtra(ui.Icons.FastForward, 'Teleport To Car', nil, TeleportHUD, TeleportHUDClosed, ui.OnlineExtraFlags.Admin)
