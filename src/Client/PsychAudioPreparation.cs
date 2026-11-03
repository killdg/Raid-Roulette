using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private readonly Queue<AudioClip> prepareAudio = new();
        private readonly HashSet<AudioClip> requestedAudio = new();
        private int preparedGunBanks;
        private bool gunPreviewPending;
        private float gunPreviewUntil;
        private float audioPrepareAt;
        private AssetBundle[]? audioBundles;
        private int audioBundleIndex;
        private readonly Queue<(AssetBundle Bundle, string Path)> fireworkAssets = new();
        private AssetBundleRequest? fireworkRequest;
        private void QueueAudioPreparation(AudioClip clip)
        {
            if (clip != null && requestedAudio.Count < 48 && clip.loadState == AudioDataLoadState.Unloaded && requestedAudio.Add(clip))
            {
                prepareAudio.Enqueue(clip);
            }
        }

        private void TickAudioPreparation()
        {
            TickFireworkAudioDiscovery();
            if (Now < audioPrepareAt || prepareAudio.Count == 0)
            {
                return;
            }

            audioPrepareAt = Now + .1f;
            var clip = prepareAudio.Dequeue();
            if (clip == null || clip.loadState != AudioDataLoadState.Unloaded)
            {
                return;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool accepted = clip.LoadAudioData();
            Log("stage=audio-prepare clip=" + clip.name + " accepted=" + accepted + " state=" + clip.loadState + " ms=" + timer
                .Elapsed.TotalMilliseconds.ToString("0.0"));
        }

        private void TickFireworkAudioDiscovery()
        {
            if (fireworkRequest != null)
            {
                if (!fireworkRequest.isDone)
                {
                    return;
                }

                if (fireworkRequest.asset is AudioClip clip)
                {
                    discoveredFireworkClips.Add(clip);
                    fireworkSounds = discoveredFireworkClips.ToArray();
                    QueueAudioPreparation(clip);
                    Log("stage=firework-audio-discovered clip=" + clip.name);
                }

                fireworkRequest = null;
            }

            if (fireworkAssets.Count > 0)
            {
                var asset = fireworkAssets.Dequeue();
                if (asset.Bundle != null)
                {
                    fireworkRequest = asset.Bundle.LoadAssetAsync<AudioClip>(asset.Path);
                }

                return;
            }

            if (!assetCacheComplete || fireworkSounds.Length > 0)
            {
                return;
            }

            if (audioBundles == null)
            {
                // Bundle metadata is distinct from searching every loaded Unity object.
                // Prioritize audio bundles, then inspect only one loaded bundle/frame.
                audioBundles = AssetBundle.GetAllLoadedAssetBundles().OrderByDescending(bundle => bundle.name.IndexOf("audio",
                     System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                Log("stage=firework-audio-metadata-start bundles=" + audioBundles.Length);
                return;
            }

            if (audioBundleIndex >= audioBundles.Length)
            {
                return;
            }

            var bundle = audioBundles[audioBundleIndex++];
            if (bundle == null || bundle.isStreamedSceneAssetBundle)
            {
                return;
            }

            foreach (string path in bundle.GetAllAssetNames())
            {
                if ((path.IndexOf("firework", System.StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("salut", System
                    .StringComparison.OrdinalIgnoreCase) >= 0) && (path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mp3", System
                    .StringComparison.OrdinalIgnoreCase)))
                {
                    fireworkAssets.Enqueue((bundle, path));
                }
            }

            if (audioBundleIndex == audioBundles.Length)
            {
                Log("stage=firework-audio-metadata-complete matches=" + fireworkAssets.Count);
            }
        }

        private void ClearAudioPreparation()
        {
            prepareAudio.Clear();
            requestedAudio.Clear();
            preparedGunBanks = 0;
            gunPreviewPending = false;
            audioPrepareAt = 0;
            audioBundles = null;
            audioBundleIndex = 0;
            fireworkAssets.Clear();
            // Do not unload native bundles or clips. An in-flight request may finish
            // normally; dropping its reference prevents callbacks after raid cleanup.
            fireworkRequest = null;
        }
    }
}
