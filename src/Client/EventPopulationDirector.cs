using System;
using System.Linq;
using EFT;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class LocalRaidDirector
    {
        private float nextPopulationSpawn;
        private int populationSequence;
        private void FillEventPopulation(Player[] players, double elapsed)
        {
            int native = Plugin.StartupCap > 0 ? Plugin.StartupCap : cap;
            int limit = Math.Max(1, Math.Min(Math.Min(200, native), cap));
            ActiveCap = limit;
            int target = Plugin.StartupTarget > 0 ? Math.Min(limit, Plugin.StartupTarget) : limit;
            bool replenishes = context.Contains(": overrun") || context.Contains(": outbreak");
            foreach (var pair in Requests.Where(pair => pair.Value.Wave == -4 && !pair.Value.Finished).ToArray())
            {
                var request = pair.Value;
                if (players.Count(player => request.Profiles.Contains(player.ProfileId)) >= request.Count || (request.Profiles
                    .Count > 0 && Time.unscaledTime - request.Submitted > 10))
                {
                    request.Finished = request.Confirmed = true;
                }
                else if (!request.Accepted && Time.unscaledTime - request.Submitted > 60)
                {
                    request.Finished = true;
                    pair.Key.ShallSpawn = false;
                }
            }

            int alive = players.Count(player => player.HealthController?.IsAlive == true && player.Profile.Info.Settings
                .Role != WildSpawnType.shooterBTR);
            int pending = Requests.Values.Where(request => request.Wave == -4 && !request.Finished).Sum(request => Math.Max(0,
                 request.Count - players.Count(player => request.Profiles.Contains(player.ProfileId))));
            Status = $"Population: alive={alive}, pending={pending}, target={target}, cap={limit}; replenishment={replenishes}";
            if (elapsed < 20 || (!replenishes && elapsed > 100) || alive + pending >= target || Time
                .unscaledTime < nextPopulationSpawn || Scenario == null || Zones.Length == 0)
            {
                return;
            }

            if (context.Contains(": overrun") && elapsed > 100 && alive + pending >= Math.Max(1, target / 2))
            {
                return;
            }

            nextPopulationSpawn = Time.unscaledTime + Math.Max(1, Math.Min(30, interval));
            int count = Math.Min(size > 0 ? Math.Min(8, size) : 4, target - alive - pending);
            string role = context.Contains(": guarded") ? RaidRoulette.Shared.GuardedPolicy
                .Roles[populationSequence++ % RaidRoulette.Shared.GuardedPolicy.Roles.Length] : context.Contains(": outbreak")
                ? "infectedAssault" : context.Contains(": raider_assault") ? "pmcBot" : context.Contains(": scav_concentrate")
                ? "assault" : populationSequence++ % 2 == 0 ? "pmcUSEC" : "pmcBEAR";
            if (context.Contains(": overrun") && populationSequence % 3 == 0)
            {
                role = "assault";
            }

            var zones = Zones.Where(zone => zone.SpawnPoints?.Any(point => Vector3.Distance(point.Position, Plugin.Local!
                .Position) >= 30 && (!role.StartsWith("pmc", StringComparison.OrdinalIgnoreCase) || role == "pmcBot" || EFT.Game
                .Spawning.SpawnCategoryExtension.ContainBotPmcCategory(point.Categories))) == true).ToArray();
            if (zones.Length == 0)
            {
                return;
            }

            var zone = zones[UnityEngine.Random.Range(0, zones.Length)];
            var spawn = new BossLocationSpawn
            {
                BossName = role,
                BossEscortType = role,
                BossEscortAmount = (count - 1).ToString(),
                BossChance = 100,
                BossZone = zone.name,
                BossDifficult = "normal",
                BossEscortDifficult = "normal",
                Time = 0,
                ForceSpawn = true,
                IgnoreMaxBots = true,
                Supports = Array.Empty<WildSpawnSupports>(),
                TriggerId = "",
                TriggerName = ""
            };
            spawn.Init();
            var requestNew = new Reinforcement
            {
                Wave = -4,
                Role = role,
                Count = count,
                Submitted = Time.unscaledTime,
                Spawn = spawn
            };
            Requests[spawn] = requestNew;
            try
            {
                Scenario.ActivateWave(spawn);
                Plugin.Trace($"[RR-POPULATION] submitted role={role} group={count} alive={alive} pending={pending} cap={limit}");
            }
            catch (Exception error)
            {
                requestNew.Finished = true;
                spawn.ShallSpawn = false;
                Plugin.Trace("[RR-POPULATION] failed=" + error.GetBaseException().Message);
            }
        }
    }
}
