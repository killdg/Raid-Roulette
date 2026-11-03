using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.AI;
using HarmonyLib;

namespace RaidRoulette.TestingClient
{
    internal sealed class LabWorkDirector
    {
        private readonly LabPowerSurges power = new();
        internal void LateTick() => power.LateTick();
        private GameWorld? world;
        private float nextTick;
        private bool opened;
        private bool triggered;
        private int kills;
        private readonly HashSet<string> victims = new();
        internal static readonly HashSet<string> Raiders = new();
        internal static bool Active => Plugin.RaidContext.Contains(": lab_work_day");

        internal static bool IsStaff(IPlayer? p) => CampaignActors.IsCivilian(p) || Active && p?.IsAI == true && p.Profile
            .Info.Settings.Role == WildSpawnType.gifter && p.Profile.Info.Nickname == "Lab Staff";
        internal static bool IsRaider(BotOwner? b) => Active && b != null && Raiders.Contains(b.ProfileId);
        public LabWorkDirector() => Player.OnPlayerDeadStatic += Killed;
        public void Stop()
        {
            Player.OnPlayerDeadStatic -= Killed;
            power.Reset();
        }

        private void Killed(Player victim, IPlayer killer, DamageInfo damage, EBodyPart part)
        {
            if (IsStaff(victim) && killer?.ProfileId == Plugin.Local?.ProfileId && victims.Add(victim.ProfileId))
            {
                kills++;
                Plugin.Trace("[RR-LAB] staff casualties=" + kills + "/3");
            }
        }

        public void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(world, current))
            {
                power.Reset();
                world = current;
                opened = triggered = false;
                kills = 0;
                victims.Clear();
                Raiders.Clear();
                LabStaffBehaviour.Reset();
                nextTick = 0;
            }

            if (current == null || !Active || Plugin.Local == null)
            {
                power.Reset();
                return;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (game?.GameTimer?.StartDateTime == null || game.Status is GameStatus.Stopped or GameStatus
                .Stopping or GameStatus.SoftStopping)
            {
                power.Reset();
                return;
            }

            power.Tick(game.GameTimer.PastTime.TotalSeconds);
            if (game.Status != GameStatus.Started || Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + 2;
            LabStaffBehaviour.Observe();
            if (!opened)
            {
                opened = true;
                int count = 0;
                foreach (var door in UnityEngine.Object.FindObjectsOfType<Door>())
                {
                    // Extraction/switch mechanisms keep their native state. Open keyed rooms only.
                    if (door != null)
                    {
                        LabStaffHubs.Capture(door);
                    }

                    if (door == null || string.IsNullOrEmpty(door.KeyId) || door.NoInteractionsAllowed || door.IsBroken)
                    {
                        continue;
                    }

                    if (door.DoorState == EDoorState.Locked)
                    {
                        door.DoorState = EDoorState.Shut;
                    }

                    if (door.DoorState == EDoorState.Shut)
                    {
                        door.Open();
                        count++;
                    }
                }

                LabStaffHubs.SurveyComplete = true;
                Plugin.Trace("[RR-LAB] opened keyed rooms=" + count);
            }

            if (kills >= 3 && !triggered && LocalRaidDirector.Scenario != null)
            {
                var zones = LocalRaidDirector.Zones.Where(z => z.SpawnPoints?.Any(p => Vector3.Distance(p.Position, Plugin.Local
                    .Position) > 30) == true).ToArray();
                if (zones.Length > 0)
                {
                    var zone = zones[UnityEngine.Random.Range(0, zones.Length)];
                    var spawn = new BossLocationSpawn
                    {
                        BossName = "pmcBot",
                        BossEscortType = "pmcBot",
                        BossEscortAmount = "3",
                        BossChance = 100,
                        BossZone = zone.name,
                        BossDifficult = "hard",
                        BossEscortDifficult = "hard",
                        Time = 0,
                        ForceSpawn = true,
                        IgnoreMaxBots = true,
                        Supports = Array.Empty<WildSpawnSupports>(),
                        TriggerId = "",
                        TriggerName = ""
                    };
                    spawn.Init();
                    var r = new LocalRaidDirector.Reinforcement
                    {
                        Role = "pmcBot",
                        Wave = -3,
                        Submitted = Time.unscaledTime,
                        Spawn = spawn
                    };
                    LocalRaidDirector.Requests[spawn] = r;
                    try
                    {
                        LocalRaidDirector.Scenario.ActivateWave(spawn);
                        triggered = true;
                        Plugin.Trace("[RR-LAB] four hostile Raiders requested after third staff kill");
                    }
                    catch (Exception e)
                    {
                        LocalRaidDirector.Requests.Remove(spawn);
                        Plugin.Trace("[RR-LAB] raider request failed=" + e.GetBaseException().Message);
                    }
                }
            }

            foreach (var p in current.RegisteredPlayers.OfType<Player>().Where(p => Raiders.Contains(p.ProfileId) && p
                .HealthController.IsAlive))
            {
                var b = p.AIData?.BotOwner;
                if (b?.BotsGroup == null || b.BotState != EBotState.Active)
                {
                    continue;
                }

                b.BotsGroup.AddEnemy(Plugin.Local, EBotEnemyCause.byKill);
                if (b.BotsGroup.Enemies.TryGetValue(Plugin.Local, out var info))
                {
                    info.EnemyLastPosition = Plugin.Local.Position;
                }

                if (b.EnemiesController.EnemyInfos.TryGetValue(Plugin.Local, out var enemy))
                {
                    enemy.IgnoreUntilAggression = false;
                    b.Memory.GoalEnemy = enemy;
                    if (enemy.IsVisible)
                    {
                        continue;
                    }
                }

                if (b.Mover == null || b.Mover.CurrentState != EBotMoverState.Default || b.Medecine.Using || b.BewareGrenade.ShallRunAway())
                {
                    continue;
                }

                if (NavMesh.SamplePosition(Plugin.Local.Position, out var hit, 2, NavMesh.AllAreas))
                {
                    var path = new NavMeshPath();
                    if (NavMesh.CalculatePath(b.Position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus
                        .PathComplete && path.corners.Length >= 2)
                    {
                        b.Mover.GoToByWay(path.corners, .8f);
                        b.MovementResume();
                        b.StandBy.CanDoStandBy = false;
                    }
                }
            }
        }
    }

    // Native firearm update assumes a gun and dereferences Reload unconditionally.
    // Staff keep native creation, activation, mover and door handling, with a small
    // movement-only update instead of those combat systems.
    [HarmonyPatch(typeof(BotOwner), nameof(BotOwner.UpdateManual))]
    internal static class LabStaffMovement
    {
        private static bool ActiveStaffWaitingForHub(BotOwner bot)
        {
            return LabWorkDirector.Active && bot.BotState == EBotState.PreActive && !LabStaffHubs.SurveyComplete;
        }

        private static bool Prefix(BotOwner __instance)
        {
            var b = __instance;
            if (!LabWorkDirector.IsStaff(b.GetPlayer))
            {
                return true;
            }

            // Wait for the one-time doorway survey before initial placement. Native
            // activation then runs normally, including its own navigation validation.
            if (ActiveStaffWaitingForHub(b))
            {
                return false;
            }

            // Prepare unarmed readiness before the native PreActive gate, not only after activation.
            LabStaffBehaviour.Register(b);
            if (b.BotState != EBotState.Active)
            {
                return true;
            }

            if (b.HealthController?.IsAlive != true)
            {
                return false;
            }

            LabStaffBehaviour.Register(b);
            b.StandBy.CanDoStandBy = false;
            b.StandBy.StandByType = BotStandByType.active;
            b.NearDoorData.Update();
            // This movement-only civilian update skips the combat brain. Explicitly
            // let native door discovery enter NearDoor before advancing the mover;
            // ManualUpdate alone only services doors already in that state.
            b.DoorOpener.UpdateDoorInteractionStatus();
            b.Mover.ManualUpdate();
            b.DoorOpener.ManualUpdate();
            if (b.GetPlayer.UpdateQueue == EUpdateQueue.Update)
            {
                b.Mover.ManualFixedUpdate();
                b.Steering.ManualFixedUpdate();
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(BotWeaponManager), nameof(BotWeaponManager.Activate))]
    internal static class LabStaffWeaponActivation
    {
        private static bool Prefix(BotWeaponManager __instance) => !LabWorkDirector.IsStaff(__instance._owner?.GetPlayer);
    }

    [HarmonyPatch(typeof(ShootData), nameof(ShootData.Activate))]
    internal static class LabStaffShootingActivation
    {
        private static bool Prefix(ShootData __instance) => !LabWorkDirector.IsStaff(__instance._owner?.GetPlayer);
    }

    [HarmonyPatch(typeof(BotWeaponSelector), nameof(BotWeaponSelector.TakeMainWeapon))]
    internal static class LabStaffEmptyHands
    {
        private static bool Prefix(BotWeaponSelector __instance) => !LabWorkDirector.IsStaff((AccessTools
            .Field(typeof(BotWeaponSelector), "_owner").GetValue(__instance) as BotOwner)?.GetPlayer);
    }

    [HarmonyPatch(typeof(StandartBotBrain), nameof(StandartBotBrain.Activate))]
    internal static class LabStaffPeacefulBrain
    {
        private static void Postfix(StandartBotBrain __instance)
        {
            var owner = AccessTools.Field(typeof(StandartBotBrain), "_owner").GetValue(__instance) as BotOwner;
            if (LabWorkDirector.IsStaff(owner?.GetPlayer))
            {
                owner!.BotsController.AICoreController.RemoveNodeController(__instance.Agent);
            }
        }
    }
}
