using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.Tripwire;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
using Random=UnityEngine.Random;
namespace RaidRoulette.TestingClient;

internal sealed class SchizophreniaDirector
{
    private GameWorld? world;
    private bool active,whisperShown;
    private float whisperAt;
    private double timeJumpAt;private readonly SchizophreniaClock timeJump=new();
    private float incidentAt,scanAt,nextError,nextGrenade,nextDecoy,nextStillCheck;
    private int assetStage;
    private readonly StillnessScare stillness=new();
    private Door[] doors=Array.Empty<Door>();
    private GrenadePrefab[] grenadePrefabs=Array.Empty<GrenadePrefab>();
    private readonly List<(GameObject Root,float Expires)> sounds=new();
    private readonly List<(float At,Action Action)> scheduled=new();
    private readonly List<Visual> visuals=new();
    private TripwireSoundController? tripwire;
    private Systems.Effects.Effects? effects;
    private int doorCount,voiceCount,shotCount,impactCount,tripCount,grenadeCount,decoyCount;
    public string Status=>!active?"":$"\nSchizophrenia: doors {doorCount}, voices {voiceCount}, shots {shotCount}, impacts {impactCount}, wires {tripCount}, grenades {grenadeCount}, decoys {decoyCount}";
    private sealed class Part
    {
        public Renderer Source=null!;public GameObject Copy=null!;
    }
    private sealed class Visual
    {
        public GameObject Root=null!;public Transform SourceRoot=null!;public Player? Actor;
        public readonly List<Part> Parts=new();public readonly Dictionary<Transform,Transform> Bones=new();public Vector3 Start,End;public float Born,Life,PoseAt,VisibilityAt;public bool Grenade,Bounced;
    }
    private static float Now=>Time.unscaledTime;
    private void Log(string message)=>Plugin.Trace("[RR-PSYCH] "+message);
    public void Stop()
    {
        active=false;timeJump.Stop();scheduled.Clear();foreach(var v in visuals)Destroy(v);visuals.Clear();
        foreach(var sound in sounds)if(sound.Root!=null)Object.Destroy(sound.Root);sounds.Clear();
        tripwire?.Dispose();tripwire=null;doors=Array.Empty<Door>();grenadePrefabs=Array.Empty<GrenadePrefab>();effects=null;stillness.Reset();
    }
    public void Tick(){try{TickCore();}catch(Exception e){if(Now>=nextError){nextError=Now+10;Log("stage=director-error "+e.GetBaseException().Message);}}}
    private void TickCore()
    {
        var current=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance:null;
        if(current is HideoutGameWorld)current=null;
        if(!ReferenceEquals(world,current)){Stop();timeJump.Reset();world=current;whisperShown=false;whisperAt=0;}
        var game=Singleton<AbstractGame>.Instantiated?Singleton<AbstractGame>.Instance:null;
        bool running=current!=null&&game?.GameTimer?.StartDateTime!=null&&game.Status is not (GameStatus.Stopped or GameStatus.Stopping or GameStatus.SoftStopping)&&Plugin.Local?.HealthController?.IsAlive==true&&Plugin.RaidContext.Contains(": schizophrenia");
        if(!running){if(active)Stop();return;}
        if(!active)
        {
            active=true;
            timeJumpAt=SchizophreniaClockPolicy.ScheduledAt(game!.GameTimer.SessionTime?.TotalSeconds??0,Random.value);
            if(whisperAt<=0){float duration=(float)(game!.GameTimer.SessionTime?.TotalSeconds??0);whisperAt=duration>305?Random.Range(300f,duration-5):float.PositiveInfinity;}
            doorCount=voiceCount=shotCount=impactCount=tripCount=grenadeCount=decoyCount=0;
            incidentAt=Now+Random.Range(15f,25f);nextGrenade=Now+80;nextDecoy=Now+45;scanAt=Now+2;assetStage=0;nextStillCheck=0;
            Log("stage=started mode=render-and-audio-only; no actors, bullets or live grenades spawned");
        }
        timeJump.Tick(game!.GameTimer.PastTime.TotalSeconds,timeJumpAt);
        if(!whisperShown&&game!.GameTimer.PastTime.TotalSeconds>=whisperAt&&Singleton<EFT.Communications.NotificationManager>.Instantiated)
        {
            EFT.Communications.NotificationManager.DisplayMessageNotification("Did you hear that?",EFT.Communications.ENotificationDurationType.Long,EFT.Communications.ENotificationIconType.Quest);
            whisperShown=true;Log("stage=whisper-notification elapsed="+game.GameTimer.PastTime.TotalSeconds);
        }
        for(int i=sounds.Count-1;i>=0;i--)if(sounds[i].Root==null||Now>=sounds[i].Expires){if(sounds[i].Root!=null)Object.Destroy(sounds[i].Root);sounds.RemoveAt(i);}
        for(int i=scheduled.Count-1;i>=0;i--)if(Now>=scheduled[i].At){var action=scheduled[i].Action;scheduled.RemoveAt(i);Attempt("scheduled",action);}
        for(int i=visuals.Count-1;i>=0;i--){var v=visuals[i];if(Now>=v.Born+v.Life||v.SourceRoot==null){Destroy(v);visuals.RemoveAt(i);}else Attempt("visual-update",()=>UpdateVisual(v));}
        if(assetStage<3&&Now>=scanAt){scanAt=Now+2;Attempt("assets-"+assetStage,CacheAssets);}
        if(Now>=nextStillCheck)
        {
            nextStillCheck=Now+0.25f;var point=Plugin.Local!.Position;
            if(stillness.Tick(Now,point.x,point.y,point.z,visuals.Count==0&&sounds.Count==0&&scheduled.Count==0&&Time.smoothDeltaTime<0.04f,Random.value))
            {Attempt("stationary-grenade",StationaryGrenade);incidentAt=Now+Random.Range(18f,35f);return;}
        }
        if(Now>=incidentAt)
        {
            incidentAt=Now+Random.Range(18f,35f);
            if(visuals.Count>0||sounds.Count>0||scheduled.Count>0)return;
            if(Time.smoothDeltaTime>=0.04f){incidentAt=Now+2;return;}
            int choice=Random.Range(0,100);
            if(choice<45)Attempt("door",DoorPulse);
            else if(choice<60)Attempt("voice",Voice);
            else if(choice<68)Attempt("shots",Gunfire);
            else if(choice<73)Attempt("tripwire",Tripwire);
            else if(choice<76&&Now>=nextGrenade){nextGrenade=Now+Random.Range(100f,160f);Attempt("grenade",Grenade);}
            else if(Now>=nextDecoy){nextDecoy=Now+Random.Range(60f,100f);Attempt("decoy",Decoy);}
            else Attempt("door",DoorPulse);
        }
    }
    private void CacheAssets()
    {
        // One cache pass per world, staggered over separate ticks; never rescan all
        // loaded Unity objects every 90 seconds during the raid.
        switch(assetStage++){case 0:doors=Object.FindObjectsOfType<Door>();break;case 1:effects=Object.FindObjectOfType<Systems.Effects.Effects>();break;case 2:grenadePrefabs=Resources.FindObjectsOfTypeAll<GrenadePrefab>();break;}
        Log($"stage=assets-cached phase={assetStage} doors={doors.Length} grenadeModels={grenadePrefabs.Length}");
    }
    private void Attempt(string stage,Action action){long began=System.Diagnostics.Stopwatch.GetTimestamp();try{action();}catch(Exception e){if(Now>=nextError){nextError=Now+10;Log("stage="+stage+" unavailable="+e.GetBaseException().Message);}}finally{double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-began)*1000d/System.Diagnostics.Stopwatch.Frequency;if(ms>=8)Log("stage=slow-effect effect="+stage+" ms="+ms.ToString("0.0"));}}
    private Vector3 Near(float min,float max)
    {
        var local=Plugin.Local!;float a=Random.Range(0f,Mathf.PI*2),r=Random.Range(min,max);
        return local.Position+new Vector3(Mathf.Cos(a)*r,1.4f,Mathf.Sin(a)*r);
    }
    private void Sound(AudioClip? clip,Vector3 point,float volume=0.8f,float maxDistance=65)
    {
        if(clip==null)return;if(sounds.Count>=12)return;
        var root=new GameObject("RR_PhantomAudio");root.transform.position=point;
        var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.dopplerLevel=0;
        source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=2;source.maxDistance=maxDistance;source.volume=volume;
        sounds.Add((root,Now+Mathf.Min(clip.length,15)+0.2f));
        // Route through the native gameplay mixer while keeping ownership of the sound's lifetime.
        if(Singleton<BetterAudio>.Instantiated){var native=Singleton<BetterAudio>.Instance.GetSource(BetterAudio.AudioSourceGroupType.Weaponry);if(native!=null){source.outputAudioMixerGroup=native.source1.outputAudioMixerGroup;native.Release();}}
        source.clip=clip;source.Play();
    }
    private void DoorPulse()
    {
        Door? door=null;int found=0;var here=Plugin.Local!.Position;
        foreach(var candidate in doors)
        {
            if(candidate==null||(candidate.transform.position-here).sqrMagnitude>=1600||candidate.GetType()!=typeof(Door)||!candidate.Operatable||(candidate.TriggersMap!=null&&candidate.TriggersMap.Length>0)||candidate.NoInteractionsAllowed||candidate.IsBroken||!string.IsNullOrEmpty(candidate.KeyId)||candidate.InteractingPlayer!=null||candidate.DoorState is not (EDoorState.Shut or EDoorState.Open))continue;
            var position=candidate.transform.position;if(world!.RegisteredPlayers.Any(p=>(p.Position-position).sqrMagnitude<=9))continue;
            if(Random.Range(0,++found)==0)door=candidate;
        }
        if(door==null)return;
        if(door.DoorState==EDoorState.Shut)door.Open();else door.Close();doorCount++;Log("stage=door id="+door.Id+" position="+door.transform.position);
    }
    private void Voice()
    {
        var donors=world!.RegisteredPlayers.OfType<Player>().Where(p=>p.IsAI&&p.Profile.Info.Side==EPlayerSide.Savage&&p.Speaker!=null).ToArray();
        if(donors.Length==0)return;
        for(int attempt=0;attempt<6;attempt++)
        {
            var banks=donors[Random.Range(0,donors.Length)].Speaker.PhrasesBanks.Values.ToArray();if(banks.Length==0)continue;
            var clips=banks[Random.Range(0,banks.Length)]?.Clips;if(clips==null)continue;
            AudioClip? chosen=null;int eligible=0;foreach(var clip in clips)if(clip?.Clip!=null&&clip.Clip.length>0.4f&&clip.Clip.length<10&&Random.Range(0,++eligible)==0)chosen=clip.Clip;
            if(chosen==null)continue;var point=Near(5,18);Sound(chosen,point,0.9f);voiceCount++;Log("stage=voice clip="+chosen.name+" position="+point);return;
        }
    }
    private void Gunfire()
    {
        var donors=world!.RegisteredPlayers.OfType<Player>().Where(p=>p.HealthController?.IsAlive==true).ToArray();
        if(donors.Length==0)return;
        AudioClip? clip=null;string bankName="";int start=Random.Range(0,donors.Length);
        for(int attempt=0;attempt<Math.Min(6,donors.Length);attempt++)
        {
            var weapon=donors[(start+attempt)%donors.Length].GetComponentInChildren<WeaponSoundPlayer>();var bank=weapon!=null?weapon.Body:null;if(bank==null)continue;
            try{clip=bank.PickSingleClip(0);bankName=bank.name;}catch{continue;}if(clip!=null)break;
        }
        if(clip==null)return;var point=Near(12,35);int count=Random.Range(1,4);var loaded=clip;
        for(int i=0;i<count;i++){float at=Now+i*Random.Range(0.13f,0.22f);scheduled.Add((at,()=>{Sound(loaded,point,0.85f,100);shotCount++;}));}
        scheduled.Add((Now+0.4f,Impact));
        Log("stage=gunfire bank="+bankName+" count="+count+" position="+point);
    }
    private void Impact()
    {
        if(effects==null)return;var local=Plugin.Local!;
        for(int attempt=0;attempt<3;attempt++)
        {
            Vector3 origin=local.Position+Vector3.up*1.5f;
            Vector3 direction=(Near(2,7)-origin+Vector3.down*Random.Range(0.4f,2f)).normalized;
            var hits=Physics.RaycastAll(origin,direction,8,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
            foreach(var hit in hits)
            {
                if(hit.collider.GetComponentInParent<Player>()!=null)break;
                if(hit.distance<1.5f)continue;
                var ballistic=hit.collider.GetComponent<BallisticCollider>()??hit.collider.GetComponentInParent<BallisticCollider>();
                if(ballistic==null)break;
                effects.Emit(ballistic.TypeOfMaterial,ballistic,hit.point,hit.normal,0.75f);impactCount++;Log("stage=impact position="+hit.point+" material="+ballistic.TypeOfMaterial);return;
            }
        }
    }
    private TripwireSoundController Wire()=>tripwire??=new TripwireSoundController(Plugin.Local!.ProfileId){_isFirstPerson=false};
    private void Tripwire()
    {
        var point=Near(1.5f,4);var storage=Wire()._soundStorage;
        Sound(storage.GetGrenadePinSound().GetClip(),point,1);scheduled.Add((Now+0.2f,()=>Sound(storage.GetFuzeSound().GetClip(),point,0.85f)));tripCount++;Log("stage=tripwire-pin position="+point);
    }
    private void StationaryGrenade()
    {
        var point=Near(2,4);var storage=Wire()._soundStorage;
        Sound(storage.GetGrenadePinSound().GetClip(),point,0.9f);
        scheduled.Add((Now+0.3f,()=>Sound(storage.GetDefuseCollisionSound().GetClip(),point,0.9f)));
        scheduled.Add((Now+0.6f,()=>Sound(storage.GetFuzeSound().GetClip(),point,0.85f)));
        tripCount++;Log("stage=stationary-grenade stationarySeconds=10 chance=0.35 cooldownSeconds=120 audio-only");
    }
    private void Grenade()
    {
        if(visuals.Count>=2)return;
        var model=grenadePrefabs.FirstOrDefault(g=>g!=null&&g.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.GetComponent<MeshFilter>()?.sharedMesh!=null));
        if(model==null){Log("stage=grenade waiting-for-loaded-model");return;}
        var target=Near(2.5f,5);if(!NavMesh.SamplePosition(target,out var hit,3,NavMesh.AllAreas))return;
        var start=Near(7,12)+Vector3.up;var visual=CreateVisual(model.transform,model.GetComponentsInChildren<Renderer>(true),start,hit.position+Vector3.up*0.08f,4,true,null);
        if(visual==null)return;Sound(Wire()._soundStorage.GetGrenadePinSound().GetClip(),start);grenadeCount++;
        Log("stage=grenade visual-only position="+hit.position);
    }
    private void Decoy()
    {
        if(visuals.Any(v=>!v.Grenade))return;
        var donors=world!.RegisteredPlayers.OfType<Player>().Where(p=>p.IsAI&&p.HealthController?.IsAlive==true&&p.PlayerBody!=null&&p.AIData?.BotOwner?.Mover?.IsMoving==true).ToArray();
        if(donors.Length==0){Log("stage=decoy waiting-for-moving-AI-model");return;}
        var donor=donors[Random.Range(0,donors.Length)];var local=Plugin.Local!;var camera=Camera.main;
        Vector3 forward=camera!=null?camera.transform.forward:local.LookDirection;forward.y=0;if(forward.sqrMagnitude<0.1f)return;forward.Normalize();
        Vector3 right=Vector3.Cross(Vector3.up,forward);float side=Random.value<0.5f?-1:1;
        var anchor=local.Position+forward*Random.Range(12f,23f)+right*side*6;
        if(!NavMesh.SamplePosition(anchor,out var start,3,NavMesh.AllAreas)||Mathf.Abs(start.position.y-local.Position.y)>3)return;
        if(!NavMesh.SamplePosition(start.position-right*side*Random.Range(8f,13f),out var end,3,NavMesh.AllAreas))return;
        var route=new NavMeshPath();if(!NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete||route.corners.Length>2)return;
        if(Physics.Linecast(start.position+Vector3.up,end.position+Vector3.up,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return;
        var renderers=new List<Renderer>();donor.PlayerBody.GetRenderersNonAlloc(renderers);
        var visual=CreateVisual(donor.Transform.Original,renderers.Where(r=>r!=null&&r.enabled&&r.gameObject.activeInHierarchy),start.position,end.position,Random.Range(2.5f,4f),false,donor);
        if(visual!=null){decoyCount++;Log("stage=decoy donorRole="+donor.Profile.Info.Settings.Role+" start="+start.position+" end="+end.position);}
    }
    private Visual? CreateVisual(Transform source,IEnumerable<Renderer> renderers,Vector3 start,Vector3 end,float life,bool grenade,Player? actor)
    {
        var v=new Visual{Root=new GameObject("RR_PhantomVisual"),SourceRoot=source,Actor=actor,Start=start,End=end,Born=Now,Life=life,Grenade=grenade};
        v.Root.transform.position=start;
        try
        {
            foreach(var renderer in renderers.Distinct().Take(6))
            {
                var part=new GameObject("mesh");part.transform.SetParent(v.Root.transform,false);
                Renderer copy;
                if(renderer is SkinnedMeshRenderer skin&&skin.sharedMesh!=null)
                {
                    Transform Bone(Transform bone)
                    {
                        if(v.Bones.TryGetValue(bone,out var existing))return existing;
                        var created=new GameObject("visual_bone").transform;created.SetParent(v.Root.transform,false);v.Bones[bone]=created;return created;
                    }
                    var skinned=part.AddComponent<SkinnedMeshRenderer>();skinned.sharedMesh=skin.sharedMesh;
                    skinned.bones=skin.bones.Select(Bone).ToArray();if(skin.rootBone!=null)skinned.rootBone=Bone(skin.rootBone);
                    skinned.localBounds=skin.localBounds;skinned.updateWhenOffscreen=false;copy=skinned;
                }
                else if(renderer is MeshRenderer&&renderer.GetComponent<MeshFilter>()?.sharedMesh is Mesh mesh)
                {part.AddComponent<MeshFilter>().sharedMesh=mesh;copy=part.AddComponent<MeshRenderer>();}
                else{Object.Destroy(part);continue;}
                copy.sharedMaterials=renderer.sharedMaterials;copy.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;copy.receiveShadows=false;
                v.Parts.Add(new Part{Source=renderer,Copy=part});
            }
            if(v.Parts.Count==0){Destroy(v);return null;}
            visuals.Add(v);UpdateVisual(v);return v;
        }
        catch{Destroy(v);throw;}
    }
    private void UpdateVisual(Visual v)
    {
        float elapsed=Now-v.Born;float t=Mathf.Clamp01(elapsed/(v.Grenade?1.15f:v.Life));
        v.Root.transform.position=Vector3.Lerp(v.Start,v.End,t)+(v.Grenade?Vector3.up*Mathf.Sin(t*Mathf.PI)*2:Vector3.zero);
        Vector3 direction=v.End-v.Start;direction.y=0;if(direction.sqrMagnitude>0.1f)v.Root.transform.rotation=Quaternion.LookRotation(direction);
        if(v.Grenade)v.Root.transform.rotation=Quaternion.Euler(elapsed*500,elapsed*220,0);
        bool updatePose=Now>=v.PoseAt;if(updatePose)v.PoseAt=Now+0.1f;
        if(updatePose)foreach(var pair in v.Bones)
        {
            if(pair.Key==null)continue;pair.Value.localPosition=v.SourceRoot.InverseTransformPoint(pair.Key.position);pair.Value.localRotation=Quaternion.Inverse(v.SourceRoot.rotation)*pair.Key.rotation;pair.Value.localScale=pair.Key.lossyScale;
        }
        if(updatePose)foreach(var part in v.Parts)
        {
            if(part.Source==null)continue;

            part.Copy.transform.localPosition=v.SourceRoot.InverseTransformPoint(part.Source.transform.position);
            part.Copy.transform.localRotation=Quaternion.Inverse(v.SourceRoot.rotation)*part.Source.transform.rotation;
            var s=part.Source.transform.lossyScale;var p=v.SourceRoot.lossyScale;part.Copy.transform.localScale=new Vector3(s.x/p.x,s.y/p.y,s.z/p.z);
        }
        if(!v.Grenade&&elapsed>1&&Now>=v.VisibilityAt)
        {
            v.VisibilityAt=Now+0.25f;
            var camera=Camera.main;if(camera!=null){var point=camera.WorldToViewportPoint(v.Root.transform.position+Vector3.up);if(point.z<0||point.x<0||point.x>1||point.y<0||point.y>1||Physics.Linecast(camera.transform.position,v.Root.transform.position+Vector3.up,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))v.Life=Mathf.Min(v.Life,elapsed+0.1f);}
        }
        if(v.Grenade&&elapsed>1.2f&&!v.Bounced){v.Bounced=true;Attempt("grenade-bounce",()=>Sound(Wire()._soundStorage.GetDefuseCollisionSound().GetClip(),v.End,0.8f));}
        if(v.Grenade&&elapsed>3.6f){if(effects!=null){var effect=effects.EffectsArray.FirstOrDefault(e=>e.Name!=null&&(e.Name.IndexOf("grenade",StringComparison.OrdinalIgnoreCase)>=0||e.Name.Equals("explosion",StringComparison.OrdinalIgnoreCase)));if(effect!=null)effects.EmitGrenade(effect.Name,v.End,Vector3.up,0.8f);}v.Life=elapsed;Log("stage=grenade-dissolved visual-effects-only");}
    }
    private static void Destroy(Visual v){if(v.Root!=null)Object.Destroy(v.Root);}
}








