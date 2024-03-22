-- Copyright 2024 <github.com/razaqq>

local selectedWeather = nil ---@type ac.WeatherType
local transitionDuration = 0.0 ---@type number

local function ChangeWeatherHUD()
  ui.text('Select Weather:')

  --ac.INIConfig()

  ui.childWindow('##weathers', vec2(ui.availableSpaceX(), 250), function ()
    for name, id in pairs(ac.WeatherType) do
      if ui.selectable(name, refbool(selectedWeather == name)) then
        selectedWeather = name
      end
    end
  end)

  transitionDuration = ui.slider('##someSliderID', transitionDuration, 0, 60, 'Transition Duration: %.0f mins')
end

local function ChangeWeatherHUDClosed(okClicked)
  if okClicked and selectedWeather then
    ac.sendChatMessage(string.format('/setcspweather %s %d', selectedWeather, transitionDuration * 60))
  end
end

ui.registerOnlineExtra(ui.Icons.FastForward, 'Change Weather', nil, ChangeWeatherHUD, ChangeWeatherHUDClosed, ui.OnlineExtraFlags.Admin)
