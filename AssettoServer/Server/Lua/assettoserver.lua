local license = [[
Copyright (C)  2025 Niewiarowski, compujuckel

This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.


Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with the Steamworks SDK by Valve Corporation, containing parts covered by the terms of the Steamworks SDK License, the licensors of this Program grant you additional permission to convey the resulting work.

Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with plugins published on https://www.patreon.com/assettoserver, the licensors of this Program grant you additional permission to convey the resulting work. 
]]

local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP()
local configUrl = baseUrl .. "/api/configuration"
local logoUrl = baseUrl .. "/assets/logo_42.png"
local srpLogoUrl = baseUrl .. "/assets/srp-logo-new.png?3"
local configurationLoading = false
local configuration
local authHeaders = {}

local function getConfiguration()
    web.get(configUrl, authHeaders, function (err, response)
        if response.status == 200 then
            ac.log("config loaded")
            configuration = stringify.parse(response.body)
            configurationLoading = false
        end
    end)
end

local function setValue(key, value)
    web.post(configUrl .. "?key=" .. key .."&value=" .. tostring(value), authHeaders, function (err, response)
        ac.debug("err", err)
        ac.debug("response", stringify(response))

        if response.status ~= 200 then
            ui.toast(ui.Icons.Ban, "Error updating " .. key .. " (" .. response.status .. ")")
        else
            local parsed = stringify.parse(response.body)
            if parsed.Status ~= "OK" then
                ui.toast(ui.Icons.Ban, "Error updating " .. key .. " (" .. parsed.ErrorMessage .. ")")
            else
                ui.toast(ui.Icons.Confirm, key .. " set to " .. tostring(value))
            end
        end
    end)
end

local apiKeyEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_ApiKey"),
    key = ac.StructItem.string(32)
}, function (sender, message)
    if sender ~= nil then return end
    ac.debug("key", message.key)
    authHeaders["X-Car-Id"] = car.sessionID
    authHeaders["X-Api-Key"] = message.key
end)

local teleportCarEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_TeleportCar"),
    position = ac.StructItem.vec3(),
    direction = ac.StructItem.vec3(),
    velocity = ac.StructItem.vec3(),
}, function (sender, message)
    if sender ~= nil then return end
    ac.debug("teleport_car_position", message.position)
    ac.debug("teleport_car_direction", message.direction)
    ac.debug("teleport_car_velocity", message.velocity)
    
    physics.setCarPosition(0, message.position, message.direction)
    physics.setCarVelocity(0, message.velocity)
end)

local collisionUpdateEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_CollisionUpdate"),
    enabled = ac.StructItem.boolean()
}, function (sender, message)
    ac.debug("collision_update_index", sender.index)
    ac.debug("collision_update_enabled", message.enabled)

    physics.disableCarCollisions(sender.index, not message.enabled, true)
end)

local teleportToPitsEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_TeleportToPits"),
    dummy = ac.StructItem.byte()
}, function (sender, message)
    if sender.index == 0 and ac.INIConfig.onlineExtras():get("EXTRA_RULES", "NO_BACK_TO_PITS", 0) == 0 then
       physics.teleportCarTo(0, ac.SpawnSet.Pits) 
    end
end)

local requestResetCarEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_RequestResetCar"),
    dummy = ac.StructItem.byte(),
}, function (sender, message)
    if sender ~= nil then return end
    ac.debug("request_reset_car", message.dummy)
end)

local luaReadyEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_LuaReady"),
    dummy = ac.StructItem.byte()
}, function () end)

local logoSize = vec2(68, 42)
local srpLogoSize = vec2(244, 64)
local isSRP = ac.getTrackID():find("^shuto_revival_project_beta") ~= nil

-- ui.textHyperlink not supported on <0.1.79
local function ui_hyperlink(link)
    if ui.textHyperlink == nil then
        ui.text(link)
    else
        if ui.textHyperlink(link) then
            os.openURL(link)
        end
    end
end

local function tab_About()
    ui.childWindow("license", ui.availableSpace(), function ()
        ui.offsetCursorY(10)
        ui.image(logoUrl, logoSize)
        ui.sameLine()
        ui.offsetCursorY(-15)
        ui.pushFont(ui.Font.Huge)
        ui.text("AssettoServer")
        ui.popFont()

        ui.textWrapped("This server runs AssettoServer, making it possible to have online traffic in Assetto Corsa. AssettoServer is free software, so you can run your own traffic server.")
        ui.text("")
        ui.textWrapped("Visit the website for more info:")
        ui.sameLine()
        ui_hyperlink("https://assettoserver.org")

        ui.textWrapped("Official Discord server:")
        ui.sameLine()
        ui_hyperlink("https://discord.gg/uXEXRcSkyz")

        ui.text("")
        ui.pushFont(ui.Font.Title)
        ui.textWrapped("Support AssettoServer development")
        ui.popFont()
        ui.textWrapped("Patreon:")
        ui.sameLine()
        ui_hyperlink("https://patreon.com/assettoserver")

        if isSRP then
            ui.offsetCursorY(10)
            ui.image(srpLogoUrl, srpLogoSize)

            ui.offsetCursorY(5)
            ui.textWrapped("This server is running the Shutoko Revival Project track.")
            ui.textWrapped("This project aims to be the definitive version of Shutoko, otherwise known as Tokyo Metropolitan Expressway, or the Wangan. Exclusively for Assetto Corsa.")
            ui.text("")
            ui.textWrapped("Official Discord server:")
            ui.sameLine()
            ui_hyperlink("https://discord.gg/shutokorevivalproject")

            ui.text("")
            ui.pushFont(ui.Font.Title)
            ui.textWrapped("Support Shutoko Revival Project development")
            ui.popFont()
            ui.textWrapped("Patreon:")
            ui.sameLine()
            ui_hyperlink("https://www.patreon.com/Shutoko_Revival_Project")
        end
    end)
end

local function tab_License()
    ui.childWindow("license", ui.availableSpace(), function ()
        ui.textWrapped(license)
    end)
end

local function ui_configObject(name, obj)
    if obj == nil then return end

    for i, value in ipairs(obj.Properties) do
        if value.Type == "object" then
            ui.treeNode(value.Name, nil, function () ui_configObject(name .. "." .. value.Name, value.Value) end)
        elseif value.Type == "list" then
            ui.treeNode(value.Name, nil, function ()
                for j, listItem in ipairs(value.Value) do
                    if value.EntryType == "object" then
                        ui.treeNode(j - 1, nil, function () ui_configObject(name .. "." .. value.Name .. "." .. j - 1, listItem) end)
                    else
                        ui.text(listItem)
                    end
                end
            end)
        elseif value.Type == "dict" then
            ui.treeNode(value.Name, nil, function ()
                for key, listItem in pairs(value.Value) do
                    if value.EntryType == "object" then
                        ui.treeNode(key, nil, function () ui_configObject(name .. "." .. value.Name .. "." .. key, listItem) end)
                    else
                        ui.treeNode(key, nil, function () ui.text(listItem) end)
                    end
                end
            end)
        else
            ui.beginGroup()
            if value.ReadOnly then ui.pushStyleColor(ui.StyleColor.Text, rgbm.new("#999")) end
            ui.textAligned(value.Name, nil, vec2(150, 0))
            if value.ReadOnly then ui.popStyleColor() end
            ui.endGroup()
            if value.Description ~= nil and ui.itemHovered() then
                ui.setTooltip(value.Description)
            end
            ui.sameLine()

            local id = name .. "." .. value.Name

            if value.Type == "System.Boolean" then
                if ui.checkbox("###" .. id, value.Value) and not value.ReadOnly then
                    value.Value = not value.Value
                end
            elseif value.Type == "enum" then
                ui.setNextItemWidth(ui.availableSpaceX() - 42)
                if ui.beginCombo("###" .. id, value.Value) then
                    for j, enumValue in ipairs(value.ValidValues) do
                        if ui.selectable(enumValue, enumValue == value.Value) then
                            value.Value = enumValue
                        end
                    end
                    ui.endCombo()
                end
            else
                local flags = ui.InputTextFlags.None
                if value.ReadOnly then
                    flags = ui.InputTextFlags.ReadOnly
                end
                ui.setNextItemWidth(ui.availableSpaceX() - 42)
                value.Value = ui.inputText("###" .. id, value.Value, flags)
            end
            ui.sameLine()

            if ui.button("###btn.".. id, vec2(30, 0), value.ReadOnly and ui.ButtonFlags.Disabled or 0) then
                ac.debug("lastId", id)
                ac.debug("lastValue", value.Value)
                setValue(id, value.Value)
            end
            ui.addIcon(ui.Icons.Save, vec2(16,16))
        end
    end
end

local function tab_Configuration()
    ui.textWrapped("This feature is experimental! Changed values will not persist after a server restart.")
    ui.childWindow("configuration", ui.availableSpace(), function ()
        ui_configObject("Root", configuration)
    end)
end

local function window_AssettoServer()
    ui.tabBar("main_tabBar", function ()
        ui.tabItem("About", tab_About)
        ui.tabItem("License", tab_License)
        if sim.isAdmin then
            if configuration == nil and not configurationLoading then
                configurationLoading = true
                getConfiguration()
            end
            ui.tabItem("Configuration", tab_Configuration)
        end
    end)
end

ui.registerOnlineExtra(ui.Icons.Info, "AssettoServer", function () return true end, window_AssettoServer, nil, ui.OnlineExtraFlags.Tool)

local resetCarControl = ac.ControlButton('__EXT_CMD_RESET', nil)
resetCarControl:onPressed(function() requestResetCarEvent({}) end)

luaReadyEvent({})
