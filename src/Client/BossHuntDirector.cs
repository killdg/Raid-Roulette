using System;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.AI;
namespace RaidRoulette.TestingClient;

internal sealed class BossHuntDirector
{
    private GameWorld? world;
    private static BotOwner? hunter;
    internal static string? AssignedHunter=>hunter?.ProfileId;
    private static Vector3? destination;
    private static bool submitting,finished;
    private float nextTick,nextPath,nextLog;
    private Vector3 lastPosition;
    private float lastMoved;
    private float bestRemaining=float.MaxValue,lastProgress;
    public string Status {get;private set;}="";
    internal static bool IsHunter(BotOwner? owner)=>!finished&&owner!=null&&ReferenceEquals(owner,hunter)&&Plugin.RaidContext.Contains(": boss_hunt")&&Plugin.Local?.HealthController?.IsAlive==true&&owner.HealthController?.IsAlive==true;
    internal static bool IsTarget(BotOwner? owner,IPlayer? target)=>IsHunter(owner)&&Plugin.IsLocal(target);
    internal static bool IsGroup(BotsGroup group)=>hunter!=null&&IsHunter(hunter)&&ReferenceEquals(group,hunter.BotsGroup);
    internal static bool OwnsRoute(BotMover mover)
        =>IsHunter(mover._owner)&&mover.CurrentState==EBotMoverState.Default&&destination.HasValue&&Vector3.Distance(mover._owner.Position,destination.Value)>3&&!mover._owner.Medecine.Using&&!mover._owner.BewareGrenade.ShallRunAway()&&(!mover._owner.EnemiesController.EnemyInfos.TryGetValue(Plugin.Local!,out var info)||!info.IsVisible);
    internal static bool AllowRoute(BotOwner owner,Vector3 target)
        =>owner?.Mover==null||!OwnsRoute(owner.Mover)||submitting||Vector3.Distance(target,destination!.Value)<3;
    internal static void Drive(BotOwner? owner)
    {
        if(owner==null||!IsHunter(owner)||owner.BotState!=EBotState.Active||owner.Mover==null||!OwnsRoute(owner.Mover)||owner.Medecine.Using||owner.BewareGrenade.ShallRunAway())return;
        owner.MovementResume();owner.Mover.TargetPose=1;owner.SetTargetMoveSpeed(1);
        owner.Steering.LookToMovingDirection();owner.Sprint(true,false);
    }
    public void Tick()
    {
        var current=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance:null;
        if(current is HideoutGameWorld)current=null;
        if(!ReferenceEquals(world,current)){world=current;hunter=null;destination=null;finished=false;Status="";nextTick=nextPath=nextLog=0;}
        if(current==null||!Plugin.RaidContext.Contains(": boss_hunt")||Time.unscaledTime<nextTick)return;
        var game=Singleton<AbstractGame>.Instantiated?Singleton<AbstractGame>.Instance:null;
        if(game?.GameTimer?.StartDateTime==null||game.Status is GameStatus.Stopped or GameStatus.Stopping or GameStatus.SoftStopping){destination=null;return;}
        nextTick=Time.unscaledTime+0.25f;
        var local=Plugin.Local;if(local==null)return;
        if(finished)return;
        if(local.HealthController?.IsAlive!=true||(hunter!=null&&hunter.HealthController?.IsAlive!=true))
        {finished=true;destination=null;Status="\nBoss Hunt: hunt ended";Plugin.Trace("[RR-HUNT] stage=ended playerAlive="+local.HealthController?.IsAlive+" hunterAlive="+hunter?.HealthController?.IsAlive);return;}
        if(hunter==null)
        {
            hunter=current.RegisteredPlayers.OfType<Player>().Where(p=>p.IsAI&&p.HealthController?.IsAlive==true&&p.Profile.Info.Settings.Role.ToString().Equals(Plugin.HuntBossRole,StringComparison.OrdinalIgnoreCase)).Select(p=>p.AIData?.BotOwner).FirstOrDefault(b=>b!=null);
            Status="\nBoss Hunt: awaiting selected boss "+Plugin.HuntBossRole;
            if(hunter==null)return;
            lastPosition=hunter.Position;lastMoved=lastProgress=Time.unscaledTime;bestRemaining=float.MaxValue;
            Plugin.Trace($"[RR-HUNT] stage=bound profile={hunter.ProfileId} role={Plugin.HuntBossRole}");
        }
        if(hunter.BotState!=EBotState.Active||hunter.BotsGroup==null||hunter.Mover==null)return;
        try
        {
            if(Vector3.Distance(lastPosition,hunter.Position)>1){lastPosition=hunter.Position;lastMoved=lastProgress=Time.unscaledTime;bestRemaining=float.MaxValue;}
            hunter.BotsGroup.AddEnemy(local,EBotEnemyCause.byKill);
            if(hunter.BotsGroup.Enemies.TryGetValue(local,out var groupInfo))groupInfo.EnemyLastPosition=local.Position;
            if(hunter.EnemiesController.EnemyInfos.TryGetValue(local,out var enemy))
            {enemy.IgnoreUntilAggression=false;hunter.Memory.GoalEnemy=enemy;}
            hunter.Exfiltration?.ResetLeaveTime();
            hunter.StandBy.CanDoStandBy=false;hunter.StandBy.StandByType=BotStandByType.active;
            float distance=Vector3.Distance(hunter.Position,local.Position);
            if(distance<bestRemaining-2){bestRemaining=distance;lastProgress=Time.unscaledTime;}
            bool visible=enemy?.IsVisible==true;
            Status=$"\nBoss Hunt: {hunter.Profile.Info.Nickname}, distance={distance:0}m, {(visible?"engaging":"pursuing")}";
            if(visible||distance<3||hunter.Mover.CurrentState!=EBotMoverState.Default||hunter.Medecine.Using||hunter.BewareGrenade.ShallRunAway())
            {destination=null;return;}
            if(Time.unscaledTime>=nextPath)
            {
                nextPath=Time.unscaledTime+2;
                if(!destination.HasValue||Vector3.Distance(destination.Value,local.Position)>3||!hunter.Mover.HasPathAndNoComplete||Time.unscaledTime-lastMoved>6||Time.unscaledTime-lastProgress>8||Vector3.Distance(hunter.Mover.LastDestination(),destination.Value)>3)
                {
                    destination=null;
                    if(NavMesh.SamplePosition(local.Position,out var hit,2,NavMesh.AllAreas))
                    {
                        var path=new NavMeshPath();
                        if(NavMesh.CalculatePath(hunter.Position,hit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete&&path.corners.Length>=2)
                        {
                            destination=hit.position;submitting=true;
                            try{hunter.Mover.GoToByWay(path.corners,0.8f);hunter.MovementResume();}
                            finally{submitting=false;}
                        }
                    }
                }
            }
            if(destination.HasValue)Drive(hunter);
            else Status=$"\nBoss Hunt: {hunter.Profile.Info.Nickname}, distance={distance:0}m, searching for a reachable path";
            if(Time.unscaledTime>=nextLog){nextLog=Time.unscaledTime+10;Plugin.Trace($"[RR-HUNT] stage=pursuit distance={distance:0.0} route={destination.HasValue} playerEnemy={hunter.BotsGroup.IsEnemy(local)} position={hunter.Position} target={local.Position} moving={hunter.Mover.IsMoving} paused={hunter.Mover.Pause} hasPath={hunter.Mover.HasPathAndNoComplete} moverState={hunter.Mover.CurrentState} noMovementFor={Time.unscaledTime-lastMoved:0.0} noProgressFor={Time.unscaledTime-lastProgress:0.0}");}
        }
        catch(Exception e){if(Time.unscaledTime>=nextLog){nextLog=Time.unscaledTime+10;Plugin.Trace("[RR-HUNT] stage=error "+e.GetBaseException().Message);}}
    }
}



