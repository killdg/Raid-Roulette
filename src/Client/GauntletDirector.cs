using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Communications;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using RaidRoulette.Shared;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    // Raid-local state only. Native spawning owns profiles, gear and activation;
    // no inventory mutation or external requests are needed for the easter egg.
    internal sealed class GauntletDirector
    {
        private sealed class Actor
        {
            internal BotOwner Owner = null!;
            internal bool Dead;
            internal bool PlayerKill;
            internal string Role = "";
        }

        private readonly Dictionary<string, Actor> actors = new();
        private readonly List<LocalRaidDirector.Reinforcement> requests = new();
        private readonly Queue<string> roles = new();
        private readonly HashSet<LocalRaidDirector.Reinforcement> retried = new();
        private readonly HashSet<string> killaHelmets = new();
        private readonly HashSet<string> tagillaHelmets = new();
        private GameWorld? world;
        private string eventId = "";
        private float nextTick;
        private float nextSpawn;
        private int wave;
        private bool initialized;
        private bool bonus;
        private bool ended;
        private bool subscribed;
        private int sequence;
        private int spawnProbe;
        private FenceTrialReceipt? fenceReceipt;
        private string? rewardedActor;
        private int rewardRemaining;
        private float nextReward;
        internal static GauntletDirector? Current { get; private set; }
        internal string Status { get; private set; } = "";
        internal static bool Active => Current?.Running == true;

        internal static void KillWaveForTesting()
        {
            var director = Current;
            if (!AdvancedMenu.Enabled || director?.Running != true || !director.IsTrials)
            {
                Plugin.Trace("[RR-GAUNTLET-TEST] Kill wave requires advanced options and an active Trials mode.");
                return;
            }

            // Snapshot only this director's bound actors. Native health death events
            // must advance the wave; never mark actors dead or bypass pending spawns.
            // Snowball is excluded because it requires genuine player-attributed kills.
            foreach (var actor in director.actors.Values.ToArray())
            {
                if (!actor.Dead && actor.Owner.GetPlayer.HealthController is ActiveHealthController health)
                {
                    health.Kill(EDamageType.Undefined);
                }
            }

            Plugin.Trace("[RR-GAUNTLET-TEST] Requested native deaths for current bound wave; progression remains native-event driven.");
        }

        private bool IsTrials => GauntletPolicy.Handles(eventId) && eventId != "snowball";
        private string[][] WaveRoles => GauntletPolicy.Stages(eventId);
        private bool Running => !ended && GauntletPolicy.Handles(eventId) && Plugin.Local?.HealthController?.IsAlive == true;

        internal static bool IsActor(IPlayer? player)
        {
            return Current?.Running == true && player != null && Current.actors.ContainsKey(player.ProfileId);
        }

        internal static bool Allies(BotsGroup group, IPlayer target)
        {
            return Current?.Running == true && Current.actors.ContainsKey(target.ProfileId) && Current.actors.Values
                .Any(actor => !actor.Dead && ReferenceEquals(actor.Owner.BotsGroup, group));
        }

        internal void Stop()
        {
            if (subscribed)
            {
                Player.OnPlayerDeadStatic -= Killed;
                subscribed = false;
            }

            foreach (var request in requests)
            {
                if (request.Spawn != null)
                {
                    request.Spawn.ShallSpawn = false;
                }
            }

            GauntletPursuit.Clear();
            if (Current == this)
            {
                Current = null;
            }
        }

        internal void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(world, current))
            {
                Stop();
                world = current;
                initialized = bonus = ended = false;
                wave = sequence = 0;
                eventId = Status = "";
                fenceReceipt = null;
                roles.Clear();
                actors.Clear();
                requests.Clear();
                retried.Clear();
                killaHelmets.Clear();
                tagillaHelmets.Clear();
                nextTick = nextSpawn = 0;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (current == null || game?.GameTimer?.StartDateTime == null || game.Status is GameStatus.Stopped or GameStatus
                .Stopping or GameStatus.SoftStopping)
            {
                return;
            }

            if (!initialized)
            {
                string selected = Plugin.RaidContext.Split(':').Last().Trim();
                if (!GauntletPolicy.Handles(selected))
                {
                    return;
                }

                initialized = true;
                eventId = selected;
                fenceReceipt = new FenceTrialReceipt(selected);
                Current = this;
                Player.OnPlayerDeadStatic += Killed;
                subscribed = true;
                QueueWave();
            }

            if (!Running)
            {
                GauntletPursuit.Clear();
                return;
            }

            GauntletPursuit.Tick();
            if (Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + 0.25f;
            int pending = 0;
            foreach (var request in requests)
            {
                if (request.Profiles.Count > 0)
                {
                    request.Finished = request.Confirmed = true;
                }
                else if (!retried.Contains(request) && (request.Failed || Time.unscaledTime - request.Submitted > (IsTrials ? 25 : 60)))
                {
                    // A native acceptance is not proof of activation. Cancel its
                    // creation token before replacing a silent request, so a late
                    // queued activation cannot become a sixth wave member.
                    request.Failed = true;
                    foreach (var creation in LocalRaidDirector.Creating.Where(pair => ReferenceEquals(pair.Value, request))
                        .Select(pair => pair.Key).ToArray())
                    {
                        creation.StopSpawn();
                    }

                    request.Finished = true;
                    request.Spawn!.ShallSpawn = false;
                    retried.Add(request);
                    roles.Enqueue(request.Role);
                    Plugin.Trace("[RR-GAUNTLET] failed spawn retried role=" + request.Role);
                }
                else if (!request.Finished)
                {
                    pending++;
                    // Accepted creation callbacks may arrive late. Do not create
                    // duplicates or count an unconfirmed request as a dead bot.
                    if (!request.TimeoutLogged && Time.unscaledTime - request.Submitted > 60)
                    {
                        request.TimeoutLogged = true;
                        Plugin.Trace("[RR-GAUNTLET] waiting for native creation callback role=" + request.Role);
                    }
                }
            }

            if (rewardRemaining > 0 && Time.unscaledTime >= nextReward && rewardedActor != null && actors
                .TryGetValue(rewardedActor, out var recipient))
            {
                nextReward = Time.unscaledTime + 10;
                rewardRemaining = GauntletRewards.Ensure(recipient.Owner.GetPlayer, GauntletPolicy.BitcoinReward(eventId, wave));
            }

            foreach (var actor in actors.Values)
            {
                // Death callback ordering can vary with native grenade damage.
                // Reconcile only confirmed actors with a definitively dead health
                // controller, regardless of who dealt the lethal damage.
                if (!actor.Dead && actor.Owner.GetPlayer.HealthController?.IsAlive == false)
                {
                    actor.Dead = true;
                    Plugin.Trace("[RR-GAUNTLET] confirmed native health death role=" + actor.Role);
                }
            }

            int dead = actors.Values.Count(actor => actor.Dead);
            if (IsTrials)
            {
                string title = GauntletPolicy.Title(eventId);
                Status = $"{title} {wave + 1}/{WaveRoles.Length}: {string.Join(" / ", WaveRoles[wave])}, "
                    + $"dead={dead}/{GauntletPolicy.TrialSquadSize}, pending={pending}";
            }
            else
            {
                Status = $"Snowball{(bonus ? " bonus" : "")}: " + (bonus ? $"dead={dead}/2, pending={pending}"
                    : $"boss {wave + 1}/{GauntletPolicy.SnowballRoles.Length}, " + $"cleared={dead}, pending={pending}");
            }

            if (roles.Count > 0 && pending < (IsTrials ? 5 : 2) && Time.unscaledTime >= nextSpawn)
            {
                nextSpawn = Time.unscaledTime + (IsTrials ? 0.25f : 2f);
                Submit(roles.Peek());
            }

            if (IsTrials && roles.Count == 0 && GauntletPolicy.WaveComplete(actors.Count, dead, pending))
            {
                wave++;
                if (wave == WaveRoles.Length)
                {
                    fenceReceipt?.Complete();
                    Complete(eventId == "extreme_trials" ? "Extreme Trials complete" : "Trials complete");
                }
                else
                {
                    QueueWave();
                }
            }

            if (eventId == "snowball" && roles.Count == 0 && pending == 0 && actors.Count == (bonus ? 2 : wave + 1) && dead == actors.Count)
            {
                if (actors.Values.Any(actor => !actor.PlayerKill))
                {
                    Complete("Snowball ended: another source killed a required hunter");
                }
                else if (bonus)
                {
                    Complete("Snowball bonus complete");
                }
                else if (wave + 1 < GauntletPolicy.SnowballRoles.Length)
                {
                    // Keep prior death attribution and helmet IDs, but enqueue
                    // exactly one next boss only after the player's confirmed
                    // kill. No required boss can fight a later wave's Goons.
                    wave++;
                    roles.Enqueue(GauntletPolicy.SnowballRoles[wave]);
                    nextSpawn = Time.unscaledTime + 3f;
                    Plugin.Trace($"[RR-GAUNTLET] Snowball next boss {wave + 1}: " + GauntletPolicy.SnowballRoles[wave]);
                }
                else
                {
                    Status = "Snowball complete";
                    fenceReceipt?.Complete();
                    if (HelmetsAdjacent())
                    {
                        bonus = true;
                        actors.Clear();
                        requests.Clear();
                        roles.Enqueue("bossKillaAgro");
                        roles.Enqueue("bossTagillaAgro");
                        Plugin.Trace("[RR-GAUNTLET] helmet easter egg activated once");
                    }
                }
            }
        }

        private void QueueWave()
        {
            actors.Clear();
            requests.Clear();
            rewardedActor = null;
            rewardRemaining = 0;
            GauntletPursuit.Clear();
            if (eventId == "snowball")
            {
                roles.Enqueue(GauntletPolicy.SnowballRoles[wave]);
            }
            else
            {
                var stage = WaveRoles[wave];
                int offset = UnityEngine.Random.Range(0, stage.Length);
                for (int index = 0; index < GauntletPolicy.TrialSquadSize; index++)
                {
                    // Finale members are independent draws; ordinary mixed teams
                    // retain balanced coverage of their native specializations.
                    int member = GauntletPolicy.IsRandomGuardFinale(eventId, wave) ? UnityEngine.Random.Range(0, stage.Length)
                        : (index + offset) % stage.Length;
                    roles.Enqueue(stage[member]);
                }
            }

            nextSpawn = Time.unscaledTime + 5;
            if (IsTrials && Singleton<NotificationManager>.Instantiated)
            {
                NotificationManager.DisplayMessageNotification(
                    $"{(GauntletPolicy.Title(eventId))}: " + $"wave {wave + 1}/{WaveRoles.Length}",
                    ENotificationDurationType.Long,
                    ENotificationIconType.Quest
                );
            }
        }

        private void Submit(string role)
        {
            var local = Plugin.Local;
            var scenario = LocalRaidDirector.Scenario;
            if (local == null || scenario == null)
            {
                return;
            }

            // Native spawn points avoid teleportation, off-map coordinates and
            // room colliders. Bonus spawns stay within 70-130m of the player.
            if (!TryFindPlayerApproach(local.Position, out var targetFloor))
            {
                Status += "; waiting for a reachable player position";
                return;
            }

            var candidates = LocalRaidDirector.Zones.SelectMany(zone => (zone.SpawnPoints ?? Array.Empty<EFT.Game.Spawning
                .ISpawnPoint>()).Select(point => new { Zone = zone, Point = point })).Where(candidate => !candidate.Point
                .SpawnBlocked && candidate.Point.NextBornTime <= Time.time).Where(candidate => Vector3.Distance(candidate.Point
                .Position, local.Position) >= (IsTrials ? 40 : 50)).Where(candidate => !bonus || Vector3.Distance(candidate.Point
                .Position, local.Position) <= 130).Where(candidate => !bonus || Vector3.Distance(candidate.Point.Position, local
                .Position) >= 70).OrderBy(candidate => Mathf.Abs(Vector3.Distance(candidate.Point.Position, local
                .Position) - (IsTrials ? 65 : bonus ? 100 : 125))).ToArray();
            int start = candidates.Length == 0 ? 0 : spawnProbe % candidates.Length;
            spawnProbe = start + 3;
            foreach (var candidate in candidates.Skip(start).Take(3))
            {
                // At most three single native path queries for placement per
                // two-second submission interval; never a scene scan.
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(candidate.Point.Position, out var floor, 2, NavMesh.AllAreas) || !NavMesh
                    .CalculatePath(floor.position, targetFloor.position, NavMesh.AllAreas, path) || (IsTrials ? path
                    .status != NavMeshPathStatus.PathComplete : path.status == NavMeshPathStatus.PathInvalid) || (IsTrials
                    && !GauntletSpawnSafety.HasUnlockedExit(path)) || !GauntletSpawnSafety.IsHiddenFromPlayer(floor.position) || path
                    .corners.Length < 2)
                {
                    continue;
                }

                var spawn = new BossLocationSpawn
                {
                    BossName = role,
                    BossEscortType = "assault",
                    BossEscortAmount = "0",
                    BossChance = 100,
                    BossZone = candidate.Zone.name,
                    BossDifficult = GauntletPolicy.UsesEquipmentProgression(eventId) ? GauntletPolicy.TrialDifficulty(GauntletPolicy
                        .StageTier(eventId, wave) * 4) : "hard",
                    BossEscortDifficult = "normal",
                    Time = 0,
                    ForceSpawn = true,
                    IgnoreMaxBots = true,
                    Supports = Array.Empty<WildSpawnSupports>(),
                    TriggerId = "",
                    TriggerName = ""
                };
                spawn.Init();
                var request = new LocalRaidDirector.Reinforcement
                {
                    Wave = -6,
                    Count = 1,
                    Role = role,
                    Submitted = Time.unscaledTime,
                    Spawn = spawn,
                    ExactPoint = candidate.Point.Position,
                    Created = owner => Created(owner, role)
                };
                requests.Add(request);
                LocalRaidDirector.Requests[spawn] = request;
                roles.Dequeue();
                try
                {
                    scenario.ActivateWave(spawn);
                    Plugin.Trace($"[RR-GAUNTLET] submitted event={eventId} wave={wave} "
                        + $"role={role} sequence={++sequence} point={candidate.Point.Position}");
                }
                catch (Exception error)
                {
                    request.Failed = true;
                    spawn.ShallSpawn = false;
                    Plugin.Trace("[RR-GAUNTLET] spawn failed: " + error.GetBaseException().Message);
                }

                return;
            }

            Status += "; waiting for a reachable native spawn point";
        }

        private static bool TryFindPlayerApproach(Vector3 playerPosition, out NavMeshHit approach)
        {
            // Factory's outdoor player spawns can sit outside the AI mesh.
            // Select a nearby approach without moving the player or an AI,
            // and reject another storey returned by the spherical query.
            foreach (float radius in new[]
            {
                4f,
                12f,
                24f,
                32f
            }

            )
            {
                if (NavMesh.SamplePosition(playerPosition, out approach, radius, NavMesh.AllAreas) && Mathf.Abs(approach.position
                    .y - playerPosition.y) <= 4f)
                {
                    return true;
                }
            }

            approach = default;
            return false;
        }

        private void Created(BotOwner owner, string role)
        {
            if (!Running)
            {
                return;
            }

            actors[owner.ProfileId] = new Actor
            {
                Owner = owner,
                Role = role
            };
            if (GauntletPolicy.UsesEquipmentProgression(eventId))
            {
                // Native client level is derived from cumulative experience.
                owner.Profile.Info.Experience = ProfileInfo.GetExperience(GauntletPolicy.StageLevel(eventId, wave));
            }

            if (IsTrials && rewardedActor == null && GauntletPolicy.BitcoinReward(eventId, wave) > 0)
            {
                rewardedActor = owner.ProfileId;
                rewardRemaining = GauntletRewards.Ensure(owner.GetPlayer, GauntletPolicy.BitcoinReward(eventId, wave));
            }

            GauntletPursuit.Add(owner);
            Plugin.Trace($"[RR-GAUNTLET] hunter bound profile={owner.ProfileId} "
                + $"role={role} state={owner.BotState} position={owner.Position}");
        }

        private void Killed(Player victim, IPlayer killer, DamageInfo damage, EBodyPart part)
        {
            if (!Running || !actors.TryGetValue(victim.ProfileId, out var actor))
            {
                return;
            }

            actor.Dead = true;
            actor.PlayerKill = killer != null && Plugin.IsLocal(killer);
            if (eventId == "snowball" && actor.PlayerKill && !bonus)
            {
                foreach (var item in victim.Profile.Inventory.Equipment.GetAllItems())
                {
                    string template = item.TemplateId.ToString();
                    if (actor.Role == "bossKilla" && template == "5c0e874186f7745dc7616606")
                    {
                        killaHelmets.Add(item.Id);
                    }

                    if (actor.Role == "bossTagilla" && (template == "60a7ad2a2198820d95707a2e" || template == "60a7ad3a0c5cb24b0134664a"))
                    {
                        tagillaHelmets.Add(item.Id);
                    }
                }
            }

            Plugin.Trace($"[RR-GAUNTLET] death role={actor.Role} profile={victim.ProfileId} " + $"playerKill={actor.PlayerKill}");
        }

        private bool HelmetsAdjacent()
        {
            var equipment = Plugin.Local?.Profile.Inventory.Equipment;
            if (equipment == null)
            {
                return false;
            }

            var items = equipment.GetAllItems().ToArray();
            foreach (var first in items.Where(item => killaHelmets.Contains(item.Id)))
            {
                foreach (var second in items.Where(item => tagillaHelmets.Contains(item.Id)))
                {
                    if (first.CurrentAddress is not GridItemAddress a || second.CurrentAddress is not GridItemAddress b || a.Grid != b.Grid)
                    {
                        continue;
                    }

                    bool ar = (int)a.LocationInGrid.r != 0;
                    bool br = (int)b.LocationInGrid.r != 0;
                    if (GauntletPolicy.Adjacent(
                        a.LocationInGrid.x,
                        a.LocationInGrid.y,
                        ar ? first.Template.Height : first.Template.Width,
                        ar ? first.Template.Width : first.Template.Height,
                        b.LocationInGrid.x,
                        b.LocationInGrid.y,
                        br ? second.Template.Height : second.Template.Width,
                        br ? second.Template.Width : second.Template.Height
                    ))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void Complete(string message)
        {
            ended = true;
            Status = message;
            GauntletPursuit.Clear();
            Plugin.Trace("[RR-GAUNTLET] " + message);
            if (Singleton<NotificationManager>.Instantiated)
            {
                NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Long, ENotificationIconType.Quest);
            }
        }
    }
}
