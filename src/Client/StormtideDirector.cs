using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Weather;
using BepInEx.Configuration;
using UnityEngine;
using Object=UnityEngine.Object;
namespace RaidRoulette.TestingClient;
internal sealed class StormtideDirector
{
 private GameWorld? world;private WeatherController? weather;private WeatherDebug? debug;private GameDateTime? clock;private TOD_Time? sky;
 private WeatherDebug.Direction direction;private Vector2 topWind;private float temperature;
 private bool enabled,locked;private float rain,cloud,wind,fog,thunder,factor,mod,realtime,nextTick,nextScan,nextLog;private DateTime date;
 private AudioSource[] sources=Array.Empty<AudioSource>();
 private readonly StormAudioAssets audioAssets=new();private readonly StormtideThunder thunderFx;private readonly StormtideRecordedRain recordedRain;
 private ConfigEntry<bool>? thunderEnabled,rainEnabled;private ConfigEntry<float>? rainVolume;private double raidElapsed;
 public StormtideDirector(){thunderFx=new(audioAssets);recordedRain=new(audioAssets);}
 public void Bind(ConfigFile config)
 {
  thunderEnabled=config.Bind("8. Stormtide","Thunder and lightning",true,"Recreated light flashes and random supplied thunder recordings during Stormtide. No live strikes or damage. Disable to keep only rain and darkness.");
  rainEnabled=config.Bind("8. Stormtide","Recorded heavy rain",true,"Crossfade from native rain to the bundled heavy rain recording over the five-minute storm buildup. Missing recordings retain native rain.");
  rainVolume=config.Bind("8. Stormtide","Recorded rain volume",1f,new ConfigDescription("Heavy rain recording volume, before the native environment mixer. Indoors it is quieter.",new AcceptableValueRange<float>(0,1)));
  MenuAction.Bind(config,"8. Stormtide","Test thunder and lightning","Play a test strike after five minutes in an active Stormtide raid. Waits for bundled recordings to finish loading.",()=>{if(clock!=null&&raidElapsed>=300&&thunderEnabled.Value)thunderFx.RequestTest();else Plugin.Trace("[RR-THUNDER] test requires five minutes in an active Stormtide raid with thunder enabled");},new ConfigurationManagerAttributes{HideDefaultButton=true,CustomDrawer=entry=>{if(GUILayout.Button("Play test strike"))entry.BoxedValue=true;}});
 }
 public void Dispose(){Stop();audioAssets.Dispose();}
 public string Status{get;private set;}="";
 public void Stop()
 {
  thunderFx.Stop();recordedRain.Stop();StormtideSound.Stop();sources=Array.Empty<AudioSource>();
  if(debug!=null){debug.WindDirection=direction;debug.TopWindDirection=topWind;debug.Temperature=temperature;debug.Rain=rain;debug.CloudDensity=cloud;debug.WindMagnitude=wind;debug.Fog=fog;debug.LightningThunderProbability=thunder;debug.Enabled=enabled;debug=null;}
  if(clock!=null){clock.TimeFactor=factor;clock.TimeFactorMod=mod;clock.ResetForce(date.AddSeconds((Time.realtimeSinceStartup-realtime)*factor*mod));clock=null;}
  if(sky!=null)sky.LockCurrentTime=locked;sky=null;weather=null;Status="";
 }
 public void Tick()
 {
  try{Core();}catch(Exception e){Plugin.Trace("[RR-STORM] error="+e.GetBaseException().Message);Stop();nextTick=Time.unscaledTime+10;}
 }
 private void Core()
 {
  var current=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance:null;if(current is HideoutGameWorld)current=null;
  if(!ReferenceEquals(world,current)){Stop();world=current;nextTick=nextScan=nextLog=0;}
  var game=Singleton<AbstractGame>.Instantiated?Singleton<AbstractGame>.Instance:null;
  if(current==null||!Plugin.RaidContext.EndsWith(": stormtide",StringComparison.Ordinal)||game?.GameTimer?.StartDateTime==null||Plugin.Local?.HealthController?.IsAlive!=true||game.Status is GameStatus.Stopped or GameStatus.Stopping or GameStatus.SoftStopping){Stop();return;}
  if(WeatherController.Instance==null||!TODSkyProvider.IsAvailable||TODSkyProvider.Instance?.CurrentTime?.GameDateTime==null)return;
  raidElapsed=game.GameTimer.PastTime.TotalSeconds;float progress=StormtidePolicy.Progress(raidElapsed,game.GameTimer.SessionTime?.TotalSeconds??0);
  audioAssets.Tick();
  if(rainEnabled?.Value==true)recordedRain.Tick(progress,rainVolume?.Value??1);else recordedRain.Stop();
  if(thunderEnabled?.Value==true)thunderFx.Tick(progress,raidElapsed);else thunderFx.Stop();
  if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+0.5f;
  if(clock==null)
  {
   weather=WeatherController.Instance;debug=weather.WeatherDebug;enabled=debug.Enabled;rain=debug.Rain;cloud=debug.CloudDensity;wind=debug.WindMagnitude;fog=debug.Fog;thunder=debug.LightningThunderProbability;direction=debug.WindDirection;topWind=debug.TopWindDirection;temperature=debug.Temperature;
   if(!enabled)debug.CopyParams(weather.WeatherCurve);
   sky=TODSkyProvider.Instance.CurrentTime;clock=sky.GameDateTime;factor=clock.TimeFactor;mod=clock.TimeFactorMod;locked=sky.LockCurrentTime;date=clock.Calculate();realtime=Time.realtimeSinceStartup;
   Plugin.Trace("[RR-STORM] stage=started fullStormAfterSeconds=300 nightStart="+StormtidePolicy.IsNight(date));
  }
  debug!.Enabled=true;debug.Rain=Mathf.Lerp(0.05f,1,progress);debug.CloudDensity=Mathf.Lerp(-0.2f,1,progress);debug.WindMagnitude=Mathf.Lerp(0.1f,0.8f,progress);debug.Fog=Mathf.Lerp(0.001f,0.012f,progress);debug.LightningThunderProbability=progress*0.7f;
  clock.TimeFactor=0;clock.TimeFactorMod=1;sky!.LockCurrentTime=false;clock.ResetForce(StormtidePolicy.ClockAt(date,progress));
  StormtideSound.Gain=StormtidePolicy.OtherSoundGain(progress);StormtideSound.RainGain=recordedRain.Playing?(float)StormAudioPolicy.NativeRain(recordedRain.Blend):StormtidePolicy.RainSoundGain(progress);StormtideSound.Active=true;
  if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+5;sources=Object.FindObjectsOfType<AudioSource>();}
  foreach(var source in sources)StormtideSound.Apply(source);
  Status=$"\nStormtide: {progress*100:0}% storm, time {clock.Calculate():HH:mm}, native rain {StormtideSound.RainGain*100:0}%, recorded rain {StormAudioPolicy.RecordedRain(recordedRain.Blend)*100:0}%, other sounds {StormtideSound.Gain*100:0}%";
  if(Time.unscaledTime>=nextLog){nextLog=Time.unscaledTime+30;Plugin.Trace("[RR-STORM] "+Status);}
 }
}


