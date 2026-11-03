using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using CommonAssets.Scripts.ArtilleryShelling;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal static class BlueDayCover
    {
        private sealed class Reaction
        {
            internal int NearbyImpacts;
            internal float LastImpact;
            internal float NextSearch;
            internal bool RiskTaker;
        }

        private static readonly Queue<Vector3> impacts = new();
        private static readonly Dictionary<string, Reaction> reactions = new();
        private static int cursor;
        internal static void Clear()
        {
            impacts.Clear();
            reactions.Clear();
            cursor = 0;
        }

        internal static void Impact(Vector3 position)
        {
            if (!Plugin.RaidContext.EndsWith(": blue_day", StringComparison.Ordinal))
            {
                return;
            }

            // Observe native impacts only. No projectile, damage or network state
            // is changed. Cap queued observations during dense bombardments.
            if (impacts.Count < 32)
            {
                impacts.Enqueue(position);
            }
        }

        internal static void Tick(GameWorld world)
        {
            BotOwner[] bots = world.RegisteredPlayers.OfType<Player>().Where(player => player.IsAI && player.HealthController?
                .IsAlive == true && player.Profile.Info.Settings.Role != WildSpawnType.shooterBTR).Select(player => player.AIData?
                .BotOwner).Where(owner => owner != null && owner.BotState == EBotState.Active && owner.Mover != null)
                .Cast<BotOwner>().ToArray();
            // Registered actors are used instead of scene-wide object discovery.
            // Process one impact and one actor's bounded shelter search per tick.
            if (impacts.Count > 0)
            {
                Vector3 impact = impacts.Dequeue();
                foreach (BotOwner bot in bots)
                {
                    if ((bot.Position - impact).sqrMagnitude > 40f * 40f)
                    {
                        continue;
                    }

                    if (!reactions.TryGetValue(bot.ProfileId, out Reaction reaction))
                    {
                        reaction = new Reaction
                        {
                            RiskTaker = UnityEngine.Random.value < .2f
                        };
                        reactions.Add(bot.ProfileId, reaction);
                    }

                    if (Time.unscaledTime - reaction.LastImpact > 30f)
                    {
                        reaction.NearbyImpacts = 0;
                    }

                    reaction.NearbyImpacts++;
                    reaction.LastImpact = Time.unscaledTime;
                }
            }

            if (bots.Length == 0)
            {
                return;
            }

            BotOwner selected = bots[cursor++ % bots.Length];
            RaidBehaviorDirector.Drive(selected);
            if (!reactions.TryGetValue(selected.ProfileId, out Reaction state) || state.RiskTaker || state.NearbyImpacts < 2
                || Time.unscaledTime - state.LastImpact > 45f || Time.unscaledTime < state.NextSearch)
            {
                return;
            }

            state.NextSearch = Time.unscaledTime + 5f;
            if (HasRoof(selected.Position))
            {
                return;
            }

            // Sample nearby walkable ground under solid overhead cover. Reject
            // incomplete paths and locked doors; never teleport actors to shelter.
            // Only eight roof probes and at most one path query run in this tick.
            float startAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            for (int index = 0; index < 8; index++)
            {
                float angle = startAngle + index * Mathf.PI * .25f;
                float radius = UnityEngine.Random.Range(8f, 35f);
                Vector3 candidate = selected.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) || Mathf.Abs(hit.position
                    .y - selected.Position.y) > 3f || !HasRoof(hit.position))
                {
                    continue;
                }

                if (RaidBehaviorDirector.SeekShelter(selected, hit.position))
                {
                    state.NextSearch = Time.unscaledTime + 30f;
                    Plugin.Trace("[RR-BLUE-DAY] shelter-route bot=" + selected.ProfileId);
                }

                break;
            }
        }

        private static bool HasRoof(Vector3 position)
        {
            return Physics.Raycast(position + Vector3.up * 1.8f, Vector3.up, 30f, Physics.DefaultRaycastLayers,
                 QueryTriggerInteraction.Ignore);
        }
    }

    [HarmonyPatch(typeof(ArtilleryShellingControllerServer), nameof(ArtilleryShellingControllerServer.ArtilleryProjectileExplosionEvent))]
    internal static class BlueDayImpactObservation
    {
        private static void Prefix(ArtilleryProjectileServer serverProjectile)
        {
            // Read before the native handler returns this projectile to its pool
            // and clears its coordinates. The zone event reports a zone centre,
            // which would incorrectly frighten bots far from the actual impact.
            BlueDayCover.Impact(serverProjectile.currentPosition);
        }
    }
}
