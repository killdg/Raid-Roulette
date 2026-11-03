using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using Diz.Resources;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private readonly HashSet<string> thrownGrenades = new();
        private Vector3 stillPoint;
        private bool stillStarted;
        private bool reloadSeen;
        private float stillSince;
        private float crowdAt;
        private float magazineUntil;
        private float magazineAt;
        private float nextMagazine;
        private float musicAttemptAt;
        private int musicAttempts;
        private GameObject? magazineAsset;
        private readonly PsychMedicalIllusion medical = new();
        internal void GrenadeThrown(string id)
        {
            if (!active || pauseScares || !thrownGrenades.Add(id))
            {
                return;
            }

            if (Random.value < .10f)
            {
                musicCorpseAnchor = null;
                musicAttempts = 3;
                musicAttemptAt = Now + 1;
                Log("stage=jukebox-grenade-roll won=true");
            }
        }

        internal void Reloaded(Magazine? magazine, bool force = false)
        {
            if (!active || magazine == null || (!force && (pauseScares || reloadSeen || Now < nextMagazine)))
            {
                return;
            }

            reloadSeen = true;
            if (!force && Random.value >= .20f)
            {
                return;
            }

            try
            {
                magazineAsset = Singleton<IEasyAssets>.Instance.GetAsset<GameObject>(magazine.Prefab);
            }
            catch (Exception e)
            {
                Log("stage=magazine-asset-unavailable " + e.GetBaseException().Message);
                return;
            }

            if (magazineAsset == null)
            {
                return;
            }

            magazineUntil = Now + Random.Range(2f, 7f);
            magazineAt = 0;
            nextMagazine = Now + 60;
            Log("stage=phantom-magazines duration=" + (magazineUntil - Now).ToString("0.0") + " template=" + magazine.TemplateId);
        }

        private void TickActionScares()
        {
            var local = Plugin.Local!;
            if (local.HandsController is not Player.FirearmController firearm || !firearm.IsInReloadOperation())
            {
                reloadSeen = false;
            }

            if (musicAttempts > 0 && music.Length > 0 && !pauseScares && Now >= musicAttemptAt && !visuals.Any(v => !v.Crowd)
                && musicVisual == null && Time.smoothDeltaTime < .04f)
            {
                musicAttempts--;
                musicAttemptAt = Now + 10;
                if (TryMusicDecoy())
                {
                    musicAttempts = 0;
                }
            }

            if (!stillStarted)
            {
                stillStarted = true;
                stillPoint = local.Position;
                stillSince = Now;
                crowdAt = Now + 10;
            }

            if ((local.Position - stillPoint).sqrMagnitude > .0009f)
            {
                foreach (var v in visuals.Where(v => v.Crowd).ToArray())
                {
                    if (v.RearCrowd)
                    {
                        if (float.IsPositiveInfinity(v.FadeAt))
                        {
                            v.FadeAt = Now + 1.25f;
                            v.FadeDuration = 2;
                            v.Life = Now - v.Born + 3.25f;
                        }
                    }
                    else
                    {
                        Destroy(v);
                        visuals.Remove(v);
                    }
                }

                stillPoint = local.Position;
                stillSince = Now;
                crowdAt = Now + 10;
            }

            if ((!pauseScares) && Now >= crowdAt && (Now - stillSince >= 10) && visuals.Count(v => v.Crowd) < 12 && Time
                .smoothDeltaTime < .05f)
            {
                crowdAt = Now + 3;
                StationaryCrowd();
            }

            if (magazineAsset != null && Now < magazineUntil && Now >= magazineAt && visuals.Count(v => v.Falling) < 6 && Time
                .smoothDeltaTime < .04f)
            {
                magazineAt = Now + .3f;
                var camera = PsychWorldScares.ViewCamera;
                var forward = camera != null ? camera.transform.forward : local.LookDirection;
                var start = (camera != null ? camera.transform.position : local.Position + Vector3.up * 1.5f)
                    + forward * .6f - Vector3.up * .3f;
                var v = CreateVisual(
                    magazineAsset.transform,
                    magazineAsset.GetComponentsInChildren<Renderer>(true),
                    start,
                    start,
                    1.8f,
                    false,
                    null
                );
                if (v != null)
                {
                    v.Static = v.Falling = true;
                    v.Velocity = forward * 2.5f + Vector3.up * Random.Range(.5f, 1.8f);
                }
            }
        }

        private void StationaryCrowd()
        {
            var local = Plugin.Local!;
            var donors = world!.RegisteredPlayers.OfType<Player>().Where(p => p.IsAI && p.HealthController?.IsAlive == true
                && p.PlayerBody != null && p.HandsController is Player.FirearmController).ToArray();
            if (donors.Length == 0)
            {
                Log("stage=stationary-crowd no-loaded-character");
                return;
            }

            Vector3 point;
            if (!(RearPoint(out point)))
            {
                Log("stage=stationary-crowd no-visible-front-placement");
                return;
            }

            var donor = donors[Random.Range(0, donors.Length)];
            var v = CreateVisual(donor.Transform.Original, ActorRenderers(donor), point, point, 3600, false, donor);
            if (v != null)
            {
                v.Static = v.Crowd = true;
                v.RearCrowd = true;
                var direction = local.Position - point;
                direction.y = 0;
                v.Root.transform.rotation = Quaternion.LookRotation(direction);
                Log("stage=stationary-crowd count=" + visuals.Count(v => v.Crowd));
            }
        }

        private void StopActionScares()
        {
            ClearNewEncounters();
            thrownGrenades.Clear();
            stillStarted = reloadSeen = false;
            musicAttempts = 0;
            magazineAsset = null;
            magazineUntil = nextMagazine = 0;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ThrowGrenade))]
    internal static class PsychGrenadeTrigger
    {
        private static void Postfix(Player __instance, ThrowWeap grenade, bool simulate, OperationResult<Player
            .GrenadeThrowResult> __result)
        {
            if (!simulate && !__result.Failed && __instance == Plugin.Local)
            {
                SchizophreniaDirector.Current?.GrenadeThrown(grenade.Id);
            }
        }
    }

    [HarmonyPatch]
    internal static class PsychReloadTrigger
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(Player.FirearmController).GetMethods().Where(m =>
             m.Name is "ReloadMag" or "QuickReloadMag");
        private static void Postfix(Player.FirearmController __instance, object[] __args)
        {
            if (Plugin.Local?.HandsController == __instance && __instance.IsInReloadOperation())
            {
                SchizophreniaDirector.Current?.Reloaded(__args.OfType<Magazine>().FirstOrDefault());
            }
        }
    }
}
