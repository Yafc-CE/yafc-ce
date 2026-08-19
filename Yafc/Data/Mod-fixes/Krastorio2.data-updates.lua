-- First biomass comes from the player hand-mining spawner corpses.
--
-- With only K2 enabled, this is enabled/reachable automatically.
-- With Space Exploration also installed, YAFC fails to autodetect this.
--
-- For simplicity's sake, we'll always enable it to be safe, as it's a core K2 mechanic.
data.script_enabled:insert{
    type = "item",
    name = "kr-biomass"
}

if not mods["space-exploration"] then
    -- Unlock the initial science lab
    data.script_enabled:insert{
        type = "entity",
        name = "kr-spaceship-research-computer"
    }
end

return ...
