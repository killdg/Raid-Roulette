using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class BlueDaySiren
    {
        private Task<WavePcm>? loading;
        private AudioClip? clip;
        private GameObject? emitter;
        private float playAt;
        private float volume;
        private bool finished;
        internal void Tick(double elapsed)
        {
            if (finished)
            {
                return;
            }

            if (loading == null)
            {
                playAt = UnityEngine.Random.Range(20f, 40f);
                volume = UnityEngine.Random.Range(.2f, .4f);
                string path = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "Audio", "AirSiren.wav");
                // One finite local file read/decode per Blue Day raid, off the
                // Unity thread. No network request or recurring background job.
                // Unity clip creation and playback remain on the main thread.
                loading = Task.Run(() => WavePcm.Read(path));
            }

            if (!loading.IsCompleted)
            {
                return;
            }

            if (loading.IsFaulted || loading.IsCanceled)
            {
                Plugin.Trace("[RR-BLUE-DAY] siren-load-failed=" + loading.Exception?.GetBaseException().Message);
                finished = true;
                return;
            }

            if (clip == null)
            {
                WavePcm wave = loading.Result;
                clip = AudioClip.Create("RR_BlueDayAirSiren", wave.Samples.Length / wave.Channels, wave.Channels, wave.Rate, false);
                if (!clip.SetData(wave.Samples, 0))
                {
                    Plugin.Trace("[RR-BLUE-DAY] siren-upload-failed");
                    finished = true;
                    return;
                }
            }

            if (elapsed < playAt)
            {
                return;
            }

            emitter = new GameObject("RR_BlueDayWarning");
            AudioSource source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.clip = clip;
            source.Play();
            finished = true;
            Plugin.Trace($"[RR-BLUE-DAY] siren-start seconds={elapsed:0.0} gain={volume:0.00}");
        }

        internal void Clear()
        {
            // Both objects belong only to this local raid. Destroying the emitter
            // stops playback on exit, including leaving before the warning ends.
            if (emitter != null)
            {
                UnityEngine.Object.Destroy(emitter);
            }

            if (clip != null)
            {
                UnityEngine.Object.Destroy(clip);
            }

            emitter = null;
            clip = null;
            loading = null;
            finished = false;
        }
    }
}
