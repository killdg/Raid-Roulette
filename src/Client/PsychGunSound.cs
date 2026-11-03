using System;
using System.Linq;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private readonly RapidSoundPolicy rapidSound = new();
        private SoundBank[] phantomBanks = Array.Empty<SoundBank>();
        private float bankScanAt;
        private readonly AudioSource? [] gunChannels = new AudioSource? [4];
        private readonly float[] gunChannelEnds = new float[4];
        private int gunChannel;
        private string lastPhantomBank = "";
        private float nextGunDiagnostic;
        private bool OverrideGunSound(WeaponSoundPlayer source, bool force = false)
        {
            var player = Plugin.Local;
            if (!active || player?.HandsController is not Player.FirearmController firearm || firearm.WeaponSoundPlayer != source)
            {
                return false;
            }

            if (!force && pauseScares)
            {
                GunDiagnostic("paused-in-F2");
                return false;
            }

            if (!force && !rapidSound.Shot(Now))
            {
                return false;
            }

            if (Now >= bankScanAt)
            {
                bankScanAt = Now + 5;
                phantomBanks = world!.RegisteredPlayers.OfType<Player>().Where(actor => actor.HandsController is Player
                    .FirearmController).Select(actor => ((Player.FirearmController)actor.HandsController).WeaponSoundPlayer)
                    .Where(sound => sound != null && sound != source && sound.Body != null && !string.Equals(sound.Body.name, source
                    .Body?.name, StringComparison.OrdinalIgnoreCase)).Select(sound => sound.Body).GroupBy(bank => bank.name)
                    .Select(group => group.First()).ToArray();
            }

            if (phantomBanks.Length == 0)
            {
                // Filled by the bounded startup bundle walk; no global Unity search
                // is allowed on the firing path.
                phantomBanks = cachedWeaponBanks.Where(bank => bank != null && !string.Equals(bank.name, source.Body?.name,
                     StringComparison.OrdinalIgnoreCase)).GroupBy(bank => bank.name).Select(group => group.First()).ToArray();
            }

            if (phantomBanks.Length == 0)
            {
                GunDiagnostic("no-alternative-banks cache=" + cachedWeaponBanks.Count);
                return false;
            }

            var choices = phantomBanks.Where(bank => bank != null && bank.name != lastPhantomBank).ToArray();
            if (choices.Length == 0)
            {
                choices = phantomBanks;
            }

            SoundBank? bank = null;
            AudioClip? clip = null;
            int start = UnityEngine.Random.Range(0, choices.Length);
            for (int attempt = 0; attempt < Math.Min(choices.Length, 8); attempt++)
            {
                var candidate = choices[(start + attempt) % choices.Length];
                AudioClip first = null!;
                AudioClip second = null!;
                float balance = 0;
                try
                {
                    candidate.PickClipsByDistance(ref first, ref second, ref balance, (int)player.Environment, 0);
                }
                catch (Exception error)
                {
                    GunDiagnostic("bank-error=" + candidate.name + " detail=" + error.GetBaseException().Message);
                    continue;
                }

                if (first != null)
                {
                    QueueAudioPreparation(first);
                }

                if (second != null)
                {
                    QueueAudioPreparation(second);
                }

                var selected = first != null && first.loadState == AudioDataLoadState.Loaded ? first : second;
                if (selected == null || selected.loadState != AudioDataLoadState.Loaded)
                {
                    GunDiagnostic("clip-not-ready bank=" + candidate.name + " first=" + (first != null ? first.name + ":" + first
                        .loadState : "null") + " second=" + (second != null ? second.name + ":" + second.loadState : "null"));
                    continue;
                }

                bank = candidate;
                clip = selected;
                break;
            }

            if (bank == null || clip == null || !Singleton<BetterAudio>.Instantiated)
            {
                GunDiagnostic("no-playable-loaded-clip banks=" + choices.Length);
                return false;
            }

            int channel = gunChannel++ % gunChannels.Length;
            var audio = gunChannels[channel];
            if (audio == null)
            {
                var root = new GameObject("RR_PhantomGunAudio");
                audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0;
                audio.dopplerLevel = 0;
                gunChannels[channel] = audio;
            }

            audio.Stop();
            audio.outputAudioMixerGroup = Singleton<BetterAudio>.Instance.GunshotMixerGroup;
            audio.volume = Mathf.Clamp(bank.BaseVolume, 0.25f, 1);
            audio.clip = clip;
            audio.Play();
            gunChannelEnds[channel] = Now + Mathf.Min(clip.length, .25f);
            lastPhantomBank = bank.name;
            // FireBullet is native audio only. Release its old loop and skip that
            // audio call after replacement playback starts. Real firing/ammo/damage
            // remain on the original firearm path and are never patched here.
            source.Release();
            AccessTools.Field(typeof(WeaponSoundPlayer), "_isFiring").SetValue(source, false);
            if (force || rapidSound.Count == 11)
            {
                Log("stage=rapid-gun-sound audible-replacement=true bank=" + bank.name + " clip=" + clip.name);
            }

            return true;
        }

        private void GunDiagnostic(string reason)
        {
            if (Now < nextGunDiagnostic)
            {
                return;
            }

            nextGunDiagnostic = Now + 10;
            Log("stage=rapid-gun-audio reason=" + reason);
        }

        private void TestGunSound()
        {
            var source = (Plugin.Local?.HandsController as Player.FirearmController)?.WeaponSoundPlayer;
            if (source == null || !OverrideGunSound(source, true))
            {
                gunPreviewPending = source != null;
                gunPreviewUntil = Now + 8;
                panelMessage = "Preparing alternate clips; preview will play when ready (up to eight seconds).";
            }
        }

        private void TickGunSounds()
        {
            TickAudioPreparation();
            if (gunPreviewPending)
            {
                var source = (Plugin.Local?.HandsController as Player.FirearmController)?.WeaponSoundPlayer;
                if (source == null || Now > gunPreviewUntil || OverrideGunSound(source, true))
                {
                    gunPreviewPending = false;
                }
            }

            for (int index = 0; index < gunChannels.Length; index++)
            {
                if (gunChannels[index] != null && Now >= gunChannelEnds[index])
                {
                    gunChannels[index]!.Stop();
                }
            }
        }

        private void StopGunSounds()
        {
            foreach (var channel in gunChannels)
            {
                if (channel != null)
                {
                    Object.Destroy(channel.gameObject);
                }
            }

            ClearAudioPreparation();
            Array.Clear(gunChannels, 0, gunChannels.Length);
            gunChannel = 0;
            lastPhantomBank = "";
        }

        internal static bool PrepareGunSound(WeaponSoundPlayer source)
        {
            try
            {
                return Current?.OverrideGunSound(source) == true;
            }
            catch (Exception exception)
            {
                Plugin.Trace("[RR-PSYCH] stage=rapid-gun-audio fallback-native=" + exception.GetBaseException().Message);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(WeaponSoundPlayer), nameof(WeaponSoundPlayer.FireBullet))]
    internal static class PsychRapidGunSound
    {
        private static bool Prefix(WeaponSoundPlayer __instance) => !SchizophreniaDirector.PrepareGunSound(__instance);
    }
}
