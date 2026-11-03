using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InputSystem;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal static class GunGameRespawn
    {
        private enum Phase
        {
            Playing,
            Choice,
            Equipping,
            Guard,
            Ending
        }

        private static Phase phase;
        private static ConfigEntry<bool>? enabled;
        private static bool? assignmentMode;
        private static bool Enabled => assignmentMode ?? enabled?.Value == true;

        internal static void SetAssignmentMode(bool? mode)
        {
            // A Fence ticket controls only this raid. Never rewrite the saved
            // menu preference; null restores it for non-Fence Gun Game raids.
            assignmentMode = mode;
        }

        private static GameWorld? world;
        private static Player? player;
        private static float readyAt;
        private static float guardUntil;
        private static float nextRestore;
        private static bool cursorHeld;
        private static bool cursorVisible;
        private static CursorLockMode cursorLock;
        private static string notice = "";
        private static bool restoring;
        internal static bool Waiting => phase == Phase.Choice;
        internal static bool Down => phase is Phase.Choice or Phase.Equipping;
        internal static bool Ending => phase == Phase.Ending;
        internal static bool Protected => (Down || phase == Phase.Guard && Time.unscaledTime < guardUntil) && !Ending && Running();

        internal static void Bind(ConfigFile config)
        {
            enabled = config.Bind(
                "9. Gun Game",
                "Respawn after lethal damage",
                false,
                AdvancedMenu.Diagnostic("Experimental solo Gun Game only. Lethal damage offers Respawn or End run. Respawn resets "
                    + "the ladder and knife bonus, restores health, and equips a pistol. The raid timer "
                    + "continues. Original weapons return only when the raid ends.")
            );
            var testTag = new ConfigurationManagerAttributes();
            AdvancedMenu.Adopt(testTag);
            MenuAction.Bind(
                config,
                "9. Gun Game",
                "Test lethal damage and respawn",
                "Advanced testing only. Applies native lethal chest damage during an active Gun Game with " +
                "respawn enabled. Resets the ladder and knife bonus; this is assisted testing, not a combat " +
                "result.",
                TestDefeat,
                testTag
            );
            var rocketTestTag = new ConfigurationManagerAttributes();
            AdvancedMenu.Adopt(rocketTestTag);
            MenuAction.Bind(
                config,
                "9. Gun Game",
                "Test LMG to rocket transition",
                "Advanced assisted testing only. Starts at LMG. An actual LMG kill switches to the rocket " +
                "using the ordinary swap path. Diagnostic kills advance locally but cannot award saved " +
                "progress or rewards until respawn or the next raid. Does not change the saved ladder.",
                () => GunGameDirector.Current?.TestRocketStage(),
                rocketTestTag
            );
        }

        private static void TestDefeat()
        {
            var local = Plugin.Local;
            if (!AdvancedMenu.Enabled || !Enabled || !Running() || Down || GunGameDirector.Current?.CanRespawn != true || local
                ?.HealthController is not ActiveHealthController health || !health.IsAlive)
            {
                Plugin.Trace("[RR-RESPAWN-TEST] ignored: active Gun Game, advanced options and respawn are required");
                return;
            }

            // Use the native damage path so the ordinary lethal interception and
            // subsequent server restart receipt are exercised. No AI kill credit
            // or reward is created by this explicitly requested local test action.
            Plugin.Trace("[RR-RESPAWN-TEST] assisted native lethal chest damage requested");
            health.ApplyDamage(EBodyPart.Chest, 10000f, new EFT.Ballistics.DamageInfo { DamageType = EDamageType.Undefined });
        }

        private static bool Running()
        {
            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            return game?.GameTimer?.StartDateTime != null && game.Status is not (GameStatus.Stopped or GameStatus
                .Stopping or GameStatus.SoftStopping) && (!game.GameTimer.SessionTime.HasValue || game.GameTimer.PastTime < game
                .GameTimer.SessionTime.Value) && Plugin.RaidContext.EndsWith(": gun_game", StringComparison.Ordinal);
        }

        internal static bool Intercept(ActiveHealthController health)
        {
            var local = Plugin.Local;
            if (local == null || local.IsAI || health.Player != local || !health.IsAlive || Ending || !Running())
            {
                return false;
            }

            if (health.GetBodyPartHealth(EBodyPart.Head).Current > 0 && health.GetBodyPartHealth(EBodyPart.Chest).Current > 0
                && health.GetBodyPartHealth(EBodyPart.Common).Current > 0)
            {
                return false;
            }

            if (Protected)
            {
                Restore(health);
                return true;
            }

            if (!Enabled || local.Profile.Info.Side == EPlayerSide.Savage || GunGameDirector.Current?.CanRespawn != true)
            {
                return false;
            }

            player = local;
            phase = Phase.Choice;
            readyAt = Time.unscaledTime + 5;
            notice = "";
            try
            {
                Restore(health);
                local.Move(Vector2.zero);
                local.EnableSprint(false);
                if (local.HandsController is Player.FirearmController firearm)
                {
                    firearm.SetTriggerPressed(false);
                }

                GunGameDirector.Current.RestartAfterDeath(Guid.NewGuid().ToString("N"));
                Plugin.Trace("[RR-RESPAWN] stage=defeated reset=pistol knifeBonus=cleared");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Trace("[RR-RESPAWN] intercept-failed=" + e.GetBaseException().Message);
                phase = Phase.Ending;
                ReleaseCursor();
                return false;
            }
        }

        private static void Restore(ActiveHealthController health)
        {
            if (restoring)
            {
                return;
            }

            restoring = true;
            try
            {
                health.RemoveNegativeEffects(EBodyPart.Common);
                health.RemoveIntoxication();
                foreach (var part in new[]
                {
                    EBodyPart.Head,
                    EBodyPart.Chest,
                    EBodyPart.Stomach,
                    EBodyPart.LeftArm,
                    EBodyPart.RightArm,
                    EBodyPart.LeftLeg,
                    EBodyPart.RightLeg
                }

                )
                {
                    health.FullRestoreBodyPart(part);
                    health.RemoveNegativeEffects(part);
                    health.Player.UpdateConditionsAfterBodyPartStateChanged(part);
                }

                health.ChangeEnergy(1000);
                health.ChangeHydration(1000);
                health.Player.UpdateSpeedLimitByHealth();
                var physical = health.Player.Physical;
                if (physical != null)
                {
                    physical.Stamina.Current = physical.Stamina.TotalCapacity.Value;
                    physical.HandsStamina.Current = physical.HandsStamina.TotalCapacity.Value;
                }
            }
            finally
            {
                restoring = false;
            }
        }

        internal static void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (!ReferenceEquals(world, current) || !Running())
            {
                ReleaseCursor();
                phase = Phase.Playing;
                player = null;
                world = current;
                return;
            }

            if (!Down)
            {
                return;
            }

            if (player == null || player.HealthController?.IsAlive != true)
            {
                ReleaseCursor();
                phase = Phase.Ending;
                return;
            }

            if (!cursorHeld)
            {
                cursorHeld = true;
                cursorVisible = Cursor.visible;
                cursorLock = Cursor.lockState;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            player.Move(Vector2.zero);
            player.EnableSprint(false);
            if (Time.unscaledTime >= nextRestore && player.HealthController is ActiveHealthController health)
            {
                nextRestore = Time.unscaledTime + 1;
                Restore(health);
            }

            if (phase == Phase.Equipping && GunGameDirector.Current?.LocalWeaponReady == true)
            {
                phase = Phase.Guard;
                guardUntil = Time.unscaledTime + 3;
                ReleaseCursor();
                EFT.Communications.NotificationManager.DisplayMessageNotification(
                    "Gun Game restarted: Level 1 — Pistol",
                    EFT.Communications.ENotificationDurationType.Long,
                    EFT.Communications.ENotificationIconType.Quest
                );
                Plugin.Trace("[RR-RESPAWN] stage=ready protectionSeconds=3");
            }
        }

        private static void ReleaseCursor()
        {
            if (!cursorHeld)
            {
                return;
            }

            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            cursorHeld = false;
        }

        internal static void Stop()
        {
            ReleaseCursor();
            phase = Phase.Playing;
            player = null;
            world = null;
        }

        private static bool VisibleFrom(Vector3 point, Player enemy) => !Physics.Linecast(
            point + Vector3.up * 1.4f,
            enemy.Position + Vector3.up * 1.4f,
            out var hit,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        ) || hit.collider.GetComponentInParent<Player>() == enemy;
        private static bool FindPoint(out Vector3 result)
        {
            result = default;
            var local = player!;
            var native = LocalRaidDirector.NativePoints;
            // The global spawn system is not always populated with PMC points.
            // Zone points remain native authored placements; combine both pools
            // instead of discarding every bot point when one PMC point exists.
            var candidates = native.Concat(LocalRaidDirector.Zones.SelectMany(zone => zone.SpawnPoints ?? Array.Empty<EFT.Game
                .Spawning.ISpawnPoint>())).Where(point => !point.SpawnBlocked).GroupBy(point => point.Position).Select(group =>
                 group.First()).OrderBy(_ => UnityEngine.Random.value).Take(128).ToArray();
            var enemies = world!.RegisteredPlayers.OfType<Player>().Where(enemy => enemy.IsAI && enemy.HealthController?
                .IsAlive == true && enemy.Profile.Info.Settings.Role != WildSpawnType.shooterBTR).ToArray();
            if (!NavMesh.SamplePosition(local.Position, out var reference, 4f, NavMesh.AllAreas) || Mathf.Abs(reference
                .position.y - local.Position.y) > 3f)
            {
                // Players can stand off the AI mesh (including some authored
                // player spawns). A respawn need not connect back to that spot.
                // Instead require an unlocked route into a living combatant's
                // mesh component. This never teleports to that combatant: the
                // separate candidate collision/distance checks remain below.
                bool foundReference = false;
                foreach (var enemy in enemies.OrderBy(enemy => (enemy.Position - local.Position).sqrMagnitude).Take(8))
                {
                    if (NavMesh.SamplePosition(enemy.Position, out reference, 2f, NavMesh.AllAreas) && Mathf.Abs(reference.position
                        .y - enemy.Position.y) <= 1.5f)
                    {
                        foundReference = true;
                        Plugin.Trace("[RR-RESPAWN] stage=route-reference source=living-ai position=" + reference.position);
                        break;
                    }
                }

                if (!foundReference)
                {
                    Plugin.Trace("[RR-RESPAWN] stage=no-placement reason=no-walkable-reference");
                    return false;
                }
            }

            int navigationRejected = 0;
            int collisionRejected = 0;
            int nearbyRejected = 0;
            int routeRejected = 0;
            var routeRejections = new Dictionary<string, int>();
            var viable = new List<(Vector3 Position, float Nearest, bool Visible, bool Pmc)>();
            foreach (var point in candidates)
            {
                if (!NavMesh.SamplePosition(point.Position, out var hit, 1.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position
                    .y - point.Position.y) > 1.5f || (hit.position - local.Position).sqrMagnitude < 100f)
                {
                    navigationRejected++;
                    continue;
                }

                if (Physics.CheckCapsule(
                    hit.position + Vector3.up * .4f,
                    hit.position + Vector3.up * 1.5f,
                    .3f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore
                ))
                {
                    collisionRejected++;
                    continue;
                }

                float nearest = enemies.Select(enemy => (enemy.Position - hit.position).sqrMagnitude).DefaultIfEmpty(10000f).Min();
                if (nearest < 64f)
                {
                    nearbyRejected++;
                    continue;
                }

                var path = new NavMeshPath();
                bool calculated = NavMesh.CalculatePath(hit.position, reference.position, NavMesh.AllAreas, path);
                string rejectionReason = "path-calculation-failed";
                if (!calculated || !GauntletSpawnSafety.HasUnlockedExit(path, out rejectionReason))
                {
                    routeRejected++;
                    routeRejections.TryGetValue(rejectionReason, out int count);
                    routeRejections[rejectionReason] = count + 1;
                    continue;
                }

                bool visible = enemies.OrderBy(enemy => (enemy.Position - hit.position).sqrMagnitude).Take(3).Any(enemy =>
                     VisibleFrom(hit.position, enemy));
                bool pmc = EFT.Game.Spawning.SpawnCategoryExtension.ContainBotPmcCategory(point.Categories);
                viable.Add((hit.position, Mathf.Sqrt(nearest), visible, pmc));
            }

            // Evaluate geometry only once per click. Distance tiers reuse that
            // bounded result rather than repeating all NavMesh/physics queries.
            foreach (float distance in new[]
            {
                25f,
                15f,
                8f
            }

            )
            {
                float best = float.NegativeInfinity;
                bool found = false;
                foreach (var point in viable)
                {
                    if (point.Nearest < distance)
                    {
                        continue;
                    }

                    float score = point.Nearest - (point.Visible ? 50f : 0f) + (point.Pmc ? 2f : 0f);
                    if (score <= best)
                    {
                        continue;
                    }

                    best = score;
                    result = point.Position;
                    found = true;
                }

                if (found)
                {
                    Plugin.Trace($"[RR-RESPAWN] stage=placement-selected candidates={candidates.Length} "
                        + $"minimumEnemyDistance={distance} position={result}");
                    return true;
                }
            }

            Plugin.Trace($"[RR-RESPAWN] stage=no-placement globalPoints={native.Length} "
                + $"candidates={candidates.Length} enemies={enemies.Length} "
                + $"navigationRejected={navigationRejected} collisionRejected={collisionRejected} "
                + $"nearbyRejected={nearbyRejected} routeRejected={routeRejected} "
                + $"routeReasons=[{string.Join(",", routeRejections.Select(pair => pair.Key + ":" + pair.Value))}] "
                + $"playerPosition={local.Position} referencePosition={reference.position}");
            return false;
        }

        internal static void Draw()
        {
            if (!Down)
            {
                return;
            }

            int priorDepth = GUI.depth;
            GUI.depth = -500;
            var prior = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prior;
            GUILayout.BeginArea(new Rect((Screen.width - 440) / 2f, (Screen.height - 260) / 2f, 440, 260), GUI.skin.window);
            GUILayout.Label("GUN GAME — DEFEATED");
            GUILayout.Label("The ladder and knife bonus have been reset.\nThe raid timer and AI continue.");
            bool previous = GUI.enabled;
            GUI.enabled = phase == Phase.Choice && Time.unscaledTime >= readyAt && GunGameDirector.Current?.ResetPending == false;
            if (GUILayout.Button(phase == Phase.Equipping ? "Equipping pistol…" : Time.unscaledTime < readyAt ? "Respawn in "
                + Mathf.CeilToInt(readyAt - Time.unscaledTime) : GunGameDirector.Current?.ResetPending == true ? "Saving restart…"
                : "Respawn — Level 1: Pistol"))
            {
                if (FindPoint(out var point) && player?.HealthController is ActiveHealthController health)
                {
                    Restore(health);
                    player.Teleport(point);
                    phase = Phase.Equipping;
                    notice = "Equipping a loaded pistol…";
                    Plugin.Trace("[RR-RESPAWN] stage=relocated position=" + point);
                }
                else
                {
                    notice = "No clear spawn point yet. Try again, or end this run.";
                }
            }

            GUI.enabled = previous;
            if (GUILayout.Button("End run"))
            {
                phase = Phase.Ending;
                ReleaseCursor();
                if (player?.HealthController is ActiveHealthController health)
                {
                    health.Kill(EDamageType.Undefined);
                }
            }

            GUILayout.Label(notice);
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }
    }

    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill))]
    internal static class GunGameLethalRespawn
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ActiveHealthController __instance) => !GunGameRespawn.Intercept(__instance);
    }

    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage),
         new[] { typeof(EBodyPart), typeof(float), typeof(EFT.Ballistics.DamageInfo) })]
    internal static class GunGameRespawnDamage
    {
        private static bool Prefix(ActiveHealthController __instance, ref float __result)
        {
            if (GunGameRespawn.Protected && Plugin.IsLocal(__instance.Player))
            {
                __result = 0;
                return false;
            }

            return true;
        }

        private static void Postfix(ActiveHealthController __instance, ref float __result)
        {
            if (GunGameRespawn.Down && Plugin.IsLocal(__instance.Player))
            {
                __result = Mathf.Max(0, __result);
            }
        }
    }

    [HarmonyPatch(typeof(MoveInputTranslator), nameof(MoveInputTranslator.TranslateAxes))]
    internal static class GunGameRespawnMove
    {
        private static bool Prefix(MoveInputTranslator __instance) => !GunGameRespawn.Down || !Plugin.IsLocal(__instance._controller);
    }

    [HarmonyPatch(typeof(PlayerInputTranslator), nameof(PlayerInputTranslator.TranslateCommand))]
    internal static class GunGameRespawnHands
    {
        private static bool Prefix(PlayerInputTranslator __instance, ref InputNode.ETranslateResult __result)
        {
            if (!GunGameRespawn.Down || !Plugin.IsLocal(__instance._player))
            {
                return true;
            }

            __result = InputNode.ETranslateResult.BlockAll;
            return false;
        }
    }

    [HarmonyPatch(typeof(EftGamePlayerOwner), nameof(EftGamePlayerOwner.TranslateCommand))]
    internal static class GunGameRespawnOwner
    {
        private static bool Prefix(EftGamePlayerOwner __instance, ref InputNode.ETranslateResult __result)
        {
            if (!GunGameRespawn.Down || !Plugin.IsLocal(__instance.Player))
            {
                return true;
            }

            __result = InputNode.ETranslateResult.BlockAll;
            return false;
        }
    }
}
