local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/static/CatMouseTougePlugin/"

local scaling = require('scaling')

local windowWidth = sim.windowWidth
local windowHeight = sim.windowHeight

local elo = -1
local hue = 180
local eloNumPos = vec2(66, 26)

local inviteSenderName = ""
local hasActiveInvite = false
local inviteActivatedAt = nil

local hasInviteMenuOpen = false
local nearbyPlayers = {}
local lastLobbyStatusRequest = 0
local lobbyCooldown = 1.0  -- Cooldown in seconds

local standings = { 0, 0, 0 }  -- Default, no rounds have been completed.
local isHudOn = false

local hasTutorialHidden = false
local keyBindings = {
    { key = "N", description = "Invite\nnearby" },
    { key = "I", description = "Invite\nmenu" },
    { key = "H", description = "Hide\ntutorial" }
}

local font = ""
local fontBold = ""
local fontSemiBold = ""

local eloHudPath = baseUrl .. "Elo.png"
local standingsHudPath = baseUrl .. "Standings.png"
local playerCardPath = baseUrl .. "PlayerCard.png"
local mKeyPath = baseUrl .. "MKey.png"
local inviteMenuPath = baseUrl .. "InviteMenu.png"
local tutorialPath = baseUrl .. "Tutorial.png"
local keyPath = baseUrl .. "Key.png"

local notificationMessage = ""
local hasIncomingNotification = false
local notificationActivatedAt = nil

local sim = ac.getSim()

-- Load fonts
local fontsURL = baseUrl .. "fonts.zip"
web.loadRemoteAssets(fontsURL, function(err, folder)
    if err then
      print("Failed to load fonts: " .. err)
      return
    end

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
            eloNumPos = scaling.vec2(66, 26)
        else
            eloNumPos = scaling.vec2(76, 26)
        end

    end)

local inviteEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Invite'),
        inviteSenderName = ac.StructItem.string(),
        inviteRecipientGuid = ac.StructItem.uint64(),
    }, function (sender, message)

        if message.inviteSenderName ~= "" and message.inviteRecipientGuid ~= 1 then
            hasActiveInvite = true
            inviteSenderName = message.inviteSenderName
        end
    end
)

local notificationEvent = ac.OnlineEvent(
    {
        ac.StructItem.key('AS_Notification'),
        message = ac.StructItem.string(64),
    }, function (sender, message)
        notificationMessage = message.message
        hasIncomingNotification = true
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


-- Utility functions

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

function DrawKey(key, pos, letterPos)
    ui.transparentWindow("tutorialWindow", pos, scaling.vec2(110,110), function ()

        letterPos = letterPos or vec2(39, 35)

        ui.drawImage(keyPath, vec2(0,0), scaling.vec2(110,110))

        ui.pushDWriteFont(fontBold)
        ui.dwriteTextAligned(key, scaling.size(40), ui.Alignment.Start, ui.Alignment.Center)
        ui.dwriteDrawText(key, scaling.size(40), letterPos)
        ui.popDWriteFont()
    end)
end

function script.drawUI()

    -- Get updated window dimensions each frame
    windowWidth = sim.windowWidth
    windowHeight = sim.windowHeight

    if isHudOn then    
        ui.transparentWindow("standingsWindow", scaling.vec2(50, windowHeight/2), scaling.vec2(387, 213), function()

            ui.drawImage(standingsHudPath, vec2(0,0), scaling.vec2(387,213))
            ui.pushDWriteFont(fontSemiBold)
            ui.dwriteDrawText("Standings", scaling.size(48), scaling.vec2(44, 37))
            -- Explicit loop through fixed indices
            for i = 1, 3 do
                local result = standings[i]
                -- Calculate position for each circle (horizontally centered)
                local circleRadius = scaling.size(25)
                local spacing = scaling.size(40)
                local totalWidth = (3 * (circleRadius * 2)) + (2 * spacing)
                local startX = scaling.size((200 - totalWidth) / 2 + 90)
                local xPos = scaling.size(startX + (i - 1) * (circleRadius * 2 + spacing) + circleRadius)
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
                ui.drawCircleFilled(scaling.vec2(xPos, 145), circleRadius, color)
            end
        end)
    end

    -- Draw elo hud element
    if elo ~= -1 then
        ui.transparentWindow("eloWindow", scaling.vec2(50, 50), scaling.vec2(196,82), function ()
            local r, g, b = HsvToRgb(hue, 0.7, 0.8)
            ui.drawImage(eloHudPath, scaling.vec2(0, 0), scaling.vec2(196, 82), rgbm(r,g,b,1))
            ui.pushDWriteFont(font)
            ui.dwriteDrawText("Elo", scaling.size(24), scaling.vec2(11, 31))
            ui.popDWriteFont()
            ui.pushDWriteFont(fontBold)
            ui.dwriteDrawText(tostring(elo), scaling.size(34), eloNumPos)
            ui.popDWriteFont()
        end)
    end

    -- Draw tutorial hud element
    if not hasTutorialHidden then
        ui.transparentWindow("tutorialWindow", vec2(scaling.size(50), windowHeight - scaling.size(465)), scaling.vec2(584, 415), function ()
            ui.drawImage(tutorialPath, vec2(0,0), scaling.vec2(584, 415))
            ui.pushDWriteFont(fontSemiBold)
            ui.dwriteDrawText("How to play", scaling.size(24), scaling.vec2(32, 32))
            ui.popDWriteFont()
            ui.pushDWriteFont(font)
            ui.dwriteDrawText("Chase car overtakes before finish: 1 point to the chase car.\nChase car stays close: draw, no points.\nLead car outruns: 1 point to the lead car.\n\nIf score is tied after the first two rounds: Sudden death.", scaling.size(12), scaling.vec2(32, 78))
            ui.popDWriteFont()
            ui.pushDWriteFont(fontSemiBold)
            ui.dwriteDrawText("Controls", scaling.size(24), scaling.vec2(32, 177))
            ui.popDWriteFont()

            local startX = scaling.size(130)
            local spacingX = scaling.size(150)  -- Space between each key+label pair
            local baseY = windowHeight - scaling.size(250)     -- Fixed vertical position
            local startXText = scaling.size(130)

            for i, binding in ipairs(keyBindings) do
                local xOffset = startX + (i - 1) * spacingX
                local XTextOffest = startXText + (i - 1)
                local keyPos = scaling.vec2(xOffset, baseY)
                local textPos = scaling.vec2(XTextOffest - scaling.size(120), 120)

                -- Draw the key
                DrawKey(binding.key, keyPos)

                -- Draw the description
                ui.pushDWriteFont(fontSemiBold)
                ui.dwriteDrawText(binding.description, scaling.size(24), textPos)
                ui.popDWriteFont()
            end
        end)
    end

    -- Draw invite menu hud.
    if hasInviteMenuOpen then
        ui.transparentWindow("inviteWindow", scaling.vec2(windowWidth - 818, 50), scaling.vec2(768,1145), function ()
            ui.drawImage(inviteMenuPath, vec2(0,0), scaling.vec2(768,1145))
            local index = 1

            local cardSpacingY = scaling.size(180)  -- Space between cards vertically
            local baseY = scaling.size(120)         -- Starting Y position

            local mousePos = ui.mouseLocalPos()

            -- Currently all the playercards are being drawn over each other.
            -- Still need to implement spacing based on index.
            while index <= 5 and nearbyPlayers[index] and nearbyPlayers[index].name ~= "" do
                local yOffset = baseY + (index - 1) * cardSpacingY  -- Calculate Y offset

                -- Draw the nearby section title once
                if index == 1 then
                    ui.pushDWriteFont(font)
                    ui.dwriteDrawText("Nearby", scaling.size(48), scaling.vec2(40, 40))
                    ui.popDWriteFont()
                end

                local cardPos = scaling.vec2(32, yOffset)
                local cardSize = scaling.vec2(737, 172)
                local cardBottomRight = cardPos + cardSize

                -- Draw player card background
                ui.drawImage(playerCardPath, scaling.vec2(32, yOffset), scaling.vec2(737, yOffset + 172))

                -- Check for mouse click inside card bounds
                if ui.mouseClicked() then
                    if mousePos.x >= cardPos.x and mousePos.x <= cardBottomRight.x and
                    mousePos.y >= cardPos.y and mousePos.y <= cardBottomRight.y then
                    -- Player card was clicked
                    print("Clicked on player:", nearbyPlayers[index].name)
                    hasInviteMenuOpen = false;
                    -- Do something, like sending an invite
                    inviteEvent({inviteSenderName = "", inviteRecipientGuid = nearbyPlayers[index].id})
                    end
                end

                -- Draw player name
                ui.pushDWriteFont(fontBold)
                local color = nearbyPlayers[index].inRace and rgbm(0.5, 0.5, 0.5, 1) or rgbm(1, 1, 1, 1)
                ui.dwriteDrawText(nearbyPlayers[index].name, scaling.size(48), scaling.vec2(212, yOffset + 62), color)
                ui.popDWriteFont()

                index = index + 1
            end
        end)
    end

    -- Draw invite hud element
    if hasActiveInvite == true then
        ui.transparentWindow("receivedInviteWindow", scaling.vec2(windowWidth-755, windowHeight-222), scaling.vec2(705,172), function ()
            ui.drawImage(playerCardPath, vec2(0,0), scaling.vec2(705,172))
            ui.drawImage(mKeyPath, scaling.vec2(560,32), scaling.vec2(670,142))
            ui.pushDWriteFont(fontBold)
            ui.dwriteDrawText(tostring(inviteSenderName), scaling.size(48), scaling.vec2(179,40))
            ui.popDWriteFont()
            ui.pushDWriteFont(font)
            ui.dwriteDrawText("Challenged you!", scaling.size(36), scaling.vec2(180,95))
        end)
    end

    -- Draw notification hud element
    if hasIncomingNotification then
        local notificationPos = scaling.vec2(windowWidth-755, windowHeight-222)
        if hasActiveInvite then
            -- If there is an active invite, draw it above.
            notificationPos = scaling.vec2(windowWidth-755, windowHeight-414)
        end

        ui.transparentWindow("notificationWindow", notificationPos, scaling.vec2(705,172), function ()
            ui.drawImage(playerCardPath, vec2(0,0), scaling.vec2(705,172))
            ui.pushDWriteFont(fontSemiBold)
            ui.dwriteDrawText(notificationMessage, scaling.size(18), scaling.vec2(179,40))
            ui.popDWriteFont()
        end)
    end
end

function InputCheck()
    if ui.keyboardButtonPressed(ui.KeyIndex.N, false) and not ui.anyItemFocused() and not ui.anyItemActive() then
        -- Send invite
        inviteEvent({inviteSenderName = "nearby", inviteRecipientGuid = 1})
    end
    if ui.keyboardButtonPressed(ui.KeyIndex.I, false) and not ui.anyItemFocused() and not ui.anyItemActive() then
        hasInviteMenuOpen = not hasInviteMenuOpen
        if hasInviteMenuOpen then
            local now = os.clock()
            if now - lastLobbyStatusRequest > lobbyCooldown then
                lastLobbyStatusRequest = now
                lobbyStatusEvent()
            end
        end
    end
    if ui.keyboardButtonPressed(ui.KeyIndex.M, false) and not ui.anyItemFocused() and not ui.anyItemActive() and hasActiveInvite then
        -- Accept invite
        inviteEvent({inviteSenderName = "a", inviteRecipientGuid = 1})
        hasActiveInvite = false
    end
    if ui.keyboardButtonPressed(ui.KeyIndex.H, false) and not ui.anyItemFocused() and not ui.anyItemActive() then
        hasTutorialHidden = not hasTutorialHidden
    end
end

function script.update(dt)
    InputCheck()
    if hasActiveInvite and inviteActivatedAt == nil then
        inviteActivatedAt = os.clock()
    end
    if hasIncomingNotification and notificationActivatedAt == nil then
        notificationActivatedAt = os.clock()
    end

    if inviteActivatedAt ~= nil and os.clock() - inviteActivatedAt >= 10 then
        hasActiveInvite = false
        inviteActivatedAt = nil
    end
    if notificationActivatedAt ~= nil and os.clock() - notificationActivatedAt >= 10 then
        hasIncomingNotification = false
        notificationActivatedAt = nil
    end
end
