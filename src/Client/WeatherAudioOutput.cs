using Comfort.Common;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal static class WeatherAudioOutput
    {
        // Do not inherit a pooled source's route: it may retain an occluded/muted
        // group until native Play explicitly assigns the source group's mixer.
        // Diagnostics describe playback locally in BepInEx logs; they are not uploaded.
        internal static string Describe(AudioSource source) =>
             $"playing={source.isPlaying} enabled={source.enabled} active={source.gameObject.activeInHierarchy} "
            + $"mute={source.mute} virtual={source.isVirtual} pitch={source.pitch:0.00} priority={source.priority} "
            + $"clipState={source.clip?.loadState} samples={source.clip?.samples} dsp={AudioSettings.dspTime:0.000}";
        internal static bool Attach(AudioSource source)
        {
            if (!Singleton<BetterAudio>.Instantiated || Singleton<BetterAudio>.Instance.MasterMixerGroup == null)
            {
                return false;
            }

            var group = Singleton<BetterAudio>.Instance.MasterMixerGroup;
            if (source.outputAudioMixerGroup != group)
            {
                source.outputAudioMixerGroup = group;
                source.spatialBlend = 0;
                source.spatialize = false;
                Plugin.Trace("[ROT-AUDIO] output=" + source.gameObject.name + " mixer=" + group.name);
            }

            source.enabled = true;
            source.mute = false;
            source.pitch = 1;
            source.priority = 16;
            // Keep procedural weather near the active player as well as using a 2D route.
            // This also avoids origin-distance handling by native audio extensions.
            if (Singleton<EFT.GameWorld>.Instantiated && Singleton<EFT.GameWorld>.Instance.MainPlayer is { } player)
            {
                source.transform.position = player.Position;
            }

            return true;
        }
    }
}
