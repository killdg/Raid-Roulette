using System;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InputSystem;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
namespace RaidRoulette.TestingClient;

internal static class GunGameRespawn
{
 private enum Phase{Playing,Choice,Equipping,Guard,Ending}
 private static Phase phase;private static ConfigEntry<bool>? enabled;
 private static GameWorld? world;private static Player? player;
 private static float readyAt,guardUntil,nextRestore;private static bool cursorHeld,cursorVisible;private static CursorLockMode cursorLock;
 private static string notice="";private static bool restoring;
 internal static bool Waiting=>phase==Phase.Choice;
 internal static bool Down=>phase is Phase.Choice or Phase.Equipping;
 internal static bool Ending=>phase==Phase.Ending;
 internal static bool Protected=>(Down||phase==Phase.Guard&&Time.unscaledTime<guardUntil)&&!Ending&&Running();
 internal static void Bind(ConfigFile config)=>enabled=config.Bind("9. Gun Game","Respawn after lethal damage",false,"Experimental solo Gun Game only. Lethal damage offers Respawn or End run. Respawn resets the ladder and knife bonus, restores health, and equips a pistol. The raid timer continues. Original weapons return only when the raid ends.");
 private static bool Running()
 {
  var game=Singleton<AbstractGame>.Instantiated?Singleton<AbstractGame>.Instance:null;
  return game?.GameTimer?.StartDateTime!=null&&game.Status is not (GameStatus.Stopped or GameStatus.Stopping or GameStatus.SoftStopping)&&(!game.GameTimer.SessionTime.HasValue||game.GameTimer.PastTime<game.GameTimer.SessionTime.Value)&&Plugin.RaidContext.EndsWith(": gun_game",StringComparison.Ordinal);
 }
 internal static bool Intercept(ActiveHealthController health)
 {
  var local=Plugin.Local;
  if(local==null||local.IsAI||health.Player!=local||!health.IsAlive||Ending||!Running())return false;
  if(health.GetBodyPartHealth(EBodyPart.Head).Current>0&&health.GetBodyPartHealth(EBodyPart.Chest).Current>0&&health.GetBodyPartHealth(EBodyPart.Common).Current>0)return false;
  if(Protected){Restore(health);return true;}
  if(enabled?.Value!=true||local.Profile.Info.Side==EPlayerSide.Savage||GunGameDirector.Current?.CanRespawn!=true)return false;
  player=local;phase=Phase.Choice;readyAt=Time.unscaledTime+5;notice="";
  try
  {
   Restore(health);local.Move(Vector2.zero);local.EnableSprint(false);if(local.HandsController is Player.FirearmController firearm)firearm.SetTriggerPressed(false);
   GunGameDirector.Current.RestartAfterDeath(Guid.NewGuid().ToString("N"));
   Plugin.Trace("[RR-RESPAWN] stage=defeated reset=pistol knifeBonus=cleared");return true;
  }
  catch(Exception e){Plugin.Trace("[RR-RESPAWN] intercept-failed="+e.GetBaseException().Message);phase=Phase.Ending;ReleaseCursor();return false;}
 }
 private static void Restore(ActiveHealthController health)
 {
  if(restoring)return;restoring=true;
  try{
  health.RemoveNegativeEffects(EBodyPart.Common);health.RemoveIntoxication();
  foreach(var part in new[]{EBodyPart.Head,EBodyPart.Chest,EBodyPart.Stomach,EBodyPart.LeftArm,EBodyPart.RightArm,EBodyPart.LeftLeg,EBodyPart.RightLeg})
  {health.FullRestoreBodyPart(part);health.RemoveNegativeEffects(part);health.Player.UpdateConditionsAfterBodyPartStateChanged(part);}
  health.ChangeEnergy(1000);health.ChangeHydration(1000);health.Player.UpdateSpeedLimitByHealth();
  var physical=health.Player.Physical;if(physical!=null){physical.Stamina.Current=physical.Stamina.TotalCapacity.Value;physical.HandsStamina.Current=physical.HandsStamina.TotalCapacity.Value;}
  }finally{restoring=false;}
 }
 internal static void Tick()
 {
  var current=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance:null;
  if(!ReferenceEquals(world,current)||!Running()){ReleaseCursor();phase=Phase.Playing;player=null;world=current;return;}
  if(!Down)return;
  if(player==null||player.HealthController?.IsAlive!=true){ReleaseCursor();phase=Phase.Ending;return;}
  if(!cursorHeld){cursorHeld=true;cursorVisible=Cursor.visible;cursorLock=Cursor.lockState;}
  Cursor.lockState=CursorLockMode.None;Cursor.visible=true;player.Move(Vector2.zero);player.EnableSprint(false);
  if(Time.unscaledTime>=nextRestore&&player.HealthController is ActiveHealthController health){nextRestore=Time.unscaledTime+1;Restore(health);}
  if(phase==Phase.Equipping&&GunGameDirector.Current?.LocalWeaponReady==true)
  {
   phase=Phase.Guard;guardUntil=Time.unscaledTime+3;ReleaseCursor();
   EFT.Communications.NotificationManager.DisplayMessageNotification("Gun Game restarted: Level 1 — Pistol",EFT.Communications.ENotificationDurationType.Long,EFT.Communications.ENotificationIconType.Quest);
   Plugin.Trace("[RR-RESPAWN] stage=ready protectionSeconds=3");
  }
 }
 private static void ReleaseCursor(){if(!cursorHeld)return;Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;cursorHeld=false;}
 internal static void Stop(){ReleaseCursor();phase=Phase.Playing;player=null;world=null;}
 private static bool VisibleFrom(Vector3 point,Player enemy)=>!Physics.Linecast(point+Vector3.up*1.4f,enemy.Position+Vector3.up*1.4f,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<Player>()==enemy;
 private static bool FindPoint(out Vector3 result)
 {
  result=default;var local=player!;
  var all=LocalRaidDirector.NativePoints;var pmc=all.Where(p=>EFT.Game.Spawning.SpawnCategoryExtension.ContainBotPmcCategory(p.Categories)).ToArray();
  if(pmc.Length>0)all=pmc;
  var candidates=all.OrderBy(_=>UnityEngine.Random.value).Take(40).ToArray();
  var enemies=world!.RegisteredPlayers.OfType<Player>().Where(p=>p.IsAI&&p.HealthController?.IsAlive==true).ToArray();
  foreach(float distance in new[]{25f,15f})
  {
   float best=float.NegativeInfinity;bool found=false;
   foreach(var point in candidates)
   {
    if(!NavMesh.SamplePosition(point.Position,out var hit,1.5f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-point.Position.y)>1.5f||(hit.position-local.Position).sqrMagnitude<100)continue;
    if(Physics.CheckCapsule(hit.position+Vector3.up*0.4f,hit.position+Vector3.up*1.5f,0.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
    float nearest=enemies.Select(p=>(p.Position-hit.position).sqrMagnitude).DefaultIfEmpty(10000).Min();if(nearest<distance*distance)continue;
    bool visible=enemies.OrderBy(p=>(p.Position-hit.position).sqrMagnitude).Take(3).Any(p=>VisibleFrom(hit.position,p));
    float score=Mathf.Sqrt(nearest)-(visible?50:0);if(score<=best)continue;best=score;result=hit.position;found=true;
   }
   if(found)return true;
  }
  return false;
 }
 internal static void Draw()
 {
  if(!Down)return;int priorDepth=GUI.depth;GUI.depth=-500;var prior=GUI.color;GUI.color=new Color(0,0,0,0.9f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=prior;
  GUILayout.BeginArea(new Rect((Screen.width-440)/2f,(Screen.height-260)/2f,440,260),GUI.skin.window);
  GUILayout.Label("GUN GAME — DEFEATED");GUILayout.Label("The ladder and knife bonus have been reset.\nThe raid timer and AI continue.");
  bool previous=GUI.enabled;
  GUI.enabled=phase==Phase.Choice&&Time.unscaledTime>=readyAt&&GunGameDirector.Current?.ResetPending==false;
  if(GUILayout.Button(phase==Phase.Equipping?"Equipping pistol…":Time.unscaledTime<readyAt?"Respawn in "+Mathf.CeilToInt(readyAt-Time.unscaledTime):GunGameDirector.Current?.ResetPending==true?"Saving restart…":"Respawn — Level 1: Pistol"))
  {
   if(FindPoint(out var point)&&player?.HealthController is ActiveHealthController health){Restore(health);player.Teleport(point);phase=Phase.Equipping;notice="Equipping a loaded pistol…";Plugin.Trace("[RR-RESPAWN] stage=relocated position="+point);}
   else notice="No clear spawn point yet. Try again, or end this run.";
  }
  GUI.enabled=previous;
  if(GUILayout.Button("End run"))
  {
   phase=Phase.Ending;ReleaseCursor();if(player?.HealthController is ActiveHealthController health)health.Kill(EDamageType.Undefined);
  }
  GUILayout.Label(notice);GUILayout.EndArea();GUI.depth=priorDepth;
 }
}

[HarmonyPatch(typeof(ActiveHealthController),nameof(ActiveHealthController.Kill))]
internal static class GunGameLethalRespawn{[HarmonyPriority(Priority.First)]private static bool Prefix(ActiveHealthController __instance)=>!GunGameRespawn.Intercept(__instance);}

[HarmonyPatch(typeof(ActiveHealthController),nameof(ActiveHealthController.ApplyDamage),new[]{typeof(EBodyPart),typeof(float),typeof(EFT.Ballistics.DamageInfo)})]
internal static class GunGameRespawnDamage
{
 private static bool Prefix(ActiveHealthController __instance,ref float __result){if(GunGameRespawn.Protected&&Plugin.IsLocal(__instance.Player)){__result=0;return false;}return true;}
 private static void Postfix(ActiveHealthController __instance,ref float __result){if(GunGameRespawn.Down&&Plugin.IsLocal(__instance.Player))__result=Mathf.Max(0,__result);}
}

[HarmonyPatch(typeof(MoveInputTranslator),nameof(MoveInputTranslator.TranslateAxes))]
internal static class GunGameRespawnMove{private static bool Prefix(MoveInputTranslator __instance)=>!GunGameRespawn.Down||!Plugin.IsLocal(__instance._controller);}

[HarmonyPatch(typeof(PlayerInputTranslator),nameof(PlayerInputTranslator.TranslateCommand))]
internal static class GunGameRespawnHands{private static bool Prefix(PlayerInputTranslator __instance,ref InputNode.ETranslateResult __result){if(!GunGameRespawn.Down||!Plugin.IsLocal(__instance._player))return true;__result=InputNode.ETranslateResult.BlockAll;return false;}}

[HarmonyPatch(typeof(EftGamePlayerOwner),nameof(EftGamePlayerOwner.TranslateCommand))]
internal static class GunGameRespawnOwner{private static bool Prefix(EftGamePlayerOwner __instance,ref InputNode.ETranslateResult __result){if(!GunGameRespawn.Down||!Plugin.IsLocal(__instance.Player))return true;__result=InputNode.ETranslateResult.BlockAll;return false;}}
