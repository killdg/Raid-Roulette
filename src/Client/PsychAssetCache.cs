using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Diz.Resources;
using EFT;
using EFT.Interactive;
using EFT.UI;
using EFT.Airdrop;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private bool assetCacheComplete;
        private IEasyBundle[] cachedBundles = Array.Empty<IEasyBundle>();
        private int bundleIndex;
        private int bundleAssetIndex;
        private readonly List<Door> discoveredDoors = new();
        private readonly HashSet<GrenadePrefab> discoveredGrenades = new();
        private readonly HashSet<AudioClip> discoveredGlassClips = new();
        private readonly HashSet<AudioClip> discoveredFireworkClips = new();
        private float publishAssetsAt;
        private readonly HashSet<SoundBank> cachedWeaponBanks = new();
        private void ObserveSceneObject(Transform transform)
        {
            // Reuse the scene walk already limited to about one millisecond/frame.
            // FindObjectsOfType traversed the entire loaded world in one native call.
            var door = transform.GetComponent<Door>();
            if (door != null)
            {
                discoveredDoors.Add(door);
            }

            if (effects == null)
            {
                effects = transform.GetComponent<Systems.Effects.Effects>();
            }
        }

        private void PublishSceneAssets()
        {
            if (discoveredDoors.Count != doors.Length && Now >= publishAssetsAt)
            {
                publishAssetsAt = Now + 1;
                doors = discoveredDoors.ToArray();
            }
        }

        private void CacheLoadedAssetsIncrementally()
        {
            if (cachedBundles.Length == 0)
            {
                if (!Singleton<IEasyAssets>.Instantiated)
                {
                    return;
                }

                // Snapshot managed bundle references only. Assets reads do not load
                // bundles or search Unity's complete object registry.
                cachedBundles = Singleton<IEasyAssets>.Instance.System.Nodes.Values.Select(node => node.Data).ToArray();
                if (Singleton<GUISounds>.Instantiated)
                {
                    music = (AccessTools.Field(typeof(GUISounds), "_mainMenuMusic")?.GetValue(Singleton<GUISounds>
                        .Instance) as AudioClip[] ?? Array.Empty<AudioClip>()).Where(clip => clip != null && clip.length > 20).ToArray();
                }

                Log("stage=incremental-assets-start bundles=" + cachedBundles.Length);
                return;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            int visited = 0;
            while (bundleIndex < cachedBundles.Length && visited < 64 && timer.Elapsed.TotalMilliseconds < .75)
            {
                var bundle = cachedBundles[bundleIndex];
                var assets = bundle.Assets;
                if (assets == null || bundleAssetIndex >= assets.Length)
                {
                    bundleIndex++;
                    bundleAssetIndex = 0;
                    visited++;
                    continue;
                }

                var asset = assets[bundleAssetIndex++];
                visited++;
                if (asset is SoundBank bank)
                {
                    CacheSoundBank(bank);
                }

                if (asset is GameObject soundPrefab)
                {
                    CachePrefabSounds(soundPrefab);
                }

                if (asset is AudioClip clip)
                {
                    string name = clip.name;
                    if (clip.length < 12 && name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 && (name.IndexOf("break",
                         StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("shat", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        discoveredGlassClips.Add(clip);
                    }

                    if (clip.length < 20 && (name.IndexOf("firework", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("salut",
                         StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        discoveredFireworkClips.Add(clip);
                    }
                }

                // Inspect only already-loaded weapon prefabs; this replaces the
                // global WeaponSoundPlayer fallback that could stall rapid fire.
                if (asset is GameObject weaponPrefab && bundle.Key.IndexOf("weapons/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var sound = weaponPrefab.GetComponentInChildren<WeaponSoundPlayer>(true);
                    if (sound != null && sound.Body != null)
                    {
                        CacheSoundBank(sound.Body);
                    }
                }

                // Only inspect loaded grenade bundles, rather than every prefab.
                if (asset is GameObject prefab && bundle.Key.IndexOf("grenad", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var grenade = prefab.GetComponentInChildren<GrenadePrefab>(true);
                    if (grenade != null)
                    {
                        discoveredGrenades.Add(grenade);
                    }
                }
            }

            if (bundleIndex >= cachedBundles.Length)
            {
                grenadePrefabs = discoveredGrenades.ToArray();
                glassSounds = discoveredGlassClips.ToArray();
                fireworkSounds = discoveredFireworkClips.ToArray();
                assetCacheComplete = true;
                Log("stage=incremental-assets-complete grenadeModels=" + grenadePrefabs.Length + " glassSounds=" + glassSounds
                    .Length + " fireworkSounds=" + fireworkSounds.Length + " weaponBanks=" + cachedWeaponBanks.Count);
            }
        }

        private void CachePrefabSounds(GameObject prefab)
        {
            // Native firework banks can be fields on a prefab component rather than
            // named standalone bundle assets. Inspect only this loaded prefab.
            foreach (var sound in prefab.GetComponentsInChildren<FireworkFlarePatronSound>(true))
            {
                var bank = sound._fireworkShotBank;
                if (bank == null)
                {
                    continue;
                }

                foreach (var environment in bank.Environments ?? Array.Empty<EnvironmentVariety>())
                {
                    if (environment?.Clips == null)
                    {
                        continue;
                    }

                    foreach (var distance in environment.Clips)
                    {
                        if (distance?.Clips == null)
                        {
                            continue;
                        }

                        foreach (var clip in distance.Clips)
                        {
                            if (clip != null && clip.length < 20)
                            {
                                discoveredFireworkClips.Add(clip);
                                QueueAudioPreparation(clip);
                            }
                        }
                    }
                }
            }

            foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
            {
                var clip = source.clip;
                if (clip != null && (clip.name.IndexOf("firework", StringComparison.OrdinalIgnoreCase) >= 0 || clip.name
                    .IndexOf("salut", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    discoveredFireworkClips.Add(clip);
                    QueueAudioPreparation(clip);
                }
            }
        }

        private void CacheSoundBank(SoundBank bank)
        {
            // Sound banks are ScriptableObjects whose clips are referenced inside
            // environment/distance variants, not top-level bundle AudioClip assets.
            if (bank.SourceType == BetterAudio.AudioSourceGroupType.Gunshots || bank.name.IndexOf("_main", StringComparison
                .OrdinalIgnoreCase) >= 0)
            {
                cachedWeaponBanks.Add(bank);
                if (preparedGunBanks < 8)
                {
                    foreach (var environment in bank.Environments ?? Array.Empty<EnvironmentVariety>())
                    {
                        var clip = environment?.Clips?.FirstOrDefault()?.Clips?.FirstOrDefault(c => c != null);
                        if (clip == null)
                        {
                            continue;
                        }

                        QueueAudioPreparation(clip);
                    }

                    preparedGunBanks++;
                }
            }

            bool glass = bank.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;
            bool firework = bank.name.IndexOf("firework", StringComparison.OrdinalIgnoreCase) >= 0 || bank.name
                .IndexOf("salut", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var environment in bank.Environments ?? Array.Empty<EnvironmentVariety>())
            {
                if (environment?.Clips == null)
                {
                    continue;
                }

                foreach (var distance in environment.Clips)
                {
                    if (distance?.Clips == null)
                    {
                        continue;
                    }

                    foreach (var clip in distance.Clips)
                    {
                        if (clip == null)
                        {
                            continue;
                        }

                        string name = clip.name;
                        if (clip.length < 12 && (glass || name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0) && (name
                            .IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("shat", StringComparison
                            .OrdinalIgnoreCase) >= 0 || bank.name.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            discoveredGlassClips.Add(clip);
                        }

                        if (clip.length < 20 && (firework || name.IndexOf("firework", StringComparison.OrdinalIgnoreCase) >= 0 || name
                            .IndexOf("salut", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            discoveredFireworkClips.Add(clip);
                        }
                    }
                }
            }
        }

        private void ResetAssetCache()
        {
            assetCacheComplete = false;
            cachedBundles = Array.Empty<IEasyBundle>();
            bundleIndex = bundleAssetIndex = 0;
            publishAssetsAt = 0;
            discoveredDoors.Clear();
            discoveredGrenades.Clear();
            discoveredGlassClips.Clear();
            discoveredFireworkClips.Clear();
            cachedWeaponBanks.Clear();
        }
    }
}
