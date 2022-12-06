local license = [[
Copyright (C)  2022 Niewiarowski, compujuckel

This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.


Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with the Steamworks SDK by Valve Corporation, containing parts covered by the terms of the Steamworks SDK License, the licensors of this Program grant you additional permission to convey the resulting work.

Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with plugins published on https://www.patreon.com/assettoserver, the licensors of this Program grant you additional permission to convey the resulting work. 
]]

local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/api/configuration"
local configuration = {}
local authHeaders = {}

local function getConfiguration()
    web.get(baseUrl, authHeaders, function (err, response)
        configuration = stringify.parse(response.body)
    end)
end

local function setValue(key, value)
    web.post(baseUrl .. "?key=" .. key .."&value=" .. tostring(value), authHeaders, function (err, response)
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
    ac.debug("sender", sender)
    ac.debug("key", message.key)
    authHeaders["X-Car-Id"] = car.sessionID
    authHeaders["X-Api-Key"] = message.key

    getConfiguration()
end)

apiKeyEvent({ key = "" })

local logoSize = vec2(128, 79)
local buttonSize = vec2(130, 0)

local function tab_About()
    ui.offsetCursorY(30)
    ui.offsetCursorX((ui.availableSpaceX() - logoSize.x) / 2)
    ui.image("https://i.imgur.com/qPNhE24.png", logoSize)
    ui.pushFont(ui.Font.Title)
    ui.textAligned("AssettoServer", vec2(0.5,0), vec2(ui.availableSpaceX(), 0))
    ui.popFont()
    ui.textAligned("Custom Assetto Corsa server with focus on freeroam", vec2(0.5,0), vec2(ui.availableSpaceX(), 0))
    ui.newLine()
    ui.offsetCursorX((ui.availableSpaceX() - buttonSize.x * 2) / 2)
    if ui.button("Website", vec2(130,0)) then
        os.openURL("https://assettoserver.org/")
    end
    ui.sameLine()
    ui.pushStyleColor(ui.StyleColor.Button, rgbm.new("#FF424D"))
    if ui.button("Become a Patron", vec2(130,0)) then
        os.openURL("https://www.patreon.com/assettoserver")
    end
    ui.popStyleColor()
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
            if value.Description ~= "" and ui.itemHovered() then
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
    ui.childWindow("configuration", ui.availableSpace(), function ()
        ui_configObject("Root", configuration)
    end)
end

local function window_AssettoServer()
    ui.tabBar("main_tabBar", function ()
        ui.tabItem("About", tab_About)
        ui.tabItem("License", tab_License)
        if sim.isAdmin then
            ui.tabItem("Configuration", tab_Configuration)
        end
    end)
end

ui.registerOnlineExtra(ui.Icons.Info, "AssettoServer", function () return true end, window_AssettoServer, nil, ui.OnlineExtraFlags.Tool)
