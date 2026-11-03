using System;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx.Configuration;

namespace RaidRoulette.TestingClient
{
    public static class RaidChoiceNames
    {
        // Keep persisted backend IDs stable when a player-facing event name changes.
        public static string EventId(string choice)
        {
            return choice.ToLowerInvariant() switch
            {
                "snowball_trials" => "snowball",
                "locked_loot" => "high_value_loot",
                "boss_run_down" => "boss_hunt",
                "the_thief" => "the_gift",
                _ => choice.ToLowerInvariant()};
        }

        public static bool Migrate(ConfigFile config)
        {
            string path = config.ConfigFilePath;
            if (!File.Exists(path))
            {
                return false;
            }

            string original = File.ReadAllText(path);
            string updated = Regex.Replace(original, @"(?m)^(Event\s*=\s*)Snowball(\s*)$", "${1}Snowball_Trials${2}");
            updated = Regex.Replace(updated, @"(?m)^Snowball \(%\)(\s*=)", "Snowball Trials (%)${1}");
            updated = Regex.Replace(updated, @"(?m)^(Event\s*=\s*)Boss_Hunt(\s*)$", "${1}Boss_Run_Down${2}");
            updated = Regex.Replace(updated, @"(?m)^Boss Hunt \(%\)(\s*=)", "Boss Run Down (%)${1}");
            updated = Regex.Replace(updated, @"(?m)^(Event\s*=\s*)The_Gift(\s*)$", "${1}The_Thief${2}");
            updated = Regex.Replace(updated, @"(?m)^The Gift \(%\)(\s*=)", "The Thief (%)${1}");
            updated = Regex.Replace(updated, @"(?m)^(Event\s*=\s*)High_Value_Loot(\s*)$", "${1}Locked_Loot${2}");
            updated = Regex.Replace(updated, @"(?m)^High[- ]Value Loot \(%\)(\s*=)", "Locked Loot (%)${1}");
            if (updated == original)
            {
                return false;
            }

            File.WriteAllText(path, updated);
            config.Reload();
            return true;
        }
    }
}
