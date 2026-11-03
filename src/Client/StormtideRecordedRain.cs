using Comfort.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    internal sealed class StormtideRecordedRain(StormAudioAssets assets)
    {
        private GameObject? root;
        private AudioSource? source;
        private AudioLowPassFilter? shelterFilter;
        private double exposure;
        public float Blend { get; private set; }
        public float Exposure => (float)exposure;
        public bool Playing => source != null;

        public void Tick(float progress, float outdoorVolume, float indoorVolume)
        {
            exposure = StormAudioPolicy.ShelterStep(exposure, RainController.IsCameraUnderRain, Time.unscaledDeltaTime);
            if (assets.Rain == null)
            {
                return;
            }

            if (root == null)
            {
                root = new GameObject(StormAudioPolicy.RainSourceName);
                source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                source.volume = 0;
                source.clip = assets.Rain;
                shelterFilter = root.AddComponent<AudioLowPassFilter>();
                shelterFilter.lowpassResonanceQ = 1;
                if (Singleton<BetterAudio>.Instantiated)
                {
                    var native = Singleton<BetterAudio>.Instance.GetSource(BetterAudio.AudioSourceGroupType.Environment);
                    if (native != null)
                    {
                        source.outputAudioMixerGroup = native.source1.outputAudioMixerGroup;
                        native.Release();
                    }
                }

                source.Play();
            }

            Blend = Mathf.MoveTowards(Blend, Mathf.Clamp01(progress), Time.unscaledDeltaTime * 0.5f);
            // Native shelter detection drives both balances without a hard doorway cutoff.
            shelterFilter!.cutoffFrequency = (float)StormAudioPolicy.ShelterCutoff(exposure);
            source!.volume = (float)StormAudioPolicy.RecordedRain(Blend) * Mathf.Lerp(Mathf.Clamp01(indoorVolume), Mathf
                .Clamp01(outdoorVolume), Exposure);
        }

        public void Stop()
        {
            if (source != null)
            {
                source.Stop();
            }

            if (root != null)
            {
                Object.Destroy(root);
            }

            root = null;
            source = null;
            shelterFilter = null;
            exposure = 0;
            Blend = 0;
        }
    }
}
