-- Resources that a mod places on surfaces it creates at runtime, like Space Exploration does. Yafc doesn't know where these spawn.
local function resource(name)
  return {
    type = "resource",
    name = name,
    minable = { result = name },
  }
end

data = {
  raw = {
    planet = {
      nauvis = {
        name = "nauvis",
        type = "planet",
        map_gen_settings = {
          autoplace_settings = {
            entity = {
              settings = {
                ["nauvis-ore"] = {},
                -- Placing the drill on the map makes it accessible without an item.
                ["test-mining-drill"] = {},
              },
            },
          },
        },
      },
    },
    item = {
      ["nauvis-ore"] = { type = "item", name = "nauvis-ore" },
      ["script-ore"] = { type = "item", name = "script-ore" },
      ["unreachable-ore"] = { type = "item", name = "unreachable-ore" },
    },
    resource = {
      ["nauvis-ore"] = resource("nauvis-ore"),
      -- No spawn location, but marked as available through data.script_enabled.
      ["script-ore"] = resource("script-ore"),
      -- No spawn location and nothing else that makes it available.
      ["unreachable-ore"] = resource("unreachable-ore"),
    },
    ["mining-drill"] = {
      ["test-mining-drill"] = {
        type = "mining-drill",
        name = "test-mining-drill",
        energy_source = { type = "void" },
        resource_categories = { "basic-solid" },
      },
    },
  },
  script_enabled = {
    { type = "entity", name = "script-ore" },
  },
}

defines.prototypes = {
  entity = {
    resource = 0,
    ["mining-drill"] = 0,
  },
  item = {
    item = 0,
  },
  ["space-location"] = {
    planet = 0,
  },
}
