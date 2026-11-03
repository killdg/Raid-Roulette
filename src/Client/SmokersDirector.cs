using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal sealed class SmokersDirector
    {
        internal static bool Active;
        private sealed class ThrowState
        {
            internal BotOwner Bot = null!;
            internal float Ready;
            internal Action<ThrowWeap> Handler = null!;
        }

        private static readonly Dictionary<BotGrenadeController, ThrowState> throws = new();
        internal static bool Allowed(BotGrenadeController controller) => !Active || controller.grenade?
            .ThrowType != ThrowWeapType.smoke_grenade || controller.ThrowindNow || !throws.TryGetValue(controller,
             out var state) || Time.unscaledTime >= state.Ready;
        internal static void Started(BotGrenadeController controller, bool accepted)
        {
            if (!Active || !accepted || controller.grenade?.ThrowType != ThrowWeapType.smoke_grenade || !throws
                .TryGetValue(controller, out var state))
            {
                return;
            }

            Schedule(state, controller.grenade);
        }

        private static void Schedule(ThrowState state, ThrowWeap grenade)
        {
            int supply = state.Bot.GetPlayer.Profile.Inventory.Equipment.GetAllItems().OfType<ThrowWeap>().Count(g => g
                .ThrowType == ThrowWeapType.smoke_grenade && g.Id != grenade.Id);
            float delay = SmokeSchedule.Delay(grenade.EmitTime, grenade.GetExplDelay);
            state.Ready = Time.unscaledTime + delay;
            Plugin.Trace($"[RR-SMOKE] stage=cooldown profile={state.Bot.ProfileId} supply={supply} delay={delay:0.0}s halfLife={grenade.EmitTime * 0.5f:0.0}s");
        }

        private static void Clear()
        {
            foreach (var pair in throws)
            {
                pair.Key.OnGrenadeThrowComplete -= pair.Value.Handler;
            }

            throws.Clear();
            SmokeRoaming.Clear();
        }

        private GameWorld? world;
        private float nextTick;
        private float nextError;
        private readonly Dictionary<string, float> attempts = new();
        private readonly HashSet<string> observed = new();
        private readonly List<(Vector3 Position, float Until)> clouds = new();
        public void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(world, current))
            {
                world = current;
                Clear();
                attempts.Clear();
                observed.Clear();
                clouds.Clear();
                nextTick = 0;
            }

            bool active = current != null && Plugin.RaidContext.EndsWith(": smokers_lobby", StringComparison.Ordinal);
            if (Active && !active)
            {
                Clear();
                attempts.Clear();
                observed.Clear();
                clouds.Clear();
            }

            Active = active;
            if (!Active || current == null || Time.unscaledTime < nextTick)
            {
                return;
            }

            SmokeRoaming.BeginTick();
            clouds.RemoveAll(c => c.Until <= Time.unscaledTime);
            nextTick = Time.unscaledTime + 0.5f;
            foreach (var player in current.RegisteredPlayers.OfType<Player>().Where(p => p.IsAI && !p.IsYourPlayer && p
                .HealthController?.IsAlive == true).ToArray())
            {
                try
                {
                    var bot = player.AIData?.BotOwner;
                    var controller = bot?.WeaponManager?.Grenades;
                    if (bot == null || controller == null || bot.BotState != EBotState.Active)
                    {
                        continue;
                    }

                    var smokes = player.Profile.Inventory.Equipment.GetAllItems().OfType<ThrowWeap>().Where(g => g
                        .ThrowType == ThrowWeapType.smoke_grenade).ToArray();
                    if (observed.Add(player.ProfileId))
                    {
                        Plugin.Trace($"[RR-SMOKE] stage=actor-ready profile={player.ProfileId} role={player.Profile.Info.Settings.Role} smokes={smokes.Length}");
                        attempts[player.ProfileId] = Time.unscaledTime + UnityEngine.Random.Range(3f, 10f);
                        var state = new ThrowState
                        {
                            Bot = bot,
                            Ready = attempts[player.ProfileId]
                        };
                        state.Handler = grenade =>
                        {
                            if (Active && grenade.ThrowType == ThrowWeapType.smoke_grenade)
                            {
                                Schedule(state, grenade);
                            }
                        };
                        throws[controller] = state;
                        controller.OnGrenadeThrowComplete += state.Handler;
                    }

                    if (smokes.Length < 2 && !controller.ThrowindNow && player.HandsController is not Player.GrenadeHandsController)
                    {
                        for (int i = smokes.Length; i < 2; i++)
                        {
                            var grenade = Singleton<ItemFactory>.Instance.CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24),
                                 "617aa4dd8166f034d57de9c5", null);
                            grenade.SpawnedInSession = true;
                            bool stored = false;
                            foreach (var slot in new[]
                            {
                                EquipmentSlot.Pockets,
                                EquipmentSlot.TacticalVest,
                                EquipmentSlot.Backpack
                            }

                            )
                            {
                                if (player.Profile.Inventory.Equipment.GetSlot(slot).ContainedItem is not CompoundItem container)
                                {
                                    continue;
                                }

                                foreach (var grid in container.Grids)
                                {
                                    var address = grid.FindLocationForItem(grenade);
                                    if (address == null || !address.AddWithoutRestrictions(grenade).Succeeded)
                                    {
                                        continue;
                                    }

                                    address.RaiseAddEvent(grenade, CommandStatus.Succeed, player.InventoryController);
                                    stored = true;
                                    break;
                                }

                                if (stored)
                                {
                                    break;
                                }
                            }

                            if (!stored)
                            {
                                break;
                            }

                            Plugin.Trace($"[RR-SMOKE] stage=resupplied profile={player.ProfileId}");
                        }

                        controller.CheckGrenade();
                        smokes = player.Profile.Inventory.Equipment.GetAllItems().OfType<ThrowWeap>().Where(g => g
                            .ThrowType == ThrowWeapType.smoke_grenade).ToArray();
                    }

                    SmokeRoaming.Tick(bot);
                    if (throws.TryGetValue(controller, out var timing) && Time.unscaledTime < timing.Ready)
                    {
                        continue;
                    }

                    if (smokes.Length == 0 || controller.ThrowindNow || player.HandsController is Player.GrenadeHandsController || Time
                        .unscaledTime < attempts[player.ProfileId])
                    {
                        continue;
                    }

                    attempts[player.ProfileId] = Time.unscaledTime + 3;
                    // Prefer the known enemy's vicinity; otherwise smoke a nearby navigable area.
                    var enemy = bot.Memory?.GoalEnemy;
                    Vector3 target;
                    if (enemy != null && Vector3.Distance(player.Position, enemy.EnemyLastPosition)is >= 8 and <= 35)
                    {
                        target = enemy.EnemyLastPosition;
                    }
                    else
                    {
                        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
                        float distance = UnityEngine.Random.Range(10f, 22f);
                        var candidate = player.Position + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                        if (!NavMesh.SamplePosition(candidate, out var hit, 4, NavMesh.AllAreas))
                        {
                            continue;
                        }

                        target = hit.position;
                    }

                    if (Vector3.Distance(player.Position, target) < 8 || clouds.Any(c => Vector3.Distance(c.Position, target) < 12))
                    {
                        continue;
                    }

                    controller.SetThrowParams(smokes[0]);
                    // Retain native ballistic/obstruction checks and the native hands/throw animation.
                    // Calling the trajectory helper explicitly allows smoke without a frag-only AI decision.
                    var trajectory = AIGrenadeHelper.CanThrowGrenade2(controller.StartThrow, target, controller.MaxPower, AIGreandeAng
                        .ang45, 64, 0.5f);
                    if (!trajectory.CanThrow)
                    {
                        continue;
                    }

                    trajectory.GrenadeType = ThrowWeapType.smoke_grenade;
                    if (!controller.SetThrowData(trajectory) || !controller.DoThrow())
                    {
                        continue;
                    }

                    clouds.Add((target, Time.unscaledTime + smokes[0].GetExplDelay + smokes[0].EmitTime));
                    attempts[player.ProfileId] = Time.unscaledTime + 3;
                    Plugin.Trace($"[RR-SMOKE] stage=throw-started profile={player.ProfileId} remainingBefore={smokes.Length} target={target}");
                }
                catch (Exception e)
                {
                    if (Time.unscaledTime >= nextError)
                    {
                        nextError = Time.unscaledTime + 15;
                        Plugin.Trace("[RR-SMOKE] stage=error " + e.GetBaseException().Message);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(BotGrenadeController), nameof(BotGrenadeController.DoThrow))]
    internal static class SmokeThrowCooldown
    {
        private static bool Prefix(BotGrenadeController __instance, ref bool __result)
        {
            if (SmokersDirector.Allowed(__instance))
            {
                return true;
            }

            __result = false;
            return false;
        }

        private static void Postfix(BotGrenadeController __instance, bool __result) => SmokersDirector.Started(__instance, __result);
    }
}
