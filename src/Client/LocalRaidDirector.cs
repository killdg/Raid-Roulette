using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using Comfort.Common;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient;

// Local only. The native scenario owns creation, equipment, placement, and AI activation.
internal sealed class LocalRaidDirector
{
    internal static BossSpawnScenario? Scenario;
    internal static BotBossSpawn? NativeSpawner;
    internal static EFT.Game.Spawning.ISpawnPoint[] NativePoints => (NativeSpawner?._spawnSystem as EFT.Game.Spawning.SpawnSystem)?._spawnPoints?.ToArray()??Array.Empty<EFT.Game.Spawning.ISpawnPoint>();
    internal static BotZone[] Zones=Array.Empty<BotZone>();
    internal static readonly Dictionary<BossLocationSpawn, Reinforcement> Requests=new();
    private static readonly TraceLimiter attemptLimit=new();
    public string Status {get;private set;}="No active raid";
    private GameWorld? world;
    private string context="";
    private bool bound;
    private float nextTick,nextSummary;
    private int interval,size,cap,waveNumber;
    private double nextWave;
    private ReinforcementClock? clock;
    private readonly Dictionary<string,string> initial=new();
    private readonly Queue<Reinforcement> queue=new();
    private readonly Dictionary<string,(Vector3 Pos,float Since)> idle=new();
    private bool startupDone;
    private int startupTarget,startupCap;
    private float nextStartupLog;
    private bool overwatchQueued,overwatchDone;
    private int overwatchCap;
    private readonly Queue<(string Zone,Vector3 Position)> rooftopQueue=new();
    private float nextOverwatchLog;
    private double lastNormalArrival;
    private readonly HashSet<string> normalProfiles=new();
    private int overwatchPlanned;
    private bool normalPmcObserved;
    private float nextGunSpawn;private int gunSequence;
    [ThreadStatic] internal static BossLocationSpawn? CurrentAttempt;
    internal static int ActiveCap=35;
    internal static readonly Dictionary<BotCreationData,Reinforcement> Creating=new();
    internal sealed class Reinforcement
    {
        internal int Wave;
        internal string Role="assault";
        internal float Submitted;
        internal bool Accepted,Finished,TimeoutLogged,Confirmed;
        internal readonly HashSet<string> Profiles=new();
        internal BossLocationSpawn? Spawn;
        internal Vector3? ExactPoint;
    }
    internal static bool Concentrate(BossLocationSpawn wave)=>Plugin.RaidContext.Contains(": scav_concentrate") && wave.ForceSpawn && wave.BossName=="assault" && wave.BossEscortType=="assault" && wave.Time>=15 && wave.Time%5==0 && wave.EscortCount<=3;
    internal static bool Managed(BossLocationSpawn wave)=>Requests.ContainsKey(wave)||Concentrate(wave);
    internal static bool Startup(BossLocationSpawn wave)=>Plugin.RaidContext.Contains(": smokers_lobby")&&Requests.TryGetValue(wave,out var r)&&r.Wave==0;
    internal static bool Rooftop(BossLocationSpawn wave)=>Plugin.RaidContext.Contains(": extreme_overwatch")&&Requests.TryGetValue(wave,out var r)&&r.Wave==-1;
    internal static void Capture(BossSpawnScenario scenario,List<BotZone> zones)
    {
        attemptLimit.Reset();Scenario=scenario;Zones=zones.Where(z=>z!=null).ToArray();Requests.Clear();Creating.Clear();
        if(Plugin.RaidContext.Contains(": extreme_overwatch"))
        {
            Plugin.Trace($"[RR-BASELINE] stage=scenario pmcZones={Zones.Length} waves={scenario.BossSpawnWaves?.Length??0}; Run supplies PMC zones, not all sniper zones");
            foreach(var wave in scenario.BossSpawnWaves??Array.Empty<BossLocationSpawn>())
                if(wave.BossType.IsPmcBot())Plugin.Trace($"[RR-BASELINE] stage=pmc-wave role={wave.BossName} chance={wave.BossChance} selected={wave.ShallSpawn} bornZone={wave.BornZone} time={wave.Time}");
        }
    }
    internal static void Stop(BossSpawnScenario scenario)
    {if(ReferenceEquals(Scenario,scenario)){Scenario=null;Zones=Array.Empty<BotZone>();Requests.Clear();Creating.Clear();}}
    internal static void ObserveProfiles(BossLocationSpawn wave,BotCreationData data)
    {
        if(!Requests.TryGetValue(wave,out var request))return;
        Creating[data]=request;
        Plugin.Trace($"[RR-WAVE] wave={request.Wave} stage=placement; waiting for creation callback and live registration");
    }
    internal static void ObserveCreated(Reinforcement request,BotOwner bot)
    {
        request.Profiles.Add(bot.ProfileId);
        if(request.Wave==-3)LabWorkDirector.Raiders.Add(bot.ProfileId);
        Plugin.Trace($"[RR-WAVE] wave={request.Wave} stage=creation-callback profile={bot.ProfileId} role={bot.Profile.Info.Settings.Role}; awaiting registration");
    }
    internal static void Outcome(BossLocationSpawn wave,bool accepted)
    {
        if(Plugin.RaidContext.Contains(": extreme_overwatch")&&wave.BossType.IsPmcBot()&&attemptLimit.Allow("pmc-result:"+wave.GetHashCode()+":"+accepted))Plugin.Trace($"[RR-BASELINE] stage=pmc-attempt-result role={wave.BossName} accepted={accepted} bornZone={wave.BornZone}");
        if(Requests.TryGetValue(wave,out var r))r.Accepted|=accepted;
        if(Managed(wave) && attemptLimit.Allow(wave.GetHashCode()+":"+accepted))Plugin.Trace($"[RR-WAVE] stage=native-attempt role={wave.BossName} zone={wave.BossZone} accepted={accepted}; awaiting live profile observation");
    }
    internal static int Alive()=>Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance.RegisteredPlayers.OfType<Player>().Count(p=>p.IsAI && p.HealthController?.IsAlive==true):0;
    private static bool Ordinary(string role)=>role is "assault" or "assaultGroup" or "pmcUSEC" or "pmcBEAR";
    public void Tick()
    {
        var current=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance:null;
        if(current is HideoutGameWorld)current=null;
        if(!ReferenceEquals(world,current))
        {
            Status="No active raid";Requests.Clear();Creating.Clear();world=current;bound=false;context="";initial.Clear();queue.Clear();idle.Clear();nextTick=0;waveNumber=0;startupDone=false;startupTarget=startupCap=0;nextStartupLog=0;overwatchQueued=overwatchDone=false;rooftopQueue.Clear();nextOverwatchLog=0;lastNormalArrival=0;normalProfiles.Clear();overwatchPlanned=0;normalPmcObserved=false;nextGunSpawn=0;gunSequence=0;
        }
        if(current==null || Plugin.Local==null || !Singleton<AbstractGame>.Instantiated)return;
        var game=Singleton<AbstractGame>.Instance;
        if(game.GameTimer?.StartDateTime==null || game.Status is GameStatus.Stopped or GameStatus.Stopping or GameStatus.SoftStopping)return;
        if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+1;
        if(!bound)
        {
            // Freeze event and controls for this world. Later F12 edits affect the next raid.
            context=Plugin.RaidContext;bound=true;Status="No Overrun reinforcement queue in this raid";
            interval=Plugin.WaveInterval.Value;size=Plugin.WaveSize.Value;cap=Plugin.AliveCap.Value;ActiveCap=cap;
            clock=new ReinforcementClock(interval);nextWave=interval;nextSummary=0;
            Plugin.Trace($"[RR-WAVE] stage=raid-bound context={context} interval={interval} configuredSize={size} cap={cap}");
        }
        var players=current.RegisteredPlayers.OfType<Player>().Where(p=>p.IsAI).ToArray();
        double elapsed=game.GameTimer.PastTime.TotalSeconds;
        if(context.Contains(": gun_game"))FillGunGame(players,elapsed);
        if(context.Contains(": smokers_lobby"))FillStartup(players,elapsed);
        if(context.Contains(": extreme_overwatch"))FillRooftops(players,elapsed);
        if(context.Contains(": overrun"))
        {
            foreach(var p in players)
                if(elapsed<=60 && Ordinary(p.Profile.Info.Settings.Role.ToString())) initial[p.ProfileId]=p.Profile.Info.Settings.Role==WildSpawnType.assaultGroup?"assault":p.Profile.Info.Settings.Role.ToString();
            var ids=new HashSet<string>(players.Select(p=>p.ProfileId));
            foreach(var pair in Requests.ToArray())
            {
                var r=pair.Value;if(r.Finished)continue;
                var live=players.FirstOrDefault(p=>r.Profiles.Contains(p.ProfileId));
                if(live!=null){r.Finished=true;r.Confirmed=true;Plugin.Trace($"[RR-WAVE] wave={r.Wave} stage=live-confirmed profile={live.ProfileId} requestedRole={r.Role} actualRole={live.Profile.Info.Settings.Role}");}
                else if(Time.unscaledTime-r.Submitted>90&&!r.TimeoutLogged){r.TimeoutLogged=true;Plugin.Trace($"[RR-WAVE] wave={r.Wave} stage=unconfirmed-timeout accepted={r.Accepted}; retaining capacity reservation and submitting no duplicate");}
            }
            if(clock!.Advance(elapsed))
            {
                int count=size>0?size:Math.Max(1,initial.Count);
                string[] roles=initial.Values.ToArray();if(roles.Length==0)roles=new[]{"assault"};
                waveNumber=clock.Waves;
                // Bound total backlog: skipped waves are explicit, never an unbounded allocation.
                int added=0;
                for(int i=0;i<count && queue.Count<160;i++){queue.Enqueue(new Reinforcement{Wave=waveNumber,Role=roles[i%roles.Length]});added++;}
                Plugin.Trace($"[RR-WAVE] wave={waveNumber} stage=queued target={count} queued={added} initialObserved={initial.Count} elapsed={elapsed:0}");
                nextWave=clock.NextDeadline; // no catch-up burst after a stall
            }
            int alive=players.Count(p=>p.HealthController?.IsAlive==true);
            int pending=Requests.Values.Count(r=>!r.Finished);
            Status=$"Overrun: waves={waveNumber}, queued={queue.Count}, pending={pending}, confirmed={Requests.Values.Count(r=>r.Confirmed)}, next at {nextWave/60:0.0} raid minutes";
            if(queue.Count>0 && ReinforcementClock.HasCapacity(alive,pending,cap) && Scenario!=null && Zones.Length>0)
            {
                var r=queue.Dequeue();
                var zone=Zones.Where(z=>z.SpawnPoints?.Any(p=>Vector3.Distance(p.Position,Plugin.Local!.Position)>=30 && (!r.Role.StartsWith("pmc",StringComparison.OrdinalIgnoreCase) || EFT.Game.Spawning.SpawnCategoryExtension.ContainBotPmcCategory(p.Categories)))==true).OrderBy(_=>UnityEngine.Random.value).FirstOrDefault();
                if(zone!=null)
                {
                    var spawn=new BossLocationSpawn {BossName=r.Role,BossEscortType="assault",BossEscortAmount="0",BossChance=100,BossZone=zone.name,BossDifficult="normal",BossEscortDifficult="normal",Time=0,ForceSpawn=true,IgnoreMaxBots=false,Supports=Array.Empty<WildSpawnSupports>(),TriggerId="",TriggerName=""};
                    spawn.Init();r.Spawn=spawn;r.Submitted=Time.unscaledTime;Requests[spawn]=r;
                    try{Scenario.ActivateWave(spawn);Plugin.Trace($"[RR-WAVE] wave={r.Wave} stage=submitted role={r.Role} zone={zone.name} alive={alive} pending={pending}");}
                    catch(Exception e){r.Finished=true;Plugin.Trace("[RR-WAVE] stage=submit-error "+e.GetBaseException().Message);}
                }
                else {queue.Enqueue(r);}
            }
            if(Time.unscaledTime>=nextSummary){nextSummary=Time.unscaledTime+30;Plugin.Trace($"[RR-WAVE] stage=summary alive={alive} pending={pending} deferred={queue.Count} liveConfirmed={Requests.Values.Count(r=>r.Confirmed)} nextAt={nextWave:0}");}
        }
        if(context.Contains(": boss_convention")&&Plugin.BossRoaming.Value)Roam(players);
    }
    private void FillGunGame(Player[] players,double elapsed)
    {
        if(!GunGameDirector.Ready||GunGameDirector.PlayerWon||Plugin.Local?.HealthController?.IsAlive!=true){Status="Gun Game: replenishment stopped after win/death, or awaiting weapon setup";return;}
        ActiveCap=Plugin.StartupCap>0?Math.Min(200,Plugin.StartupCap):Math.Max(8,cap);
        int target=Plugin.StartupTarget>0?Plugin.StartupTarget:GunGameSpawnPolicy.MinimumPopulation(ActiveCap);
        foreach(var pair in Requests.Where(p=>p.Value.Wave==-2&&!p.Value.Finished))
        {
            var request=pair.Value;
            if(players.Any(p=>request.Profiles.Contains(p.ProfileId))){request.Finished=request.Confirmed=true;Plugin.Trace("[RR-GUNGAME-SPAWN] stage=registered role="+request.Role);}
            else if(!request.Accepted&&Time.unscaledTime-request.Submitted>60){request.Finished=true;pair.Key.ShallSpawn=false;Plugin.Trace("[RR-GUNGAME-SPAWN] stage=cancelled-unaccepted; replacement will retry");}
        }
        int alive=players.Count(p=>p.HealthController?.IsAlive==true),ordinary=players.Count(p=>p.HealthController?.IsAlive==true&&p.Profile.Info.Settings.Role!=WildSpawnType.shooterBTR);
        int pending=Requests.Values.Count(r=>r.Wave==-2&&!r.Finished);
        Status=$"Gun Game replenishment: ordinary alive={ordinary}, target={target}, pending={pending}, total alive={alive}, cap={ActiveCap}";
        if(!GunGameSpawnPolicy.CanSubmit(GunGameDirector.Ready,GunGameDirector.PlayerWon,Plugin.Local?.HealthController?.IsAlive==true,elapsed,ordinary,pending,alive,ActiveCap,target)||Time.unscaledTime<nextGunSpawn||Scenario==null||Zones.Length==0)return;
        nextGunSpawn=Time.unscaledTime+2;
        string role=new[]{"assault","pmcUSEC","assault","pmcBEAR"}[gunSequence++%4];
        var eligible=Zones.Where(z=>z.SpawnPoints?.Any(p=>Vector3.Distance(p.Position,Plugin.Local!.Position)>=30&&(!role.StartsWith("pmc",StringComparison.OrdinalIgnoreCase)||EFT.Game.Spawning.SpawnCategoryExtension.ContainBotPmcCategory(p.Categories)))==true).ToArray();
        if(eligible.Length==0){role="assault";eligible=Zones.Where(z=>z.SpawnPoints?.Any(p=>Vector3.Distance(p.Position,Plugin.Local!.Position)>=30)==true).ToArray();}
        if(eligible.Length==0){Plugin.Trace("[RR-GUNGAME-SPAWN] stage=waiting-for-safe-zone");return;}
        var zone=eligible[UnityEngine.Random.Range(0,eligible.Length)];
        var requestNew=new Reinforcement{Role=role,Wave=-2,Submitted=Time.unscaledTime};
        var spawn=new BossLocationSpawn{BossName=role,BossEscortType="assault",BossEscortAmount="0",BossChance=100,BossZone=zone.name,BossDifficult="normal",BossEscortDifficult="normal",Time=0,ForceSpawn=true,IgnoreMaxBots=true,Supports=Array.Empty<WildSpawnSupports>(),TriggerId="",TriggerName=""};
        spawn.Init();requestNew.Spawn=spawn;Requests[spawn]=requestNew;
        try{Scenario.ActivateWave(spawn);Plugin.Trace($"[RR-GUNGAME-SPAWN] stage=submitted role={role} zone={zone.name} ordinary={ordinary} pending={pending+1}");}
        catch(Exception e){requestNew.Finished=true;spawn.ShallSpawn=false;Plugin.Trace("[RR-GUNGAME-SPAWN] submit-error="+e.GetBaseException().Message);}
    }
    private void FillStartup(Player[] players,double elapsed)
    {
        if(startupDone)return;
        if(startupTarget==0&&Plugin.StartupTarget>0&&Plugin.StartupCap>0)
        {
            startupCap=Math.Min(200,Plugin.StartupCap);startupTarget=Math.Min(startupCap,Plugin.StartupTarget);ActiveCap=startupCap;
            Plugin.Trace($"[RR-START] stage=target target={startupTarget} cap={startupCap}");
        }
        if(startupTarget==0||elapsed<5)return;
        int alive=players.Count(p=>p.HealthController?.IsAlive==true);
        foreach(var request in Requests.Values.Where(r=>!r.Finished))
        {
            if(players.Any(p=>request.Profiles.Contains(p.ProfileId)))
            {request.Finished=true;request.Confirmed=true;Plugin.Trace($"[RR-START] stage=live-confirmed role={request.Role}");}
        }
        int pending=Requests.Values.Count(r=>!r.Finished);
        Status=$"Smokers Lobby start: alive={alive}, target={startupTarget}, cap={startupCap}, pending={pending}";
        if(alive>=startupTarget||elapsed>90)
        {
            startupDone=true;Plugin.Trace($"[RR-START] stage=finished reason={(alive>=startupTarget?"target-reached":"startup-window-ended")} alive={alive} pending={pending} target={startupTarget}; no further startup spawns");return;
        }
        if(Time.unscaledTime>=nextStartupLog){nextStartupLog=Time.unscaledTime+10;Plugin.Trace("[RR-START] stage=progress "+Status);}
        if(!StartupFillPolicy.CanSubmit(alive,pending,Requests.Count,startupTarget,startupCap,elapsed)||Scenario==null||Zones.Length==0)return;
        string role=new[]{"assault","pmcUSEC","assault","pmcBEAR"}[Requests.Count%4];
        var zone=Zones.Where(z=>z.SpawnPoints?.Any(p=>Vector3.Distance(p.Position,Plugin.Local!.Position)>=30&&(!role.StartsWith("pmc",StringComparison.OrdinalIgnoreCase)||EFT.Game.Spawning.SpawnCategoryExtension.ContainBotPmcCategory(p.Categories)))==true).OrderBy(_=>UnityEngine.Random.value).FirstOrDefault();
        if(zone==null)return;
        var r=new Reinforcement{Role=role,Wave=0,Submitted=Time.unscaledTime};
        var spawn=new BossLocationSpawn{BossName=role,BossEscortType="assault",BossEscortAmount="0",BossChance=100,BossZone=zone.name,BossDifficult="normal",BossEscortDifficult="normal",Time=0,ForceSpawn=true,IgnoreMaxBots=false,Supports=Array.Empty<WildSpawnSupports>(),TriggerId="",TriggerName=""};
        spawn.Init();r.Spawn=spawn;Requests[spawn]=r;
        try{Scenario.ActivateWave(spawn);Plugin.Trace($"[RR-START] stage=submitted role={role} zone={zone.name} alive={alive} pending={pending+1}");}
        catch(Exception e){r.Finished=true;Plugin.Trace("[RR-START] stage=submit-error "+e.GetBaseException().Message);}
    }
    private void FillRooftops(Player[] players,double elapsed)
    {
        if(overwatchDone||elapsed<5)return;
        if(!overwatchQueued)
        {
            foreach(var player in players)if(normalProfiles.Add(player.ProfileId))lastNormalArrival=elapsed;
            normalPmcObserved|=players.Any(p=>p.Profile.Info.Settings.Role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR);
            if(!normalPmcObserved)
            {
                Status="Extreme Overwatch: waiting for normal PMC registration; extras held";
                if(elapsed>=180){overwatchDone=true;Plugin.Trace("[RR-ROOF] stage=baseline-incomplete no normal PMC registered within 180 seconds; no extras submitted");}
                return;
            }
            if(!OverwatchPolicy.BaselineReady(elapsed,elapsed-lastNormalArrival))
            {Status=$"Extreme Overwatch: waiting for normal starting AI (registered={normalProfiles.Count})";return;}
            if(Plugin.OverwatchZones.Length==0||Plugin.OverwatchPerZone<=0||Plugin.OverwatchCap<=0||NativeSpawner==null)return;
            overwatchQueued=true;overwatchCap=Math.Min(200,OverwatchPolicy.ExtraCount(Plugin.OverwatchPoints.Length,Plugin.OverwatchCap));
            var selected=new List<Vector3>();
            var allZones=NativeSpawner._allZones;var nativePoints=NativePoints;
            Plugin.Trace($"[RR-ROOF] stage=lookup-source allZones={allZones.Length} globalSpawnPoints={nativePoints.Length} bossScenarioZones={Zones.Length}");
            foreach(var configured in Plugin.OverwatchPoints)
            {
                var position=new Vector3(configured.X,configured.Y,configured.Z);
                var zone=allZones.FirstOrDefault(z=>string.Equals(z.name,configured.Zone,StringComparison.OrdinalIgnoreCase));
                var point=nativePoints.FirstOrDefault(p=>p.BotZoneName==configured.Zone&&Vector3.Distance(p.Position,position)<0.5f);
                if(point==null||zone==null){Plugin.Trace($"[RR-ROOF] stage=native-point-missing zone={configured.Zone} zoneFound={zone!=null} pointFound={point!=null} position={position}");continue;}
                if(selected.Any(p=>Vector3.Distance(p,point.Position)<1))continue;
                selected.Add(point.Position);
                for(int i=0;i<2&&rooftopQueue.Count<overwatchCap;i++)rooftopQueue.Enqueue((zone!.name,point.Position));
            }
            overwatchPlanned=rooftopQueue.Count;
            Plugin.Trace($"[RR-ROOF] stage=planned nativePoints={selected.Count} extras={overwatchPlanned} normalRegistered={normalProfiles.Count}; two extras per native point, independent of baseline population");
        }
        foreach(var request in Requests.Values.Where(r=>!r.Finished))
        {
            var live=players.FirstOrDefault(p=>request.Profiles.Contains(p.ProfileId));
            if(live==null)continue;
            request.Finished=true;request.Confirmed=true;
            Plugin.Trace($"[RR-ROOF] stage=live-confirmed profile={live.ProfileId} role={live.Profile.Info.Settings.Role} requested={request.ExactPoint} actual={live.Position} offset={(request.ExactPoint.HasValue?Vector3.Distance(live.Position,request.ExactPoint.Value):0):0.0}");
        }
        int alive=players.Count(p=>p.HealthController?.IsAlive==true),pending=Requests.Values.Count(r=>!r.Finished);
        Status=$"Extreme Overwatch extras: queued={rooftopQueue.Count}, pending={pending}, confirmed={Requests.Values.Count(r=>r.Confirmed)}/{overwatchPlanned}, total AI={alive}";
        if(Time.unscaledTime>=nextOverwatchLog){nextOverwatchLog=Time.unscaledTime+15;Plugin.Trace("[RR-ROOF] stage=progress "+Status);}
        if(elapsed>240||(rooftopQueue.Count==0&&pending==0))
        {overwatchDone=true;Plugin.Trace("[RR-ROOF] stage=finished "+Status);return;}
        if(rooftopQueue.Count==0||Scenario==null)return;
        var slot=rooftopQueue.Dequeue();
        var nativePoint=NativePoints.FirstOrDefault(p=>Vector3.Distance(p.Position,slot.Position)<0.5f);
        if(nativePoint==null){Plugin.Trace($"[RR-ROOF] stage=point-disappeared position={slot.Position}");return;}
        if(nativePoint.SpawnBlocked||nativePoint.NextBornTime>Time.time){rooftopQueue.Enqueue(slot);return;}
        if(Vector3.Distance(slot.Position,Plugin.Local!.Position)<30){rooftopQueue.Enqueue(slot);return;}
        var requestNew=new Reinforcement{Wave=-1,Role="marksman",Submitted=Time.unscaledTime,ExactPoint=slot.Position};
        var spawn=new BossLocationSpawn{BossName="marksman",BossEscortType="assault",BossEscortAmount="0",BossChance=100,BossZone=slot.Zone,BossDifficult="hard",BossEscortDifficult="hard",Time=0,ForceSpawn=true,IgnoreMaxBots=true,Supports=Array.Empty<WildSpawnSupports>(),TriggerId="",TriggerName=""};
        spawn.Init();requestNew.Spawn=spawn;Requests[spawn]=requestNew;
        try{Scenario.ActivateWave(spawn);Plugin.Trace($"[RR-ROOF] stage=submitted zone={slot.Zone} position={slot.Position}");}
        catch(Exception e){requestNew.Finished=true;Plugin.Trace("[RR-ROOF] stage=submit-error "+e.GetBaseException().Message);}
    }
    private void Roam(Player[] players)
    {
        foreach(var p in players)
        {
            if(p.HealthController?.IsAlive!=true || !p.Profile.Info.Settings.Role.ToString().StartsWith("boss",StringComparison.OrdinalIgnoreCase))continue;
            var bot=p.AIData?.BotOwner;
            if(bot?.Memory==null || bot.PatrollingData==null || bot.Mover==null)continue;
            if(bot.Memory.HaveEnemy || bot.Mover.IsMoving){idle[p.ProfileId]=(p.Position,Time.unscaledTime);continue;}
            if(!idle.TryGetValue(p.ProfileId,out var old)||Vector3.Distance(old.Pos,p.Position)>1){idle[p.ProfileId]=(p.Position,Time.unscaledTime);continue;}
            if(Time.unscaledTime-old.Since<20)continue;
            idle[p.ProfileId]=(p.Position,Time.unscaledTime);
            try
            {
                // Ask the boss's own patrol controller to select a valid point. It retains
                // path validation, followers and brain priority; we never teleport or replace combat.
                bot.PatrollingData.Unpause();bot.PatrollingData.BossGoNextPoint();
                Plugin.Trace($"[RR-ROAM] stage=patrol-request profile={p.ProfileId} role={p.Profile.Info.Settings.Role}; movement remains native AI controlled");
            }
            catch(Exception e){Plugin.Trace("[RR-ROAM] stage=request-error "+e.GetBaseException().Message);}
        }
    }
}
[HarmonyPatch(typeof(BossSpawnScenario),"Run")]
internal static class DirectorCapture
{private static void Postfix(BossSpawnScenario __instance,List<BotZone> __0)=>LocalRaidDirector.Capture(__instance,__0);}
[HarmonyPatch(typeof(BossSpawnScenario),"Stop")]
internal static class DirectorStop
{private static void Prefix(BossSpawnScenario __instance)=>LocalRaidDirector.Stop(__instance);}
[HarmonyPatch(typeof(BotBossSpawn),"TrySpawn")]
internal static class WaveAttemptObserver
{
    private static bool Prefix(BotBossSpawn __instance,BossLocationSpawn __0,ref bool __result,out BossLocationSpawn? __state)
    {
        LocalRaidDirector.NativeSpawner=__instance;
        __state=LocalRaidDirector.CurrentAttempt;
        LocalRaidDirector.CurrentAttempt=LocalRaidDirector.Managed(__0)?__0:null;
        if(!LocalRaidDirector.Managed(__0))return true;
        if(LocalRaidDirector.Requests.TryGetValue(__0,out var managed)&&managed.Wave==-2&&(managed.Finished||GunGameDirector.PlayerWon||Plugin.Local?.HealthController?.IsAlive!=true)){__result=false;return false;}
        if(LocalRaidDirector.Rooftop(__0))return true; // Separate finite rooftop budget, above normal map population.
        int count=1+Math.Max(0,__0.EscortCount);
        if(LocalRaidDirector.Alive()+count<=LocalRaidDirector.ActiveCap)return true;
        __result=false;return false;
    }
    private static void Postfix(BossLocationSpawn __0,bool __result)=>LocalRaidDirector.Outcome(__0,__result);
    private static Exception? Finalizer(Exception? __exception,BossLocationSpawn? __state){LocalRaidDirector.CurrentAttempt=__state;return __exception;}
}
[HarmonyPatch(typeof(BotBossSpawn),"SpawnBossAndFollowers")]
internal static class WavePlacementObserver
{private static bool Prefix(BotCreationData __0,BossLocationSpawn __1,ref System.Collections.Generic.List<EFT.Game.Spawning.ISpawnPoint> __5,ref System.Threading.Tasks.Task __result)
    {
        if(LocalRaidDirector.Requests.ContainsKey(__1))
        {
            __5=new System.Collections.Generic.List<EFT.Game.Spawning.ISpawnPoint>(__5);
            var request=LocalRaidDirector.Requests[__1];
            if(LocalRaidDirector.Rooftop(__1)&&request.ExactPoint.HasValue)
                __5=LocalRaidDirector.NativePoints.Where(p=>Vector3.Distance(p.Position,request.ExactPoint.Value)<=0.5f).ToList();
            var local=Plugin.Local;
            if(local!=null)__5.RemoveAll(p=>Vector3.Distance(p.Position,local.Position)<30);
            if(request.ExactPoint.HasValue)__5.RemoveAll(p=>Vector3.Distance(p.Position,request.ExactPoint.Value)>0.5f);
            if(__5.Count==0){__result=System.Threading.Tasks.Task.CompletedTask;Plugin.Trace("[RR-WAVE] stage=placement-block no point at least 30m from player; request left unconfirmed");return false;}
        }
        LocalRaidDirector.ObserveProfiles(__1,__0);return true;
    }}

[HarmonyPatch(typeof(BotBossSpawn),"SpawnBossMain")]
internal static class WaveCreationObserver
{
    private static void Prefix(BotCreationData __0,ref Action<BotOwner> __5)
    {
        if(!LocalRaidDirector.Creating.TryGetValue(__0,out var request))return;
        var original=__5;
        __5=bot=>{original?.Invoke(bot);if(bot!=null)LocalRaidDirector.ObserveCreated(request,bot);};
    }
}

[HarmonyPatch(typeof(BotZonesLeaveController),"IsZoneBlockFor")]
internal static class ConcentrateZoneGate
{
    private static readonly TraceLimiter limit=new();
    private static void Postfix(BotZonesLeaveController __instance,BotZone __0,WildSpawnType __1,ref bool __result)
    {
        var wave=LocalRaidDirector.CurrentAttempt;
        if(wave==null)return;
        if(limit.Allow(wave.GetHashCode()+":"+__1+":"+__result))Plugin.Trace($"[RR-WAVE] stage=zone-gate role={__1} zone={__0.name} blocked={__result}");
        bool concentrated=LocalRaidDirector.Concentrate(wave)&&__1==WildSpawnType.assault;
        bool startup=(LocalRaidDirector.Startup(wave)||LocalRaidDirector.Rooftop(wave))&&__1.ToString()==wave.BossName;
        if(!__result || !(concentrated||startup))return;
        __instance.UnBlockZoneFor(__0,__1);__result=false;
        Plugin.Trace($"[RR-WAVE] stage=managed-zone-unblocked zone={__0.name} role={__1} startup={startup}");
    }
}



