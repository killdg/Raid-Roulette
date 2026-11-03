using System;
using System.Linq;

namespace RaidRoulette.Shared
{
    public static class GauntletPolicy
    {
        // SPT 4.1.6 Snowball achievement 6529097eccf6aa5f8737b3d0.
        // followerTagilla is an alternative to bossTagilla, not another target.
        public static readonly string[] SnowballRoles =
        {
            "bossBully",
            "bossKojaniy",
            "bossGluhar",
            "bossKilla",
            "bossSanitar",
            "bossTagilla",
            "bossBoar",
            "bossKolontay",
            "bossPartisan",
            "bossKnight",
            "followerBigPipe",
            "followerBirdEye",
            "bossZryachiy"
        };
        // Combat archetypes, not peaceful actors, vehicle crew, named bosses,
        // or duplicate event skins. Guards remain separate waves with native kits.
        public static readonly string[] TrialRoles =
        {
            "assault",
            "cursedAssault",
            "marksman",
            "pmcUSEC",
            "pmcBEAR",
            "pmcBot",
            "exUsec",
            "followerBully",
            "followerKojaniy",
            "followerSanitar",
            "followerGluharScout",
            "followerGluharAssault",
            "followerGluharSecurity",
            "followerBoar",
            "followerBoarClose1",
            "followerBoarClose2",
            "followerKolontayAssault",
            "followerKolontaySecurity",
            "followerZryachiy",
            "sectantWarrior"
        };
        public static readonly string[] ExtremeRoles =
        {
            "bossBully",
            "bossSanitar",
            "bossBoar",
            "bossKolontay",
            "bossGluhar",
            "bossTagilla",
            "bossKojaniy",
            "bossKilla",
            "bossTagillaAgro",
            "bossKillaAgro"
        };
        public static readonly string[][] NormalStages =
        {
            new[]
            {
                "assault"
            },
            new[]
            {
                "cursedAssault"
            },
            new[]
            {
                "marksman"
            },
            new[]
            {
                "pmcUSEC",
                "pmcBEAR"
            },
            new[]
            {
                "pmcBot"
            },
            new[]
            {
                "exUsec"
            },
            new[]
            {
                "followerBully",
                "followerKojaniy",
                "followerSanitar"
            },
            new[]
            {
                "followerGluharScout",
                "followerGluharAssault",
                "followerGluharSecurity",
                "followerBoar",
                "followerBoarClose1",
                "followerBoarClose2",
                "followerKolontayAssault",
                "followerKolontaySecurity"
            },
            new[]
            {
                "followerZryachiy"
            },
            new[]
            {
                "sectantWarrior"
            }
        };
        public static readonly string[][] GuardStages = CreateGuardStages();
        private static string[][] CreateGuardStages()
        {
            string[][] teams =
            {
                new[]
                {
                    "followerBully"
                },
                new[]
                {
                    "followerKojaniy"
                },
                new[]
                {
                    "followerSanitar"
                },
                new[]
                {
                    "followerGluharScout",
                    "followerGluharAssault",
                    "followerGluharSecurity"
                },
                new[]
                {
                    "followerBoar",
                    "followerBoarClose1",
                    "followerBoarClose2"
                },
                new[]
                {
                    "followerKolontayAssault",
                    "followerKolontaySecurity"
                },
                new[]
                {
                    "followerZryachiy"
                }
            };
            // The finale reuses only native guard roles from the preceding teams.
            return teams.Concat(new[] { teams.SelectMany(team => team).Distinct().ToArray() }).ToArray();
        }

        public static bool IsRandomGuardFinale(string eventId, int wave)
        {
            return eventId == "guard_trials" && wave == GuardStages.Length - 1;
        }

        public static string Title(string eventId)
        {
            return eventId switch
            {
                "snowball" => "Snowball Trials",
                "guard_trials" => "Guard Trials",
                "extreme_trials" => "Extreme Trials",
                "total_trial" => "Total Trial",
                _ => "Trials"
            };
        }

        public static string[][] Stages(string eventId)
        {
            if (eventId == "trials")
            {
                return NormalStages;
            }

            if (eventId == "guard_trials")
            {
                return GuardStages;
            }

            string[] roles = eventId == "extreme_trials" ? ExtremeRoles : TrialRoles;
            return roles.Select(role => new[] { role }).ToArray();
        }

        public static bool UsesEquipmentProgression(string eventId)
        {
            return eventId == "trials" || eventId == "guard_trials" || eventId == "total_trial";
        }

        public static int StageTier(string eventId, int wave)
        {
            return Math.Max(0, Math.Min(4, (int)Math.Floor(4d * wave / Math.Max(1, Stages(eventId).Length - 1))));
        }

        public static int StageLevel(string eventId, int wave)
        {
            return 10 + (int)Math.Round(38d * Math.Max(0, wave) / Math.Max(1, Stages(eventId).Length - 1));
        }

        public static int FindStage(string eventId, string role)
        {
            return Array.FindIndex(Stages(eventId), roles => roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        public const int TrialSquadSize = 5;
        public static bool AllowActivation(bool exclusive, bool managed)
        {
            return !exclusive || managed;
        }

        public static bool Handles(string eventId)
        {
            return eventId == "snowball" || eventId == "trials" || eventId == "extreme_trials" || eventId == "guard_trials"
                || eventId == "total_trial";
        }

        public static int TrialLevel(int wave)
        {
            return Math.Min(60, 10 + Math.Max(0, wave) * 2);
        }

        public static int BitcoinReward(string eventId, int wave)
        {
            return eventId == "extreme_trials" ? 10 : UsesEquipmentProgression(eventId) && wave == Stages(eventId).Length - 1 ? 1 : 0;
        }

        public static int TrialWave(string role)
        {
            return Array.FindIndex(TrialRoles, candidate => candidate.Equals(role, StringComparison.OrdinalIgnoreCase));
        }

        public static int TrialTier(int wave)
        {
            return Math.Min(4, Math.Max(0, wave) / 4);
        }

        public static string TrialDifficulty(int wave)
        {
            return wave < 3 ? "normal" : wave < 10 ? "hard" : "impossible";
        }

        public static bool WaveComplete(int confirmed, int dead, int pending)
        {
            return confirmed >= TrialSquadSize && dead == confirmed && pending == 0;
        }

        // Edge adjacency counts; diagonals and separated inventory grids do not.
        public static bool Adjacent(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh)
        {
            bool verticalOverlap = ay < by + bh && by < ay + ah;
            bool horizontalOverlap = ax < bx + bw && bx < ax + aw;
            return ((ax + aw == bx || bx + bw == ax) && verticalOverlap) || ((ay + ah == by || by + bh == ay) && horizontalOverlap);
        }
    }
}
