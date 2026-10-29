using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.Ballistics;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using JsonType;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace RaidRoulette.TestingClient;

internal sealed class GunGameDirector
{
 private sealed class Actor{public int Desired,Applied,Attempt;public bool Won,Knife,Changing;public float RefillAt,EmptySince=-1,HandsAt;public Item? Pending;public int PendingStage,ProgressPending;public bool HandsDispatched,Finishing;public readonly HashSet<string> Victims=new();}
 internal static bool PlayerWon,Ready;internal static GunGameDirector? Current;
 private readonly Dictionary<string,(DamageInfo Info,float At)> directDamage=new();
 private GameWorld? world;private string token="",eventId="";private JArray[]? kits;private bool initialized,beginQueued,winShown,bonusShown;private float retryAt,nextRecover;
 private volatile bool busy;
 private readonly ConcurrentQueue<Action> completed=new();private readonly Queue<(JObject Request,Action<JObject> Reply)> requests=new();
 private readonly Dictionary<string,Actor> actors=new();private readonly HashSet<string> rewards=new();private readonly HashSet<string> launcherAmmo=new();
 public string Status{get;private set;}="";
 internal bool ResetPending{get;private set;}
 internal bool CanRespawn=>initialized&&eventId=="gun_game"&&world!=null&&Plugin.Local!=null&&actors.ContainsKey(Plugin.Local.ProfileId);
 internal bool LocalWeaponReady=>Plugin.Local!=null&&actors.TryGetValue(Plugin.Local.ProfileId,out var state)&&state.Applied==0&&!state.Changing&&state.Pending==null&&Plugin.Local.HandsController?.Item is Weapon weapon&&Loaded(weapon);
 internal void RestartAfterDeath(string deathId)
 {
  var player=Plugin.Local!;
  if(directDamage.TryGetValue(player.ProfileId,out var direct)&&Time.unscaledTime-direct.At<0.25f&&direct.Info.Player?.iPlayer is IPlayer killer)RecordKill(player,killer,direct.Info,EBodyPart.Common,player.ProfileId+":"+deathId);
  var previous=actors[player.ProfileId];var state=new Actor{Applied=-1,Attempt=previous.Attempt+1};actors[player.ProfileId]=state;
  PlayerWon=winShown=bonusShown=false;ResetPending=true;
  Send(new JObject{["Action"]="respawn",["DeathId"]=deathId},response=>
  {
   state.Attempt=response["attempt"]!.Value<int>();state.Desired=response["stage"]!.Value<int>();state.Won=response["won"]!.Value<bool>();state.Knife=response["knife"]!.Value<bool>();ResetPending=false;
   if(player.HealthController?.IsAlive==true)GrantRewards(player,(JArray?)response["rewards"]??new JArray());
  });
 }
 public GunGameDirector(){Current=this;Player.OnPlayerDeadStatic+=Killed;}
 public void Stop(){Player.OnPlayerDeadStatic-=Killed;}
 private void Send(JObject request,Action<JObject> reply){request["Token"]=token;requests.Enqueue((request,reply));}
 private void Dispatch()
 {
  if(busy||requests.Count==0||Time.unscaledTime<retryAt)return;busy=true;var entry=requests.Dequeue();string expected=token;
  Task.Run(async()=>{try{var response=JObject.Parse(await Backend.Post(entry.Request.ToString(Formatting.None),"/client/raidroulette/gungame"));if(response["data"] is JObject data)response=data;var result=response;completed.Enqueue(()=>{if(expected==token){if(result["ok"]?.Value<bool>()==true)entry.Reply(result);else{Plugin.Trace("[RR-GUNGAME] rejected="+result["error"]);beginQueued=false;retryAt=Time.unscaledTime+5;if(entry.Request["Action"]?.ToString() is "kill" or "mail_rewards" or "respawn")requests.Enqueue(entry);}}});}catch(Exception e){completed.Enqueue(()=>{if(expected!=token)return;Plugin.Trace("[RR-GUNGAME] request-error="+e.GetBaseException().Message);beginQueued=false;retryAt=Time.unscaledTime+5;if(entry.Request["Action"]?.ToString() is "kill" or "mail_rewards" or "respawn")requests.Enqueue(entry);});}finally{busy=false;}});
 }
 public void Tick()
 {
  try{Core();}catch(Exception e){Plugin.Trace("[RR-GUNGAME] director-error="+e.GetBaseException().Message);retryAt=Time.unscaledTime+5;}
 }
 private void Core()
 {
  while(completed.TryDequeue(out var action))action();
  var current=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance:null;if(current is HideoutGameWorld)current=null;
  if(!ReferenceEquals(world,current)){world=current;token=Guid.NewGuid().ToString("N");kits=null;initialized=beginQueued=winShown=bonusShown=false;actors.Clear();directDamage.Clear();requests.Clear();rewards.Clear();launcherAmmo.Clear();Status="";retryAt=0;PlayerWon=Ready=ResetPending=false;}
  Dispatch();
  if(current==null)
  {
   if(Time.unscaledTime>=nextRecover&&!busy&&requests.Count==0){nextRecover=Time.unscaledTime+30;Send(new JObject{["Action"]="recover"},_=>{});}return;
  }
  var game=Singleton<AbstractGame>.Instantiated?Singleton<AbstractGame>.Instance:null;var local=Plugin.Local;
  if(local==null||local.IsAI||local.HealthController?.IsAlive!=true||game?.GameTimer?.StartDateTime==null||game.Status is GameStatus.Stopped or GameStatus.Stopping or GameStatus.SoftStopping)return;
  eventId=Plugin.RaidContext.EndsWith(": gun_game",StringComparison.Ordinal)?"gun_game":Plugin.RaidContext.EndsWith(": ak50",StringComparison.Ordinal)?"ak50":"";
  if(eventId.Length==0)return;
  if(local.Profile.Info.Side==EPlayerSide.Savage){Status="Gun Game/AK50 weapon swaps require a PMC raid";return;}
  if(!initialized&&!beginQueued&&Time.unscaledTime>=retryAt)
  {
   beginQueued=true;Send(new JObject{["Action"]="begin",["ProfileId"]=local.ProfileId},response=>
   {
    Plugin.StartupCap=response["populationCap"]?.Value<int>()??Plugin.StartupCap;Plugin.StartupTarget=response["populationTarget"]?.Value<int>()??Plugin.StartupTarget;
    kits=((JArray)response["kits"]!).OfType<JArray>().ToArray();if(kits.Length==9)foreach(var item in kits[8].OfType<JObject>()){string slot=item["slotId"]?.ToString()??"";if(slot=="cartridges"||slot.StartsWith("camora")||slot.StartsWith("patron"))launcherAmmo.Add(item["_tpl"]!.ToString());}Status="Gun Game/AK50: loading native weapon assets";WarmAssets(kits,token);var state=new Actor{Desired=response["stage"]?.Value<int>()??0,Applied=-1,Attempt=response["attempt"]?.Value<int>()??0,Won=response["won"]?.Value<bool>()??false,Knife=response["knife"]?.Value<bool>()??false};actors[local.ProfileId]=state;
    Plugin.Trace("[RR-GUNGAME] stage=escrow-saved originalItems="+((JArray)response["originalIds"]!).Count+" kits="+kits.Length);
   });
  }
  if(!initialized||kits==null)return;
  var players=eventId=="ak50"?new[]{local}:current.RegisteredPlayers.OfType<Player>().Where(p=>p.HealthController?.IsAlive==true&&(p.IsYourPlayer||Ordinary(p))).ToArray();
  foreach(var player in players)
  {
   if(player.IsYourPlayer&&(GunGameRespawn.Waiting||ResetPending))continue;
   if(!actors.TryGetValue(player.ProfileId,out var state)){state=new();actors[player.ProfileId]=state;}
   if(state.Pending!=null&&state.HandsDispatched&&Time.unscaledTime-state.HandsAt>5&&!player.InventoryController.IsChangingWeapon){state.HandsDispatched=false;state.HandsAt=Time.unscaledTime;}
   if(state.Pending!=null&&Time.unscaledTime>=state.HandsAt&&!state.HandsDispatched)EquipPending(player,state);
   int wanted=eventId=="ak50"?0:state.Desired;
   if(!state.Changing&&state.Applied!=wanted&&Time.unscaledTime>=state.RefillAt)Swap(player,state,wanted);
   if(eventId=="gun_game"&&wanted>=7&&!state.Changing&&Time.unscaledTime>=state.RefillAt)
   {
    var equipped=player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon).ContainedItem as Weapon;
    bool exhausted=equipped==null||!Loaded(equipped);
    if(!exhausted)state.EmptySince=-1;
    else if(wanted==7)
    {
     if(state.EmptySince<0){state.EmptySince=Time.unscaledTime;Plugin.Trace("[RR-GUNGAME] stage=rocket-empty waitingSeconds=10 actor="+player.ProfileId);}
     if(GunGameFlowPolicy.RefillRocket(wanted,state.Desired,state.ProgressPending,Time.unscaledTime-state.EmptySince))Swap(player,state,wanted);
    }
    else if(exhausted)Swap(player,state,wanted);
   }
  }
  var mine=actors[local.ProfileId];Status=eventId=="ak50"?"\nAK50: original weapons held for trader return":$"\nGun Game: {(mine.Won?"WIN":Names[mine.Desired])}, stage {mine.Desired+1}/9, knife bonus {(mine.Knife?"ready":"hidden")}";
 }
 private async void WarmAssets(JArray[] loadouts,string expected)
 {
  try
  {
   var resources=new List<ResourceKey>();
   foreach(var entry in loadouts.SelectMany(k=>k.OfType<JObject>()))
   {
    var item=Singleton<ItemFactory>.Instance.CreateItem(Guid.NewGuid().ToString("N").Substring(0,24),entry["_tpl"]!.ToString(),null);
    foreach(var key in new[]{item.Prefab,item.UsePrefab})if(key!=null&&!string.IsNullOrEmpty(key.path))resources.Add(key);
   }
   var factory=Singleton<ObjectsFactory>.Instance;
   await factory.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,ObjectsFactory.AssemblyType.Local,resources.Distinct().ToArray(),JobYieldPriority.General,null,factory.PoolsCancellationToken);
   completed.Enqueue(()=>{if(expected==token){initialized=Ready=true;Plugin.Trace("[RR-GUNGAME] stage=assets-ready resourceKeys="+resources.Distinct().Count());}});
  }
  catch(Exception e){completed.Enqueue(()=>{if(expected==token){beginQueued=false;retryAt=Time.unscaledTime+10;Plugin.Trace("[RR-GUNGAME] asset-load-error="+e.GetBaseException().Message);}});}
 }
 private static bool Ordinary(Player p){string role=p.Profile.Info.Settings.Role.ToString().ToLowerInvariant();return p.IsAI&&role.Length>0&&role is not ("player" or "playerscav" or "shooterbtr");}
 private static readonly string[] Categories={"pistol","shotgun","smg","sniperRifle","ak50","assaultRifle","machinegun","rocket","grenadeLauncher"};
 private static readonly string[] Names={"Pistol","Shotgun","SMG","Sniper","AK50","Assault Rifle","LMG","Rocket Launcher","Grenade Launcher"};
 private string Kind(DamageInfo damage)
 {
  if(damage.DamageType==EDamageType.Melee||damage.Weapon is Knife)return "knife";
  string template=damage.Weapon?.TemplateId??damage.SourceId??"";
  if(template=="676bf44c5539167c3603e869"||damage.SourceId=="67446fdd752be02c220f27b3")return "rocket";
  if(template=="67d0576f29f580ebc10efd08")return "ak50";
  if(launcherAmmo.Contains(damage.SourceId??"")||launcherAmmo.Contains(template))return "grenadeLauncher";
  return damage.Weapon is Weapon weapon?weapon.Template.weapClass:"";
 }
 private void Killed(Player victim,IPlayer killer,DamageInfo damage,EBodyPart part)=>RecordKill(victim,killer,damage,part,victim.ProfileId);
 private void RecordKill(Player victim,IPlayer killer,DamageInfo damage,EBodyPart part,string victimId)
 {
  if(!initialized||eventId!="gun_game"||world==null)return;
  bool captured=directDamage.TryGetValue(victim.ProfileId,out var direct)&&Time.unscaledTime-direct.At<0.25f;
  if(captured){damage=direct.Info;killer=damage.Player?.iPlayer??killer;}
  directDamage.Remove(victim.ProfileId);
  Plugin.Trace($"[RR-GUNGAME] stage=kill-observed victim={victim.ProfileId} killer={killer?.ProfileId} type={damage.DamageType} weapon={damage.Weapon?.TemplateId} source={damage.SourceId} direct={captured}");
  if(killer==null||killer.ProfileId==victim.ProfileId||victim.IsYourPlayer&&GunGameRespawn.Ending||killer.IsYourPlayer&&GunGameRespawn.Down)return;
  string kind=Kind(damage);if(kind.Length==0)return;string killerId=killer.ProfileId;
  completed.Enqueue(()=>
  {
   var player=world?.RegisteredPlayers.OfType<Player>().FirstOrDefault(p=>p.ProfileId==killerId);if(player==null||(!player.IsYourPlayer&&!Ordinary(player)))return;
   if(!actors.TryGetValue(killerId,out var state)){state=new();actors[killerId]=state;}
   if(!state.Victims.Add(victimId))return;
   if(player.IsYourPlayer)
   {
    state.ProgressPending++;
    Send(new JObject{["Action"]="kill",["Victim"]=victimId,["Kind"]=kind,["Attempt"]=state.Attempt},response=>
    {
     if(!actors.TryGetValue(player.ProfileId,out var currentActor)||!ReferenceEquals(currentActor,state))return;
     state.ProgressPending=Math.Max(0,state.ProgressPending-1);
     state.Desired=response["stage"]!.Value<int>();state.Won=response["won"]!.Value<bool>();PlayerWon=state.Won;state.Knife=response["knife"]!.Value<bool>();
     if(state.Won&&!winShown){winShown=true;Notify("you win");if(Singleton<GUISounds>.Instantiated)Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.AchievementCompleted);}
     if(state.Won&&state.Knife&&!bonusShown){bonusShown=true;Notify("Secret condition complete: +2 Bitcoins");}
     if(player.HealthController?.IsAlive==true)GrantRewards(player,(JArray?)response["rewards"]??new JArray());
    });
   }
   else if(kind=="knife")state.Knife=true;
   else if(!state.Won&&kind==Categories[state.Desired]){if(state.Desired==8)state.Won=true;else state.Desired++;}
  });
 }
 private static void Notify(string text)=>NotificationManager.DisplayMessageNotification(text,ENotificationDurationType.Long,ENotificationIconType.Quest);
 private FlatItem[] Fresh(JArray kit,bool fresh)
 {
  var copy=(JArray)kit.DeepClone();if(fresh){var ids=copy.OfType<JObject>().ToDictionary(i=>i["_id"]!.ToString(),_=>Guid.NewGuid().ToString("N").Substring(0,24));foreach(var i in copy.OfType<JObject>()){i["_id"]=ids[i["_id"]!.ToString()];string? parent=i["parentId"]?.ToString();if(parent!=null&&ids.TryGetValue(parent,out var replacement))i["parentId"]=replacement;}}
  return copy.OfType<JObject>().Select(i=>new FlatItem{_id=new MongoID(i["_id"]!.ToString()),_tpl=new MongoID(i["_tpl"]!.ToString()),parentId=string.IsNullOrEmpty(i["parentId"]?.ToString())?(MongoID?)null:new MongoID(i["parentId"]!.ToString()),slotId=i["slotId"]?.ToString()!,upd=i["upd"] is JObject update?new UnparsedData{JToken=update}:null!,location=i["location"] is JToken location&&location.Type!=JTokenType.Null?new UnparsedData{JToken=location}:null!}).ToArray();
 }
 private void Swap(Player player,Actor state,int index)
 {
  state.RefillAt=Time.unscaledTime+5;string expectedToken=token;
  var flat=Fresh(kits![index],true);var result=Singleton<ItemFactory>.Instance.FlatItemsToTree(flat);
  if(result.DeserializationErrors.Count>0)throw new InvalidOperationException("Weapon preset could not deserialize: "+result.DeserializationErrors[0]);
  var root=result.Items[flat[0]._id];if(root is Weapon automatic&&automatic.Template.weapFireType?.Contains(Weapon.EFireMode.fullauto)==true)automatic.FireMode.SetFireMode(Weapon.EFireMode.fullauto);state.Changing=true;state.RefillAt=Time.unscaledTime+3;
  if(player.IsAI&&player.AIData?.BotOwner?.WeaponManager is BotWeaponManager changingManager){changingManager.Selector.IsWeaponReady=false;changingManager.Selector.IsChanging=true;}
  try{player.Proceed(false,empty=>
  {
   try
   {
    if(expectedToken!=token||world==null||player.HealthController?.IsAlive!=true||!actors.TryGetValue(player.ProfileId,out var live)||!ReferenceEquals(live,state))return;
    if(!string.IsNullOrEmpty(empty.Error))throw new InvalidOperationException(empty.Error);
    var old=new List<(Slot Slot,Item Item)>();var equipment=player.Profile.Inventory.Equipment;
    foreach(var slotName in new[]{EquipmentSlot.FirstPrimaryWeapon,EquipmentSlot.SecondPrimaryWeapon,EquipmentSlot.Holster})
    {var slot=equipment.GetSlot(slotName);var item=slot.ContainedItem;if(item==null)continue;var address=item.CurrentAddress;var removed=slot.RemoveItemWithoutRestrictions();if(!removed.Succeeded)throw new InvalidOperationException("Cannot remove old event weapon");address.RaiseRemoveEvent(item,CommandStatus.Succeed,player.InventoryController);old.Add((slot,item));}
    try
    {
     var slot=equipment.GetSlot(index==0&&eventId!="ak50"?EquipmentSlot.Holster:EquipmentSlot.FirstPrimaryWeapon);var added=slot.AddWithoutRestrictions(root);if(!added.Succeeded)throw new InvalidOperationException("Cannot equip event weapon");slot.CreateItemAddress().RaiseAddEvent(root,CommandStatus.Succeed,player.InventoryController);
     state.Pending=root;state.PendingStage=index;state.HandsAt=Time.unscaledTime+0.1f;state.HandsDispatched=false;state.EmptySince=-1;
     Plugin.Trace("[RR-GUNGAME] stage=inventory-swapped actor="+player.ProfileId+" level="+index+" tpl="+root.TemplateId);
    }
    catch{foreach(var previous in old){if(previous.Slot.ContainedItem==null){previous.Slot.AddWithoutRestrictions(previous.Item);previous.Slot.CreateItemAddress().RaiseAddEvent(previous.Item,CommandStatus.Succeed,player.InventoryController);}}throw;}


   }
   catch(Exception e){Plugin.Trace("[RR-GUNGAME] swap-error="+e.GetBaseException().Message);state.RefillAt=Time.unscaledTime+5;}
   finally{if(state.Pending==null)state.Changing=false;}
  },false);}catch{state.Changing=false;throw;}
 }
 private static bool Loaded(Weapon weapon)=>weapon.GetCurrentMagazine()?.Count>0||(weapon.Chambers??Array.Empty<Slot>()).Any(c=>c.ContainedItem is Ammo ammo&&!ammo.IsUsed);
 internal void ObserveDamage(Player victim,DamageInfo damage)
 {
  if(!initialized||eventId!="gun_game")return;
  if(damage.Player?.iPlayer==null){directDamage.Remove(victim.ProfileId);return;}
  directDamage[victim.ProfileId]=(damage,Time.unscaledTime);
 }
 private void EquipPending(Player player,Actor state)
 {
  var root=state.Pending!;state.HandsDispatched=true;string expected=token;
  void Failed(string error){state.HandsDispatched=false;state.HandsAt=Time.unscaledTime+1;Plugin.Trace("[RR-GUNGAME] hands-retry actor="+player.ProfileId+" error="+error);}
  void ReadyInHands(IHandsController hands)
  {
   if(expected!=token||player.HealthController?.IsAlive!=true||state.Pending!=root||state.Finishing||!actors.TryGetValue(player.ProfileId,out var live)||!ReferenceEquals(live,state))return;
   state.Finishing=true;try{
   if(hands==null||hands.Item!=root||player.HandsController?.Item!=root){Failed("native callback did not equip the requested item");return;}
   if(player.HandsController is Player.FirearmController firearm){if(firearm.IsInSpawnOperation())firearm.FastForwardCurrentState();firearm.SetAnimatorValues();}
   if(player.IsAI&&hands is IFirearmHandsController)
   {
    var manager=player.AIData.BotOwner.WeaponManager;var slot=state.PendingStage==0&&eventId!="ak50"?EquipmentSlot.Holster:EquipmentSlot.FirstPrimaryWeapon;
    foreach(var oldSlot in new[]{EquipmentSlot.FirstPrimaryWeapon,EquipmentSlot.SecondPrimaryWeapon,EquipmentSlot.Holster})manager.info.Remove(oldSlot);manager.Selector._lastEquipmentSlot=slot;manager.Selector._mainWeapon=slot;manager.UpdateWeaponsList();manager.UpdateHandsController(hands,out var ready);manager.Selector.IsChanging=false;manager.Selector.IsWeaponReady=ready;manager.Selector.OnActiveEquipmentSlotChanged?.Invoke(slot);
    if(!ready){Failed("AI weapon manager did not accept the new controller");return;}
   }
   if(player.IsAI&&root is ThrowWeap){var selector=player.AIData.BotOwner.WeaponManager.Selector;selector.IsChanging=false;selector.IsWeaponReady=true;}
   state.Applied=state.PendingStage;state.Pending=null;state.Changing=false;state.HandsDispatched=false;state.RefillAt=Time.unscaledTime+0.5f;
   Plugin.Trace($"[RR-GUNGAME] stage=hands-ready actor={player.ProfileId} level={state.Applied} template={root.TemplateId} loaded={(root is Weapon gun?Loaded(gun):true)} ai={player.IsAI}");
   }catch(Exception e){Failed(e.GetBaseException().Message);}finally{state.Finishing=false;}
  }
  try
  {
   if(player.HandsController?.Item==root){ReadyInHands(player.HandsController);return;}
   if(root is Weapon gun)player.Proceed(gun,result=>{if(result.Succeed)ReadyInHands(result.Value);else Failed(result.Error??"weapon controller failed");},false);
   else if(root is ThrowWeap grenade)player.Proceed(grenade,(Result<IGrenadeController> result)=>{if(result.Succeed)ReadyInHands(result.Value);else Failed(result.Error??"grenade controller failed");},false);
   else Failed("unsupported event item");
  }
  catch(Exception e){Failed(e.GetBaseException().Message);}
 }
 private static void Remove(Player player,Item item){var address=item.CurrentAddress;if(address==null)return;if(address.RemoveWithoutRestrictions(item).Succeeded)address.RaiseRemoveEvent(item,CommandStatus.Succeed,player.InventoryController);}
 private static bool TryStore(Player player,Item item)
 {
  foreach(var slotName in new[]{EquipmentSlot.Pockets,EquipmentSlot.TacticalVest,EquipmentSlot.Backpack,EquipmentSlot.SecuredContainer})
  if(player.Profile.Inventory.Equipment.GetSlot(slotName).ContainedItem is CompoundItem container)
  foreach(var grid in container.Grids){var address=grid.FindLocationForItem(item);if(address!=null&&address.AddWithoutRestrictions(item).Succeeded){address.RaiseAddEvent(item,CommandStatus.Succeed,player.InventoryController);return true;}}
  return false;
 }
 private void GrantRewards(Player player,JArray issued)
 {
  var mail=new JArray();foreach(var entry in issued.OfType<JObject>())
  {
   string id=entry["_id"]!.ToString();if(rewards.Contains(id)||player.Profile.Inventory.Equipment.GetAllItems().Any(i=>i.Id==id))continue;
   var item=Singleton<ItemFactory>.Instance.CreateItem(id,"59faff1d86f7746c51718c9c",null);item.SpawnedInSession=true;
   if(!TryStore(player,item)){mail.Add(id);Notify("Inventory full: Bitcoin reward sent by trader mail");}rewards.Add(id);
  }
  if(mail.Count>0)Send(new JObject{["Action"]="mail_rewards",["ItemIds"]=mail},_=>{});
 }
}















