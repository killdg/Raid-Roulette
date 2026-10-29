using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace RaidRoulette.TestingClient;

internal sealed class LiveBotDiagnostics
{
    private GameWorld? world;
    private float nextScan, nextSummary;
    private bool warned;
    private readonly Dictionary<string,string> seen = new();
    private readonly Dictionary<string,int> roleCounts = new();
    private readonly Dictionary<string,int> changes = new();
    public void Tick(ManualLogSource log,string lastReportedRaid)
    {
        var current=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance:null;
        if(current is HideoutGameWorld) current=null;
        if(!ReferenceEquals(world,current))
        {
            if(world != null) Summary(log,"world-ended");
            world=current;seen.Clear();roleCounts.Clear();changes.Clear();warned=false;nextScan=nextSummary=0;
            if(current!=null)log.LogInfo("[RR-DIAG] stage=client-world-start; observing registered AI, not assuming successful spawns from server schedules");
        }
        if(current==null || Time.unscaledTime<nextScan) return;
        nextScan=Time.unscaledTime+1f;
        try
        {
            foreach(var player in current.RegisteredPlayers.OfType<Player>().ToArray())
            {
                if(player==null || !player.IsAI || player.Profile?.Info?.Settings==null || player.Profile.Inventory?.Equipment==null) continue;
                string id=player.ProfileId;
                string role=player.Profile.Info.Settings.Role.ToString();
                var weapons=player.Profile.Inventory.Equipment.GetAllItems().OfType<Weapon>()
                    .Select(w=>$"{w.TemplateId}:{w.WeapClass}").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                string value=$"role={role} side={player.Profile.Info.Side} weapons=[{string.Join(",",weapons)}]";
                bool first=!seen.TryGetValue(id,out var old);
                if(first)
                {
                    if(seen.Count>=500)continue;
                    roleCounts[role]=roleCounts.TryGetValue(role,out int count)?count+1:1;
                    log.LogInfo($"[RR-DIAG] stage=client-live-observed bot={id} {value}");
                    seen[id]=value;
                }
                else if(old!=value)
                {
                    int updates=changes.TryGetValue(id,out int count)?count:0;
                    if(updates<3) log.LogInfo($"[RR-DIAG] stage=client-live-changed bot={id} {value}");
                    changes[id]=updates+1;seen[id]=value;
                }
            }
            if(Time.unscaledTime>=nextSummary)
            {
                nextSummary=Time.unscaledTime+30f;
                Summary(log,"periodic");
                log.LogInfo("[RR-DIAG] stage=client-backend-context lastReportedRaid="+lastReportedRaid);
                if(lastReportedRaid.Contains("clone_wars"))
                {
                    var actors=current.RegisteredPlayers.OfType<Player>().Where(p=>p.IsAI && p.HealthController?.IsAlive==true).Take(40).ToArray();
                    foreach(var actor in actors)
                    {
                        var owner=actor.AIData?.BotOwner;
                        log.LogInfo($"[RR-CLONE] stage=live-affiliation profile={actor.ProfileId} role={actor.Profile.Info.Settings.Role} side={actor.Side} profileGroup={actor.GroupId} team={NativeSpawnDiagnostics.Read(actor.Profile.Info,"TeamId")} aiGroup={owner?.BotsGroup?.Id} state={owner?.BotState} ready={owner?.WeaponManager?.IsReady} hands={actor.HandsController?.GetType().Name} goal={owner?.Memory?.GoalEnemy?.Person?.ProfileId}");
                    }
                    int pairs=0;
                    foreach(var actor in actors.Where(p=>p.Profile.Info.Settings.Role.ToString() is "pmcUSEC" or "pmcBEAR"))
                    {
                        var group=actor.AIData?.BotOwner?.BotsGroup;if(group==null)continue;
                        foreach(var other in actors.Where(p=>p!=actor).OrderBy(p=>Vector3.Distance(p.Position,actor.Position)).Take(6))
                        {
                            if(pairs++>=64)break;
                            log.LogInfo($"[RR-CLONE] stage=live-relation source={actor.ProfileId} target={other.ProfileId} targetRole={other.Profile.Info.Settings.Role} distance={Vector3.Distance(actor.Position,other.Position):0.0} sameAiGroup={ReferenceEquals(group,other.AIData?.BotOwner?.BotsGroup)} enemy={group.IsEnemy(other)} reciprocalEnemy={other.AIData?.BotOwner?.BotsGroup?.IsEnemy(actor)}");
                        }
                        if(pairs>=64)break;
                    }
                }
            }
        }
        catch(Exception e) { if(!warned) {warned=true;log.LogWarning("[RR-DIAG] Live observer failed: "+e.GetBaseException().Message);} }
    }
    private void Summary(ManualLogSource log,string why) => log.LogInfo($"[RR-DIAG] stage=client-role-summary reason={why} uniqueObserved={seen.Count} roles=[{string.Join(",",roleCounts.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value))}]. Counts are observed profiles, not current alive totals.");
}
