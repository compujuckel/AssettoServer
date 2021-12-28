local EventType = {
  None = 0,
  RaceChallenge = 1,
  RaceCountdown = 2,
  RaceInProgress = 3,
  RaceEnded = 4
}

local rivalHealth = 1.0
local ownHealth = 1.0
local rivalId = 0
local rivalName = ""
local lastEvent = EventType.None
local raceStartTime = -1
local raceEndTime = 0
local lastWinner = 0
local ownSessionId = ac.getCar(0).sessionID

function GetDriverNameBySessionId(sessionId)
  local count = ac.getSimState().carsCount
  for i = 0, count do
    local car = ac.getCar(i)
    if car.sessionID == sessionId then
      return ac.getDriverName(car.index)
    end
  end
end

local raceUpdateEvent = ac.OnlineEvent({
    eventType = ac.StructItem.byte(),
    eventData = ac.StructItem.int32(),
    ownHealth = ac.StructItem.float(),
    rivalHealth = ac.StructItem.float()
  }, function (sender, data)
    -- only accept packets from server
    if sender ~= nil then
      return
    end

    ac.debug("sender", sender)
    ac.debug("eventType", data.eventType)
    ac.debug("eventData", data.eventData)
    ac.debug("ownHealth", ownHealth)
    ac.debug("rivalHealth", rivalHealth)

    if data.eventType == EventType.RaceChallenge then
      rivalId = data.eventData
      rivalName = GetDriverNameBySessionId(data.eventData)
      ac.debug("rivalName", rivalName)
    end

    if data.eventType == EventType.RaceCountdown then
      raceStartTime = data.eventData
    end

    if data.eventType == EventType.RaceInProgress then
      ownHealth = data.ownHealth
      rivalHealth = data.rivalHealth
    end

    if data.eventType == EventType.RaceEnded then
      lastWinner = data.eventData
      raceEndTime = GetSessionTime()
    end

    lastEvent = data.eventType
  end)


function GetSessionTime()
  return ac.getSimState().timeToSessionStart * -1
end

-- sending a new message:
--raceUpdateEvent{ eventType = 1, eventData = 2, ownHealth = 0.6, rivalHealth = 0.3 }

function script.drawUI(dt)
  ac.debug("lastEvent", lastEvent)
  ac.debug("rivalId", rivalId)

  local currentTime = GetSessionTime()
  local raceTimeElapsed = raceStartTime - currentTime

  if lastEvent == EventType.RaceCountdown then
    DrawHealthHud(0)

    if raceTimeElapsed > -3000 and raceTimeElapsed < 0 then
      local text = math.ceil(raceTimeElapsed / 1000) * -1
      DrawTextCentered(text)
    end
  end

  if lastEvent == EventType.RaceInProgress then
    if(raceTimeElapsed > 0 and raceTimeElapsed < 1000) then
      DrawTextCentered("Go!")
    end

    DrawHealthHud(GetSessionTime() - raceStartTime)
  end

  if lastEvent == EventType.RaceEnded and raceEndTime > GetSessionTime() - 3000 then
    DrawHealthHud(raceEndTime - raceStartTime)

    if lastWinner == 255 then
      DrawTextCentered("Race cancelled")
    elseif lastWinner == ownSessionId then
      DrawTextCentered("You won the race!")
    else
      DrawTextCentered("You lost the race.")
    end
  end
end

function DrawTextCentered(text)
  local uiState = ac.getUI()

  ui.transparentWindow('raceText', vec2(uiState.windowSize.x / 2 - 250, uiState.windowSize.y / 2 - 50), vec2(500,100), function ()
    ui.pushFont(ui.Font.Huge)
    
    local size = ui.measureText(text)
    ui.setCursorX(ui.getCursorX() + ui.availableSpaceX() / 2 - (size.x / 2))
    ui.text(text)

    ui.popFont()
  end)
end

function HealthBar(size, progress, direction)
  progress = math.clamp(progress, 0, 1)

  ui.drawRect(ui.getCursor(), ui.getCursor() + size, rgbm(1,1,1,1))

  local p1, p2
  if direction == -1 then
    p1 = ui.getCursor() + vec2(size.x * (1 - progress), 0)
    p2 = ui.getCursor() + size
  else
    p1 = ui.getCursor()
    p2 = ui.getCursor() + vec2(size.x * progress, size.y)
  end

  ui.drawRectFilled(p1, p2, rgbm(1,1,1,1))

  ui.dummy(size)
end

function DrawHealthHud(time)
  local uiState = ac.getUI()

  ui.toolWindow('raceChallengeHUD', vec2(uiState.windowSize.x / 2 - 500, 25), vec2(1000, 120), function ()
    ui.pushFont(ui.Font.Title)

    ui.columns(3)
    ui.text("PLAYER")
    ui.nextColumn()

    local laptime = ac.lapTimeToString(time)
    local size = ui.measureText(laptime)
    
    ui.setCursorX(ui.getCursorX() + ui.availableSpaceX() / 2 - (size.x / 2))
    ui.text(laptime)
    ui.nextColumn()
    ui.textAligned("RIVAL", ui.Alignment.End, vec2(-1,0))

    ui.columns(2)
    HealthBar(vec2(ui.availableSpaceX(), 30), ownHealth, -1)
    ui.textAligned(ac.getDriverName(0), ui.Alignment.Start, vec2(-1,0))
    ui.nextColumn()
    HealthBar(vec2(ui.availableSpaceX(), 30), rivalHealth, 1)
    ui.textAligned(rivalName, ui.Alignment.End, vec2(-1,0))

    ui.popFont()
  end)
end