using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    internal sealed class StormAudioAssets
    {
        private sealed class Loaded
        {
            public string Name = "";
            public string Error = "";
            public WavePcm? Wave;
        }

        private readonly string[] names =
        {
            "Thunder1.wav",
            "ThunderReplacement.wav",
            "Thunder3.wav",
            "HeavyRain.wav"
        };
        private Task<Loaded[]>? loading;
        private int uploaded;
        private readonly AudioClip? [] clips = new AudioClip? [4];
        public AudioClip[] Thunder { get; private set; } = Array.Empty<AudioClip>();
        public AudioClip? Rain => clips[3];

        public void Tick()
        {
            if (loading == null)
            {
                string folder = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "Audio");
                loading = Task.Run(() => names.Select(name =>
                {
                    try
                    {
                        return new Loaded
                        {
                            Name = name,
                            Wave = WavePcm.Read(Path.Combine(folder, name))
                        };
                    }
                    catch (Exception e)
                    {
                        return new Loaded
                        {
                            Name = name,
                            Error = e.GetBaseException().Message
                        };
                    }
                }).ToArray());
            }

            if (!loading.IsCompleted || uploaded >= clips.Length)
            {
                return;
            }

            if (loading.IsFaulted)
            {
                Plugin.Trace("[RR-STORM-AUDIO] load-error=" + loading.Exception?.GetBaseException().Message);
                uploaded = clips.Length;
                return;
            }

            var item = loading.Result[uploaded];
            if (item.Wave is WavePcm wave)
            {
                var clip = AudioClip.Create(item.Name, wave.Samples.Length / wave.Channels, wave.Channels, wave.Rate, false);
                if (!clip.SetData(wave.Samples, 0))
                {
                    Object.Destroy(clip);
                    Plugin.Trace("[RR-STORM-AUDIO] upload-failed=" + item.Name);
                }
                else
                {
                    clips[uploaded] = clip;
                    Plugin.Trace("[RR-STORM-AUDIO] loaded=" + item.Name + " seconds=" + clip.length.ToString("0.0"));
                }

                item.Wave = null;
            }
            else
            {
                Plugin.Trace("[RR-STORM-AUDIO] missing=" + item.Name + " error=" + item.Error);
            }

            uploaded++;
            if (uploaded >= 2)
            {
                Thunder = clips.Take(3).Where(c => c != null).Cast<AudioClip>().ToArray();
            }
        }

        public void Dispose()
        {
            foreach (var clip in clips)
            {
                if (clip != null)
                {
                    Object.Destroy(clip);
                }
            }

            Thunder = Array.Empty<AudioClip>();
        }
    }
}
