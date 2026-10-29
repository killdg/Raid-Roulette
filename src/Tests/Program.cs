using RaidRoulette.Testing;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Utils.Cloners;
using BodyPart = SPTarkov.Server.Core.Models.Eft.Common.Tables.BodyPart;
using Customization = SPTarkov.Server.Core.Models.Eft.Common.Tables.Customization;
using System.Reflection;
using RaidRoulette.Services;
using RaidRoulette.Configuration;
using RaidRoulette.Core;
using RaidRoulette.Runtime;
using RaidRoulette.Patches;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Services.InRaid;
static void Check(bool ok, string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); }
var table=(LocationTable)RuntimeHelpers.GetUninitializedObject(typeof(LocationTable));
var location = new Location { Base = new LocationBase { Id="bigmap", BossLocationSpawn = [new BossLocationSpawn { BossName="bossBully", BossChance=22, BossZone="ZoneA", BossDifficulty="easy", BossEscortDifficulty="normal", BossEscortAmount="2", BossEscortType="followerBully", Time=17, IsRandomTimeSpawn=true, TriggerId="original", Supports=[] }] } };
typeof(LocationTable).GetProperty("Bigmap")!.SetValue(table,location);
var loot = new LootService(table); var bosses=new BossSpawnService(table);
foreach(var cached in new[]{false,true}) {
 var data = new LooseLoot { SpawnpointCount=new SpawnpointCount { Mean=10, Std=2 } };
 var original = new LazyLoad<LooseLoot>(()=>cached ? data : new LooseLoot { SpawnpointCount=new SpawnpointCount{Mean=10,Std=2} },cached);
 location.LooseLoot=original;var rollback=new List<Action>();
 loot.ApplyLooseLootMultiplierForRaid("bigmap",3,rollback.Add);
 Check(location.LooseLoot.Value!.SpawnpointCount!.Mean==30 && location.LooseLoot.Value.SpawnpointCount.Std==6,"Loot multiplier on every read; cached="+cached);
 Check(original.Value!.SpawnpointCount!.Mean==10,"Original loader untouched; cached="+cached);
 rollback.AsEnumerable().Reverse().ToList().ForEach(a=>a());
 Check(ReferenceEquals(location.LooseLoot,original) && location.LooseLoot.Value!.SpawnpointCount!.Mean==10,"Loot loader rollback; cached="+cached);
}
var actions=new List<Action>();var boss=location.Base.BossLocationSpawn[0];
var huntResult=bosses.EnableBossHuntForRaid("bigmap",actions.Add);var hunterSpawn=location.Base.BossLocationSpawn.Single(s=>s.SptId=="rr-boss-hunt");
Check(huntResult.Success&&hunterSpawn.BossChance==100&&hunterSpawn.ForceSpawn==true&&hunterSpawn.IgnoreMaxBots==true,"Boss Hunt requests one starting hunter at 100% chance independently of ordinary population");
Check(hunterSpawn.BossEscortAmount=="0"&&!hunterSpawn.Supports.Any()&&hunterSpawn.Time==-1&&hunterSpawn.BossName!="bossZryachiy","Boss Hunt excludes stationary Zryachiy and removes guards/triggers from the selected hunter");
Check(boss.BossChance==22,"Boss Hunt does not mutate the source boss template");
actions.ForEach(a=>a());Check(location.Base.BossLocationSpawn.Count==1&&ReferenceEquals(location.Base.BossLocationSpawn[0],boss)&&boss.BossChance==22,"Boss Hunt rollback restores the original spawn list and references");
actions.Clear();var settings=new BossConventionConfig();settings.GlobalSettings.BossDifficulty="hard"; settings.GlobalSettings.IncludeGuards=false;
bosses.EnableBossConventionForRaid("bigmap",settings,actions.Add);
Check(boss.BossDifficulty=="hard" && boss.BossEscortAmount=="0" && !boss.Supports.Any(),"Convention difficulty and guards");
actions.AsEnumerable().Reverse().ToList().ForEach(a=>a());Check(boss.Time==17 && boss.IsRandomTimeSpawn==true && boss.TriggerId=="original" && boss.BossDifficulty=="easy" && boss.BossEscortAmount=="2" && boss.BossChance==22,"Convention restores all original properties");
actions.Clear();bosses.AddRaiderGroupsForRaid("bigmap",2,actions.Add);Check(location.Base.BossLocationSpawn.Count==3,"Raider groups added");actions.ForEach(a=>a());Check(location.Base.BossLocationSpawn.Count==1,"Raider groups removed");
Check(EventRegistry.GetEvents().Count==32,"Thirty-two event types discovered including Lab Work Day");
Check(new EventManager(new RaidRouletteConfig { EventChance=0 }).SelectEvent().Event.Id=="normal","Zero event chance selects normal");
Check(typeof(LocationLifecycleService).GetMethod("GenerateLocationAndLoot",new[]{typeof(SPTarkov.Server.Core.Models.Common.MongoId),typeof(string),typeof(bool)})!=null,"Exact SPT generation hook exists");
var cfg=new RaidRouletteConfig{Debug=new DebugConfig{ForceEvent="loot_surge"}};
RaidRouletteRuntime.Initialize(cfg,loot,bosses,_=>{},_=>{});
var prefix=typeof(StartLocalRaidPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static)!;
var finalizer=typeof(StartLocalRaidPatch).GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static)!;
object?[] call={new SPTarkov.Server.Core.Models.Common.MongoId(),"bigmap",null};prefix.Invoke(null,call);Check(location.LooseLoot!.Value!.SpawnpointCount!.Mean==20,"Generation prefix applies event");
var failure=new Exception("generation failed");Check(ReferenceEquals(finalizer.Invoke(null,new[]{failure,call[2]}),failure),"Finalizer preserves original failure");Check(location.LooseLoot.Value!.SpawnpointCount!.Mean==10,"Failure cleanup restores loader");
object?[] again={new SPTarkov.Server.Core.Models.Common.MongoId(),"bigmap",null};prefix.Invoke(null,again);finalizer.Invoke(null,new[]{null,again[2]});Check(location.LooseLoot.Value!.SpawnpointCount!.Mean==10,"Next generation succeeds and cleans up");
if(!args.Contains("--skip-harmony-smoke"))
{
 var harmony = new HarmonyLib.Harmony("rr.port.validation");
 harmony.PatchAll(Assembly.GetExecutingAssembly());
 Check(HarmonyLib.Harmony.GetPatchInfo(typeof(LocationLifecycleService).GetMethod("GenerateLocationAndLoot")!)!.Owners.Contains("rr.port.validation"), "Harmony binds real SPT generation method");
 harmony.UnpatchSelf();
}
else Console.WriteLine("SKIP Harmony runtime binding smoke test (run separately without --skip-harmony-smoke)");
// Synthetic SPT tables keep these checks independent of a licensed game database.
MongoId Id(int n) => new(n.ToString("x24"));
var items=new Dictionary<MongoId,TemplateItem>();
void AddItem(int n,string name,string? weaponClass=null,int armor=0) => items[Id(n)]=new TemplateItem { Id=Id(n),Name=name,Properties=new TemplateItemProperties { WeapClass=weaponClass,ArmorClass=armor } };
AddItem(1,"weapon_pistol","pistol");AddItem(2,"weapon_shotgun","shotgun");AddItem(3,"weapon_sniper","sniperRifle");AddItem(4,"weapon_m4a1","assaultRifle");AddItem(5,"weapon_gl40","grenadeLauncher");AddItem(6,"weapon_revolver","revolver");
AddItem(10,"mag_30");AddItem(11,"mag_100");
items[Id(10)].Properties!.Cartridges=[new Slot{MaxCount=30}];items[Id(11)].Properties!.Cartridges=[new Slot{MaxCount=100}];
items[Id(4)].Properties!.Slots=[new Slot{Name="mod_magazine",Properties=new SlotProperties{Filters=[new SlotFilter{Filter=[Id(10),Id(11)]}]}}];
AddItem(20,"ammo_low");items[Id(20)].Properties!.PenetrationPower=10;
AddItem(21,"ammo_high");items[Id(21)].Properties!.PenetrationPower=60;
AddItem(30,"armor_6",armor:6);AddItem(31,"armor_3",armor:3);AddItem(32,"helmet_6",armor:6);
BotType MakeTemplate() => new() {
 BotInventory=new BotTypeInventory {
 Equipment=new(){ [EquipmentSlots.FirstPrimaryWeapon]=new(){[Id(1)]=1,[Id(2)]=1,[Id(3)]=1,[Id(4)]=1,[Id(5)]=1,[Id(6)]=1}, [EquipmentSlots.SecondPrimaryWeapon]=new(){[Id(4)]=1}, [EquipmentSlots.Holster]=new(){[Id(1)]=1}, [EquipmentSlots.ArmorVest]=new(){[Id(30)]=1,[Id(31)]=1},[EquipmentSlots.Headwear]=new(){[Id(32)]=1}},
 Ammo=new(){["CaliberTest"]=new(){[Id(20)]=1,[Id(21)]=1}}, Mods=new(){[Id(4)]=new(){["mod_magazine"]=[Id(10),Id(11)]}}, Items=new ItemPools { Backpack=new(),Pockets=new(),SecuredContainer=new(),SpecialLoot=new(),TacticalVest=new() } },
 BotChances=new Chances{EquipmentChances=new(){["FirstPrimaryWeapon"]=100,["SecondPrimaryWeapon"]=100,["Holster"]=100},WeaponModsChances=new(),EquipmentModsChances=new()},
 BotGeneration=new Generation{Items=new GenerationWeightingItems{Grenades=new GenerationData{Weights=new(){[2]=1},Whitelist=new()}}},
 BotAppearance=new Appearance{Body=new(){[Id(101)]=1},Feet=new(){[Id(102)]=1},Hands=new(){[Id(103)]=1},Head=new(),Voice=new()}
};
var botTypes=new Dictionary<string,BotType?>{["assault"]=MakeTemplate(),["usec"]=MakeTemplate(),["bosskilla"]=MakeTemplate(),["sectantwarrior"]=MakeTemplate(),["infectedassault"]=MakeTemplate()};
var botTable=(BotTable)RuntimeHelpers.GetUninitializedObject(typeof(BotTable));typeof(BotTable).GetProperty("Types")!.SetValue(botTable,botTypes);
var templateTable=(TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));typeof(TemplateTable).GetProperty("Items")!.SetValue(templateTable,items);
var warnings=new List<string>();var rules=new RaidRulesService(templateTable,botTable,table,new SPTarkov.Server.Core.Utils.Cloners.FastCloner(),warnings.Add);var options=new V2Settings();
BotGenerationDetails Details(bool playerScav=false,bool pmc=false)=>new(){Role=pmc?"pmcUSEC":"assault",RoleLowercase=pmc?"pmcusec":"assault",Side=pmc?"Usec":"Savage",IsPmc=pmc,IsPlayerScav=playerScav};
foreach(var (eventId,expected) in new[]{("get_handy",new[]{Id(1),Id(6)}),("i_have_a_shot",new[]{Id(2)}),("marksman",new[]{Id(3)})})
{
 var source=MakeTemplate();var result=rules.PrepareInventory(source,Details(),new(eventId),options);
 var slot=eventId=="get_handy"?EquipmentSlots.Holster:EquipmentSlots.FirstPrimaryWeapon;
 Check(result.BotInventory.Equipment[slot].Keys.Order().SequenceEqual(expected.Order()),eventId+" correct weapon class");
 Check(result.BotChances.EquipmentChances[slot.ToString()]==100 && result.BotChances.EquipmentChances["SecondPrimaryWeapon"]==0,eventId+" forced main / no backup");
 Check(result.BotGeneration.Items.Grenades.Weights.Keys.Single()==2,eventId+" native grenade count retained for flashbang conversion");
 Check(source.BotInventory.Equipment[EquipmentSlots.FirstPrimaryWeapon].Count==6 && !ReferenceEquals(result,source),eventId+" source template isolated");
}
var playerTemplate=MakeTemplate();Check(ReferenceEquals(rules.PrepareInventory(playerTemplate,Details(true),new("get_handy"),options),playerTemplate),"Player Scav gear excluded");
var bossDetails=Details();bossDetails.Role="bossKilla";Check(ReferenceEquals(rules.PrepareInventory(playerTemplate,bossDetails,new("get_handy"),options),playerTemplate),"Boss gear excluded from ordinary rules");
var drum=rules.PrepareInventory(MakeTemplate(),Details(),new("overloaded"),options);Check(drum.BotInventory.Equipment[EquipmentSlots.FirstPrimaryWeapon].Keys.Single()==Id(4),"Overloaded selects compatible weapon");Check(drum.BotInventory.Mods[Id(4)]["mod_magazine"].SetEquals([Id(11)]),"Overloaded restricts real 100-round magazine");Check(items[Id(10)].Properties!.Cartridges!.First().MaxCount==30,"No magazine capacity edits");
options.ExplosiveScavChancePercent=100;Check(rules.PrepareInventory(MakeTemplate(),Details(),new("explosive_scav_units"),options).BotInventory.Equipment[EquipmentSlots.FirstPrimaryWeapon].Keys.Single()==Id(5),"100 percent explosive Scavs get GL");
options.ExplosiveScavChancePercent=0;var zero=MakeTemplate();Check(ReferenceEquals(rules.PrepareInventory(zero,Details(),new("explosive_scav_units"),options),zero),"Zero percent explosive chance unchanged");Check(ReferenceEquals(rules.PrepareInventory(zero,Details(pmc:true),new("explosive_scav_units"),options),zero),"Explosive rule excludes PMCs");
var geared=rules.PrepareInventory(MakeTemplate(),Details(),new("geared"),options);Check(geared.BotInventory.Equipment[EquipmentSlots.ArmorVest].Keys.Single()==Id(30),"GEARED class-6 body armor");Check(geared.BotInventory.Ammo["CaliberTest"].Keys.Single()==Id(21),"GEARED highest penetration within caliber");Check(geared.BotInventory.Equipment[EquipmentSlots.FirstPrimaryWeapon].Keys.Single()==Id(4),"GEARED curated rifle pool");
var roleBefore=Details();var pmcDetails=rules.PrepareRole(roleBefore,new("pmc_sweep"));Check(pmcDetails.IsPmc && pmcDetails.Role.StartsWith("pmc") && roleBefore.Role=="assault","PMC conversion without modifying input");Check(rules.PrepareRole(Details(),new("outbreak")).Role=="infectedAssault","Outbreak requests infected bot template");
BotBase Bot(int body)=>new(){Customization=new Customization{Body=Id(body),Feet=Id(body+1),Hands=Id(body+2)}};
var matrix=new RaidRuleSession("matrix_glitch");var b1=Bot(201);var b2=Bot(301);var b3=Bot(401);rules.ApplyAppearance(b1,Details(),matrix,options);rules.ApplyAppearance(b2,Details(),matrix,options);rules.ApplyAppearance(b3,Details(pmc:true),matrix,options);Check(b1.Customization!.Body==b2.Customization!.Body && b3.Customization!.Body!=b1.Customization.Body,"Matrix shares Scav clothes, separates PMC faction");
options.MatrixClothingMode="shared";rules.ApplyAppearance(b3,Details(pmc:true),matrix,options);rules.ApplyAppearance(b2,Details(),matrix,options);Check(b2.Customization!.Body==b3.Customization!.Body,"Matrix shared mode");
var cult=Bot(501);rules.ApplyAppearance(cult,Details(),new("fake_cult"),options);Check(cult.Customization!.Body==Id(101),"Fake Cult uses installed outfit");
var poser=Bot(601);rules.ApplyAppearance(poser,Details(),new("posers_everywhere"),options);Check(poser.Customization!.Body==Id(101),"Killa poser outfit");
LocationBase MapData()=>new(){Id="bigmap",BotMax=20,BotMaxPvE=20,SpawnPointParams=[new SpawnPointParam{BotZoneName="ZoneA"},new SpawnPointParam{BotZoneName="ZoneB"}],BossLocationSpawn=[new BossLocationSpawn{BossName="bossBully",BossChance=20,BossZone="ZoneA"}],Waves=[new Wave{WildSpawnType=WildSpawnType.assault,SlotsMin=2,SlotsMax=4},new Wave{WildSpawnType=WildSpawnType.pmcUSEC,SlotsMin=2,SlotsMax=4},new Wave{WildSpawnType=WildSpawnType.marksman,SlotsMin=1,SlotsMax=2}]};
var concentrated=MapData();rules.ApplyMap(concentrated,"scav_concentrate",options);
var groups=concentrated.BossLocationSpawn.Where(x=>x.SptId?.StartsWith("rr-concentrate-")==true).ToArray();
Check(groups.Length==5 && groups.Sum(x=>1+int.Parse(x.BossEscortAmount!))==20,"Concentrate schedules 20 Scavs across five native timed groups");
Check(groups.Select(x=>x.BossZone).Distinct().Count()==1 && groups.All(x=>x.BossChance==100 && x.ForceSpawn==true && x.IgnoreMaxBots!=true),"Concentrate one zone and respects native caps");
Check(groups.Select(x=>x.Time).SequenceEqual(new double?[]{15,20,25,30,35}),"Concentrate groups staggered over first 35 seconds");
Check(concentrated.Waves.Any(w=>w.WildSpawnType==WildSpawnType.pmcUSEC),"Concentrate preserves PMC waves");
var baseline=MapData();var baselineBefore=System.Text.Json.JsonSerializer.Serialize(baseline);rules.ApplyMap(baseline,"overrun",options);Check(System.Text.Json.JsonSerializer.Serialize(baseline)==baselineBefore,"Overrun preserves baseline schedules and caps");
var noZones=MapData();noZones.SpawnPointParams=[];noZones.BossLocationSpawn=[];var oldWaves=noZones.Waves;rules.ApplyMap(noZones,"scav_concentrate",options);Check(ReferenceEquals(oldWaves,noZones.Waves),"Concentrate keeps baseline waves when no zone is available");
var handgun=rules.PrepareInventory(MakeTemplate(),Details(),new("get_handy"),options);Check(handgun.BotInventory.Equipment[EquipmentSlots.FirstPrimaryWeapon].Keys.Order().SequenceEqual(new[]{Id(1),Id(6)}.Order()) && handgun.BotChances.EquipmentChances["FirstPrimaryWeapon"]==0,"Get Handy spare weapon candidates remain nonempty and handgun-only without equipping primary");

var sweep=MapData();rules.ApplyMap(sweep,"pmc_sweep",options);Check(sweep.BossLocationSpawn.Count==0 && sweep.Waves.All(w=>w.WildSpawnType is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR),"PMC Sweep replaces Scavs and boss schedules");
var outbreak=MapData();rules.ApplyMap(outbreak,"outbreak",options);Check(outbreak.Waves.All(w=>w.WildSpawnType==WildSpawnType.infectedAssault) && outbreak.BossLocationSpawn.Count==0,"Outbreak infected-only schedule");
var sniper=MapData();sniper.Waves[2].SpawnPoints="ZoneSnipeTest";
sniper.SpawnPointParams=sniper.SpawnPointParams!.Concat(new[]{new SpawnPointParam{BotZoneName="ZoneSnipeTest",Categories=["Bot"],Position=new Vector3{X=1,Y=25,Z=1}},new SpawnPointParam{BotZoneName="ZoneSnipeTest",Categories=["Bot"],Position=new Vector3{X=8,Y=25,Z=1}}}).ToArray();
string baselineWaves=System.Text.Json.JsonSerializer.Serialize(sniper.Waves),baselineBosses=System.Text.Json.JsonSerializer.Serialize(sniper.BossLocationSpawn);
rules.ApplyMap(sniper,"extreme_overwatch",options);
Check(System.Text.Json.JsonSerializer.Serialize(sniper.Waves)==baselineWaves&&System.Text.Json.JsonSerializer.Serialize(sniper.BossLocationSpawn)==baselineBosses&&sniper.BotMax==20&&sniper.BotMaxPvE==20,"Overwatch leaves normal waves, boss waves and map population caps unchanged");
var roofStatus=System.Text.Json.JsonSerializer.SerializeToElement(RaidRoulette.Testing.TestingControl.Update(new(){Action="status"}));
Check(roofStatus.GetProperty("overwatchPoints").GetArrayLength()==2&&roofStatus.GetProperty("overwatchPerZone").GetInt32()==2,"Overwatch transmits two native rooftop coordinates and requests two extras at each");
Check(OverwatchPlan.Points(sniper).All(p=>p.Y==25)&&OverwatchPlan.Zones(sniper).SequenceEqual(new[]{"ZoneSnipeTest"}),"Overwatch keeps native roof elevation and excludes ordinary ground spawn zones");
var crowded=MapData();rules.ApplyMap(crowded,"no_safe_space",options);Check(crowded.Waves[0].SlotsMax==12 && crowded.BotMax==60,"No Safe Space triples waves and respects alive cap");options.MaxScheduledBots=5;crowded=MapData();rules.ApplyMap(crowded,"no_safe_space",options);Check(crowded.Waves.Sum(w=>w.SlotsMax??0)<=5 && crowded.Waves.All(w=>(w.SlotsMin??0)<=(w.SlotsMax??0)),"Scheduled budget bounds and valid min/max");
Check(MapData().Waves[0].SlotsMax==4,"Map fixtures unchanged by other results");
Check(!new V2Settings().Events["outbreak"].Enabled && !new V2Settings().Events["geared"].Enabled,"Experimental events opt in by default");
var originalInfected=botTypes["infectedassault"];botTypes.Remove("infectedassault");Check(!rules.CanRun("outbreak","bigmap",options),"Missing zombie template excludes Outbreak");botTypes["infectedassault"]=originalInfected;
Check(!rules.CanRun("extreme_overwatch","unknown",options),"Unsupported map excludes rooftop event");
var sessionA=new MongoId();var sessionB=new MongoId();RaidRulesRuntime.Initialize(new(),rules);RaidRulesRuntime.Activate(sessionA,"get_handy");RaidRulesRuntime.Activate(sessionB,"marksman");Check(RaidRulesRuntime.Get(sessionA)?.EventId=="get_handy" && RaidRulesRuntime.Get(sessionB)?.EventId=="marksman","Concurrent sessions keep independent events");RaidRulesRuntime.Clear(sessionA);Check(RaidRulesRuntime.Get(sessionA)==null && RaidRulesRuntime.Get(sessionB)!=null,"Raid cleanup leaves other session intact");RaidRulesRuntime.Activate(sessionB,"normal");Check(RaidRulesRuntime.Get(sessionB)==null,"Normal raid clears previous bot rules");
var forbidden=new RaidRouletteConfig { Debug=new DebugConfig{ForceEvent="outbreak"} };Check(new EventManager(forbidden).SelectEvent().Event.Id=="normal","Forced disabled event falls back to normal");var unavailable=new RaidRouletteConfig{Debug=new DebugConfig{ForceEvent="get_handy"}};Check(new EventManager(unavailable).SelectEvent(_=>false).Event.Id=="normal","Forced unavailable event falls back to normal");
var jsonOptions=new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true};var oldConfig=System.Text.Json.JsonSerializer.Deserialize<RaidRouletteConfig>("{\"EventChance\":35}",jsonOptions)!;Check(oldConfig.V2.Events.Count==25 && oldConfig.EventChance==35,"Old config retains existing chance and gains 25 event defaults");
var addonSettings=new RaidRouletteConfig { Debug=new DebugConfig{ForceEvent="get_handy"} };RaidRulesRuntime.Initialize(addonSettings,rules);RaidRouletteRuntime.Initialize(addonSettings,loot,bosses,_=>{},_=>{});
var generationSession=new MongoId();object?[] generationArgs={generationSession,"bigmap",null};prefix.Invoke(null,generationArgs);Check(RaidRulesRuntime.Get(generationSession)==null,"Bot rules unpublished until generation succeeds");var postfix=typeof(StartLocalRaidPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static)!;postfix.Invoke(null,new[]{MapData(),generationArgs[2]});Check(RaidRulesRuntime.Get(generationSession)?.EventId=="get_handy","Successful generation publishes matching session rules");finalizer.Invoke(null,new[]{failure,generationArgs[2]});Check(RaidRulesRuntime.Get(generationSession)==null,"Finalizer failure clears published rules");
var matrixA=new RaidRuleSession("matrix_glitch");var matrixB=new RaidRuleSession("matrix_glitch");var outfitA=Bot(701);var outfitB=Bot(801);rules.ApplyAppearance(outfitA,Details(),matrixA,new());rules.ApplyAppearance(outfitB,Details(),matrixB,new());Check(outfitA.Customization!.Body!=outfitB.Customization!.Body,"Matrix clothing does not leak between sessions");
var normal=MakeTemplate();Check(ReferenceEquals(rules.PrepareInventory(normal,Details(),new("normal"),options),normal),"Normal raid bot templates untouched");
var verificationState=new RaidRuleSession("overloaded");var verificationBot=new BotBase { Inventory=new BotBaseInventory {Equipment=Id(900),Items=[new Item{Id=Id(901),Template=Id(4),ParentId=Id(900).ToString(),SlotId="FirstPrimaryWeapon"},new Item{Id=Id(902),Template=Id(10),ParentId=Id(901).ToString(),SlotId="mod_magazine"}]}};
int before=warnings.Count;rules.VerifyGeneratedBot(verificationBot,Details(),verificationState,options);rules.VerifyGeneratedBot(verificationBot,Details(),verificationState,options);Check(warnings.Count==before+1,"Invalid generated magazine logs once per raid");
var noCapabilityTable=(TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));typeof(TemplateTable).GetProperty("Items")!.SetValue(noCapabilityTable,new Dictionary<MongoId,TemplateItem>());var emptyRules=new RaidRulesService(noCapabilityTable,botTable,table,new SPTarkov.Server.Core.Utils.Cloners.FastCloner(),_=>{});Check(!emptyRules.CanRun("get_handy","bigmap",options) && !emptyRules.CanRun("overloaded","bigmap",options),"Missing gun/mag templates exclude loadout events");
var overweight=new RaidRouletteConfig{EventChance=100};foreach(var toggle in overweight.V2.Events.Values)toggle.Weight=int.MaxValue;Check(new EventManager(overweight).SelectEvent(_=>true).Event!=null,"Very large event weights bounded without overflow");
var helmetBoss=Details();helmetBoss.IsPlayerScav=true;Check(ReferenceEquals(rules.PrepareRole(helmetBoss,new("outbreak")),helmetBoss),"Outbreak does not convert player Scav requests");
// Runtime menu selection: no restart, preserves file config, isolates one-shot/repeat semantics.
var menuConfig = new RaidRouletteConfig { EventChance = 0, Debug = new DebugConfig { ForceEvent = "marksman" } };
string MenuJson(object value) => System.Text.Json.JsonSerializer.Serialize(value);
var menuRequest = System.Text.Json.JsonSerializer.Deserialize<RaidRoulette.Testing.TestingRequest>("{\"action\":\"apply\",\"event\":\"get_handy\",\"repeat\":false}")!;
Check(MenuJson(RaidRoulette.Testing.TestingControl.Update(menuRequest)).Contains("\"ok\":true"), "Menu JSON request accepted");
var menuManager = new EventManager(menuConfig);
Check(menuManager.SelectEvent(_=>true, RaidRoulette.Testing.TestingControl.Take()).Event.Id == "get_handy", "Menu selection overrides file ForceEvent without modifying config");
Check(menuConfig.Debug.ForceEvent == "marksman", "Menu leaves file configuration intact");
Check(menuManager.SelectEvent(_=>true, RaidRoulette.Testing.TestingControl.Take()).Event.Id == "normal", "One-shot reverts to random rather than old file forced event");
RaidRoulette.Testing.TestingControl.Update(new() { Action="apply", Event="get_handy", Repeat=true });
Check(RaidRoulette.Testing.TestingControl.Take()=="get_handy" && RaidRoulette.Testing.TestingControl.Take()=="get_handy", "Repeat selection survives multiple generations");
Check(MenuJson(RaidRoulette.Testing.TestingControl.Update(new() { Action="apply", Event="not_real" })).Contains("\"ok\":false") && RaidRoulette.Testing.TestingControl.Take()=="get_handy", "Invalid event rejected without changing queued selection");
RaidRoulette.Testing.TestingControl.Update(new() { Action="apply", Event="outbreak", Repeat=true });
Check(menuManager.SelectEvent(_=>true,RaidRoulette.Testing.TestingControl.Take()).Event.Id=="normal", "Menu cannot bypass disabled experimental event");
RaidRoulette.Testing.TestingControl.Update(new() { Action="apply", Event="get_handy", Repeat=true });
Check(menuManager.SelectEvent(_=>false,RaidRoulette.Testing.TestingControl.Take()).Event.Id=="normal", "Menu respects map/template eligibility");
RaidRoulette.Testing.TestingControl.Record("normal","factory4_day","test fallback");
Check(MenuJson(RaidRoulette.Testing.TestingControl.Update(new())).Contains("test fallback"), "Status includes last actual event and fallback reason");
RaidRoulette.Testing.TestingControl.Update(new() { Action="apply", Event="config_file" });
Check(menuManager.SelectEvent(_=>true, RaidRoulette.Testing.TestingControl.Take()).Event.Id=="marksman", "Config File restores original ForceEvent behavior");
RaidRoulette.Testing.TestingControl.Update(new() { Action="apply", Event="outbreak", Repeat=true, AllowDisabled=true });
var forcedTest=RaidRoulette.Testing.TestingControl.Take(out var allowTest);
Check(menuManager.SelectEvent(_=>true,forcedTest,allowTest).Event.Id=="outbreak", "Explicit menu testing override permits a disabled forced event");
Check(menuManager.SelectEvent(_=>false,forcedTest,allowTest).Event.Id=="normal", "Explicit testing override still cannot bypass missing map/assets");
Check(!menuConfig.V2.Events["outbreak"].Enabled, "Experimental testing override does not enable random events or change file config");
RaidRoulette.Testing.TestingControl.Update(new() { Action="apply", Event="config_file" });
// Exercise actual SPT route deserialization/dispatch with the client's exact JSON shape.
var liveJson=new SPTarkov.Server.Core.Utils.JsonUtil([]);
var testingRouter=new RaidRoulette.Testing.TestingRouter(liveJson);
var wireReply=await testingRouter.HandleStaticAsync("/client/raidroulette/testing", "{\"action\":\"apply\",\"event\":\"cultist_invasion\",\"repeat\":true,\"allowDisabled\":true}", new MongoId(), "");
Check(wireReply.ToString()!.Contains("Queued: cultist_invasion"), "Actual SPT router accepts cultist selection wire payload");
Check(RaidRoulette.Testing.TestingControl.Take()=="cultist_invasion"&&RaidRoulette.Testing.TestingControl.Take()=="cultist_invasion", "Force mode stays selected across raid generations");
var readReply=await testingRouter.HandleStaticAsync("/client/raidroulette/testing", "{\"action\":\"status\"}", new MongoId(), "");
Check(readReply.ToString()!.Contains("cultist_invasion"), "Automatic status reads do not change selection");
await testingRouter.HandleStaticAsync("/client/raidroulette/testing", "{\"action\":\"apply\",\"event\":\"random\",\"repeat\":true,\"allowDisabled\":false}", new MongoId(), "");
Check(RaidRoulette.Testing.TestingControl.Take()==""&&RaidRoulette.Testing.TestingControl.Take()=="", "Force OFF wire payload restores random across subsequent raids");
// Diagnostic observers must describe the real data without changing it.
var auditLines=new List<string>();RaidDiagnostics.Initialize(auditLines.Add,templateTable);
var auditSession=new MongoId();var auditMap=MapData();auditMap.BossLocationSpawn=[new BossLocationSpawn{BossName="sectantPriest",BossEscortType="sectantWarrior",BossEscortAmount="2,3,4",BossZone="BotZone",BossChance=100}];
RaidDiagnostics.Begin(auditSession,"cultist_invasion",auditMap);
Check(auditLines.Any(x=>x.Contains("cultistEntries=1"))&&auditLines.Any(x=>x.Contains("sectantPriest")&&x.Contains("sectantWarrior")),"Diagnostics record actual output schedule roles");
var liveBot=new BotBase{Id=Id(999),Info=new Info{Settings=new BotInfoSettings{Role="assault"},Side="Savage"},Inventory=new BotBaseInventory{Equipment=Id(900),Items=[new Item{Id=Id(903),Template=Id(5),ParentId=Id(900).ToString(),SlotId="FirstPrimaryWeapon"}]}};
RaidDiagnostics.Bot(auditSession,"response",liveBot);
Check(auditLines.Last().Contains("role=assault")&&auditLines.Last().Contains("grenadeLauncher"),"Diagnostics record actual bot role and generated weapon class");
Check(liveBot.Info.Settings.Role=="assault"&&liveBot.Inventory.Items.Single().Template==Id(5),"Diagnostics do not change role or weapon");
var observeResponse=typeof(RaidRoulette.Patches.DiagnosticResponsePatch).GetMethod("Trace",BindingFlags.Static|BindingFlags.NonPublic)!;
IEnumerable<BotBase?> responseList=new List<BotBase?>{liveBot,null};
var observed=await (Task<IEnumerable<BotBase?>>)observeResponse.Invoke(null,new object[]{Task.FromResult(responseList),auditSession})!;
Check(ReferenceEquals(observed,responseList),"Response trace preserves original returned collection");
var traceFailure=new InvalidOperationException("original-generator-failure");bool sameFailure=false;
try {await (Task<IEnumerable<BotBase?>>)observeResponse.Invoke(null,new object[]{Task.FromException<IEnumerable<BotBase?>>(traceFailure),auditSession})!;}catch(Exception e){sameFailure=ReferenceEquals(e,traceFailure);}
Check(sameFailure,"Response trace preserves original generator exception");
int priorLines=auditLines.Count;RaidDiagnostics.Log(new MongoId(),"request","role=assault");Check(auditLines.Count==priorLines+1&&auditLines.Last().Contains("unmapped-session"),"Different bot-request session is visible instead of silently misattributed");
RaidDiagnostics.Clear(auditSession);priorLines=auditLines.Count;RaidDiagnostics.Log(auditSession,"request","role=assault");Check(auditLines.Count==priorLines,"Diagnostic session cleanup stops tracing");
RaidDiagnostics.Begin(auditSession,"normal",auditMap);Check(auditLines.Count==priorLines,"Unrelated events do not enable backend trace");
RaidDiagnostics.Initialize(_=>throw new Exception("logger failure"),templateTable);RaidDiagnostics.Begin(auditSession,"cultist_invasion",auditMap);RaidDiagnostics.Bot(auditSession,"response",liveBot);Check(liveBot.Info.Settings.Role=="assault","Logger failures do not block or mutate bot generation");
RaidDiagnostics.Clear(auditSession);
// Cultist wave workaround: force only added cultists and separate initial same-zone boss activation.
var originalSpawnList=location.Base.BossLocationSpawn;var savedSchedule=originalSpawnList.ToArray();var cultistRollback=new List<Action>();
var forceResult=bosses.AddCultistGroupsForRaid("bigmap",2,cultistRollback.Add);
var addedCultists=originalSpawnList.Except(savedSchedule).ToArray();
Check(forceResult.Success&&addedCultists.Length==2,"Cultist workaround adds requested two groups");
Check(addedCultists.All(x=>x.ForceSpawn==true&&x.IgnoreMaxBots==true),"Added cultists bypass native conflicting-boss group gate");
Check(addedCultists.Select(x=>x.Time).SequenceEqual(new double?[]{15,30}),"Cultist groups stagger after initial boss activation");
Check(savedSchedule.All(x=>originalSpawnList.Contains(x))&&originalSpawnList.First().BossName==savedSchedule.First().BossName,"Original boss entries retained");
cultistRollback.ForEach(a=>a());Check(originalSpawnList.SequenceEqual(savedSchedule),"Forced cultist schedule rolls back without changing original bosses");
var raiderRollback=new List<Action>();bosses.AddRaiderGroupsForRaid("bigmap",1,raiderRollback.Add);var addedRaiders=originalSpawnList.Except(savedSchedule).ToArray();
Check(addedRaiders.Length==1&&addedRaiders[0].Time==-1,"Raider timing unchanged by targeted cultist workaround");raiderRollback.ForEach(a=>a());

// Clone Wars: equipment tree only, independent IDs, intact caches and frozen source.
var cloneService=new CloneWarsService(new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
var playerSource=new BotBase{
 Id=Id(1000),Aid=9876,Info=new(){Nickname="Danny",Level=42,Experience=123456,Side="Usec",Settings=new(){Role="pmcUSEC"}},
 Customization=new(){Head=Id(101),Body=Id(102),Feet=Id(103),Hands=Id(104),Voice=Id(105)},
 Skills=new(){Common=[new(){Id=SPTarkov.Server.Core.Models.Enums.SkillTypes.Endurance,Progress=321}],Mastering=[new(){Id="weapon",Progress=77}],Points=12},
 Inventory=new(){Equipment=Id(1001),Stash=Id(1009),FastPanel=new(){["1"]=Id(1002),["2"]=Id(1010)},FavoriteItems=[Id(1002),Id(1010)],Items=[
 new(){Id=Id(1001),Template=Id(2001)},new(){Id=Id(1002),Template=Id(2002),ParentId=Id(1001).ToString(),SlotId="FirstPrimaryWeapon",Upd=new(){StackObjectsCount=1}},
 new(){Id=Id(1003),Template=Id(2003),ParentId=Id(1002).ToString(),SlotId="mod_magazine"},
 new(){Id=Id(1004),Template=Id(2004),ParentId=Id(1003).ToString(),SlotId="cartridges",Upd=new(){StackObjectsCount=30}},
 new(){Id=Id(1009),Template=Id(2009)},new(){Id=Id(1010),Template=Id(2010),ParentId=Id(1009).ToString(),SlotId="hideout"}]}
};
var frozen=cloneService.Capture(playerSource);
Check(frozen.Kit.Items!.Count==4 && frozen.Kit.Stash==null,"Clone snapshot excludes stash and copies all equipped descendants");
Check(frozen.Kit.FastPanel!.Count==1 && frozen.Kit.FavoriteItems!.Count()==1,"Snapshot removes shortcuts and favourites pointing outside the carried kit");
string savedPlayer=System.Text.Json.JsonSerializer.Serialize(playerSource);
BotBase CloneTarget()=>new(){Id=new MongoId(),Aid=444,Info=new(){Nickname="Original",Level=5,Side="Bear",Settings=new(){Role="pmcBEAR",BotDifficulty="hard"}},Inventory=new(){Equipment=Id(3000),Items=[new(){Id=Id(3001),Template=Id(3002),ParentId=Id(3000).ToString(),SlotId="Dogtag",Upd=new(){Dogtag=new(){Nickname="Original",ProfileId=Id(3000).ToString()}}}]}};
var cachedClone=CloneTarget();var cloneA=cloneService.CopyForResponse(frozen,cachedClone);var cloneB=cloneService.CopyForResponse(frozen,cachedClone);
cachedClone.Info!.GroupId="native-squad-a";cachedClone.Info.TeamId="native-team-a";
var affiliated=cloneService.CopyForResponse(frozen,cachedClone);
Check(affiliated.Info!.GroupId=="native-squad-a"&&affiliated.Info.TeamId=="native-team-a"&&affiliated.Aid==cachedClone.Aid,"Clone preserves generated squad, team and account identity");
var separateSquad=CloneTarget();separateSquad.Info!.GroupId="native-squad-b";separateSquad.Info.TeamId="native-team-b";
var separateClone=cloneService.CopyForResponse(frozen,separateSquad);
Check(separateClone.Info!.GroupId!=affiliated.Info.GroupId&&separateClone.Info.TeamId!=affiliated.Info.TeamId,"Identical clone appearance does not merge distinct native squads or teams");
Check(cachedClone.Info!.Nickname=="Original" && cachedClone.Inventory!.Items!.Count==1,"Clone generation does not mutate the native cachedClone profile");
Check(cloneA.Info!.Nickname=="Danny" && cloneA.Info.Level==42 && cloneA.Info.Experience==123456,"Clone matches nickname, player level and XP");
Check(cloneA.Info.Settings!.Role=="pmcBEAR" && cloneA.Info.Side=="Bear" && cloneA.Info.Settings.BotDifficulty=="hard","Clone preserves native spawn role, faction and AI difficulty");
Check(cloneA.Customization!.Voice==playerSource.Customization.Voice && cloneA.Customization.Head==playerSource.Customization.Head,"Clone matches face and voice");
Check(cloneA.Skills!.Common!.Single().Progress==321 && cloneA.Skills.Mastering!.Single().Progress==77,"Clone carries common skills and weapon mastery");
var aIds=cloneA.Inventory!.Items!.Select(i=>i.Id).ToHashSet();var bIds=cloneB.Inventory!.Items!.Select(i=>i.Id).ToHashSet();
Check(cloneA.Id!=cloneB.Id && cloneA.Id!=playerSource.Id && !aIds.Overlaps(bIds) && !aIds.Overlaps(playerSource.Inventory.Items!.Select(i=>i.Id)),"Every clone has independent profile and item IDs");
Check(cloneA.Inventory.Items!.Where(i=>i.Id!=cloneA.Inventory.Equipment).All(i=>aIds.Contains(new MongoId(i.ParentId!))),"Rebased equipment preserves every parent/child link");
Check(aIds.Contains(cloneA.Inventory.FastPanel!["1"])&&cloneA.Inventory.FavoriteItems!.All(aIds.Contains),"Shortcut and favourite references point to rebased items");
var cloneTag=cloneA.Inventory.Items!.Single(i=>i.SlotId=="Dogtag").Upd!.Dogtag!;
Check(cloneTag.ProfileId==cloneA.Id.ToString()&&cloneTag.Nickname=="Danny"&&cloneTag.Level==42,"Native dogtag identifies its own clone and copied level");
Check(System.Text.Json.JsonSerializer.Serialize(playerSource)==savedPlayer,"Original player profile and carried inventory remain byte-equivalent");
playerSource.Info.Nickname="Changed";playerSource.Skills.Common!.Single().Progress=999;playerSource.Inventory.Items![3].Upd!.StackObjectsCount=1;
var laterClone=cloneService.CopyForResponse(frozen,CloneTarget());
Check(laterClone.Info!.Nickname=="Danny"&&laterClone.Skills!.Common!.Single().Progress==321&&laterClone.Inventory!.Items!.Single(i=>i.Template==Id(2004)).Upd!.StackObjectsCount==30,"Mid-raid profile changes do not alter the frozen clone snapshot");
var plainScav=new BotBase{Info=new(){Settings=new(){Role="assault"}}};var plainBoss=new BotBase{Info=new(){Settings=new(){Role="bossKilla"}}};
Check(ReferenceEquals(cloneService.CopyForResponse(frozen,plainScav),plainScav)&&ReferenceEquals(cloneService.CopyForResponse(frozen,plainBoss),plainBoss),"Clone Wars excludes Scavs and bosses");
Check(!cloneService.Apply(frozen,CloneTarget(),new(){IsPmc=true,Role="pmcBEAR",IsPlayerScav=true}),"Player Scav generation is excluded from Clone Wars");
var cloneSession=new MongoId();CloneWarsRuntime.Initialize(cloneService,_=>playerSource,_=>{});Check(CloneWarsRuntime.Capture(cloneSession),"Runtime freezes a valid session PMC");
IEnumerable<BotBase?> cloneBatch=new BotBase?[]{cachedClone,plainScav,null};var transformed=(await CloneWarsRuntime.TransformResponse(cloneSession,Task.FromResult(cloneBatch))).ToArray();
Check(transformed.Length==3&&!ReferenceEquals(transformed[0],cachedClone)&&ReferenceEquals(transformed[1],plainScav)&&transformed[2]==null,"Response transform handles cachedClone PMC, Scav and null entries independently");
CloneWarsRuntime.Clear(cloneSession);var unchangedTask=Task.FromResult(cloneBatch);Check(ReferenceEquals(CloneWarsRuntime.TransformResponse(cloneSession,unchangedTask),unchangedTask),"Raid cleanup removes clone transform without wrapping unrelated requests");

// The Gift: validate prize bounds, native backpack geometry and immutable caches.
var giftItems=new Dictionary<MongoId,TemplateItem>();
foreach(string id in new[]{GiftService.Hat,GiftService.Bitcoin,GiftService.Paper,GiftService.Roubles,GiftService.Dollars,GiftService.Euros,GiftService.Blicky,GiftService.BlickyMagazine,GiftService.ToyRounds,GiftService.StaffVest})
 giftItems[new MongoId(id)]=new(){Id=new MongoId(id),Name=id,Properties=new(){Width=1,Height=1,StackMaxSize=id==GiftService.Roubles?500000:50000}};
var bagTpl=Id(4000);giftItems[bagTpl]=new(){Id=bagTpl,Name="test_bag",Properties=new(){Grids=[new(){Name="main",Properties=new(){CellsH=4,CellsV=4}}]}};
giftItems[new MongoId(GiftService.Paper)].Properties!.Width=2;
var giftTable=(TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
typeof(TemplateTable).GetProperty("Items")!.SetValue(giftTable,giftItems);
typeof(TemplateTable).GetProperty("Handbook")!.SetValue(giftTable,new HandbookBase{Categories=[],Items=[new(){Id=new MongoId(GiftService.Dollars),Price=150},new(){Id=new MongoId(GiftService.Euros),Price=170}]});
var giftBotTable=(BotTable)RuntimeHelpers.GetUninitializedObject(typeof(BotTable));
typeof(BotTable).GetProperty("Types")!.SetValue(giftBotTable,new Dictionary<string,BotType?>{["gifter"]=MakeTemplate(),["assault"]=MakeTemplate(),["bosssanitar"]=MakeTemplate()});
var giftService=new GiftService(giftTable,giftBotTable,new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
Check(giftService.Available,"Gift requires native Santa template, prize items and valid currency prices");
var giftOriginal=new BotBase{Id=Id(4001),Info=new(){Nickname="Santa",Side="Savage",Settings=new(){Role="gifter",StandingForKill=-1}},Inventory=new(){Equipment=Id(4002),Items=[
 new(){Id=Id(4002),Template=Id(4003)},new(){Id=Id(4004),Template=bagTpl,ParentId=Id(4002).ToString(),SlotId="Backpack"},
 new(){Id=Id(4005),Template=Id(4006),ParentId=Id(4002).ToString(),SlotId="FirstPrimaryWeapon"},
 new(){Id=Id(4007),Template=Id(4008),ParentId=Id(4004).ToString(),SlotId="main"}]}};
string beforeGift=System.Text.Json.JsonSerializer.Serialize(giftOriginal);
var prize=giftService.CopyForResponse(giftOriginal);var prizeItems=prize.Inventory!.Items!;
Check(prize.Info!.Nickname=="The Gift" && prize.Info.Settings!.StandingForKill==0,"Gift nickname and explicit native kill-standing value");
Check(!prizeItems.Any(i=>i.SlotId is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "ArmorVest" or "TacticalVest"),"Gift carries no primary weapon, armor or rig");
var toy=prizeItems.Single(i=>i.SlotId=="Holster");
Check(toy.Template==new MongoId(GiftService.Blicky)&&toy.ParentId==prize.Inventory.Equipment.ToString(),"Gift equips only the Blicky in its holster");
var toyMag=prizeItems.Single(i=>i.SlotId=="mod_magazine");
Check(toyMag.Template==new MongoId(GiftService.BlickyMagazine)&&toyMag.ParentId==toy.Id.ToString(),"Blicky has its required compatible magazine");
Check(prizeItems.Single(i=>i.SlotId=="cartridges") is var toyStack && toyStack.ParentId==toyMag.Id.ToString()&&toyStack.Template==new MongoId(GiftService.ToyRounds)&&toyStack.Upd?.StackObjectsCount==19,"Toy rounds are loaded in the Blicky magazine");
Check(prizeItems.Single(i=>i.SlotId=="patron_in_weapon") is var chamber && chamber.ParentId==toy.Id.ToString()&&chamber.Template==new MongoId(GiftService.ToyRounds)&&chamber.Upd?.StackObjectsCount==1,"Blicky has a compatible chambered toy round");
giftItems.Remove(new MongoId(GiftService.Blicky));Check(!giftService.Available,"Missing Blicky prevents scheduling an incomplete Gift loadout");
giftItems[new MongoId(GiftService.Blicky)]=new(){Id=new MongoId(GiftService.Blicky),Name="Blicky",Properties=new(){Width=2,Height=1}};
Check(prizeItems.Single(i=>i.SlotId=="Headwear").Template==new MongoId(GiftService.Hat),"Gift wears the Santa hat");
bool validCash=true,validCoins=true,validGeometry=true;var coinCounts=new HashSet<double>();
for(int sample=0;sample<128;sample++)
{
 var generated=giftService.CopyForResponse(giftOriginal);var contents=generated.Inventory!.Items!.Where(i=>i.ParentId==generated.Inventory.Items!.Single(i=>i.SlotId=="Backpack").Id.ToString()).ToArray();
 double Qty(string id)=>contents.Where(i=>i.Template.ToString()==id).Sum(i=>i.Upd?.StackObjectsCount??1);
 double cash=Qty(GiftService.Roubles)+Qty(GiftService.Dollars)*150+Qty(GiftService.Euros)*170;
 validCash&=cash>=200000&&cash<=400000&&Qty(GiftService.Dollars)>0&&Qty(GiftService.Euros)>0;
 validCoins&=Qty(GiftService.Bitcoin)>=0&&Qty(GiftService.Bitcoin)<=3;coinCounts.Add(Qty(GiftService.Bitcoin));
 var cells=new HashSet<(int,int)>();
 foreach(var item in contents)
 {
  var pos=System.Text.Json.JsonSerializer.SerializeToElement(item.Location);int x=pos.GetProperty("x").GetInt32(),y=pos.GetProperty("y").GetInt32();
  int width=giftItems[item.Template].Properties!.Width??1,height=giftItems[item.Template].Properties!.Height??1;
  for(int dx=0;dx<width;dx++)for(int dy=0;dy<height;dy++)validGeometry&=x+dx<4&&y+dy<4&&cells.Add((x+dx,y+dy));
 }
}
Check(validCash,"Gift mixed cash stays within 200â€“400k roubles equivalent across randomized samples");
Check(validCoins,"Gift bitcoin count stays within zero to three");
Check(coinCounts.SetEquals(new double[]{0,1,2,3}),"Gift randomized samples exercise all four bitcoin counts, including zero");
Check(!prizeItems.Any(i=>i.ParentId==prizeItems.Single(x=>x.SlotId=="Backpack").Id.ToString() && i.Template==new MongoId(GiftService.BlickyMagazine)),"Gift backend does not place a spare magazine in the prize backpack");
Check(validGeometry,"Gift items fit backpack geometry without overlap, including non-square paper");
Check(prizeItems.Select(i=>i.Id).Distinct().Count()==prizeItems.Count && !prizeItems.Select(i=>i.Id).Intersect(giftOriginal.Inventory!.Items!.Select(i=>i.Id)).Any(),"Gift profile uses independent equipment item IDs");
Check(System.Text.Json.JsonSerializer.Serialize(giftOriginal)==beforeGift,"Gift leaves the native Santa cache unchanged");
Check(ReferenceEquals(giftService.CopyForResponse(plainScav),plainScav),"Gift prize transform excludes ordinary Scavs");
var giftMap=MapData();giftMap.BossLocationSpawn.Add(new(){BossName="gifter",BossChance=50,BossZone="ZoneA"});var keptWaves=giftMap.Waves;
giftService.ConfigureMap(giftMap);
Check(giftMap.BossLocationSpawn.Count(s=>s.BossName=="gifter")==1 && giftMap.BossLocationSpawn.Single(s=>s.BossName=="gifter").Time==15 && ReferenceEquals(keptWaves,giftMap.Waves),"Gift schedules one timed runner while preserving ordinary waves");

Check(prize.Customization?.Body==Id(101),"Gift uses ordinary Scav clothing underneath its Santa hat");

Check(GiftService.Paper==SPTarkov.Server.Core.Models.Enums.ItemTpl.BARTER_TOILET_PAPER.ToString(),"Trail and backpack use official SPT toilet-paper template, not soap");

// Smokers Lobby: generated AI only, finite valid inventories and immutable source profiles.
giftItems[new MongoId(SmokersService.Smoke)]=new(){Id=new MongoId(SmokersService.Smoke),Name="m18",Properties=new(){Width=1,Height=1,ThrowType=ThrowWeapType.smoke_grenade}};
giftItems[new MongoId(SmokersService.Pockets)]=new(){Id=new MongoId(SmokersService.Pockets),Name="pockets",Properties=new(){Grids=[new(){Name="pocket1",Properties=new(){CellsH=1,CellsV=3}},new(){Name="pocket2",Properties=new(){CellsH=2,CellsV=3}},new(){Name="pocket3",Properties=new(){CellsH=2,CellsV=3}},new(){Name="pocket4",Properties=new(){CellsH=2,CellsV=3}},new(){Name="pocket5",Properties=new(){CellsH=1,CellsV=3}}]}};
var fragId=Id(7000);giftItems[fragId]=new(){Id=fragId,Name="frag",Properties=new(){Width=1,Height=1,ThrowType=ThrowWeapType.frag_grenade}};
var smokers=new SmokersService(giftTable,new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
Check(smokers.Available,"Smoke event requires real smoke and native roomy pocket templates");
var smokeSource=CloneTarget();smokeSource.Inventory!.Items!.Add(new(){Id=Id(7001),Template=fragId,ParentId=smokeSource.Inventory.Equipment.ToString(),SlotId="test-frag"});
string beforeSmoke=System.Text.Json.JsonSerializer.Serialize(smokeSource);
var smokeCounts=new HashSet<int>();bool validSmoke=true;
for(int sample=0;sample<128;sample++)
{
 var equipped=smokers.CopyForResponse(smokeSource);var all=equipped.Inventory!.Items!;
 var supply=all.Where(i=>i.Template==new MongoId(SmokersService.Smoke)).ToArray();smokeCounts.Add(supply.Length);
 var pockets=all.Single(i=>i.SlotId=="Pockets");var occupied=new HashSet<string>();
 validSmoke&=supply.Length>=5&&supply.Length<=10&&!all.Any(i=>i.Template==fragId);
 foreach(var smoke in supply)
 {
  var pos=System.Text.Json.JsonSerializer.SerializeToElement(smoke.Location);int x=pos.GetProperty("x").GetInt32(),y=pos.GetProperty("y").GetInt32();
  var grid=giftItems[pockets.Template].Properties!.Grids!.Single(g=>g.Name==smoke.SlotId);
  validSmoke&=smoke.ParentId==pockets.Id.ToString()&&x>=0&&y>=0&&x<grid.Properties!.CellsH&&y<grid.Properties.CellsV&&occupied.Add(smoke.SlotId+":"+x+":"+y)&&smoke.Upd?.StackObjectsCount==1;
 }
 validSmoke&=all.Select(i=>i.Id).Distinct().Count()==all.Count;
}
Check(validSmoke,"Smoke supplies have 5â€“10 individual grenades, valid grid cells, unique IDs and no frag grenades");
Check(smokeCounts.SetEquals(Enumerable.Range(5,6)),"Smoke supply randomized samples cover all six counts");
Check(System.Text.Json.JsonSerializer.Serialize(smokeSource)==beforeSmoke,"Smoke supply never mutates the generated profile cache");
foreach(string role in new[]{"assault","pmcUSEC","pmcBEAR","bossTagilla","followerBully"})
{
 var npc=CloneTarget();npc.Info!.Settings!.Role=role;var supplied=smokers.CopyForResponse(npc);
 Check(supplied.Info!.Settings!.Role==role&&supplied.Info.GroupId==npc.Info.GroupId&&supplied.Id==npc.Id&&supplied.Inventory!.Items!.Count(i=>i.Template==new MongoId(SmokersService.Smoke))>=5,"Smokes include generated "+role+" without changing AI identity");
}
var humanProfile=new BotBase{Info=new(){Nickname="Human"}};Check(ReferenceEquals(smokers.CopyForResponse(humanProfile),humanProfile),"Profiles without an AI role are excluded from smoke equipment");
var humanScav=CloneTarget();humanScav.Info!.Settings!.Role="playerscav";Check(ReferenceEquals(smokers.CopyForResponse(humanScav),humanScav),"Explicit player Scav role is excluded from smoke equipment");
var nonSmokeSession=new MongoId();var nonSmokeTask=Task.FromResult<IEnumerable<BotBase?>>([smokeSource,humanProfile]);SmokersRuntime.Initialize(smokers);
Check(ReferenceEquals(SmokersRuntime.TransformResponse(nonSmokeSession,nonSmokeTask),nonSmokeTask),"Smoke response transform is inert outside its event session");
RaidRulesRuntime.Activate(nonSmokeSession,"smokers_lobby");
var smokeBatch=(await SmokersRuntime.TransformResponse(nonSmokeSession,Task.FromResult<IEnumerable<BotBase?>>([smokeSource,humanProfile,null]))).ToArray();
Check(!ReferenceEquals(smokeBatch[0],smokeSource)&&smokeBatch[0]!.Inventory!.Items!.Count(i=>i.Template==new MongoId(SmokersService.Smoke))>=5&&ReferenceEquals(smokeBatch[1],humanProfile)&&smokeBatch[2]==null,"Active smoke session transforms NPC responses while preserving human and null entries");
RaidRulesRuntime.Clear(nonSmokeSession);
Check(new EventManager(new()).SelectEvent(_=>true,"smokers_lobby").Event.Id=="smokers_lobby"&&new V2Settings().Events["smokers_lobby"].Weight==0,"Smokers Lobby can be forced while remaining excluded from default random selection");
var withPocketItems=CloneTarget();var originalPocket=new Item{Id=Id(7010),Template=new MongoId(SmokersService.Pockets),ParentId=withPocketItems.Inventory!.Equipment.ToString(),SlotId="Pockets"};
withPocketItems.Inventory.Items!.Add(originalPocket);withPocketItems.Inventory.Items.Add(new(){Id=Id(7011),Template=new MongoId(GiftService.Paper),ParentId=originalPocket.Id.ToString(),SlotId="pocket2",Location=new{x=0,y=0,r=0}});
withPocketItems.Inventory.Items.Add(new(){Id=Id(7012),Template=fragId,ParentId=originalPocket.Id.ToString(),SlotId="pocket3"});withPocketItems.Inventory.FastPanel=new(){["4"]=Id(7012)};
var repacked=smokers.CopyForResponse(withPocketItems);
Check(repacked.Inventory!.Items!.Any(i=>i.Id==Id(7011)&&i.ParentId==originalPocket.Id.ToString())&&repacked.Inventory.FastPanel?.Count==0,"Existing pocket loot is preserved and removed grenade shortcuts are cleared");
Check(StartupPopulation.Target(20,50)==10&&StartupPopulation.Target(20,100)==20,"Factory startup targets span 10â€“20 for a 20-bot cap");
Check(StartupPopulation.Target(17,50)==9&&StartupPopulation.Target(0,100)==0,"Odd caps round up and absent caps disable startup fill");
Check(Enumerable.Range(50,51).All(percent=>StartupPopulation.Target(60,percent)>=30&&StartupPopulation.Target(60,percent)<=60),"Every startup percentage remains inside half to full map capacity");
var populationStatus=System.Text.Json.JsonSerializer.SerializeToElement(RaidRoulette.Testing.TestingControl.Update(new(){Action="status"}));
RaidRoulette.Testing.TestingControl.SetStartupPopulation(20,15);
populationStatus=System.Text.Json.JsonSerializer.SerializeToElement(RaidRoulette.Testing.TestingControl.Update(new(){Action="status"}));
Check(populationStatus.GetProperty("startupTarget").GetInt32()==15&&populationStatus.GetProperty("startupCap").GetInt32()==20,"Startup target and cap are transmitted through backend status");
RaidRoulette.Testing.TestingControl.Record("normal","factory4_day",null);
populationStatus=System.Text.Json.JsonSerializer.SerializeToElement(RaidRoulette.Testing.TestingControl.Update(new(){Action="status"}));
Check(populationStatus.GetProperty("startupTarget").GetInt32()==0,"A subsequent unrelated raid clears the startup plan");
Check(populationStatus.GetProperty("huntBossRole").GetString()=="","A subsequent unrelated raid clears the selected hunting boss role");
var huntTable=(LocationTable)RuntimeHelpers.GetUninitializedObject(typeof(LocationTable));
typeof(LocationTable).GetProperty("Bigmap")!.SetValue(huntTable,new Location{Base=new(){Id="bigmap",SpawnPointParams=[new(){BotZoneName="ZoneGround"}],BossLocationSpawn=[new(){BossName="marksman",BossZone="ZoneSnipeTower",BossChance=50}]}});
var foreignBoss=new BossLocationSpawn{BossName="bossTagilla",BossZone="FactoryZone",BossChance=25,BossEscortAmount="3",Supports=[]};
typeof(LocationTable).GetProperty("Woods")!.SetValue(huntTable,new Location{Base=new(){Id="woods",BossLocationSpawn=[foreignBoss,new(){BossName="bossZryachiy",BossZone="StationaryZone",BossChance=100}]}});
var crossMapHunt=new BossSpawnService(huntTable);var huntRollback=new List<Action>();var crossResult=crossMapHunt.EnableBossHuntForRaid("bigmap",huntRollback.Add);
Check(crossResult.Success&&crossResult.BossName=="bossTagilla"&&crossResult.Zone=="ZoneGround","Boss Hunt can select a mobile boss from another map while excluding sniper zones");
Check(foreignBoss.BossChance==25&&foreignBoss.BossEscortAmount=="3","Cross-map hunter setup leaves the source boss and guards unchanged");
huntRollback.ForEach(a=>a());
Check(new EventManager(new()).SelectEvent(_=>true,"time_warp").Event.Id=="time_warp"&&new V2Settings().Events["time_warp"].Weight==0,"Time Warp is available for forced testing with zero default random weight");
var unwarpedMap=MapData();string beforeWarp=System.Text.Json.JsonSerializer.Serialize(unwarpedMap);rules.ApplyMap(unwarpedMap,"time_warp",new());
Check(System.Text.Json.JsonSerializer.Serialize(unwarpedMap)==beforeWarp,"Time Warp does not alter map raid limits, spawns or loot");
Check(rules.CanRun("time_warp","bigmap",new())&&!rules.CanRun("time_warp","missing",new()),"Time Warp requires a supported outdoor map");

// Enforce Overloaded after cached profile generation, across every equipped gun slot.
items[Id(4)].Properties!.DefAmmo=Id(20);items[Id(4)].Properties!.Chambers=[new(){Name="patron_in_weapon"}];
items[Id(1)].Properties!.DefAmmo=Id(20);items[Id(1)].Properties!.Slots=[new(){Name="mod_magazine",Properties=new(){Filters=[new(){Filter=[Id(7100),Id(7101)]}]}}];
items[Id(7100)]=new(){Id=Id(7100),Name="real_pistol_drum_84",Properties=new(){Cartridges=[new(){Name="cartridges",MaxCount=84}]}};
items[Id(7101)]=new(){Id=Id(7101),Name="pistol_999_infectedMagazin",Properties=new(){Cartridges=[new(){Name="cartridges",MaxCount=999}]}};
items[Id(11)].Properties!.Cartridges!.First().Name="cartridges";
var overloadGlobals=(GlobalTable)RuntimeHelpers.GetUninitializedObject(typeof(GlobalTable));
var overloadPresets=new Dictionary<MongoId,Preset>{[Id(7200)]=new(){Id=Id(7200),Encyclopedia=Id(4),Items=[new(){Id=Id(7201),Template=Id(4)},new(){Id=Id(7202),Template=Id(10),ParentId=Id(7201).ToString(),SlotId="mod_magazine"}]},[Id(7300)]=new(){Id=Id(7300),Encyclopedia=Id(1),Items=[new(){Id=Id(7301),Template=Id(1)}]}};
typeof(GlobalTable).GetProperty("ItemPresets")!.SetValue(overloadGlobals,overloadPresets);
var overloadedService=new OverloadedService(templateTable,overloadGlobals,new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
Check(overloadedService.Available(100)&&overloadedService.SidearmCapacity==84,"Overloaded uses real 100-round main weapons and 84-round pistols, not infected 999-round magazines");
var cachedLoadout=new BotBase{Id=Id(7400),Info=new(){Nickname="Cached",Side="Usec",Settings=new(){Role="pmcUSEC"}},Inventory=new(){Equipment=Id(7401),Items=[new(){Id=Id(7401),Template=Id(9999)},new(){Id=Id(7402),Template=Id(3),ParentId=Id(7401).ToString(),SlotId="FirstPrimaryWeapon"},new(){Id=Id(7403),Template=Id(2),ParentId=Id(7401).ToString(),SlotId="SecondPrimaryWeapon"},new(){Id=Id(7404),Template=Id(1),ParentId=Id(7401).ToString(),SlotId="Holster"},new(){Id=Id(7405),Template=Id(10),ParentId=Id(7401).ToString(),SlotId="spare"}]}};
string cachedLoadoutBefore=System.Text.Json.JsonSerializer.Serialize(cachedLoadout),presetsBefore=System.Text.Json.JsonSerializer.Serialize(overloadPresets.Values.ToArray());
var enforced=overloadedService.CopyForResponse(cachedLoadout,100);var enforcedItems=enforced.Inventory!.Items!;
foreach(string slot in new[]{"FirstPrimaryWeapon","SecondPrimaryWeapon","Holster"})
{
 var gun=enforcedItems.Single(i=>i.SlotId==slot&&i.ParentId==enforced.Inventory.Equipment.ToString());
 var magazine=enforcedItems.Single(i=>i.ParentId==gun.Id.ToString()&&i.SlotId=="mod_magazine");
 Check(magazine.Template==(slot=="Holster"?Id(7100):Id(11)),"Overloaded enforces the correct real magazine in "+slot);
 Check(enforcedItems.Single(i=>i.ParentId==magazine.Id.ToString()&&i.SlotId=="cartridges").Upd!.StackObjectsCount==(slot=="Holster"?84:100),"Overloaded supplies a fully loaded magazine in "+slot);
}
Check(!enforcedItems.Any(i=>i.Template==Id(10)||i.Template==Id(7101)||i.Template==Id(3)||i.Template==Id(2)),"Overloaded removes ordinary cached weapons and undersized spare magazines");
Check(enforcedItems.Select(i=>i.Id).Distinct().Count()==enforcedItems.Count&&enforcedItems.Where(i=>i.Id!=enforced.Inventory.Equipment).All(i=>enforcedItems.Any(p=>p.Id.ToString()==i.ParentId)),"Enforced weapons and magazines have unique IDs and valid attachment parents");
Check(System.Text.Json.JsonSerializer.Serialize(cachedLoadout)==cachedLoadoutBefore&&System.Text.Json.JsonSerializer.Serialize(overloadPresets.Values.ToArray())==presetsBefore,"Enforcement preserves original cached profiles and shared weapon presets");
foreach(string role in new[]{"assault","assaultGroup","pmcUSEC","pmcBEAR"})
{
 var actor=new BotBase{Info=new(){Settings=new(){Role=role}},Inventory=cachedLoadout.Inventory};
 Check(!ReferenceEquals(overloadedService.CopyForResponse(actor,100),actor),"Final Overloaded enforcement includes "+role);
}
Check(ReferenceEquals(overloadedService.CopyForResponse(plainBoss,100),plainBoss)&&ReferenceEquals(overloadedService.CopyForResponse(humanScav,100),humanScav)&&ReferenceEquals(overloadedService.CopyForResponse(humanProfile,100),humanProfile),"Overloaded leaves bosses and player-role profiles untouched");
OverloadedRuntime.Initialize(overloadedService);var overloadSession=new MongoId();var overloadTask=Task.FromResult<IEnumerable<BotBase?>>([cachedLoadout]);
Check(ReferenceEquals(OverloadedRuntime.TransformResponse(overloadSession,overloadTask),overloadTask),"Overloaded final enforcement is inert outside its raid event");
RaidRulesRuntime.Activate(overloadSession,"overloaded");
Check(!ReferenceEquals((await OverloadedRuntime.TransformResponse(overloadSession,overloadTask)).Single(),cachedLoadout),"Active Overloaded response hook transforms cached AI profiles");
RaidRulesRuntime.Clear(overloadSession);
int dbArg=Array.IndexOf(args,"--native-db");
if(dbArg>=0&&dbArg+1<args.Length)
{
 var dbRoot=args[dbArg+1];var nativeJson=new SPTarkov.Server.Core.Utils.JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
 var nativeItems=nativeJson.Deserialize<Dictionary<MongoId,TemplateItem>>(File.ReadAllText(System.IO.Path.Combine(dbRoot,"templates","items.json")))!;
 using var globalDocument=System.Text.Json.JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(dbRoot,"globals.json")));
 var nativePresets=nativeJson.Deserialize<Dictionary<MongoId,Preset>>(globalDocument.RootElement.GetProperty("ItemPresets").GetRawText())!;
 var nativeTemplates=(TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));typeof(TemplateTable).GetProperty("Items")!.SetValue(nativeTemplates,nativeItems);
 var nativeGlobals=(GlobalTable)RuntimeHelpers.GetUninitializedObject(typeof(GlobalTable));typeof(GlobalTable).GetProperty("ItemPresets")!.SetValue(nativeGlobals,nativePresets);
 var eventArsenal=new EventArsenal(nativeTemplates,nativeGlobals,new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
 bool InFamily(MongoId item,MongoId target){var seen=new HashSet<MongoId>();while(seen.Add(item)){if(item==target)return true;if(!nativeItems.TryGetValue(item,out var entry))break;item=entry.Parent;}return false;}
 var arsenalPresetsBefore=nativeJson.Serialize(nativePresets);
 Check(EventArsenal.Ladder.SequenceEqual(new[]{"pistol","shotgun","smg","sniperRifle","ak50","assaultRifle","machinegun","rocket","grenadeLauncher"}),"Gun Game inserts AK50 immediately after sniper and ends with grenade launcher");
 foreach(string category in EventArsenal.Ladder.Concat(new[]{"ak50"}))
 {
  Check(eventArsenal.Available(category),"Installed database supports ladder category "+category);
  bool valid=true,compatibleMods=true,complete=true,noConflicts=true;int extraAccessories=0;var variants=new HashSet<string>();
  for(int sample=0;sample<12;sample++)
  {
   var kit=eventArsenal.Kit(category);var root=kit[0];var ids=kit.Select(i=>i.Id.ToString()).ToHashSet();
   variants.Add(string.Join(";",kit.Where(i=>i.SlotId?.StartsWith("mod_")==true&&i.SlotId!="mod_magazine").Select(i=>i.Template.ToString()).Order()));
   int arsenalBaseline=nativePresets.Values.FirstOrDefault(p=>p.Encyclopedia==root.Template)?.Items?.Count(i=>i.SlotId?.StartsWith("mod_")==true&&i.SlotId!="mod_magazine")??0;
   extraAccessories+=Math.Max(0,kit.Count(i=>i.SlotId?.StartsWith("mod_")==true&&i.SlotId!="mod_magazine")-arsenalBaseline);
   foreach(var item in kit){
    foreach(var required in nativeItems[item.Template].Properties?.Slots??[])
     if(required.Required==true&&required.Name?.StartsWith("camora")!=true&&required.Name?.StartsWith("patron")!=true){bool present=kit.Any(i=>i.ParentId==item.Id.ToString()&&i.SlotId==required.Name);if(!present)Console.WriteLine("DETAIL missing required "+nativeItems[item.Template].Name+" / "+required.Name);complete&=present;}
    foreach(var other in kit.Where(i=>i.Id!=item.Id)){bool pair=!(nativeItems[item.Template].Properties?.ConflictingItems??[]).Any(conflict=>InFamily(other.Template,conflict));if(!pair)Console.WriteLine("DETAIL conflict "+nativeItems[item.Template].Name+" / "+nativeItems[other.Template].Name);noConflicts&=pair;}
   }
   valid&=root.ParentId==null&&ids.Count==kit.Count&&kit.Skip(1).All(i=>i.ParentId!=null&&ids.Contains(i.ParentId));
   if(category!="impact")
   {
    valid&=kit.Any(i=>i.ParentId!=null&&i.Template==nativeItems[root.Template].Properties!.DefAmmo&&i.Upd?.StackObjectsCount>0);
    foreach(var item in kit.Where(i=>i.ParentId!=null))
    {
     var parent=kit.Single(i=>i.Id.ToString()==item.ParentId);var props=nativeItems[parent.Template].Properties;
     var slot=(props?.Cartridges??[]).Concat(props?.Chambers??[]).Concat(props?.Slots??[]).FirstOrDefault(s=>s.Name==item.SlotId);
     valid&=slot!=null;
     if(item.SlotId?.StartsWith("mod_")==true){bool fits=(slot?.Properties?.Filters??[]).Any(f=>(f.Filter??[]).Any(filter=>InFamily(item.Template,filter)));if(!fits)Console.WriteLine("DETAIL incompatible "+nativeItems[item.Template].Name+" / "+nativeItems[parent.Template].Name+" / "+item.SlotId);compatibleMods&=fits;}
     if(slot?.MaxCount!=null)valid&=slot.MaxCount>0&&item.Upd?.StackObjectsCount<=slot.MaxCount;
    }
   }
  }
  Check(valid,"Native "+category+" kits have a single root, unique IDs, valid native slots and loaded compatible ammo");
  Check(compatibleMods&&complete&&noConflicts,"Native "+category+" randomized attachments satisfy slot filters, required parts and conflicts");
  if(category is "pistol" or "shotgun" or "sniperRifle" or "ak50" or "smg" or "assaultRifle" or "machinegun")Check(extraAccessories>0&&variants.Count>1,"Native "+category+" receives additional accessories with varied builds");
 }
 Check(nativeJson.Serialize(nativePresets)==arsenalPresetsBefore,"Random weapon accessories do not mutate native shared presets");
 var nativeF1=nativeItems[new MongoId(GrenadierService.Smoke)];Check(nativeF1.Properties?.ThrowType==ThrowWeapType.frag_grenade,"Grenadier supplies a genuine native fragmentation grenade");
 var nativeOverload=new OverloadedService(nativeTemplates,nativeGlobals,new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
 Check(nativeOverload.Available(100)&&nativeOverload.SidearmCapacity==84,"Installed item database and presets support 100+ round primaries and genuine 84-round PM/PB sidearms");
 var nativeOldPistol=nativeItems.Values.First(t=>t.Properties?.WeapClass=="pistol").Id;
 var nativeNpc=new BotBase{Info=new(){Settings=new(){Role="pmcUSEC"}},Inventory=new(){Equipment=Id(8100),Items=[new(){Id=Id(8100),Template=Id(9999)},new(){Id=Id(8101),Template=nativeOldPistol,ParentId=Id(8100).ToString(),SlotId="FirstPrimaryWeapon"},new(){Id=Id(8102),Template=nativeOldPistol,ParentId=Id(8100).ToString(),SlotId="SecondPrimaryWeapon"},new(){Id=Id(8103),Template=nativeOldPistol,ParentId=Id(8100).ToString(),SlotId="Holster"}]}};
 foreach(string role in new[]{"assault","pmcUSEC","bossKilla","followerBully"})
 {
  nativeNpc.Info!.Settings!.Role=role;var swapped=eventArsenal.CopyAi(nativeNpc,"pistol",true);
  Check(!ReferenceEquals(swapped,nativeNpc)&&swapped.Inventory!.Items!.Count(i=>nativeItems.TryGetValue(i.Template,out var t)&&!string.IsNullOrEmpty(t.Properties?.WeapClass))==1,"Gun Game starts combat actor "+role+" with one pistol");
 }
 foreach(string role in new[]{"shooterBTR","player","playerscav"}){nativeNpc.Info!.Settings!.Role=role;Check(ReferenceEquals(eventArsenal.CopyAi(nativeNpc,"ak50",true),nativeNpc),"Global weapon event excludes "+role);}
 nativeNpc.Info!.Settings!.Role="pmcUSEC";var nativeGrenadier=new GrenadierService(nativeTemplates,new SPTarkov.Server.Core.Utils.Cloners.FastCloner());var fragged=nativeGrenadier.CopyForResponse(nativeNpc);
 Check(fragged.Inventory!.Items!.Count(i=>i.Template.ToString()==GrenadierService.Smoke) is >=10 and <=15,"Grenadier equips a native PMC with 10-15 F-1 grenades in native pockets");
 bool actualMagsValid=true,actualGraphsValid=true;
 for(int sample=0;sample<64;sample++)
 {
  var actual=nativeOverload.CopyForResponse(nativeNpc,100);var inventory=actual.Inventory!.Items!;
  foreach(var gun in inventory.Where(i=>i.ParentId==actual.Inventory.Equipment.ToString()&&i.SlotId is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster"))
  {
   var magazine=inventory.Single(i=>i.ParentId==gun.Id.ToString()&&i.SlotId=="mod_magazine");
   double actualCapacity=nativeItems[magazine.Template].Properties!.Cartridges!.Max(c=>c.MaxCount??0);
   actualMagsValid&=(gun.SlotId=="Holster"?actualCapacity==84:actualCapacity>=100)&&nativeItems[magazine.Template].Name?.Contains("infected",StringComparison.OrdinalIgnoreCase)!=true;
   actualMagsValid&=nativeItems[gun.Template].Properties!.Slots!.Where(s=>s.Name=="mod_magazine").SelectMany(s=>s.Properties!.Filters!).SelectMany(f=>f.Filter!).Contains(magazine.Template);
  }
  actualGraphsValid&=inventory.Select(i=>i.Id).Distinct().Count()==inventory.Count&&inventory.Where(i=>i.Id!=actual.Inventory.Equipment).All(i=>inventory.Any(parent=>parent.Id.ToString()==i.ParentId));
 }
 Check(actualMagsValid,"Sixty-four native-preset loadouts have compatible 100+ magazines on both primaries and real 84-round sidearms");
 Check(actualGraphsValid,"Native weapon presets retain valid attachment graphs after capacity enforcement");
 var categoryService=new WeaponCategoryService(nativeTemplates,nativeGlobals,new SPTarkov.Server.Core.Utils.Cloners.FastCloner());
 var nativeFrag=nativeItems.Values.First(t=>t.Properties?.ThrowType==ThrowWeapType.frag_grenade).Id;
 nativeNpc.Inventory!.Items!.Add(new(){Id=Id(8190),Template=nativeFrag,ParentId=Id(8100).ToString(),SlotId="Pockets"});
 var originalNpcJson=System.Text.Json.JsonSerializer.Serialize(nativeNpc);
 foreach(var (eventId,classes) in new[]{("get_handy",new[]{"pistol","revolver"}),("i_have_a_shot",new[]{"shotgun"}),("marksman",new[]{"sniperRifle"})})
 {
  Check(categoryService.Available(eventId),eventId+" has complete native loadable presets");
  bool valid=true;
  foreach(string role in new[]{"assault","assaultGroup","cursedAssault","marksman","pmcUSEC","pmcBEAR"})
  for(int sample=0;sample<24;sample++)
  {
   nativeNpc.Info!.Settings!.Role=role;
   var categoryBot=categoryService.CopyForResponse(nativeNpc,eventId);var inventory=categoryBot.Inventory!.Items!;
   var guns=inventory.Where(i=>nativeItems.TryGetValue(i.Template,out var t)&&!string.IsNullOrEmpty(t.Properties?.WeapClass)).ToArray();
   valid&=guns.Length==1&&guns.All(i=>classes.Contains(nativeItems[i.Template].Properties!.WeapClass!));
   valid&=guns.All(i=>i.ParentId==categoryBot.Inventory.Equipment.ToString()&&i.SlotId==(eventId=="get_handy"?"Holster":"FirstPrimaryWeapon"));
   valid&=inventory.Any(i=>i.Upd?.StackObjectsCount>0&&nativeItems.TryGetValue(i.Template,out var t)&&t.Properties?.Caliber!=null);
   valid&=inventory.Where(i=>nativeItems.TryGetValue(i.Template,out var t)&&t.Properties?.ThrowType!=null).All(i=>i.Template.ToString()=="5a0c27731526d80618476ac4");
   valid&=inventory.Any(i=>i.Template.ToString()=="5a0c27731526d80618476ac4");
   valid&=inventory.Select(i=>i.Id).Distinct().Count()==inventory.Count&&inventory.Where(i=>i.Id!=categoryBot.Inventory.Equipment).All(i=>inventory.Any(parent=>parent.Id.ToString()==i.ParentId));
  }
  Check(valid,eventId+" enforces class, loaded weapons, valid IDs/attachments and flashbangs across 144 cached PMC/Scav loadouts");
  foreach(string role in new[]{"player","playerscav","bossKilla","bossTagilla","followerBully"}){nativeNpc.Info!.Settings!.Role=role;Check(ReferenceEquals(categoryService.CopyForResponse(nativeNpc,eventId),nativeNpc),eventId+" excludes "+role);}
 }
 nativeNpc.Info!.Settings!.Role="pmcUSEC";
 Check(System.Text.Json.JsonSerializer.Serialize(nativeNpc)==originalNpcJson,"Category loadout transformations preserve the original cached profile");
 WeaponCategoryRuntime.Initialize(categoryService);
 var categorySession=new MongoId();var nativeTask=Task.FromResult<IEnumerable<BotBase?>>([nativeNpc]);
 Check(ReferenceEquals(WeaponCategoryRuntime.TransformResponse(categorySession,nativeTask),nativeTask),"Category response hook is inert outside its events");
 RaidRulesRuntime.Activate(categorySession,"get_handy");
 Check(!ReferenceEquals((await WeaponCategoryRuntime.TransformResponse(categorySession,nativeTask)).Single(),nativeNpc),"Category response hook covers cached profiles in active events");RaidRulesRuntime.Clear(categorySession);
}
int mapArg=Array.IndexOf(args,"--native-map");
if(mapArg>=0&&mapArg+1<args.Length)
{
 using var mapJson=System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[mapArg+1]));
 var nativeRoofMap=new LocationBase{Id="TarkovStreets",SpawnPointParams=mapJson.RootElement.GetProperty("SpawnPointParams").EnumerateArray().Where(p=>p.TryGetProperty("BotZoneName",out var name)&&name.ValueKind==System.Text.Json.JsonValueKind.String&&p.TryGetProperty("Position",out _)).Select(p=>new SpawnPointParam{BotZoneName=p.GetProperty("BotZoneName").GetString(),Categories=p.TryGetProperty("Categories",out var cats)?cats.EnumerateArray().Select(c=>c.GetString()!).ToArray():[],Position=new Vector3{X=p.GetProperty("Position").GetProperty("x").GetSingle(),Y=p.GetProperty("Position").GetProperty("y").GetSingle(),Z=p.GetProperty("Position").GetProperty("z").GetSingle()}}).ToArray()};
 var nativeRoofPoints=OverwatchPlan.Points(nativeRoofMap);
 Check(nativeRoofPoints.Length==13&&OverwatchPlan.Zones(nativeRoofMap).Length==6,"Installed Streets map resolves exactly 13 native sniper points across six zones (26 extras)");
 Check(nativeRoofPoints.All(p=>p.Y>10),"Installed Streets sniper coordinates retain their native elevated positions");
}









var psychConfig=new RaidRouletteConfig();
Check(psychConfig.V2.Events["schizophrenia"].Weight==0,"Schizophrenia is available for forced testing without adding random raid weight");
Check(new EventManager(psychConfig).SelectEvent(_=>true,"schizophrenia").Event.Name=="Schizophrenia","Backend selection recognizes Schizophrenia with the requested name");
var psychSession=new MongoId();RaidRulesRuntime.Activate(psychSession,"schizophrenia");
Check(RaidRulesRuntime.Get(psychSession)?.EventId=="schizophrenia","New local event publishes a matching raid context");RaidRulesRuntime.Clear(psychSession);
var progression=new GunGameLedger();
Check(!progression.Won&&progression.Stage==0&&progression.EligibleReward==0,"Gun Game begins at pistol with no reward");
var reordered=new GunGameLedger{Stage=4};reordered.RecordKill("wrong-sniper","sniperRifle");reordered.RecordKill("old-impact","impact");Check(reordered.Stage==4&&!reordered.Won,"AK50 stage rejects sniper and removed impact-grenade kills");reordered.RecordKill("ak50-kill","ak50");Check(reordered.Stage==5,"AK50 kill advances to assault rifle");var finale=new GunGameLedger{Stage=8};finale.RecordKill("final-impact","impact");Check(!finale.Won,"Removed impact grenade cannot win the final stage");finale.RecordKill("final-launcher","grenadeLauncher");Check(finale.Won&&finale.EligibleReward==1,"Grenade-launcher kill wins the nine-stage ladder and Bitcoin");
progression.RecordKill("early-knife","knife");Check(progression.Knife&&progression.Stage==0&&progression.EligibleReward==0,"Secret knife kill is tracked without advancing the weapon ladder");
for(int stage=0;stage<9;stage++){progression.RecordKill("kill-"+stage,EventArsenal.Ladder[stage]);Check(progression.Won==(stage==8),"Gun Game requires the correct stage kill through level "+stage);}
Check(progression.Won&&progression.EligibleReward==3,"Nine scored kills plus an earlier knife kill earn three Bitcoins total");
progression.RecordKill("extra-knife","knife");Check(progression.EligibleReward==3,"Additional knife kills cannot multiply the secret reward");
var lateKnife=new GunGameLedger();foreach(var (category,index) in EventArsenal.Ladder.Select((value,index)=>(value,index)))lateKnife.RecordKill("late-"+index,category);
Check(lateKnife.Won&&lateKnife.EligibleReward==1,"Completing the ladder without a knife earns one Bitcoin");lateKnife.RecordKill("late-knife","knife");Check(lateKnife.EligibleReward==3,"Knife bonus can complete after the main win within the same raid");
var duplicateKill=new GunGameLedger();duplicateKill.RecordKill("same-victim","pistol");duplicateKill.RecordKill("same-victim","shotgun");Check(duplicateKill.Stage==1,"Repeated callbacks for the same victim cannot advance Gun Game twice");duplicateKill.RecordKill("wrong-gun","assaultRifle");Check(duplicateKill.Stage==1,"Kills with the wrong weapon category do not skip ladder stages");
var escrowClone=new SPTarkov.Server.Core.Utils.Cloners.FastCloner();var escrowInventory=escrowClone.Clone(cachedLoadout.Inventory)!;
var escrowGun=escrowInventory.Items!.First(i=>i.SlotId=="FirstPrimaryWeapon");escrowGun.Upd=new(){Repairable=new(){Durability=37,MaxDurability=58}};
escrowInventory.Items.Add(new(){Id=Id(8901),Template=Id(11),ParentId=escrowGun.Id.ToString(),SlotId="mod_magazine"});escrowInventory.Items.Add(new(){Id=Id(8902),Template=Id(20),ParentId=Id(8901).ToString(),SlotId="cartridges",Upd=new(){StackObjectsCount=42}});escrowInventory.Items.Add(new(){Id=Id(8903),Template=Id(99),ParentId=escrowGun.Id.ToString(),SlotId="mod_scope"});
string escrowOriginalJson=System.Text.Json.JsonSerializer.Serialize(escrowInventory);var snapshot=WeaponEscrow.Snapshot(escrowInventory,escrowClone);
Check(snapshot.Count>3&&snapshot.Any(i=>i.Upd?.Repairable?.Durability==37)&&snapshot.Any(i=>i.ParentId==escrowGun.Id.ToString()),"Weapon escrow snapshots complete original gun trees and durability");
Check(System.Text.Json.JsonSerializer.Serialize(escrowInventory)==escrowOriginalJson,"Escrow snapshot creation leaves the original inventory untouched");
var escrowState=new GunGameLedger{Token="test-return",Event="gun_game",ReturnItems=snapshot,OriginalIds=snapshot.Select(i=>i.Id.ToString()).ToList()};
int sent=0;List<Item>? returnedKit=null;WeaponEscrow.DeliverOnce(escrowState,escrowInventory.Items!,escrowClone,batch=>{sent++;returnedKit=batch;});
Check(sent==1&&escrowState.Returned&&!WeaponEscrow.DeliverOnce(escrowState,escrowInventory.Items!,escrowClone,_=>sent++),"Weapon return is idempotent across repeated end/recovery callbacks");
Check(returnedKit!.Count==snapshot.Count&&returnedKit.All(i=>!escrowState.OriginalIds.Contains(i.Id.ToString()))&&returnedKit.Any(i=>i.Upd?.Repairable?.Durability==37),"Trader return uses fresh IDs and preserves original condition");
Check(returnedKit.Where(i=>i.ParentId!=null).All(i=>returnedKit.Any(p=>p.Id.ToString()==i.ParentId))&&returnedKit.Where(i=>i.ParentId==null).All(i=>i.Location==null),"Returned weapon attachments stay linked and roots are detached for native mail placement");
var ledgerJson=new SPTarkov.Server.Core.Utils.JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
var reloadedEscrow=ledgerJson.Deserialize<GunGameLedger>(ledgerJson.Serialize(escrowState)!)!;Check(reloadedEscrow.Returned&&!WeaponEscrow.DeliverOnce(reloadedEscrow,escrowInventory.Items!,escrowClone,_=>sent++)&&sent==1,"Saved/reloaded escrow cannot send the original weapons twice");
var failedEscrow=new GunGameLedger{Token="retry",ReturnItems=snapshot,OriginalIds=snapshot.Select(i=>i.Id.ToString()).ToList()};
try{WeaponEscrow.DeliverOnce(failedEscrow,escrowClone.Clone(cachedLoadout.Inventory!.Items)!,escrowClone,_=>throw new IOException("Simulated mail failure"));}catch(IOException){}
Check(!failedEscrow.Returned&&failedEscrow.ReturnItems.Count==snapshot.Count,"Failed mail delivery retains the pending original snapshot for recovery");
progression.IssueRewards();progression.IssueRewards();Check(progression.Rewards.Count==3&&progression.Rewards.Select(i=>i.Id).Distinct().Count()==3,"Repeated reward issuance produces exactly three distinct Bitcoins for the secret win");
var restoredWin=ledgerJson.Deserialize<GunGameLedger>(ledgerJson.Serialize(progression)!)!;restoredWin.IssueRewards();Check(restoredWin.Rewards.Count==3&&restoredWin.Won&&restoredWin.Knife,"Reward state survives save/reload without duplicating Bitcoins");

var savedProfile=new SPTarkov.Server.Core.Models.Eft.Profile.SptProfile{ExtensionData=new(){["raidRouletteWeaponEscrow"]=progression}};
var restoredProfile=ledgerJson.Deserialize<SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>(ledgerJson.Serialize(savedProfile)!)!;
typeof(GunGameRuntime).GetField("json",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,ledgerJson);
var stateFromExtension=(GunGameLedger)typeof(GunGameRuntime).GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[restoredProfile])!;
Check(stateFromExtension.Won&&stateFromExtension.Knife&&stateFromExtension.Rewards.Count==3,"Actual profile-extension loader recovers the durable weapon/reward ledger after JSON reload");
var markedMail=new SPTarkov.Server.Core.Models.Eft.Profile.SystemData{ExtensionData=new(){["rrDelivery"]="return:test-token"}};
var restoredMarker=ledgerJson.Deserialize<SPTarkov.Server.Core.Models.Eft.Profile.SystemData>(ledgerJson.Serialize(markedMail)!)!;
Check((bool)typeof(GunGameRuntime).GetMethod("IsMarker",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[restoredMarker,"return:test-token"])!,"Mail idempotency markers survive native JSON serialization without appearing in the letter text");
TestingControl.SetStartupPopulation(15,8);Check(TestingControl.PopulationCap==15&&TestingControl.PopulationTarget==8,"Gun Game can transmit a native population cap and rounded-up half-population floor");TestingControl.Record("normal","factory4_day",null);Check(TestingControl.PopulationCap==0&&TestingControl.PopulationTarget==0,"A following raid clears the previous population plan");
int friendArg=Array.IndexOf(args,"--friend-config");
var restartLedger=new GunGameLedger{Token="same-raid",Event="gun_game",Stage=5,Knife=true,ReturnItems=snapshot.ToList(),OriginalIds=snapshot.Select(i=>i.Id.ToString()).ToList()};
restartLedger.Victims.Add("old-kill");string restartSnapshot=ledgerJson.Serialize(restartLedger.ReturnItems)!;
Check(restartLedger.Restart("life-1")&&restartLedger.Stage==0&&!restartLedger.Knife&&!restartLedger.Won&&restartLedger.Attempt==1,"Lethal restart resets ladder and knife condition to pistol in the same raid");
Check(!restartLedger.Restart("life-1")&&restartLedger.Attempt==1,"Retrying the same respawn request cannot reset or increment a life twice");
Check(!restartLedger.RecordKill("late-old-shot","pistol",0)&&!restartLedger.Victims.Contains("late-old-shot")&&restartLedger.RecordKill("new-life-shot","pistol",1)&&restartLedger.Stage==1,"Stale previous-life kill acknowledgements cannot advance the new attempt");
Check(restartLedger.Token=="same-raid"&&!restartLedger.Returned&&ledgerJson.Serialize(restartLedger.ReturnItems)==restartSnapshot&&restartLedger.Victims.Contains("old-kill"),"Respawn preserves the original weapon escrow and whole-raid victim deduplication");
restartLedger.Won=true;restartLedger.Knife=true;restartLedger.IssueRewards();restartLedger.Restart("life-2");foreach(var (kind,index) in EventArsenal.Ladder.Select((kind,index)=>(kind,index)))restartLedger.RecordKill("re-win-"+index,kind,restartLedger.Attempt);restartLedger.RecordKill("re-knife","knife",restartLedger.Attempt);restartLedger.IssueRewards();
Check(restartLedger.Rewarded==3&&restartLedger.Rewards.Count==3,"Repeated wins after respawn cannot duplicate the raid's Bitcoin rewards");
var reloadedRestart=ledgerJson.Deserialize<GunGameLedger>(ledgerJson.Serialize(restartLedger))!;Check(!reloadedRestart.Restart("life-2")&&reloadedRestart.Attempt==2,"Respawn idempotency and attempt counters survive durable JSON reload");
if(friendArg>=0&&friendArg+1<args.Length)
{
 var friendConfig=System.Text.Json.JsonSerializer.Deserialize<RaidRouletteConfig>(File.ReadAllText(args[friendArg+1]))!;
 var friendAllowed=new HashSet<string>{"the_gift","clone_wars","boss_hunt","smokers_lobby","time_warp","stormtide","cultist_invasion","schizophrenia","gun_game"};
 Check(friendConfig.EventChance==90&&string.IsNullOrEmpty(friendConfig.Debug.ForceEvent),"Friend preset uses random selection with normal raids available and no forced event");
 Check(friendConfig.V2.Events.Where(e=>e.Value.Enabled&&e.Key!="normal").Select(e=>e.Key).ToHashSet().SetEquals(friendAllowed),"Friend preset enables exactly the nine requested special events");
 var friendManager=new EventManager(friendConfig);
 foreach(var entry in EventRegistry.GetEvents())Check(friendManager.SelectEvent(_=>true,entry.Id).Event.Id==(entry.Id=="normal"||friendAllowed.Contains(entry.Id)?entry.Id:"normal"),"Friend forced-selection allowlist: "+entry.Id);
 Check(Enumerable.Range(0,500).All(_=>{string id=friendManager.SelectEvent().Event.Id;return id=="normal"||friendAllowed.Contains(id);}),"Friend random selection never chooses Extreme Overwatch or other disabled events");
}


// Register the actual embedded quests through SPT's native CustomQuestService.
var questTemplates=(TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
typeof(TemplateTable).GetProperty("Quests")!.SetValue(questTemplates,new Dictionary<MongoId,Quest>());
var questLocale=new GlobalLocaleDictionary();
var questLocales=new LocaleTable{Global=new(){["en"]=new LazyLoad<GlobalLocaleDictionary>(()=>questLocale,true)},Menu=new(),Languages=new(){["en"]="English"}};
var questService=new SPTarkov.Server.Core.Services.Modding.Custom.CustomQuestService(questTemplates,questLocales,null!,(SPTarkov.Server.Core.Models.Spt.Config.QuestConfig)RuntimeHelpers.GetUninitializedObject(typeof(SPTarkov.Server.Core.Models.Spt.Config.QuestConfig)));
TraderQuestRuntime.Initialize(null!,ledgerJson,questService);
Check(questTemplates.Quests.Count==10,"All ten embedded quests register through native SPT quest service without a quest-loader mod");
Check(questLocales.Global["en"].Value!.Values.Any(v=>v.Contains("weapon case")),"Native lazy locale loading receives trader briefings, objectives and completion text");
var questIds=RaidRoulette.Shared.QuestIds.Entries;
foreach(var e in questIds)
{
 var q=questTemplates.Quests[new MongoId(e[1])];
 Check(q.Conditions.AvailableForStart!.Any(c=>c.ConditionType=="Level"&&c.Value==10),"Level-ten quest unlock: "+e[0]);
 Check(q.Conditions.AvailableForFinish!.Any(c=>c.Id.ToString()==e[2]&&c.ConditionType=="CounterCreator"&&c.Value==double.Parse(e[4])),"Native event-gated counter schema: "+e[0]);
 var restoredQuest=ledgerJson.Deserialize<Quest>(ledgerJson.Serialize(q))!;
 Check(restoredQuest.Id==q.Id&&restoredQuest.Rewards!["Success"].Count==q.Rewards!["Success"].Count,"Native quest JSON roundtrip: "+e[0]);
}
Check(questIds.Select(e=>e[0]).SequenceEqual(new[]{"stormtide","smokers_lobby","the_gift","time_warp","schizophrenia","clone_wars","ak50","boss_hunt","cultist_invasion","gun_game"}),"Quest chain starts with Stormtide and ends with Gun Game");
for(int i=0;i<questIds.Length;i++)
{
 var starts=questTemplates.Quests[new MongoId(questIds[i][1])].Conditions.AvailableForStart!.Where(c=>c.ConditionType=="Quest").ToArray();
 Check(i==0?starts.Length==0:starts.Length==1&&starts[0].Target!.Item==questIds[i-1][1]&&starts[0].Status!.SetEquals(new[]{QuestStatusEnum.Success}),"Sequential native unlock requires prior quest completion: "+questIds[i][0]);
}
var stormQuest=questTemplates.Quests[new MongoId(questIds.Single(e=>e[0]=="stormtide")[1])];
Check(stormQuest.Rewards!["Success"].Single().Items!.Single().Template.ToString()=="59fb023c86f7746d0d4b423c","Stormtide rewards the standard weapon case");
var schizoQuest=questTemplates.Quests[new MongoId(questIds.Single(e=>e[0]=="schizophrenia")[1])];
Check(schizoQuest.Conditions.AvailableForStart!.Any(c=>c.ConditionType=="Quest"&&c.Target!.Item==questIds.Single(e=>e[0]=="time_warp")[1]&&c.Status!.Contains(QuestStatusEnum.Success)),"Schizophrenia requires successful Time Warp completion");
Check(TraderQuestRuntime.MapAllowed("gun_game","factory4_day")&&!TraderQuestRuntime.MapAllowed("gun_game","woods")&&TraderQuestRuntime.MapAllowed("clone_wars","sandbox_high")&&!TraderQuestRuntime.MapAllowed("clone_wars","laboratory"),"Quest Factory and Ground Zero restrictions");
Check(TraderQuestRuntime.MapAllowed("cultist_invasion","laboratory")&&!TraderQuestRuntime.MapAllowed("cultist_invasion","woods")&&!TraderQuestRuntime.MapAllowed("the_gift","laboratory")&&!TraderQuestRuntime.MapAllowed("stormtide","factory4_night"),"Quest Labs, Gift and outdoor restrictions");
var rewardPmc=new PmcData{Id=new MongoId("aaaaaaaaaaaaaaaaaaaaaaaa")};
var refQuest=questTemplates.Quests[new MongoId(questIds.Single(e=>e[0]=="gun_game")[1])];
var refRoll=TraderQuestRuntime.Personalize(refQuest,rewardPmc);
Check(refRoll.Rewards!["Success"].Count==50&&ledgerJson.Serialize(refRoll)==ledgerJson.Serialize(TraderQuestRuntime.Personalize(refQuest,rewardPmc)),"Ref reward has fifty native assortment roots and remains stable across repeated previews");
Check(ledgerJson.Serialize(refRoll)!=ledgerJson.Serialize(TraderQuestRuntime.Personalize(refQuest,new PmcData{Id=new MongoId("bbbbbbbbbbbbbbbbbbbbbbbb")})),"Different characters receive different deterministic reward selections");
foreach(var reward in refRoll.Rewards!["Success"])
{
 var ids=reward.Items!.Select(i=>i.Id.ToString()).ToHashSet();
 Check(reward.Items!.Skip(1).All(i=>i.ParentId!=null&&ids.Contains(i.ParentId))&&reward.Items![0].ParentId==null,"Ref assortment reward retains its full attachment tree: "+reward.Id);
}
Check(!ReferenceEquals(refRoll,refQuest)&&refQuest.Rewards!["Success"].Count==50,"Per-character rewards do not mutate the shared quest templates");

// Exercise raid-end credit against an in-memory native SaveServer. InvalidOrUnloadableProfile
// makes native SaveProfileAsync return before any filesystem access in this fixture.
var questSave=(SPTarkov.Server.Core.Servers.SaveServer)RuntimeHelpers.GetUninitializedObject(typeof(SPTarkov.Server.Core.Servers.SaveServer));
var questSession=new MongoId();
var questPmc=new PmcData{Id=new MongoId(),Info=new(){Level=10,Nickname="Quest Tester"},Inventory=new(){Items=[]},TaskConditionCounters=[],Quests=questIds.Select(e=>new QuestStatus{QId=new MongoId(e[1]),Status=QuestStatusEnum.Started,StartTime=1,StatusTimers=[],CompletedConditions=[]}).ToList()};
var questProfile=new SPTarkov.Server.Core.Models.Eft.Profile.SptProfile{ProfileInfo=new(),CharacterData=new(){PmcData=questPmc},ExtensionData=[]};
questProfile.ProfileInfo!.GetType().GetProperty("InvalidOrUnloadableProfile")!.SetValue(questProfile.ProfileInfo,true);
typeof(SPTarkov.Server.Core.Servers.SaveServer).GetField("profiles",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(questSave,new System.Collections.Concurrent.ConcurrentDictionary<MongoId,SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>(new[]{new KeyValuePair<MongoId,SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>(questSession,questProfile)}));
typeof(TraderQuestRuntime).GetField("save",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,questSave);
string[] QE(string eventId)=>questIds.Single(e=>e[0]==eventId);
void QCount(string id,double v)=>questPmc.TaskConditionCounters![new MongoId(id)]=new(){Id=new MongoId(id),SourceId=new MongoId(),Type="CounterCreator",Value=v};
double QValue(string id)=>questPmc.TaskConditionCounters!.GetValueOrDefault(new MongoId(id))?.Value??0;
void QFocus(string eventId){int index=Array.FindIndex(questIds,e=>e[0]==eventId);foreach(var q in questPmc.Quests!){int n=Array.FindIndex(questIds,e=>e[1]==q.QId.ToString());q.Status=n<index?QuestStatusEnum.Success:QuestStatusEnum.Started;}}
Check(TraderQuestRuntime.Select(questSession,"bigmap")=="stormtide","When all old quests are accepted, only Stormtide selects; Boss Hunt cannot jump ahead");
Check(!TraderQuestRuntime.PrerequisitesComplete(questPmc,QE("gun_game")[1])&&!TraderQuestRuntime.PrerequisitesComplete(questPmc,QE("boss_hunt")[1]),"Previously accepted later quests are blocked without deleting their progress");
async Task QFinish(double seconds=600,bool alive=true){var request=new SPTarkov.Server.Core.Models.Eft.Match.EndLocalRaidRequestData{Results=new(){Profile=questPmc,PlayTime=seconds,Result=alive?SPTarkov.Server.Core.Models.Enums.ExitStatus.SURVIVED:SPTarkov.Server.Core.Models.Enums.ExitStatus.KILLED}};TraderQuestRuntime.Capture(questSession,request);await TraderQuestRuntime.Finish(questSession,request);}
TraderQuestRuntime.Begin(questSession,"factory4_day","normal");QCount(QE("smokers_lobby")[2],5);await QFinish();
Check(QValue(QE("smokers_lobby")[2])==0,"Ordinary raids cannot credit Smokers Lobby even when an incoming event counter is changed");
QFocus("smokers_lobby");TraderQuestRuntime.Begin(questSession,"factory4_day","smokers_lobby");QCount(QE("smokers_lobby")[2],3);await QFinish(alive:false);
Check(QValue(QE("smokers_lobby")[2])==3,"Smokers kills persist across death because this quest has no survival requirement");
QFocus("time_warp");TraderQuestRuntime.Begin(questSession,"Woods","time_warp");await QFinish(1199);Check(QValue(QE("time_warp")[2])==0,"Nineteen minutes fifty-nine seconds cannot complete Time Warp");
QFocus("time_warp");TraderQuestRuntime.Begin(questSession,"Woods","time_warp");await QFinish(1200,alive:false);Check(QValue(QE("time_warp")[2])==1200,"Twenty minutes completes Time Warp even if the player subsequently dies");
QFocus("gun_game");TraderQuestRuntime.Begin(questSession,"woods","gun_game");TraderQuestRuntime.GunGameWin(questSession);Check(QValue(QE("gun_game")[2])==0,"A Gun Game win outside Factory does not satisfy Prapor");
TraderQuestRuntime.Begin(questSession,"factory4_day","gun_game");TraderQuestRuntime.GunGameWin(questSession);await QFinish();Check(QValue(QE("gun_game")[2])==1,"An actual Factory Gun Game win survives the native end-of-raid profile merge");
QFocus("boss_hunt");TraderQuestRuntime.Begin(questSession,"woods","boss_hunt");await QFinish();Check(QValue(QE("boss_hunt")[2])==0&&TraderQuestRuntime.Select(questSession,"woods")=="boss_hunt","If the hunter dies without personal quest credit, Boss Hunt remains the next quest event");
var stormEntry=QE("stormtide");QFocus("stormtide");TraderQuestRuntime.Begin(questSession,"tarkovstreets","stormtide");
var recoveredGuns=Enumerable.Range(0,5).Select(_=>new Item{Id=new MongoId(),Template=new MongoId("67d0576f29f580ebc10efd08"),Upd=new(){SpawnedInSession=true}}).ToArray();questPmc.Inventory!.Items!.AddRange(recoveredGuns);await QFinish();
Check(QValue(stormEntry[2])==1&&recoveredGuns.All(i=>TraderQuestRuntime.AllowedItem(questSession,stormEntry[3],i,questPmc)),"Five extracted FIR Stormtide weapons satisfy the milestone and become eligible for native hand-in");
Check(!TraderQuestRuntime.AllowedItem(questSession,stormEntry[3],new Item{Id=new MongoId(),Template=recoveredGuns[0].Template,Upd=new(){SpawnedInSession=true}},questPmc),"An unrelated FIR weapon is rejected by the Stormtide hand-in predicate");
var cloneEntry=QE("clone_wars");QFocus("clone_wars");TraderQuestRuntime.Begin(questSession,"Sandbox_high","clone_wars");
var tags=Enumerable.Range(0,5).Select(_=>new Item{Id=new MongoId(),Template=new MongoId("59f32bb586f774757e1e8442"),Upd=new(){SpawnedInSession=true,Dogtag=new(){Nickname="Quest Tester"}}}).ToArray();questPmc.Inventory.Items.AddRange(tags);await QFinish();
Check(QValue(cloneEntry[2])==1&&tags.All(i=>TraderQuestRuntime.AllowedItem(questSession,cloneEntry[3],i,questPmc)),"Ground Zero clone tags require FIR status and the exact player nickname");tags[0].Upd!.Dogtag!.Nickname="Someone Else";
Check(!TraderQuestRuntime.AllowedItem(questSession,cloneEntry[3],tags[0],questPmc),"A wrong-name dogtag cannot be handed over even if its item ID was previously registered");
var schizoEntry=QE("schizophrenia");QFocus("schizophrenia");TraderQuestRuntime.Begin(questSession,"woods","schizophrenia");QCount(schizoEntry[5],5);await QFinish(alive:false);
Check(QValue(schizoEntry[2])==0&&QValue(schizoEntry[5])==0,"Dying resets the single-raid Schizophrenia kill requirement and gives no survival credit");
TraderQuestRuntime.Begin(questSession,"woods","schizophrenia");QCount(schizoEntry[5],5);
var medCondition=schizoQuest.Conditions.AvailableForFinish!.Single(c=>c.Id.ToString()==schizoEntry[3]);var medical=Enumerable.Range(0,2).Select(_=>new Item{Id=new MongoId(),Template=new MongoId(medCondition.Target!.List![0])}).ToArray();questPmc.Inventory.Items.AddRange(medical);await QFinish();
Check(QValue(schizoEntry[2])==1&&QValue(schizoEntry[5])==5&&medical.All(i=>TraderQuestRuntime.AllowedItem(questSession,schizoEntry[3],i,questPmc)),"Five personal kills, two recovered medical items and survival satisfy the same-raid trial");
var questReload=ledgerJson.Deserialize<SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>(ledgerJson.Serialize(questProfile))!;
Check(questReload.ExtensionData!.ContainsKey("raidRouletteQuestItems")&&questReload.CharacterData!.PmcData!.TaskConditionCounters!.ContainsKey(new MongoId(stormEntry[2])),"Quest provenance ledger and native condition counters survive a profile JSON roundtrip");

var staffSource=ledgerJson.Serialize(giftOriginal);var staff=giftService.CopyForResponse(giftOriginal,true);
Check(staff.Info!.Nickname=="Lab Staff"&&staff.Info.Settings!.Role=="gifter"&&staff.Inventory!.Items!.All(i=>i.Id==staff.Inventory.Equipment||i.SlotId is "Backpack" or "Pockets" or "TacticalVest"),"Lab staff inventories contain no firearms, knives, grenades, Santa hats or gift prizes");
Check(ledgerJson.Serialize(giftOriginal)==staffSource&&staff.Customization!.Body==Id(101),"Staff transformation copies the source and uses native ordinary human clothing");
var workMap=MapData();workMap.BotMax=20;workMap.BotMaxPvE=20;var workWaves=ledgerJson.Serialize(workMap.Waves);var nativeBossCount=workMap.BossLocationSpawn!.Count;
giftService.ConfigureLab(workMap);var workStaff=workMap.BossLocationSpawn.Where(s=>s.SptId=="rr-lab-staff").ToArray();int staffCount=workStaff.Length*3;
Check(workStaff.Length is >0 and <=4&&workStaff.All(s=>s.BossName=="gifter"&&s.BossEscortType=="gifter"&&s.BossEscortAmount=="2"&&s.Time>=60&&s.Time<=84&&s.IgnoreMaxBots==true),"Lab Work Day adds up to twelve timed staff after the opening minute");
Check(workMap.BotMax==20+staffCount&&workMap.BotMaxPvE==20+staffCount&&ledgerJson.Serialize(workMap.Waves)==workWaves&&workMap.BossLocationSpawn.Count==nativeBossCount+workStaff.Length,"Lab Work Day reserves extra staff capacity and preserves ordinary wave and boss schedules");
GiftRuntime.Initialize(giftService);
Check(RaidRulesRuntime.CanRun("lab_work_day","laboratory")&&!RaidRulesRuntime.CanRun("lab_work_day","woods"),"Lab Work Day is restricted to Labs");

TraderQuestRuntime.SetSide(questSession,false);QCount(QE("time_warp")[2],0);TraderQuestRuntime.Begin(questSession,"woods","time_warp");await QFinish(1500);
Check(TraderQuestRuntime.Select(questSession,"woods")==null&&QValue(QE("time_warp")[2])==0,"Scav raids cannot force or advance the PMC trader quest chain");
TraderQuestRuntime.SetSide(questSession,true);

QFocus("stormtide");QCount(QE("boss_hunt")[2],0);TraderQuestRuntime.Begin(questSession,"woods","boss_hunt");QCount(QE("boss_hunt")[2],1);await QFinish();
Check(QValue(QE("boss_hunt")[2])==0,"A forced Boss Hunt cannot advance a later quest while Stormtide is pending");
QCount(QE("gun_game")[2],1);QFocus("stormtide");Check(!TraderQuestRuntime.PrerequisitesComplete(questPmc,QE("gun_game")[1])&&QValue(QE("gun_game")[2])==1,"Existing later-quest progress stays intact while completion waits its turn");


var taggedSource=cloneService.Capture(playerSource);taggedSource.Appearance.DogTag=new MongoId("67471938bafff850080488b7");
var tagSamples=Enumerable.Range(0,64).Select(_=>cloneService.CopyForResponse(taggedSource,CloneTarget())).ToArray();
var sampleTags=tagSamples.Select(b=>b.Inventory!.Items!.Single(i=>i.SlotId=="Dogtag")).ToArray();
Check(sampleTags.Select(i=>i.Id).Distinct().Count()==64&&sampleTags.All(i=>RaidRoulette.Shared.CloneTagIdentity.IsClone(i.Id.ToString())&&i.Id!=taggedSource.Appearance.DogTag),"Sixty-four clones sharing custom dogtag appearance receive distinct identifiable loot IDs");
Check(sampleTags.All(i=>i.Upd!.SpawnedInSession==true)&&taggedSource.Appearance.DogTag.ToString()=="67471938bafff850080488b7","Clone tags are FIR while the player's custom appearance remains unchanged");
var noGeneratedTag=CloneTarget();noGeneratedTag.Inventory!.Items=[];var fallbackClone=cloneService.CopyForResponse(taggedSource,noGeneratedTag);var fallbackTag=fallbackClone.Inventory!.Items!.Single(i=>i.SlotId=="Dogtag");
Check(fallbackTag.Template.ToString()=="59f32bb586f774757e1e8442"&&fallbackTag.Upd!.Dogtag!.ProfileId==fallbackClone.Id.ToString(),"Missing generated tags receive a valid independent faction tag fallback");
QFocus("clone_wars");var customTag=new Item{Id=new MongoId("67471938bafff850080488b7"),Template=new MongoId("6662e9f37fa79a6d83730fa0"),Upd=new(){SpawnedInSession=true,Dogtag=new(){Nickname=questPmc.Info!.Nickname}}};
Check(TraderQuestRuntime.AllowedItem(questSession,QE("clone_wars")[3],customTag,questPmc),"Previously saved native custom dogtags remain eligible without the old failed provenance record");
customTag.Upd!.SpawnedInSession=false;Check(!TraderQuestRuntime.AllowedItem(questSession,QE("clone_wars")[3],customTag,questPmc),"Custom tags still require found-in-raid status");customTag.Upd.SpawnedInSession=true;customTag.Upd.Dogtag!.Nickname="Different";
Check(!TraderQuestRuntime.AllowedItem(questSession,QE("clone_wars")[3],customTag,questPmc),"Custom tags still require the exact player nickname");
var nativeCloneCondition=questTemplates.Quests[new MongoId(QE("clone_wars")[1])].Conditions.AvailableForFinish!.Single(c=>c.ConditionType=="HandoverItem");
Check(nativeCloneCondition.Target!.List!.Contains("6662e9f37fa79a6d83730fa0")&&nativeCloneCondition.Target.List.Count>2,"Clone handover schema includes standard and native custom dogtag styles");
QCount(QE("clone_wars")[2],0);QCount(QE("clone_wars")[3],4);TraderQuestRuntime.SyncCloneHandIn(questPmc);Check(QValue(QE("clone_wars")[2])==0,"Four handed-in tags cannot satisfy five-tag recovery milestone");
QCount(QE("clone_wars")[3],5);TraderQuestRuntime.SyncCloneHandIn(questPmc);Check(QValue(QE("clone_wars")[2])==1,"Five verified native hand-ins also complete recovery proof, including older legitimately collected tags");

var moddedQuestTemplates=(TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));typeof(TemplateTable).GetProperty("Quests")!.SetValue(moddedQuestTemplates,new Dictionary<MongoId,Quest>());
var moddedItems=(TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));var dogtagBase=new MongoId("590c745b86f7743cc433c5f2");var moddedTagId=new MongoId("aaaaaaaaaaaaaaaaaaaaaabb");
typeof(TemplateTable).GetProperty("Items")!.SetValue(moddedItems,new Dictionary<MongoId,TemplateItem>{[dogtagBase]=new(){Id=dogtagBase},[moddedTagId]=new(){Id=moddedTagId,Parent=dogtagBase},[new MongoId("59f32bb586f774757e1e8442")]=new(){Id=new MongoId("59f32bb586f774757e1e8442"),Parent=dogtagBase}});
var moddedQuestService=new SPTarkov.Server.Core.Services.Modding.Custom.CustomQuestService(moddedQuestTemplates,questLocales,null!,(SPTarkov.Server.Core.Models.Spt.Config.QuestConfig)RuntimeHelpers.GetUninitializedObject(typeof(SPTarkov.Server.Core.Models.Spt.Config.QuestConfig)));
TraderQuestRuntime.Initialize(questSave,ledgerJson,moddedQuestService,moddedItems);
Check(moddedQuestTemplates.Quests[new MongoId(QE("clone_wars")[1])].Conditions.AvailableForFinish!.Single(c=>c.ConditionType=="HandoverItem").Target!.List!.Contains(moddedTagId.ToString()),"Runtime quest registration discovers additional installed dogtag styles by their native template family");

var collisionId=new MongoId("67471938bafff850080488b7");var retainedTag=new Item{Id=collisionId,Template=new MongoId("6662e9f37fa79a6d83730fa0"),ParentId="case",SlotId="main",Location=new{x=1,y=2,r=0},Upd=new(){SpawnedInSession=true,Dogtag=new(){Nickname="Repair Test",ProfileId=new MongoId().ToString(),Level=69}}};
var repairPmc=new PmcData{Id=new MongoId(),Info=new(){Nickname="Repair Test"},Customization=new(){DogTag=collisionId},Inventory=new(){Items=[retainedTag],FavoriteItems=[collisionId],FastPanel=new(){["1"]=collisionId}}};
string tagPayload=ledgerJson.Serialize(retainedTag.Upd)!;var rebased=CloneDogtagRepair.Apply(repairPmc);
Check(rebased.Count==1&&retainedTag.Id!=collisionId&&repairPmc.Inventory.Items!.Count==1&&ledgerJson.Serialize(retainedTag.Upd)==tagPayload,"Legacy repair changes only the colliding item ID and keeps the surviving tag, FIR status, owner and level");
Check(repairPmc.Inventory.FastPanel!["1"]==retainedTag.Id&&repairPmc.Inventory.FavoriteItems!.Single()==retainedTag.Id&&repairPmc.Customization.DogTag==collisionId,"Legacy repair updates item references while leaving the selected custom appearance unchanged");
Check(CloneDogtagRepair.Apply(repairPmc).Count==0,"Legacy repair is idempotent across repeated profile fetches");
var unrelatedTag=new Item{Id=collisionId,Template=retainedTag.Template,Upd=new(){SpawnedInSession=true,Dogtag=new(){Nickname="Someone Else",ProfileId=new MongoId().ToString()}}};repairPmc.Inventory.Items.Add(unrelatedTag);
Check(CloneDogtagRepair.Apply(repairPmc).Count==0&&unrelatedTag.Id==collisionId,"Legacy repair does not modify unrelated dogtags");

var uniformStaff=giftService.CopyForResponse(giftOriginal,true);
Check(uniformStaff.Inventory!.Items!.Single(i=>i.SlotId=="TacticalVest").Template.ToString()==GiftService.StaffVest,"Every lab staff member receives the native Security vest with no weapons");
Check(uniformStaff.Info!.Settings!.Experience==0&&uniformStaff.Info.Settings.UseSimpleAnimator==false,"Staff have zero native kill XP and use the full native animator");
Check(giftService.StaffAvailable,"Medical clothing pools and native Security vest are available before Lab Work Day can run");

Check(!EventIntegration.Active&&EventIntegration.Select(new MongoId(),"woods")==null,"Events-only mode has no campaign override");
EventIntegration.Begin(new MongoId(),"woods","stormtide");EventIntegration.GunGameWin(new MongoId());
bool blockedWithoutCore=false;try{EventIntegration.Register(new TestCampaign());}catch(InvalidOperationException){blockedWithoutCore=true;}
Check(blockedWithoutCore,"Quest campaign cannot register before the core is ready");
EventIntegration.Ready=true;var testCampaign=new TestCampaign();EventIntegration.Register(testCampaign);
var splitSession=new MongoId();EventIntegration.Begin(splitSession,"woods","stormtide");EventIntegration.GunGameWin(splitSession);
Check(EventIntegration.Active&&EventIntegration.Select(splitSession,"woods")=="stormtide"&&testCampaign.Begins==1&&testCampaign.Wins==1,"Optional campaign receives selected raids and Gun Game wins through the core contract");
bool duplicateBlocked=false;try{EventIntegration.Register(new TestCampaign());}catch(InvalidOperationException){duplicateBlocked=true;}
Check(duplicateBlocked,"Conflicting campaign providers are rejected");
Check(RaidRoulette.QuestPack.CampaignSelection.Resolve(null,false)=="normal","Quest-only mode keeps non-quest raids normal");
Check(RaidRoulette.QuestPack.CampaignSelection.Resolve(null,true)==null,"Optional mixed mode allows random events between quests");
Check(RaidRoulette.QuestPack.CampaignSelection.Resolve("boss_hunt",true)=="boss_hunt"&&RaidRoulette.QuestPack.CampaignSelection.Resolve("boss_hunt",false)=="boss_hunt","Eligible quest event wins over the between-quest setting");

var chanceTestProfile=new RaidRoulette.Shared.RaidChanceProfile{Mode="IndividualPercentages",Percentages=new(){["normal"]=10,["clone_wars"]=10,["stormtide"]=80}};
var chanceAck=TestingControl.Update(new TestingRequest{Action="settings",Chances=chanceTestProfile});
Check(chanceAck.GetType().GetProperty("ok")!.GetValue(chanceAck) is true,"Backend accepts validated F12 chance settings");
var badChanceProfile=chanceTestProfile.Copy();badChanceProfile.Percentages["stormtide"]=90;
var rejectedChance=TestingControl.Update(new TestingRequest{Action="settings",Chances=badChanceProfile});
Check(rejectedChance.GetType().GetProperty("ok")!.GetValue(rejectedChance) is false&&TestingControl.Chances!.Percentages["stormtide"]==80,"Invalid percentages preserve the last accepted backend profile");
TestingControl.Update(new TestingRequest{Action="apply",Event="stormtide",Repeat=false,QueueId="once-test",AllowDisabled=true});
Check(TestingControl.Take()=="stormtide"&&TestingControl.Take()=="","Queued manual choice is consumed once and then returns to automatic selection");
TestingControl.Update(new TestingRequest{Action="apply",Event="stormtide",Repeat=false,QueueId="once-test",Chances=chanceTestProfile});
Check(TestingControl.Take()=="","Resending the same acknowledged queue cannot rearm a consumed selection");
TestingControl.Update(new TestingRequest{Action="apply",Event="clone_wars",Repeat=false,QueueId="new-once-test",AllowDisabled=true});
TestingControl.Update(new TestingRequest{Action="settings",Chances=chanceTestProfile});
Check(TestingControl.Take()=="clone_wars","Editing chance settings does not erase a pending one-shot choice");
Check(new SemanticVersioning.Range(">=2.0.0-preview.31 <2.1.0").IsSatisfied(new SemanticVersioning.Version("2.0.0-preview.31")),"Quest pack dependency range accepts the new compatible core preview");
sealed class TestCampaign:IEventCampaign
{
 public int Begins,Wins;
 public string? Select(MongoId session,string? map)=>"stormtide";
 public void Begin(MongoId session,string map,string id)=>Begins++;
 public void GunGameWin(MongoId session)=>Wins++;
}
