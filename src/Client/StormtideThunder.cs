using System;

using Comfort.Common;
using UnityEngine;
using Object=UnityEngine.Object;
using Random=UnityEngine.Random;
namespace RaidRoulette.TestingClient;

internal sealed class StormtideThunder
{
 private readonly StormAudioAssets assets;
 public StormtideThunder(StormAudioAssets assets){this.assets=assets;}
 private GameObject? root;private AudioSource? sound;private Light? flash;
 private int previous=-1;private float flashAt=-100,audioAt=-1,strength;
 private readonly ThunderSchedule schedule=new();
 private bool test;
 private float retryAt;
 public void RequestTest()=>test=true;
 public void Tick(float progress,double elapsed)
 {
  if(Time.unscaledTime<retryAt)return;
  try{Core(progress,elapsed);}catch(Exception e){Stop(false);retryAt=Time.unscaledTime+30;Plugin.Trace("[RR-THUNDER] unavailable="+e.GetBaseException().Message);}
 }
 private void Core(float progress,double elapsed)
 {
  var clips=assets.Thunder;if(clips.Length==0)return;
  if(root==null)
  {
   root=new GameObject(ThunderPolicy.SourceName);sound=root.AddComponent<AudioSource>();sound.playOnAwake=false;sound.loop=false;sound.spatialBlend=0;sound.dopplerLevel=0;
   if(Singleton<BetterAudio>.Instantiated){var native=Singleton<BetterAudio>.Instance.GetSource(BetterAudio.AudioSourceGroupType.Environment);if(native!=null){sound.outputAudioMixerGroup=native.source1.outputAudioMixerGroup;native.Release();}}
   flash=root.AddComponent<Light>();flash.type=LightType.Directional;flash.shadows=LightShadows.None;flash.color=new Color(0.78f,0.85f,1);flash.enabled=false;
  }
  float now=Time.unscaledTime;
  float light=ThunderPolicy.Flash(now-flashAt)*strength;
  flash!.intensity=light;flash.enabled=light>0&&RainController.IsCameraUnderRain;
  if(audioAt>=0&&now>=audioAt)
  {
   audioAt=-1;sound!.volume=RainController.IsCameraUnderRain?0.8f:0.3f;sound.panStereo=Random.Range(-0.15f,0.15f);sound.Play();
   Plugin.Trace("[RR-THUNDER] stage=thunder indoor="+!RainController.IsCameraUnderRain);
  }
  bool available=audioAt<0&&!sound!.isPlaying;int before=schedule.Rolls;
  bool automatic=schedule.Tick(elapsed,available,Random.value);
  if(schedule.Rolls!=before)Plugin.Trace("[RR-THUNDER] stage=chance-roll success="+automatic+" elapsed="+elapsed.ToString("0")+" nextRoll="+schedule.NextRollAt.ToString("0"));
  if((test&&elapsed>=300||automatic)&&available)
  {
   test=false;flashAt=now;strength=Mathf.Lerp(0.65f,1.5f,progress);root.transform.rotation=Quaternion.Euler(Random.Range(40f,70f),Random.Range(0f,360f),0);
   previous=StormAudioPolicy.ChooseThunder(previous,clips.Length,Random.value);sound!.clip=clips[previous];audioAt=now+(float)ThunderPolicy.Delay(Random.value);
   flash.intensity=strength;flash.enabled=RainController.IsCameraUnderRain;
   Plugin.Trace("[RR-THUNDER] stage=strike clip="+sound.clip.name+" delay="+(audioAt-now).ToString("0.00"));
  }
 }
 public void Stop(bool resetSchedule=true)
 {
  test=false;audioAt=-1;flashAt=-100;if(resetSchedule)schedule.Reset();
  if(sound!=null)sound.Stop();if(flash!=null)flash.enabled=false;if(root!=null)Object.Destroy(root);root=null;sound=null;flash=null;
 }
 public void Dispose()=>Stop();
}
