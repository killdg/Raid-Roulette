using Comfort.Common;
using UnityEngine;
using Object=UnityEngine.Object;
namespace RaidRoulette.TestingClient;

internal sealed class StormtideRecordedRain(StormAudioAssets assets)
{
 private GameObject? root;private AudioSource? source;
 public float Blend{get;private set;}
 public bool Playing=>source!=null;
 public void Tick(float progress,float volume)
 {
  if(assets.Rain==null)return;
  if(root==null)
  {
   root=new GameObject(StormAudioPolicy.RainSourceName);source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.dopplerLevel=0;source.volume=0;source.clip=assets.Rain;
   if(Singleton<BetterAudio>.Instantiated){var native=Singleton<BetterAudio>.Instance.GetSource(BetterAudio.AudioSourceGroupType.Environment);if(native!=null){source.outputAudioMixerGroup=native.source1.outputAudioMixerGroup;native.Release();}}
   source.Play();
  }
  Blend=Mathf.MoveTowards(Blend,Mathf.Clamp01(progress),Time.unscaledDeltaTime*0.5f);
  source!.volume=(float)StormAudioPolicy.RecordedRain(Blend)*Mathf.Clamp01(volume)*(RainController.IsCameraUnderRain?1:0.35f);
 }
 public void Stop(){if(source!=null)source.Stop();if(root!=null)Object.Destroy(root);root=null;source=null;Blend=0;}
}
