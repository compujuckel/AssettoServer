local baseRes = vec2(2560, 1440) -- Reference resolution
local currentRes = vec2(sim.windowWidth, sim.windowHeight)
local scaleFactor = math.min(currentRes.x / baseRes.x, currentRes.y / baseRes.y, 1)

local scaling = {}

function scaling.vec2(x, y)
  return vec2(x, y) * scaleFactor
end

function scaling.size(size)
  return size * scaleFactor
end

function scaling.get()
  return scaleFactor
end

return scaling