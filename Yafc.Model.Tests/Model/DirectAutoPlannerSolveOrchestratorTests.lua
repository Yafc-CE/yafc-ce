data = {
  raw = {
    item = {
      raw = {
        type = "item",
        name = "raw",
      },
      intermediate = {
        type = "item",
        name = "intermediate",
      },
      product = {
        type = "item",
        name = "product",
      },
    },
    recipe = {
      ["make-intermediate"] = {
        type = "recipe",
        name = "make-intermediate",
        ingredients = {
          { type = "item", name = "raw", amount = 2 },
        },
        energy_required = 1,
        results = {
          { type = "item", name = "intermediate", amount = 1 },
        },
      },
      ["make-product"] = {
        type = "recipe",
        name = "make-product",
        ingredients = {
          { type = "item", name = "intermediate", amount = 1 },
        },
        energy_required = 1,
        results = {
          { type = "item", name = "product", amount = 1 },
        },
      },
    },
  },
}
defines.prototypes = {
  entity = {},
  item = {
    item = 0,
  },
}
