local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/static/CatMouseTougePlugin/"

local elo = -1
local hue = 180
local eloNumPos = vec2(66, 26)

local inviteSenderName = ""
local hasActiveInvite = false

local hasInviteMenuOpen = false
local connectedPlayers = {}
local nearbyPlayers = {}

local inviteActivatedAt = nil
local standings = { 0, 0, 0 }  -- Default, no rounds have been completed.
local isHudOn = false

local font = ""
local fontBold = ""
local fontSemiBold = ""

local eloHudPath = baseUrl .. "Elo.png"
local standingsHudPath = baseUrl .. "Standings.png"
local playerCardPath = baseUrl .. "PlayerCard.png"
local mKeyPath = baseUrl .. "MKey.png"
local inviteMenuPath = baseUrl .. "InviteMenu.png"

local sim = ac.getSim()

-- Load fonts
local fontsURL = baseUrl .. "fonts.zip"
web.loadRemoteAssets(fontsURL, function(err, folder)
    if err then
      print("Failed to load fonts: " .. err)
      return
    end

  
    -- Assuming the .ttf file is directly inside the ZIP
    local fontPath = folder .. "/twoweekendgo-regular.otf"
    local fontPathBold = folder .. "/twoweekendgo-bold.otf"
    local fontPathSemiBold = folder .. "/twoweekendgo-semibold.otf"
    font = string.format("Two Weekend Go:%s", fontPath)
    fontBold = string.format("Two Weekend Go:%s", fontPathBold)
    fontSemiBold = string.format("Two Weekend Go:%s", fontPathSemiBold)
  
end)

-- Events
local standingEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Standing'),
        result1 = ac.StructItem.int32(),
        result2 = ac.StructItem.int32(),
        result3 = ac.StructItem.int32(),
        isHudOn = ac.StructItem.boolean()
    }, function (sender, message)
        
        standings[1] = message.result1
        standings[2] = message.result2
        standings[3] = message.result3
        isHudOn = message.isHudOn


    end)

local eloEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Elo'),
        elo = ac.StructItem.int32()
    }, function (sender, message)

        elo = message.elo
        hue = (elo / 2000) * 360 - 80
        if elo >= 1000 then
            eloNumPos = vec2(66, 26)
        else
            eloNumPos = vec2(76, 26)
        end

    end)

local inviteEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Invite'),
        inviteSenderName = ac.StructItem.string(),
    }, function (sender, message)

        hasActiveInvite = true
        inviteSenderName = message.inviteSenderName
    end
)

local lobbyStatusEvent = ac.OnlineEvent({
    ac.StructItem.key('AS_LobbyStatus'),
    nearbyName1 = ac.StructItem.string(),
    nearbyId1 = ac.StructItem.uint64(),
    nearbyInRace1 = ac.StructItem.boolean(),
    nearbyName2 = ac.StructItem.string(),
    nearbyId2 = ac.StructItem.uint64(),
    nearbyInRace2 = ac.StructItem.boolean(),
    nearbyName3 = ac.StructItem.string(),
    nearbyId3 = ac.StructItem.uint64(),
    nearbyInRace3 = ac.StructItem.boolean(),
    nearbyName4 = ac.StructItem.string(),
    nearbyId4 = ac.StructItem.uint64(),
    nearbyInRace4 = ac.StructItem.boolean(),
    nearbyName5 = ac.StructItem.string(),
    nearbyId5 = ac.StructItem.uint64(),
    nearbyInRace5 = ac.StructItem.boolean(),

}, function (sender, message)

    -- Update nearby players
    nearbyPlayers[1] = {
        name = message.nearbyName1,
        id = message.nearbyId1,
        inRace = message.nearbyInRace1
      }
    nearbyPlayers[2] = {
        name = message.nearbyName2,
        id = message.nearbyId2,
        inRace = message.nearbyInRace2
      }
    nearbyPlayers[3] = {
        name = message.nearbyName3,
        id = message.nearbyId3,
        inRace = message.nearbyInRace3
      }
    nearbyPlayers[4] = {
        name = message.nearbyName4,
        id = message.nearbyId4,
        inRace = message.nearbyInRace4
      }
    nearbyPlayers[5] = {
        name = message.nearbyName5,
        id = message.nearbyId5,
        inRace = message.nearbyInRace5
      }
end)

-- Set the variables
eloEvent({elo = elo})

function HsvToRgb(h, s, v)
    local c = v * s
    local x = c * (1 - math.abs((h / 60) % 2 - 1))
    local m = v - c
    local r, g, b

    if h < 60 then r, g, b = c, x, 0
    elseif h < 120 then r, g, b = x, c, 0
    elseif h < 180 then r, g, b = 0, c, x
    elseif h < 240 then r, g, b = 0, x, c
    elseif h < 300 then r, g, b = x, 0, c
    else r, g, b = c, 0, x
    end

    return r + m, g + m, b + m
end

function script.drawUI()

    -- Get updated window dimensions each frame
    local windowWidth = sim.windowWidth
    local windowHeight = sim.windowHeight


    if isHudOn then    
        ui.transparentWindow("standingsWindow", vec2(50, windowHeight/2), vec2(387, 213), function()

            ui.drawImage(standingsHudPath, vec2(0,0), vec2(387,213))
            ui.pushDWriteFont(fontSemiBold)
            ui.dwriteDrawText("Standings", 48, vec2(44, 37))
            
            -- Explicit loop through fixed indices
            for i = 1, 3 do
                local result = standings[i]
                
                -- Calculate position for each circle (horizontally centered)
                local circleRadius = 25
                local spacing = 40
                local totalWidth = (3 * (circleRadius * 2)) + (2 * spacing)
                local startX = (200 - totalWidth) / 2 + 90
                local xPos = startX + (i - 1) * (circleRadius * 2 + spacing) + circleRadius
                
                -- Set color based on result
                local color
                if result == 0 then
                    color = rgbm(0.5, 0.5, 0.5, 0.1) -- Gray for not played
                elseif result == 1 then
                    color = rgbm(0.561, 0.651, 0.235, 1) -- Green for won
                else
                    color = rgbm(0.349, 0.0078, 0.0078, 1) -- Red for lost
                end
                
                -- Draw circle with appropriate color
                ui.drawCircleFilled(vec2(xPos, 145), circleRadius, color)
            end
        end)
    end

    -- Draw elo hud element
    if elo ~= -1 then
        ui.transparentWindow("eloWindow", vec2(50, 50), vec2(196,82), function ()
            local r, g, b = HsvToRgb(hue, 0.7, 0.8)
            ui.drawImage(eloHudPath, vec2(0, 0), vec2(196, 82), rgbm(r,g,b,1))
            ui.pushDWriteFont(font)
            ui.dwriteDrawText("Elo", 24, vec2(11, 31))
            ui.popDWriteFont()
            ui.pushDWriteFont(fontBold)
            ui.dwriteDrawText(tostring(elo), 34, eloNumPos)
            ui.popDWriteFont()
    
        end)
    end

    -- Draw invite menu hud.
    if hasInviteMenuOpen then
        ui.transparentWindow("inviteWindow", vec2(windowWidth - 818, 50), vec2(768,1145), function ()
            ui.drawImage(inviteMenuPath, vec2(0,0), vec2(768,1145))
            local color = rgbm(1,1,1,1)
            local index = 1

            -- Currently all the playercards are being drawn over each other.
            -- Still need to implement spacing based on index.
            while index <= 5 and nearbyPlayers[index] and nearbyPlayers[index].name ~= "" do
                -- Draw the nearby section
                ui.pushDWriteFont(font)
                ui.dwriteDrawText("Nearby", 48, vec2(40,40))
                ui.popDWriteFont()
                ui.drawImage(playerCardPath, vec2(32,120), vec2(737,292))
                ui.pushDWriteFont(fontBold)
                if nearbyPlayers[index].inRace then
                    color = rgbm(0.5, 0.5, 0.5, 1)
                else
                    color = rgbm(1,1,1,1)
                end
                ui.dwriteDrawText(nearbyPlayers[index].name, 48, vec2(212,182), color)

                index = index + 1
            end
        end)
    end

    -- Draw invite hud element
    if hasActiveInvite == true then
        ui.transparentWindow("receivedInviteWindow", vec2(windowWidth-755, windowHeight-222), vec2(705,172), function ()
            ui.drawImage(playerCardPath, vec2(0,0), vec2(705,172))
            ui.drawImage(mKeyPath, vec2(560,32), vec2(670,142))
            ui.pushDWriteFont(fontBold)
            ui.dwriteDrawText(tostring(inviteSenderName), 48, vec2(179,40))
            ui.popDWriteFont()
            ui.pushDWriteFont(font)
            ui.dwriteDrawText("Challenged you!", 36, vec2(180,95))
        end)
    end
end

function InputCheck()
    if ui.keyboardButtonPressed(ui.KeyIndex.N, false) and not ui.anyItemFocused() and not ui.anyItemActive() then
        -- Send invite
        inviteEvent({inviteSenderName = "senderName"})
    end
    if ui.keyboardButtonPressed(ui.KeyIndex.I, false) and not ui.anyItemFocused() and not ui.anyItemActive() then
        hasInviteMenuOpen = not hasInviteMenuOpen
        if hasInviteMenuOpen then lobbyStatusEvent() end -- This should have a cooldown
        -- Probably just start a timer and also check if it has run about before calling lobbyStatusEvent
    end

end

function script.update(dt)
    InputCheck()
    if hasActiveInvite and inviteActivatedAt == nil then
        inviteActivatedAt = os.clock()
    end

    if inviteActivatedAt ~= nil and os.clock() - inviteActivatedAt >= 10 then
        hasActiveInvite = false
        inviteActivatedAt = nil
    end
end
