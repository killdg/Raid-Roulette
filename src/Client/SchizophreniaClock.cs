using System;
using UnityEngine;
using EFT;
namespace RaidRoulette.TestingClient;

internal sealed class SchizophreniaClock
{
 private bool consumed;private GameDateTime? clock;private TOD_Time? sky;private DateTime original,shifted;
 private float factor,mod,returnAt;private bool locked;
 internal bool Active=>clock!=null;
 internal void Reset(){Stop();consumed=false;}
 internal void Tick(double elapsed,double scheduled)
 {
  try{Core(elapsed,scheduled);}catch(Exception e){Stop();consumed=true;Plugin.Trace("[RR-PSYCH] stage=time-jump-unavailable "+e.GetBaseException().Message);}
 }
 private void Core(double elapsed,double scheduled)
 {
  if(clock!=null)
  {
   if(Time.unscaledTime>=returnAt){Stop();Plugin.Trace("[RR-PSYCH] stage=time-jump-restored time="+original.ToString("HH:mm:ss"));return;}
   clock.TimeFactor=0;clock.TimeFactorMod=1;clock.ResetForce(shifted);return;
  }
  if(consumed||elapsed<scheduled||!TODSkyProvider.IsAvailable||TODSkyProvider.Instance?.CurrentTime?.GameDateTime==null)return;
  sky=TODSkyProvider.Instance.CurrentTime;clock=sky.GameDateTime;original=clock.Calculate();shifted=SchizophreniaClockPolicy.Shift(original);
  factor=clock.TimeFactor;mod=clock.TimeFactorMod;locked=sky.LockCurrentTime;consumed=true;returnAt=Time.unscaledTime+(float)SchizophreniaClockPolicy.Duration;
  clock.TimeFactor=0;clock.TimeFactorMod=1;sky.LockCurrentTime=false;clock.ResetForce(shifted);
  Plugin.Trace("[RR-PSYCH] stage=time-jump from="+original.ToString("HH:mm:ss")+" to="+shifted.ToString("HH:mm:ss")+" seconds=60");
 }
 internal void Stop()
 {
  if(clock==null)return;clock.TimeFactor=factor;clock.TimeFactorMod=mod;clock.ResetForce(original);if(sky!=null)sky.LockCurrentTime=locked;clock=null;sky=null;
 }
}
