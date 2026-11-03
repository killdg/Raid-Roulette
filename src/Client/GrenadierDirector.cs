using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal sealed class GrenadierDirector
    {
        private GameWorld? world;
        private float nextTick;
        private float nextError;
        private readonly Dictionary<string, float> attempts = new();
        private readonly HashSet<string> observed = new();
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
                attempts.Clear();
                observed.Clear();
                nextTick = 0;
            }

            if (current == null || !Plugin.RaidContext.EndsWith(": grenadier", StringComparison.Ordinal) || Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + 0.5f;
            foreach (var player in current.RegisteredPlayers.OfType<Player>().Where(p => p.IsAI && !p.IsYourPlayer && p
                .HealthController?.IsAlive == true && (p.Profile.Info.Settings.Role.ToString()
                .ToLowerInvariant()is "assault" or "assaultgroup" or "cursedassault" or "marksman" or "pmcusec" or "pmcbear")).ToArray())
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
                        .ThrowType == ThrowWeapType.frag_grenade).ToArray();
                    if (smokes.Length < 2 && !controller.ThrowindNow && player.HandsController is not Player.GrenadeHandsController)
                    {
                        for (int i = smokes.Length; i < 2; i++)
                        {
                            var grenade = Singleton<ItemFactory>.Instance.CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24),
                                 "5710c24ad2720bc3458b45a3", null);
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
                        }

                        controller.CheckGrenade();
                        smokes = player.Profile.Inventory.Equipment.GetAllItems().OfType<ThrowWeap>().Where(g => g
                            .ThrowType == ThrowWeapType.frag_grenade).ToArray();
                    }

                    if (observed.Add(player.ProfileId))
                    {
                        Plugin.Trace($"[RR-GRENADIER] stage=actor-ready profile={player.ProfileId} role={player.Profile.Info.Settings.Role} smokes={smokes.Length}");
                        attempts[player.ProfileId] = Time.unscaledTime + UnityEngine.Random.Range(5f, 12f);
                        controller.OnGrenadeThrowComplete += _ => Plugin
                            .Trace($"[RR-GRENADIER] stage=throw-completed profile={player.ProfileId}");
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

                    if (Vector3.Distance(player.Position, target) < 8)
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

                    trajectory.GrenadeType = ThrowWeapType.frag_grenade;
                    if (!controller.SetThrowData(trajectory) || !controller.DoThrow())
                    {
                        continue;
                    }

                    attempts[player.ProfileId] = Time.unscaledTime + UnityEngine.Random.Range(6f, 12f);
                    Plugin.Trace($"[RR-GRENADIER] stage=throw-started profile={player.ProfileId} remainingBefore={smokes.Length} target={target}");
                }
                catch (Exception e)
                {
                    if (Time.unscaledTime >= nextError)
                    {
                        nextError = Time.unscaledTime + 15;
                        Plugin.Trace("[RR-GRENADIER] stage=error " + e.GetBaseException().Message);
                    }
                }
            }
        }
    }
}
