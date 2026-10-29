namespace RaidRoulette.Configuration;

public sealed class EventToggle
{
    public bool Enabled { get; set; } = true;
    public int Weight { get; set; } = 15;
}

public sealed class V2Settings
{
    public int MaxAliveBots { get; set; } = 60;
    public int MaxScheduledBots { get; set; } = 180;
    public int ConcentratedScavs { get; set; } = 20;
    public double PmcSweepMultiplier { get; set; } = 1.5;
    public double PopulationMultiplier { get; set; } = 3;
    public double SniperMultiplier { get; set; } = 5;
    public int ExplosiveScavChancePercent { get; set; } = 50;
    public int MinimumMagazineCapacity { get; set; } = 100;
    public int GearedBodyArmorClass { get; set; } = 6;
    public string[] GearedWeaponNameTokens { get; set; } = ["m4a1", "hk416", "spear", "sr25", "rsass", "mdr"];
    // "shared" makes one clothing set for everybody; "faction" makes one per faction.
    public string MatrixClothingMode { get; set; } = "faction";
    public Dictionary<string, EventToggle> Events { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lab_work_day"] = new() { Weight = 0 }, ["gun_game"] = new() { Weight = 0 }, ["stormtide"] = new() { Weight = 0 }, ["ak50"] = new() { Weight = 0 }, ["grenadier"] = new() { Weight = 0 }, ["schizophrenia"] = new() { Weight = 0 }, ["time_warp"] = new() { Weight = 0 }, ["smokers_lobby"] = new() { Weight = 0 }, ["the_gift"] = new() { Weight = 0 }, ["clone_wars"] = new() { Weight = 0 }, ["overrun"] = new() { Weight = 0 }, ["scav_concentrate"] = new(), ["pmc_sweep"] = new(),
        ["get_handy"] = new(), ["i_have_a_shot"] = new(), ["marksman"] = new(),
        ["extreme_overwatch"] = new(), ["matrix_glitch"] = new(),
        ["no_safe_space"] = new(), ["posers_everywhere"] = new(), ["fake_cult"] = new(),
        // Opt in after individually testing these on your installed AI/spawn mods.
        ["explosive_scav_units"] = new() { Enabled = false },
        ["outbreak"] = new() { Enabled = false },
        ["geared"] = new() { Enabled = false },
        ["overloaded"] = new() { Enabled = false }
    };
}




