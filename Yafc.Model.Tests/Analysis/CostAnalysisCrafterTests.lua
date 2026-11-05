-- Crafters with very high energy or size, without emissions, or powered by heat, and recipes with several crafters.
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

local function sized_burner(name, category, energy_usage, fuel_category, size)
  return crafter(name, category, energy_usage, { type = "burner", fuel_categories = { fuel_category } }, size)
end

local function placed_by(entity, item)
  entity.placeable_by = { item = item, count = 1 }
  return entity
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

local function locked_recipe(name, category)
  local recipe = free_recipe(name, category)
  recipe.enabled = false
  return recipe
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
                ["a-wood-furnace"] = {},
                ["b-peat-furnace"] = {},
                ["c-small-assembler"] = {},
                ["hand-press"] = {},
                ["b-unlocked-assembler"] = {},
                ["b-basic-assembler"] = {},
                lab = {},
                ["flexible-furnace"] = {},
              },
            },
          },
        },
      },
    },
    item = {
      wood = { type = "item", name = "wood", fuel_category = "chemical", fuel_value = "2MJ" },
      peat = { type = "item", name = "peat", fuel_category = "peat", fuel_value = "2MJ" },
      mixed = { type = "item", name = "mixed" },
      pressed = { type = "item", name = "pressed" },
      ["locked-or-not"] = { type = "item", name = "locked-or-not" },
      varied = { type = "item", name = "varied" },
      ["milestone-or-not"] = { type = "item", name = "milestone-or-not" },
      -- A science pack, so it's one of the default milestones, which the test project leaves locked.
      pack = { type = "item", name = "pack" },
      ["a-milestone-assembler"] = { type = "item", name = "a-milestone-assembler", place_result = "a-milestone-assembler" },
      -- Places the base assembler. Its variant is placed with this item too.
      ["base-assembler"] = { type = "item", name = "base-assembler", place_result = "base-assembler" },
      flexible = { type = "item", name = "flexible" },
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
      peat = free_recipe("peat", "crafting"),
      ["base-assembler"] = free_recipe("base-assembler", "crafting"),
      mixed = free_recipe("mixed", "mixed"),
      pressed = free_recipe("pressed", "pressed"),
      ["locked-or-not"] = free_recipe("locked-or-not", "locked-or-not"),
      varied = free_recipe("varied", "varied"),
      ["milestone-or-not"] = free_recipe("milestone-or-not", "milestone-or-not"),
      pack = free_recipe("pack", "crafting"),
      ["a-milestone-assembler"] = locked_recipe("a-milestone-assembler", "crafting"),
      flexible = free_recipe("flexible", "flexible"),
    },
    technology = {
      -- Researched with the pack, so the milestone assembler can be built, but not with the current milestones.
      ["advanced-assembling"] = {
        type = "technology",
        name = "advanced-assembling",
        unit = { count = 1, time = 1, ingredients = { { "pack", 1 } } },
        effects = { { type = "unlock-recipe", recipe = "a-milestone-assembler" } },
      },
    },
    ["assembling-machine"] = {
      gatherer = crafter("gatherer", "crafting", "1kW", { type = "void" }),
      -- 500 MJ and 2000 MJ per craft.
      ["modest-furnace"] = crafter("modest-furnace", "modest", "500MW", burner()),
      ["hungry-furnace"] = crafter("hungry-furnace", "hungry", "2000MW", burner()),
      -- 20 tiles wide.
      ["huge-assembler"] = crafter("huge-assembler", "huge", "1kW", { type = "void" }, 20),
      -- Without emissions, and with 5 pollution per minute (a number, since the tests read the data as Factorio 1.1).
      ["clean-assembler"] = crafter("clean-assembler", "clean", "1kW", { type = "void" }),
      ["dirty-furnace"] = crafter("dirty-furnace", "dirty", "1MW", burner(5)),
      ["heated-assembler"] = crafter("heated-assembler", "heated", "1kW", { type = "heat", max_temperature = 500 }),
      -- Crafters are sorted by name. The first two burn different fuels, which used to end the loop before the third one.
      ["a-wood-furnace"] = sized_burner("a-wood-furnace", "mixed", "500MW", "chemical", 5),
      ["b-peat-furnace"] = sized_burner("b-peat-furnace", "mixed", "500MW", "peat", 5),
      ["c-small-assembler"] = crafter("c-small-assembler", "mixed", "1kW", { type = "void" }),
      -- Shares a crafting category with the character, which comes first by name.
      ["hand-press"] = sized_burner("hand-press", "pressed", "500MW", "chemical", 5),
      -- Not placed on the map and without an item, so it's never accessible.
      ["a-locked-assembler"] = crafter("a-locked-assembler", "locked-or-not", "1kW", { type = "void" }, 1),
      ["b-unlocked-assembler"] = crafter("b-unlocked-assembler", "locked-or-not", "1kW", { type = "void" }),
      -- Like a Pyanodon TURD variant: placed with the base assembler's item, so it's accessible as soon as that item is.
      ["base-assembler"] = crafter("base-assembler", "varied", "1kW", { type = "void" }),
      -- The milestone assembler is 1 wide and needs the pack. The basic assembler is 3 wide and is unlocked.
      ["a-milestone-assembler"] = crafter("a-milestone-assembler", "milestone-or-not", "1kW", { type = "void" }, 1),
      ["b-basic-assembler"] = crafter("b-basic-assembler", "milestone-or-not", "1kW", { type = "void" }),
      ["a-variant-assembler"] = placed_by(crafter("a-variant-assembler", "varied", "1kW", { type = "void" }, 1), "base-assembler"),
      -- Burns wood or peat.
      ["flexible-furnace"] = crafter("flexible-furnace", "flexible", "500MW", { type = "burner", fuel_categories = { "chemical", "peat" } }),
    },
    lab = {
      lab = {
        type = "lab",
        name = "lab",
        inputs = { "pack" },
        energy_source = { type = "void" },
        energy_usage = "1kW",
        selection_box = { { -1.5, -1.5 }, { 1.5, 1.5 } },
      },
    },
    character = {
      character = {
        type = "character",
        name = "character",
        crafting_categories = { "pressed" },
      },
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
    character = 0,
    lab = 0,
  },
  item = {
    item = 0,
  },
  ["space-location"] = {
    planet = 0,
  },
}
