using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Weather;
using BepInEx.Configuration;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    internal sealed class StormtideDirector
    {
        private GameWorld? world;
        private WeatherController? weather;
        private WeatherDebug? debug;
        private GameDateTime? clock;
        private TOD_Time? sky;
        private WeatherDebug.Direction direction;
        private Vector2 topWind;
        private float temperature;
        private bool enabled;
        private bool locked;
        private float rain;
        private float cloud;
        private float wind;
        private float fog;
        private float thunder;
        private float factor;
        private float mod;
        private float realtime;
        private float nextTick;
        private float nextScan;
        private float nextLog;
        private DateTime date;
        private AudioSource[] sources = Array.Empty<AudioSource>();
        private readonly StormAudioAssets audioAssets = new();
        private readonly StormtideThunder thunderFx;
        private readonly StormtideRecordedRain recordedRain;
        private ConfigEntry<bool>? rainEnabled;
        private ConfigEntry<bool>? lightFlicker;
        private readonly LightningLightFlicker lighting = new();
        private ConfigEntry<float>? outdoorRainVolume;
        private ConfigEntry<float>? indoorRainVolume;
        private ConfigEntry<float>? outdoorGameplayVolume;
        private ConfigEntry<float>? indoorGameplayVolume;
        private double raidElapsed;
        private readonly HurricaneDirector hurricane = new();
        private ConfigEntry<bool>? hurricaneEnabled;
        private ConfigEntry<float>? hurricaneChance;
        private bool hurricaneRolled;
        private bool hurricaneSelected;
        public StormtideDirector()
        {
            thunderFx = new(
                audioAssets,
                () =>
            {
                if (lightFlicker?.Value == true)
                {
                    lighting.Strike();
                }
            }
            );
            recordedRain = new(audioAssets);
        }

        public void Bind(ConfigFile config)
        {
            hurricaneEnabled = config.Bind(
                "8. Stormtide",
                "Night hurricane effects",
                true,
                "Allow cosmetic wind, outdoor eye/goggle drops and night-only outdoor debris during Stormtide."
            );
            hurricaneChance = config.Bind(
                "8. Stormtide",
                "Night hurricane chance (%)",
                30f,
                new ConfigDescription(
                    "One roll per Stormtide raid at the first nighttime moment (20:00–06:00). "
                        + "No daytime hurricane effects. Outdoor debris remains visible through windows while indoors.",
                    new AcceptableValueRange<float>(0, 100)
                )
            );
            lightFlicker = config.Bind(
                "8. Stormtide",
                "Night lightning flicker",
                true,
                AdvancedMenu.Diagnostic("Flicker controllable map lights on Stormtide lightning strikes at night. Baked lights and "
                    + "player equipment are excluded; original brightness is restored.")
            );
            rainEnabled = config.Bind(
                "8. Stormtide",
                "Recorded heavy rain",
                true,
                AdvancedMenu.Diagnostic("Crossfade from native rain to the bundled heavy rain recording over the five-minute storm "
                    + "buildup. Missing recordings retain native rain.")
            );
            outdoorRainVolume = SoundPercentage(config, "Outdoor rain volume (%)", 70f);
            indoorRainVolume = SoundPercentage(config, "Indoor rain volume (%)", 40f);
            outdoorGameplayVolume = SoundPercentage(config, "Outdoor gameplay volume (%)", 25f);
            indoorGameplayVolume = SoundPercentage(config, "Indoor gameplay volume (%)", 60f);
            MenuAction.Bind(
                config,
                "8. Stormtide",
                "Test thunder and lightning",
                "Play a test strike after five minutes in an active Stormtide raid. Waits for bundled recordings to finish loading.",
                () =>
            {
                if (clock != null && raidElapsed >= 300)
                {
                    thunderFx.RequestTest();
                }
                else
                {
                    Plugin.Trace("[RR-THUNDER] test requires five minutes in an active Stormtide raid ");
                }
            },
                new ConfigurationManagerAttributes { HideDefaultButton = true, CustomDrawer = entry =>
            {
                if (GUILayout.Button("Play test strike"))
                {
                    entry.BoxedValue = true;
                }
            } }
            );
        }

        private static ConfigEntry<float> SoundPercentage(ConfigFile config, string name, float defaultValue)
        {
            return config.Bind(
                "8. Stormtide",
                name,
                defaultValue,
                new ConfigDescription("Maximum storm sound level. Shelter transitions are smoothed.",
                     new AcceptableValueRange<float>(0f, 100f))
            );
        }

        public void Dispose()
        {
            Stop();
            lighting.Dispose();
            hurricane.Dispose();
            audioAssets.Dispose();
        }

        public void LateTick()
        {
            if (lightFlicker?.Value == true)
            {
                lighting.LateTick();
            }
            else
            {
                lighting.Stop();
            }
        }

        public string Status { get; private set; } = "";

        public void Stop()
        {
            lighting.Stop();
            hurricane.Stop();
            thunderFx.Stop();
            recordedRain.Stop();
            StormtideSound.Stop();
            sources = Array.Empty<AudioSource>();
            if (debug != null)
            {
                debug.WindDirection = direction;
                debug.TopWindDirection = topWind;
                debug.Temperature = temperature;
                debug.Rain = rain;
                debug.CloudDensity = cloud;
                debug.WindMagnitude = wind;
                debug.Fog = fog;
                debug.LightningThunderProbability = thunder;
                debug.Enabled = enabled;
                debug = null;
            }

            if (clock != null)
            {
                clock.TimeFactor = factor;
                clock.TimeFactorMod = mod;
                clock.ResetForce(date.AddSeconds((Time.realtimeSinceStartup - realtime) * factor * mod));
                clock = null;
            }

            if (sky != null)
            {
                sky.LockCurrentTime = locked;
            }

            sky = null;
            weather = null;
            Status = "";
        }

        public void Tick()
        {
            try
            {
                Core();
            }
            catch (Exception e)
            {
                Plugin.Trace("[RR-STORM] error=" + e.GetBaseException().Message);
                Stop();
                nextTick = Time.unscaledTime + 10;
            }
        }

        private void Core()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(world, current))
            {
                lighting.Dispose();
                hurricane.Dispose();
                hurricaneRolled = false;
                hurricaneSelected = false;
                Stop();
                world = current;
                nextTick = nextScan = nextLog = 0;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (current == null || !Plugin.RaidContext.EndsWith(": stormtide", StringComparison.Ordinal) || game?.GameTimer?
                .StartDateTime == null || Plugin.Local?.HealthController?.IsAlive != true || game.Status is GameStatus
                .Stopped or GameStatus.Stopping or GameStatus.SoftStopping)
            {
                Stop();
                return;
            }

            if (WeatherController.Instance == null || !TODSkyProvider.IsAvailable || TODSkyProvider.Instance?.CurrentTime?
                .GameDateTime == null)
            {
                return;
            }

            raidElapsed = game.GameTimer.PastTime.TotalSeconds;
            float progress = StormtidePolicy.Progress(raidElapsed, game.GameTimer.SessionTime?.TotalSeconds ?? 0);
            if (lightFlicker?.Value == true)
            {
                lighting.Discover();
            }
            else
            {
                lighting.Stop();
            }

            audioAssets.Tick();
            if (rainEnabled?.Value == true)
            {
                recordedRain.Tick(progress, (outdoorRainVolume?.Value ?? 70f) / 100f, (indoorRainVolume?.Value ?? 40f) / 100f);
            }
            else
            {
                recordedRain.Stop();
            }

            // The roll belongs to this GameWorld, never to a frame or doorway crossing.
            // Stormtide can reach night after a daytime start; wait until then to roll.
            bool night = HurricanePolicy.NightDebris(TODSkyProvider.Instance.Cycle.Hour);
            if (night && hurricaneEnabled?.Value == true && !hurricaneRolled)
            {
                hurricaneRolled = true;
                hurricaneSelected = UnityEngine.Random.value < (hurricaneChance?.Value ?? 30f) / 100f;
                Plugin.Trace("[RR-STORM] night hurricane selected=" + hurricaneSelected);
            }

            if (night && hurricaneEnabled?.Value == true && hurricaneSelected)
            {
                hurricane.Tick(progress, raidElapsed, RainController.IsCameraUnderRain ? 1 : 0, 48, true);
            }
            else
            {
                hurricane.Stop();
            }

            thunderFx.Tick(progress, raidElapsed);
            if (Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + 0.5f;
            if (clock == null)
            {
                weather = WeatherController.Instance;
                debug = weather.WeatherDebug;
                enabled = debug.Enabled;
                rain = debug.Rain;
                cloud = debug.CloudDensity;
                wind = debug.WindMagnitude;
                fog = debug.Fog;
                thunder = debug.LightningThunderProbability;
                direction = debug.WindDirection;
                topWind = debug.TopWindDirection;
                temperature = debug.Temperature;
                if (!enabled)
                {
                    debug.CopyParams(weather.WeatherCurve);
                }

                sky = TODSkyProvider.Instance.CurrentTime;
                clock = sky.GameDateTime;
                factor = clock.TimeFactor;
                mod = clock.TimeFactorMod;
                locked = sky.LockCurrentTime;
                date = clock.Calculate();
                realtime = Time.realtimeSinceStartup;
                Plugin.Trace("[RR-STORM] stage=started fullStormAfterSeconds=300 nightStart=" + StormtidePolicy.IsNight(date));
            }

            debug!.Enabled = true;
            debug.Rain = Mathf.Lerp(0.05f, 1, progress);
            debug.CloudDensity = Mathf.Lerp(-0.2f, 1, progress);
            debug.WindMagnitude = Mathf.Lerp(0.1f, 0.8f, progress);
            debug.Fog = Mathf.Lerp(0.001f, 0.012f, progress);
            debug.LightningThunderProbability = progress * 0.7f;
            clock.TimeFactor = 0;
            clock.TimeFactorMod = 1;
            sky!.LockCurrentTime = false;
            clock.ResetForce(StormtidePolicy.ClockAt(date, progress));
            float gameplayLevel = Mathf.Lerp(
                (indoorGameplayVolume?.Value ?? 60f) / 100f,
                (outdoorGameplayVolume?.Value ?? 25f) / 100f,
                recordedRain.Exposure
            );
            StormtideSound.Gain = Mathf.Lerp(1f, gameplayLevel, progress);
            StormtideSound.RainGain = recordedRain.Playing ? (float)StormAudioPolicy.NativeRain(recordedRain.Blend)
                : StormtidePolicy.RainSoundGain(progress);
            StormtideSound.Active = true;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 5;
                sources = Object.FindObjectsOfType<AudioSource>();
            }

            foreach (var source in sources)
            {
                StormtideSound.Apply(source);
            }

            Status = $"\nStormtide: {progress * 100:0}% storm, time {clock.Calculate():HH:mm}, native rain " +
                $"{StormtideSound.RainGain * 100:0}%, recorded rain {StormAudioPolicy.RecordedRain(recordedRain.Blend) * 100:0}" +
                $"%, other sounds {StormtideSound.Gain * 100:0}%";
            if (Time.unscaledTime >= nextLog)
            {
                nextLog = Time.unscaledTime + 30;
                Plugin.Trace("[RR-STORM] " + Status);
            }
        }
    }
}
