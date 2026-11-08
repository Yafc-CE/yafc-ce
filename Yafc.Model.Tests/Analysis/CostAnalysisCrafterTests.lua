-- Crafters with very high energy or size, without emissions, or powered by heat, recipes with several crafters, crafters
-- designed around modules, and mining drills.
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

local function with_speed(entity, speed)
  entity.crafting_speed = speed
  return entity
end

-- Like Pyanodon's farm buildings: one module slot and -100% speed, so the crafter only works with its module.
local function needs_modules(entity, module_categories)
  entity.module_slots = 1
  entity.allowed_module_categories = module_categories
  entity.allowed_effects = { "speed" }
  entity.effect_receiver = { base_effect = { speed = -1 } }
  return entity
end

local function module(name, category, speed)
  return { type = "module", name = name, category = category, effect = { speed = speed } }
end

local function drill(name, mining_speed, size)
  local half = size / 2
  return {
    type = "mining-drill",
    name = name,
    mining_speed = mining_speed,
    energy_usage = "1kW",
    energy_source = { type = "void" },
    resource_categories = { "basic-solid" },
    vector_to_place_result = { 0, -half - 0.35 },
    selection_box = { { -half, -half }, { half, half } },
  }
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
                ["a-slow-assembler"] = {},
                ["b-fast-assembler"] = {},
                farm = {},
                complex = {},
                stone = {},
                ["a-small-drill"] = {},
                ["b-big-drill"] = {},
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
      speedy = { type = "item", name = "speedy" },
      ["locked-speeds"] = { type = "item", name = "locked-speeds" },
      ["c-slow-locked-assembler"] = { type = "item", name = "c-slow-locked-assembler", place_result = "c-slow-locked-assembler" },
      ["d-fast-locked-assembler"] = { type = "item", name = "d-fast-locked-assembler", place_result = "d-fast-locked-assembler" },
      farmed = { type = "item", name = "farmed" },
      complexed = { type = "item", name = "complexed" },
      stone = { type = "item", name = "stone" },
    },
    module = {
      -- Both farm modules need the pack, so neither is unlocked with the current milestones.
      ["slow-farm-module"] = module("slow-farm-module", "farm", 1),
      ["fast-farm-module"] = module("fast-farm-module", "farm", 2),
      -- Unlocked, but faster than the farm modules.
      ["other-module"] = module("other-module", "other", 3),
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
      speedy = free_recipe("speedy", "speedy"),
      ["locked-speeds"] = free_recipe("locked-speeds", "locked-speeds"),
      ["c-slow-locked-assembler"] = locked_recipe("c-slow-locked-assembler", "crafting"),
      ["d-fast-locked-assembler"] = locked_recipe("d-fast-locked-assembler", "crafting"),
      farmed = free_recipe("farmed", "farmed"),
      complexed = free_recipe("complexed", "complexed"),
      ["slow-farm-module"] = locked_recipe("slow-farm-module", "crafting"),
      ["fast-farm-module"] = locked_recipe("fast-farm-module", "crafting"),
      ["other-module"] = free_recipe("other-module", "crafting"),
    },
    technology = {
      -- Researched with the pack, so these can be built, but not with the current milestones.
      ["advanced-assembling"] = {
        type = "technology",
        name = "advanced-assembling",
        unit = { count = 1, time = 1, ingredients = { { "pack", 1 } } },
        effects = {
          { type = "unlock-recipe", recipe = "a-milestone-assembler" },
          { type = "unlock-recipe", recipe = "c-slow-locked-assembler" },
          { type = "unlock-recipe", recipe = "d-fast-locked-assembler" },
          { type = "unlock-recipe", recipe = "slow-farm-module" },
          { type = "unlock-recipe", recipe = "fast-farm-module" },
        },
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
      -- The slow assembler is 3 wide, the fast one 5 wide and twice as fast.
      ["a-slow-assembler"] = crafter("a-slow-assembler", "speedy", "1kW", { type = "void" }),
      ["b-fast-assembler"] = with_speed(crafter("b-fast-assembler", "speedy", "1kW", { type = "void" }, 5), 2),
      -- Both need the pack. The slow one is 5 wide, the fast one 3 wide and twice as fast.
      ["c-slow-locked-assembler"] = crafter("c-slow-locked-assembler", "locked-speeds", "1kW", { type = "void" }, 5),
      ["d-fast-locked-assembler"] = with_speed(crafter("d-fast-locked-assembler", "locked-speeds", "1kW", { type = "void" }), 2),
      farm = needs_modules(crafter("farm", "farmed", "1kW", { type = "void" }), { "farm" }),
      -- Like Pyanodon's Reproductive Complex: accepts several module categories, so it has no default module.
      complex = needs_modules(crafter("complex", "complexed", "1kW", { type = "void" }), { "farm", "other" }),
    },
    resource = {
      stone = {
        type = "resource",
        name = "stone",
        minable = { mining_time = 1, result = "stone" },
        -- A density of 0 can't be estimated, so mining stone gets no extra penalty.
        autoplace = { probability_expression = 0 },
      },
    },
    ["mining-drill"] = {
      -- The small drill is 2 wide and mines at half speed. The big drill is 5 wide and 4 times as fast.
      ["a-small-drill"] = drill("a-small-drill", 0.5, 2),
      ["b-big-drill"] = drill("b-big-drill", 2, 5),
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
    resource = 0,
    ["mining-drill"] = 0,
  },
  item = {
    item = 0,
    module = 0,
  },
  ["space-location"] = {
    planet = 0,
  },
}
