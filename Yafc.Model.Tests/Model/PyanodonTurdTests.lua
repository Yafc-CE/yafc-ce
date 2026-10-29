-- Pyanodon-like farms: buildings that only accept their own module category, a farm module, and TURD modules with the
-- "turd-select-<subtech>" technologies that pypostprocessing creates for Yafc.
local farm_effects = { "consumption", "speed", "pollution", "productivity" }

local function farm(name, category, module_categories)
  return {
    type = "assembling-machine",
    name = name,
    crafting_speed = 0.1,
    crafting_categories = { category },
    energy_source = {
      type = "burner",
      fuel_categories = { "chemical" },
    },
    energy_usage = "1MW",
    module_slots = 4,
    allowed_module_categories = module_categories,
    allowed_effects = farm_effects,
  }
end

local function free_recipe(name, category, result, time)
  return {
    type = "recipe",
    name = name,
    category = category,
    energy_required = time,
    ingredients = {},
    results = { { type = "item", name = result, amount = 1 } },
  }
end

local function turd_selection(subtech)
  return {
    type = "technology",
    name = "turd-select-" .. subtech,
    enabled = false,
    unit = { count = 1, ingredients = {}, time = 60 },
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
                -- Placing the buildings on the map makes them accessible without items.
                ["gatherer"] = {},
                ["moondrop-greenhouse-mk01"] = {},
                ["moondrop-greenhouse-mk02"] = {},
                ["guar-gum-plantation"] = {},
                ["turd-only-farm-mk01"] = {},
                ["reproductive-complex"] = {},
              },
            },
          },
        },
      },
    },
    item = {
      wood = { type = "item", name = "wood", fuel_category = "chemical", fuel_value = "2MJ" },
      moondrop = { type = "item", name = "moondrop" },
      guar = { type = "item", name = "guar" },
      ["turd-only-crop"] = { type = "item", name = "turd-only-crop" },
    },
    module = {
      -- The farm module of the moondrop greenhouses.
      ["moondrop-mk01"] = { type = "module", name = "moondrop-mk01", category = "moondrop", tier = 1, effect = { speed = 1 } },
      -- TURD modules, sharing the category of the farm modules. Tiered TURD modules have one variant per building tier.
      ["carbon-capture-module-mk01"] = {
        type = "module", name = "carbon-capture-module-mk01", category = "moondrop", tier = 1, effect = { speed = 0.5, consumption = -0.5 },
      },
      ["carbon-capture-module-mk02"] = {
        type = "module", name = "carbon-capture-module-mk02", category = "moondrop", tier = 2, effect = { speed = 1, consumption = -0.5 },
      },
      ["hh-module-mk01"] = { type = "module", name = "hh-module-mk01", category = "guar", tier = 1, effect = { speed = 0.5 } },
      ["turd-only-upgrade-module"] = {
        type = "module", name = "turd-only-upgrade-module", category = "turd-only", tier = 1, effect = { speed = 10 },
      },
      -- Named like a TURD module, but without a turd-select technology.
      ["lonely-module"] = { type = "module", name = "lonely-module", category = "lonely", tier = 1, effect = { speed = 1 } },
    },
    technology = {
      ["turd-select-carbon-capture"] = turd_selection("carbon-capture"),
      ["turd-select-hh"] = turd_selection("hh"),
      ["turd-select-turd-only-upgrade"] = turd_selection("turd-only-upgrade"),
    },
    recipe = {
      wood = free_recipe("wood", "crafting", "wood", 1),
      ["moondrop-mk01"] = free_recipe("moondrop-mk01", "crafting", "moondrop-mk01", 1),
      -- Craftable, as pypostprocessing intends for Yafc once pybugreports#1632 is fixed. carbon-capture-module-mk02 stays uncraftable, as today.
      ["carbon-capture-module-mk01"] = free_recipe("carbon-capture-module-mk01", "crafting", "carbon-capture-module-mk01", 1),
      ["turd-only-upgrade-module"] = free_recipe("turd-only-upgrade-module", "crafting", "turd-only-upgrade-module", 1),
      moondrop = free_recipe("moondrop", "moondrop-farm", "moondrop", 60),
      guar = free_recipe("guar", "guar-farm", "guar", 60),
      ["turd-only-crop"] = free_recipe("turd-only-crop", "turd-only-farm", "turd-only-crop", 60),
    },
    ["assembling-machine"] = {
      -- Crafts the farm and TURD modules.
      gatherer = {
        type = "assembling-machine",
        name = "gatherer",
        crafting_speed = 1,
        crafting_categories = { "crafting" },
        energy_source = { type = "void" },
        energy_usage = "1kW",
      },
      -- Farms with a farm module (moondrop-mk01) and a tiered TURD upgrade (carbon-capture).
      ["moondrop-greenhouse-mk01"] = farm("moondrop-greenhouse-mk01", "moondrop-farm", { "moondrop" }),
      ["moondrop-greenhouse-mk02"] = farm("moondrop-greenhouse-mk02", "moondrop-farm", { "moondrop" }),
      -- A tier-1 building without the -mk01 suffix, like Pyanodon's guar gum plantation.
      ["guar-gum-plantation"] = farm("guar-gum-plantation", "guar-farm", { "guar" }),
      -- A farm whose only module is a TURD module, so it has no default module.
      ["turd-only-farm-mk01"] = farm("turd-only-farm-mk01", "turd-only-farm", { "turd-only" }),
      -- Accepts the modules of several farms, like Pyanodon's Reproductive Complex, so it is not the target of their TURD upgrades.
      ["reproductive-complex"] = farm("reproductive-complex", "moondrop-farm", { "moondrop", "turd-only" }),
    },
  },
}
defines.prototypes = {
  entity = {
    ["assembling-machine"] = 0,
  },
  item = {
    item = 0,
    module = 0,
  },
  ["space-location"] = {
    planet = 0,
  },
}
