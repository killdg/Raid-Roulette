using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using EFT.Communications;
using EFT.UI.SessionEnd;
using HarmonyLib;
namespace RaidRoulette.TestingClient;

internal static class LabStaffExperience
{
 internal sealed class Ledger
 {internal readonly HashSet<string> Victims=new();internal int PerKill,Requested,Gross,Net,Applied;internal bool Finished;}
 private static readonly ConditionalWeakTable<Profile,Ledger> ledgers=new();
 private static ConfigEntry<int> penalty=null!;
 private static string lastProfile="";private static Ledger? lastLedger;
 internal static string Summary="";
 internal static void Bind(ConfigFile config)=>penalty=config.Bind("10. Lab Work Day","XP penalty per staff kill",LabStaffPolicy.DefaultPenalty,new ConfigDescription("Deduct this much from the final raid XP award per personally killed staff member, after native bonuses. Final raid XP cannot fall below zero. Frozen at raid start.",new AcceptableValueRange<int>(0,10000)));
 internal static void Begin(BaseStatisticsManager manager)
 {
  if(manager.Player?.IsYourPlayer!=true)return;
  if(TransitController.IsTransit(manager.Profile.Id,out int _))return;
  ledgers.Remove(manager.Profile);ledgers.Add(manager.Profile,new Ledger{PerKill=penalty?.Value??LabStaffPolicy.DefaultPenalty});Summary="";lastProfile="";lastLedger=null;
 }
 internal static bool Kill(BaseStatisticsManager manager,string id,string name,WildSpawnType role,DamageInfo damage,EDamageType lethalDamageType,EBodyPart bodyPart,EPlayerSide side,string account,int level,float distance)
 {
  if(!LabWorkDirector.Active||!LabStaffPolicy.IsStaff(role.ToString(),name))return false;
  if(manager.Player?.IsYourPlayer==true)
  {
   var state=ledgers.GetValue(manager.Profile,_=>new Ledger{PerKill=penalty?.Value??LabStaffPolicy.DefaultPenalty});
   if(state.Finished)return true;
   if(state.Victims.Add(id))
   {
    state.Requested+=state.PerKill;
    // Keep a casualty in the end-of-raid list, classified as an ordinary human
    // rather than the internal neutral boss spawn role. Combat counters stay untouched.
    manager.Profile.EftStats.Victims.Add(new(account,id,name,side,manager.CurrentSessionLength,level,bodyPart,damage.Weapon?.ShortName,lethalDamageType,distance,WildSpawnType.assault));
    NotificationManager.DisplayMessageNotification("Lab staff killed: -"+state.PerKill.ToString("N0")+" raid XP",ENotificationDurationType.Long,ENotificationIconType.Quest);
    Plugin.Trace("[RR-LAB-XP] civilian="+id+" penalty="+state.PerKill+" total="+state.Requested+"; no boss/kill/headshot/streak XP or combat quest credit");
   }
  }
  return true;
 }
 internal static int Apply(int gross,BaseStatisticsManager manager)
 {
  if(manager.Player?.IsYourPlayer!=true||!ledgers.TryGetValue(manager.Profile,out var state)||state.Victims.Count==0)return gross;
  if(state.Finished)return state.Net;
  state.Gross=Math.Max(0,gross);state.Net=LabStaffPolicy.Award(state.Gross,state.Requested);
  state.Applied=state.Gross-state.Net;
  // Called before native XP storage and level-change notifications. Native code
  // adds only this nonnegative award, preserving all pre-raid experience.
  state.Finished=true;
  lastProfile=manager.Profile.Id;lastLedger=state;
  Summary="\nCivilian casualties: "+state.Victims.Count+" | Raid XP penalty: "+state.Requested.ToString("N0")+" ("+state.Applied.ToString("N0")+" applied)";
  Plugin.Trace("[RR-LAB-XP] gross="+state.Gross+" requested="+state.Requested+" awarded="+state.Net+"; pre-raid XP preserved");
  return state.Net;
 }
 internal static void ShowBreakdown(SessionResultExperienceCount screen,Profile profile)
 {
  if(!ledgers.TryGetValue(profile,out var state))state=lastProfile==profile.Id?lastLedger:null;
  if(state?.Finished!=true)return;
  var label=AccessTools.Field(typeof(SessionResultExperienceCount),"_bonusValue").GetValue(screen);
  if(label!=null)AccessTools.Property(label.GetType(),"text").SetValue(label,state.Gross.ToString("N0")+" XP after bonuses - "+state.Applied.ToString("N0")+" civilian penalty = "+state.Net.ToString("N0")+" XP");
 }
}
[HarmonyPatch(typeof(BaseStatisticsManager),nameof(BaseStatisticsManager.BeginStatisticsSession))]
internal static class LabStaffXpBegin
{private static void Postfix(BaseStatisticsManager __instance)=>LabStaffExperience.Begin(__instance);}
[HarmonyPatch(typeof(BaseStatisticsManager),nameof(BaseStatisticsManager.OnEnemyKill))]
internal static class LabStaffXpKill
{
 private static bool Prefix(BaseStatisticsManager __instance,string playerProfileId,string playerName,WildSpawnType role,DamageInfo damage,EDamageType lethalDamageType,EBodyPart bodyPart,EPlayerSide playerSide,string playerAccountId,int level,float distance)
 {return !LabStaffExperience.Kill(__instance,playerProfileId,playerName,role,damage,lethalDamageType,bodyPart,playerSide,playerAccountId,level,distance);}
}
[HarmonyPatch(typeof(BaseStatisticsManager),nameof(BaseStatisticsManager.OnEnemyDamage))]
internal static class LabStaffXpDamage
{private static bool Prefix(string playerProfileId)=>!LabStaffBehaviour.IsStaffId(playerProfileId);}
[HarmonyPatch(typeof(BaseStatisticsManager),nameof(BaseStatisticsManager.EndStatisticsSession),new[]{typeof(ExitStatus),typeof(float)})]
internal static class LabStaffXpEnd
{
 private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
 {
  var code=instructions.ToList();
  bool Store(CodeInstruction i)=>i.opcode==OpCodes.Stfld&&i.operand is FieldInfo field&&field.Name=="TotalSessionExperience";
  if(code.Count(Store)!=1)throw new InvalidOperationException("Civilian XP adjustment requires exactly one final session XP store.");
  foreach(var i in code)
  {
   if(Store(i))
   {
    // Reuse the labeled instruction, as in the native activation hook. This
    // preserves branch targets without crossing Mono/.NET Label type identities.
    var store=new CodeInstruction(i.opcode,i.operand);i.opcode=OpCodes.Ldarg_0;i.operand=null;
    yield return i;yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(LabStaffExperience),nameof(LabStaffExperience.Apply)));yield return store;
    continue;
   }
   yield return i;
  }
 }
}
[HarmonyPatch(typeof(SessionResultExperienceCount),nameof(SessionResultExperienceCount.Show),new[]{typeof(Profile),typeof(bool),typeof(ExitStatus)})]
internal static class LabStaffXpResult
{private static void Postfix(SessionResultExperienceCount __instance,Profile profile)=>LabStaffExperience.ShowBreakdown(__instance,profile);}
