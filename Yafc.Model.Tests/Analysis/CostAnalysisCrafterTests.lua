-- Crafters with very high energy or size, without emissions, or powered by heat.
local function crafter(name, category, energy_usage, energy_source, size)
  local half = (size or 3) / 2
  return {
    type = "assembling-machine",
    name = name,
    crafting_speed = 1,
    crafting_categories = { category },
    energy_source = energy_source,
    energy_usage = energy_usage,
    selection_box = { { -half, -half }, { half, half } },
  }
end

local function burner(emissions)
  return { type = "burner", fuel_categories = { "chemical" }, emissions_per_minute = emissions }
end

local function free_recipe(name, category)
  return {
    type = "recipe",
    name = name,
    category = category,
    energy_required = 1,
    ingredients = {},
    results = { { type = "item", name = name, amount = 1 } },
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
                -- Placing the crafters on the map makes them accessible without items.
                ["gatherer"] = {},
                ["modest-furnace"] = {},
                ["hungry-furnace"] = {},
                ["huge-assembler"] = {},
                ["clean-assembler"] = {},
                ["dirty-furnace"] = {},
                ["heat-source"] = {},
                ["heated-assembler"] = {},
              },
            },
          },
        },
      },
    },
    item = {
      wood = { type = "item", name = "wood", fuel_category = "chemical", fuel_value = "2MJ" },
      modest = { type = "item", name = "modest" },
      hungry = { type = "item", name = "hungry" },
      huge = { type = "item", name = "huge" },
      clean = { type = "item", name = "clean" },
      dirty = { type = "item", name = "dirty" },
      heated = { type = "item", name = "heated" },
    },
    recipe = {
      wood = free_recipe("wood", "crafting"),
      modest = free_recipe("modest", "modest"),
      hungry = free_recipe("hungry", "hungry"),
      huge = free_recipe("huge", "huge"),
      clean = free_recipe("clean", "clean"),
      dirty = free_recipe("dirty", "dirty"),
      heated = free_recipe("heated", "heated"),
    },
    ["assembling-machine"] = {
      gatherer = crafter("gatherer", "crafting", "1kW", { type = "void" }),
      -- 500 MJ and 2000 MJ per craft, on both sides of the 1000 MJ that a recipe with a heat-powered crafter is charged.
      ["modest-furnace"] = crafter("modest-furnace", "modest", "500MW", burner()),
      ["hungry-furnace"] = crafter("hungry-furnace", "hungry", "2000MW", burner()),
      -- 20 tiles wide, above the size of 15 that a recipe with a heat-powered crafter is charged.
      ["huge-assembler"] = crafter("huge-assembler", "huge", "1kW", { type = "void" }, 20),
      -- Without emissions, and with 5 pollution per minute (a number, since the tests read the data as Factorio 1.1).
      ["clean-assembler"] = crafter("clean-assembler", "clean", "1kW", { type = "void" }),
      ["dirty-furnace"] = crafter("dirty-furnace", "dirty", "1MW", burner(5)),
      -- Its energy and size are small, so that the test can tell them from the values the recipe is charged instead.
      ["heated-assembler"] = crafter("heated-assembler", "heated", "1kW", { type = "heat", max_temperature = 500 }),
    },
    reactor = {
      -- Makes heat accessible, which the heat-powered crafter needs.
      ["heat-source"] = {
        type = "reactor",
        name = "heat-source",
        consumption = "1MW",
        energy_source = { type = "void" },
        heat_buffer = { max_temperature = 500 },
        selection_box = { { -1.5, -1.5 }, { 1.5, 1.5 } },
      },
    },
  },
}
defines.prototypes = {
  entity = {
    ["assembling-machine"] = 0,
    reactor = 0,
  },
  item = {
    item = 0,
  },
  ["space-location"] = {
    planet = 0,
  },
}
