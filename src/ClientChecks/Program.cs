using Mono.Cecil;
using var questClient=AssemblyDefinition.ReadAssembly(args[3]);
var root = args[0];
using var game = AssemblyDefinition.ReadAssembly(root + "/EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll");
int checks = 0;
void Check(bool good, string name) { if (!good) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
foreach (var item in new (string Type,string Method,string Return,int Count)[] {
    ("BotWeaponManager","get_IsReady","System.Boolean",0),
    ("EFT.BaseStatisticsManager","BeginStatisticsSession","System.Void",0),
    ("EFT.BaseStatisticsManager","EndStatisticsSession","System.Void",2),
    ("EFT.BaseStatisticsManager","OnEnemyKill","System.Void",18),
    ("EFT.BaseStatisticsManager","OnEnemyDamage","System.Void",13),
    ("EFT.Player","OnMakingShot","System.Void",2),
    ("EFT.UI.SessionEnd.SessionResultExperienceCount","Show","System.Void",3),
    ("EFT.Profile","SetUpDogTagSlot","System.Void",2),
    ("EFT.Quests.TaskConditionCounter","set_Value","System.Void",1),
    ("EFT.Quests.ConditionCounterManager","LoadConditionCounters","System.Void",0),
    ("BotWeaponManager","Activate","System.Void",0),
    ("ShootData","Activate","System.Void",0),
    ("StandartBotBrain","Activate","System.Void",0),
    ("BotWeaponSelector","TakeMainWeapon","System.Void",0),
    ("AICoreController","RemoveNodeController","System.Void",1),
    ("EFT.Quests.ConditionCounterManager","Test","System.Void",3),
    ("EFT.Quests.ConditionsConnectorsManager`1","GetItemsForConditionIEnumerable","System.Collections.Generic.IEnumerable`1<EFT.InventoryLogic.Item>",2),
    ("EFT.BotMemory","set_GoalEnemy","System.Void",1),
    ("BotGroupEnemyInfo","set_EnemyLastPosition","System.Void",1),
    ("EFT.UI.SessionEnd.SessionResultExitStatus","Show","System.Void",7),
    ("EFT.Communications.NotificationManager","DisplayMessageNotification","System.Void",4),
    ("EFT.GameDateTime","ResetForce","System.Void",1),
    ("EFT.GameDateTime","Calculate","System.DateTime",0),
    ("BotGrenadeController","SetThrowParams","System.Void",1),
    ("BotGrenadeController","SetThrowData","System.Boolean",1),
    ("BotGrenadeController","DoThrow","System.Boolean",0),
    ("BotPathController","SetCurPath","System.Void",1),
    ("BotPathController","Stop","System.Void",0),
    ("BotItemTaker","HaveItemToTake","System.Boolean",0),
    ("BotItemTaker","Take","System.Void",1),
    ("BotItemTaker","PickupAction","System.Void",4),
    ("BotItemTaker","RefreshClosestItems","System.Void",0),
    ("BotMover","Stop","System.Void",0),
    ("BotMover","MovementPause","System.Void",2),
    ("BotMover","MovePlayer","System.Void",0),
    ("EFT.BotOwner","UpdateManual","System.Void",0),
    ("BotExfiltrationData","WannaLeave","System.Boolean",0),
    ("BotGiftData","HaveUngiftedPlayerNear","System.Boolean",0),
    ("BotGiftData","UpdateGiftTarget","System.Void",0),
    ("BotGiftData","CalcGiftsOnStart","System.Void",0),
    ("BotItemDropper","RefreshItemToDrop","System.Void",0),
    ("BotsGroup","IsEnemy","System.Boolean",1), ("BotsGroup","AddEnemy","System.Boolean",2),
    ("EnemyInfo","get_IsVisible","System.Boolean",0), ("EnemyInfo","get_CanShoot","System.Boolean",0),
    ("EFT.BotMemory","get_GoalEnemy","EnemyInfo",0), ("EFT.BotMemory","get_HaveEnemy","System.Boolean",0),
    ("EFT.Player","ApplyDamageInfo","System.Void",4), ("EFT.Player","ReceiveDamage","System.Void",5),
    ("EFT.MovementContext","get_SprintSpeed","System.Single",0),
    ("BossLocationSpawn","Init","System.Void",0),
    ("BossSpawnScenario","Stop","System.Void",0),
    ("PatrollingData","BossGoNextPoint","System.Void",0),
    ("PatrollingData","Unpause","System.Void",0),
    ("BotBossSpawn","SpawnBossMain","System.Threading.Tasks.Task",6),
    ("BossSpawnScenario","Run","System.Void",2),
    ("BossSpawnScenario","ActivateWave","System.Void",1),
    ("BossSpawnScenario","ActivateWavesAfterGameStarted","System.Void",1),
    ("BotBossSpawn","TrySpawn","System.Boolean",6),
    ("BotBossSpawn","SpawnBossAndFollowers","System.Threading.Tasks.Task",6),
    ("BotZonesLeaveController","IsZoneBlockFor","System.Boolean",2),
    ("BotZonesLeaveController","UnBlockZoneFor","System.Void",2) })
{
    var type = game.MainModule.Types.Single(t=>t.FullName==item.Type);
    Check(type.Methods.Count(m=>m.Name==item.Method && m.ReturnType.FullName==item.Return && m.Parameters.Count==item.Count)==1, "Game patch signature " + item.Type + "." + item.Method);
}
var stamina = game.MainModule.Types.Single(t=>t.Name=="Stamina");
foreach(var field in new[]{"Current","Overuse","TotalCapacity"}) Check(stamina.Fields.Any(f=>f.Name==field && f.IsPublic),"Public stamina field "+field);
var player = game.MainModule.Types.Single(t=>t.FullName=="EFT.Player");
Check(player.Fields.Any(f=>f.Name=="Physical" && f.IsPublic),"Player Physical API");
var update=game.MainModule.Types.Single(t=>t.FullName=="EFT.BotOwner").Methods.Single(m=>m.Name=="UpdateManual");
Check(update.Body.Instructions.Count(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="BotWeaponManager" && m.Name=="get_IsReady")==1,"Installed game has exactly one activation readiness call to replace");
Check(update.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="UnityEngine.AI.NavMesh" && m.Name=="SamplePosition"),"Native activation still owns the navigation check");
if(args.Length>1 && File.Exists(args[1]))
{
using var common = AssemblyDefinition.ReadAssembly(args[1]);
var handler=common.MainModule.Types.Single(t=>t.FullName=="SPT.Common.Http.RequestHandler");
Check(handler.Methods.Any(m=>m.Name=="PostJsonAsync" && m.IsStatic && m.IsPublic && m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(new[]{"System.String","System.String"}) && m.ReturnType.FullName=="System.Threading.Tasks.Task`1<System.String>"),"SPT launcher-session async HTTP API (reference 4.1.0)");
}
else Console.WriteLine("NOTE SPT.Common HTTP reference unavailable; unchanged reflection HTTP adapter was not rechecked.");
if(args.Length>2)
{
    using var client=AssemblyDefinition.ReadAssembly(args[2]);
    var activation=client.MainModule.Types.Single(x=>x.Name=="GiftUnarmedActivation");
    Check(activation.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch" && a.ConstructorArguments.Any(v=>v.Value?.ToString()=="UpdateManual")),"Activation patch targets UpdateManual rather than an inlineable getter");
    Check(activation.Methods.Any(m=>m.Name=="Transpiler")&&!activation.Methods.Any(m=>m.Name=="Postfix"),"Caller transpiler replaces the previous getter postfix");
    var readiness=activation.Methods.Single(m=>m.Name=="ReadyForActivation");
    Check(readiness.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Isinst && i.Operand is TypeReference t && t.Name=="EmptyHandsController"),"Readiness exception requires actual empty hands");
    Check(!readiness.Body.Instructions.Any(i=>i.Operand is MethodReference m && (m.Name=="set_IsReady"||m.Name=="set_BotState"||m.Name=="method_10")),"Readiness helper does not mutate weapon readiness or force bot activation");
    Check(!client.MainModule.AssemblyReferences.Any(x=>x.Name.StartsWith("Fika")),"Local client has no Fika assembly dependency");
    var choices=client.MainModule.Types.Single(x=>x.Name=="RaidChoice");
    Check(choices.Fields.Any(x=>x.Name=="Clone_Wars")&&choices.Fields.Any(x=>x.Name=="The_Gift"),"Compiled menu exposes Clone Wars and The Gift");
    Check(client.MainModule.Types.Any(x=>x.Name=="LiveRaidMonitor"),"Standalone monitor is compiled into the client");
    var huntDirector=client.MainModule.Types.Single(x=>x.Name=="BossHuntDirector");
    Check(huntDirector.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BotMover"&&m.Name=="GoToByWay"),"Boss Hunt pursues through the native path mover");
    Check(!huntDirector.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&(m.Name=="Teleport"||m.Name=="SetVisible"||m.Name=="set_timeScale")),"Boss Hunt does not teleport actors, fake visibility or alter simulation speed");
    foreach(var hook in new[]{"GiftBeforeMovement","GiftMovement"})
    {
        var patch=client.MainModule.Types.Single(t=>t.Name==hook);
        Check(patch.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BossHuntDirector"&&m.Name=="Drive"),"Hunter movement is refreshed by native "+hook+" hook");
    }
    Check(huntDirector.Methods.Single(m=>m.Name=="Drive").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="MovementResume"),"Hunter movement refresh clears native idle pauses before moving");
    var psych=client.MainModule.Types.Single(t=>t.Name=="SchizophreniaDirector");
    IEnumerable<TypeDefinition> PsychTypes(TypeDefinition type)=>new[]{type}.Concat(type.NestedTypes.SelectMany(PsychTypes));
    var psychCalls=PsychTypes(psych).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
    Check(choices.Fields.Any(f=>f.Name=="Schizophrenia"),"Schizophrenia is selectable in the client menu");
    Check(psychCalls.Any(m=>m.Name=="set_bones")&&!psychCalls.Any(m=>m.Name=="BakeMesh")&&psychCalls.Any(m=>m.Name=="GetRenderersNonAlloc"),"Hallucination figures borrow native rendered geometry rather than copying player objects");
    Check(!psychCalls.Any(m=>m.Name is "Instantiate" or "ThrowItem" or "CreateItem" or "CreatePlayer" or "ApplyDamageInfo" or "ApplyShot" or "CreateGrenade"),"Hallucination director does not clone actors, create inventory items or apply damage");
    var addedComponents=psychCalls.OfType<GenericInstanceMethod>().Where(m=>m.Name=="AddComponent").SelectMany(m=>m.GenericArguments).Select(t=>t.FullName).ToArray();
    Check(addedComponents.Length>0&&addedComponents.All(t=>t is "UnityEngine.MeshFilter" or "UnityEngine.MeshRenderer" or "UnityEngine.SkinnedMeshRenderer" or "UnityEngine.AudioSource"),"Hallucination objects contain only mesh rendering and audio components, without collision or AI");
    Check(psychCalls.Any(m=>m.DeclaringType.Name=="Effects"&&m.Name=="Emit")&&psychCalls.Any(m=>m.DeclaringType.Name=="Effects"&&m.Name=="EmitGrenade"),"Phantom impacts and grenade bursts use native visual effects APIs");
    Check(psychCalls.Any(m=>m.Name=="GetGrenadePinSound")&&psychCalls.Any(m=>m.Name=="GetFuzeSound"),"Tripwire hallucinations use native pin and fuze sound assets");
    Check(!psych.Methods.Single(m=>m.Name=="TickCore").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name.StartsWith("FindObject")),"Hallucination frame tick no longer performs periodic scene-wide asset discovery");
    Check(psychCalls.Any(m=>m.DeclaringType.Name=="StillnessScare"&&m.Name=="Tick")&&psych.Methods.Any(m=>m.Name=="StationaryGrenade"),"Stationary-player scare is wired to audio-only grenade cues");
    Check(psych.Methods.Single(m=>m.Name=="Stop").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Destroy"),"Hallucination director destroys owned visuals and audio during cleanup");
    var allowedRefs=new[]{"netstandard","Assembly-CSharp","UnityEngine.CoreModule","UnityEngine.AIModule","0Harmony","BepInEx","UnityEngine.IMGUIModule","Newtonsoft.Json","UnityEngine.AudioModule","UnityEngine.PhysicsModule","Comfort","AnimationSystem.Types","ItemComponent.Types"};
    Check(client.MainModule.AssemblyReferences.All(r=>allowedRefs.Contains(r.Name)),"Client assembly references only audited Tarkov, Unity and base SPT/BepInEx libraries");
    var gunGame=client.MainModule.Types.Single(t=>t.Name=="GunGameDirector");
    var gunCalls=PsychTypes(gunGame).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
    Check(new[]{"Gun_Game","Stormtide","AK50","Grenadier"}.All(name=>choices.Fields.Any(f=>f.Name==name)),"All four new events are selectable in the compiled client");
    Check(gunCalls.Any(m=>m.Name=="FlatItemsToTree")&&gunCalls.Any(m=>m.Name=="RemoveItemWithoutRestrictions")&&gunCalls.Any(m=>m.Name=="Proceed"&&m.Parameters.Count>0&&m.Parameters[0].ParameterType.Name=="Weapon"),"Gun Game builds native weapon trees and swaps inventory/hands through native APIs");
    Check(gunCalls.Any(m=>m.Name=="LoadBundlesAndCreatePools"),"Weapon-swap events preload native model bundles before changing hands");
    Check(gunCalls.Any(m=>m.Name=="SetFireMode"),"Automatically equipped weapons select full auto when supported");
    Check(gunCalls.Any(m=>m.Name=="PlayUISound")&&gunCalls.Any(m=>m.Name=="DisplayMessageNotification"),"Gun Game uses native achievement audio and bottom notification APIs");
    var respawn=client.MainModule.Types.Single(t=>t.Name=="GunGameRespawn");
    var respawnCalls=PsychTypes(respawn).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
    Check(respawnCalls.Any(m=>m.Name=="FullRestoreBodyPart")&&respawnCalls.Any(m=>m.Name=="RemoveNegativeEffects")&&respawnCalls.Any(m=>m.Name=="ChangeHydration"),"Respawn restores native body parts, removes negative effects and replenishes hydration");
    Check(respawnCalls.Any(m=>m.Name=="SamplePosition")&&respawnCalls.Any(m=>m.Name=="CheckCapsule")&&respawnCalls.Any(m=>m.Name=="Teleport"),"Respawn placement uses native points with NavMesh and collision checks before teleporting");
    Check(respawnCalls.Any(m=>m.Name=="RestartAfterDeath")&&respawnCalls.Any(m=>m.Name=="get_LocalWeaponReady"),"Respawn resets the saved attempt and waits for a loaded pistol before releasing controls");
    Check(!respawnCalls.Any(m=>m.DeclaringType.Name=="GameTimer"&&m.Name.StartsWith("set_"))&&!respawnCalls.Any(m=>m.DeclaringType.FullName=="UnityEngine.Time"&&m.Name=="set_timeScale"),"Respawning never resets the raid timer or pauses the AI simulation");
    var killPrefix=client.MainModule.Types.Single(t=>t.Name=="GunGameLethalRespawn").Methods.Single(m=>m.Name=="Prefix");
    Check(killPrefix.ReturnType.FullName=="System.Boolean"&&killPrefix.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Intercept"),"Respawn intercepts the lethal health event before native death teardown");
    var clockEffect=client.MainModule.Types.Single(t=>t.Name=="SchizophreniaClock");
    var clockCalls=clockEffect.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
    Check(clockCalls.Any(m=>m.Name=="ResetForce")&&clockCalls.All(m=>!(m.DeclaringType.Name=="GameTimer"&&m.Name.StartsWith("set_")))&&clockEffect.Fields.Any(f=>f.Name=="consumed"),"Once-per-raid Schizophrenia time jump owns only the world clock, not the countdown");
    Check(new[]{"TimeFactor","TimeFactorMod"}.All(name=>clockEffect.Methods.Single(m=>m.Name=="Stop").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name==name||i.Operand is MethodReference m&&m.Name=="set_"+name)),"Time-jump cleanup restores both original world-clock speed factors");
    Check(client.MainModule.Types.Any(t=>t.Name=="StormtideNativeAudio")&&client.MainModule.Types.Single(t=>t.Name=="StormtideDirector").Methods.Any(m=>m.Name=="Stop"),"Stormtide managed-audio attenuation and restoration are compiled");
    var thunder=client.MainModule.Types.Single(t=>t.Name=="StormtideThunder");
    var thunderCalls=PsychTypes(thunder).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
    var assets=client.MainModule.Types.Single(t=>t.Name=="StormAudioAssets");
    var recordingNames=assets.Methods.Single(m=>m.IsConstructor).Body.Instructions.Where(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr).Select(i=>(string)i.Operand).ToArray();
    Check(recordingNames.SequenceEqual(new[]{"Thunder1.wav","ThunderReplacement.wav","HeavyRain.wav"}),"Compiled audio pool contains only Thunder 1, the replacement recording and heavy rain");
    Check(thunderCalls.Any(m=>m.DeclaringType.Name=="ThunderSchedule"&&m.Name=="Tick"),"Automatic strikes use the raid-elapsed probabilistic schedule");
    var assetCalls=PsychTypes(assets).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
    Check(assetCalls.Any(m=>m.DeclaringType.Name=="AudioClip"&&m.Name=="Create")&&assetCalls.Any(m=>m.DeclaringType.Name=="Task"&&m.Name=="Run")&&assetCalls.Any(m=>m.DeclaringType.Name=="WavePcm"&&m.Name=="Read"),"Stormtide decodes bundled recordings off the main thread and uploads owned Unity clips");
    Check(thunderCalls.Any(m=>m.Name=="set_shadows")&&!thunderCalls.Any(m=>m.Name is "ApplyDamageInfo" or "ApplyShot" or "CreateGrenade"),"Lightning has no shadow rendering or gameplay damage path");
    Check(thunder.Methods.Single(m=>m.Name=="Stop").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Destroy")&&assets.Methods.Single(m=>m.Name=="Dispose").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Destroy"),"Thunder cleanup removes its source/light and shared assets release recording clips on shutdown");
    Check(!client.MainModule.Types.Any(t=>t.Name=="ThunderSound"),"Recorded thunder fully replaces the previous synthetic sound generator");
    Check(PsychTypes(psych).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string message&&message=="Did you hear that?"),"Schizophrenia includes the requested once-per-raid whisper notification");
    var stormTargets=client.MainModule.Types.Single(t=>t.Name=="StormtideNativeAudio").Methods.Single(m=>m.Name=="TargetMethods");
    Check(!stormTargets.Body.Instructions.Any(i=>i.Operand is string name&&name is "Play" or "PlayScheduled")&&!stormTargets.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="GetTypes"),"Storm audio targets an explicit volume-method allowlist, never playback stubs or a broad type scan");
    foreach(string sourceName in new[]{"BetterSource","SuperSourceDistant"})
    {
        var nativeVolume=game.MainModule.Types.Single(t=>t.Name==sourceName).Methods.Single(m=>m.Name=="UpdateSourceVolume");
        Check(nativeVolume.HasBody&&nativeVolume.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="set_volume"),sourceName+" volume target has a concrete native volume-update body");
    }
    var startup=client.MainModule.Types.Single(t=>t.Name=="Plugin").Methods.Single(m=>m.Name=="Awake");
    Check(startup.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CreateClassProcessor")&&!startup.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="PatchAll"),"Client startup isolates Harmony patch classes instead of rolling back the entire client");
    Check(startup.Body.ExceptionHandlers.Count>=2,"Patch initialization and failed-patch cleanup have independent exception guards");
    Check(gunCalls.Any(m=>m.Name=="UpdateHandsController")&&gunCalls.Any(m=>m.Name=="set_IsWeaponReady"),"Gun Game refreshes AI weapon/controller readiness after the actual native hands callback");
    Check(gunCalls.Any(m=>m.Name=="FastForwardCurrentState")&&gunCalls.Any(m=>m.Name=="SetAnimatorValues"),"Newly drawn firearms finish their spawn state and synchronize chamber/magazine animator values");
    var capture=client.MainModule.Types.Single(t=>t.Name=="GunGameDamageCapture");
    Check(capture.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.Name=="ObserveDamage"),"Gun Game captures direct health damage before rocket explosion deaths notify listeners");
    var health=game.MainModule.Types.Single(t=>t.FullName=="EFT.HealthSystem.ActiveHealthController");
    Check(health.Methods.Any(m=>m.Name=="ApplyDamage"&&m.Parameters.Count==3&&m.Parameters[2].ParameterType.FullName=="EFT.Ballistics.DamageInfo"),"Installed health controller exposes the exact three-argument damage capture target");
    var localDirector=client.MainModule.Types.Single(x=>x.Name=="LocalRaidDirector");
    Check(localDirector.Methods.Single(m=>m.Name=="get_NativePoints").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.DeclaringType.FullName=="EFT.Game.Spawning.SpawnSystem"&&f.Name=="_spawnPoints"),"Rooftop lookup reads the global native spawn collection rather than the PMC-only zone list");
    var warp=client.MainModule.Types.Single(x=>x.Name=="TimeWarpDirector");
    Check(choices.Fields.Any(x=>x.Name=="Time_Warp"),"Time Warp is selectable in the compiled menu");
    Check(warp.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).All(i=>i.Operand is not MethodReference m||!(m.DeclaringType.FullName=="UnityEngine.Time"&&m.Name=="set_timeScale")&&!(m.DeclaringType.FullName=="EFT.GameTimer"&&m.Name.StartsWith("set_"))),"Time Warp never changes simulation timeScale or raid timer properties");
    Check(warp.Methods.Single(m=>m.Name=="Stop").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.FullName=="EFT.GameDateTime"&&m.Name=="ResetForce"),"Time Warp restoration rebases the original clock timeline");
    Check(choices.Fields.Any(x=>x.Name=="Smokers_Lobby")&&client.MainModule.Types.Any(x=>x.Name=="SmokersDirector"),"Smokers Lobby menu and native throw director are compiled into client");
    Check(client.MainModule.Types.Any(x=>x.Name=="GiftRouteOwnership")&&client.MainModule.Types.Any(x=>x.Name=="GiftNoPickup"),"Route ownership and pickup guards are compiled into the client");
    var plugin=client.MainModule.Types.Single(x=>x.Name=="Plugin");
    Check(plugin.Methods.Single(x=>x.Name=="OnGUI").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="LiveRaidMonitor"&&m.Name=="Draw"),"Plugin OnGUI draws the standalone monitor independently of F12");
    Check(client.MainModule.Types.Any(x=>x.Name=="RaidRoster"),"Compiled F12 roster is included");
    Check(choices.Fields.Any(x=>x.Name=="Overrun"),"Compiled menu exposes Overrun");
    Check(!client.MainModule.Types.Any(x=>x.Name.Contains("Admin")),"Compiled local client contains no admin controller");
    Check(client.MainModule.Types.Any(x=>x.Name=="CultistZoneFix")&&client.MainModule.Types.Any(x=>x.Name=="DamagePatch"),"Cultist fix and Ghost Review retained");
}
if(args.Length>2)
{
 using var renamed=AssemblyDefinition.ReadAssembly(args[2]);
 Check(renamed.Name.Name=="RaidRoulette.Testing","Client assembly uses the Raid Roulette name");
 var plugin=renamed.MainModule.Types.Single(t=>t.Name=="Plugin");
 var identity=plugin.CustomAttributes.Single(a=>a.AttributeType.Name=="BepInPlugin");
 Check(identity.ConstructorArguments[0].Value.ToString()=="com.killdg.raidroulette.testing"&&identity.ConstructorArguments[1].Value.ToString()=="Raid Roulette","BepInEx menu and configuration identity use Raid Roulette");
 var director=renamed.MainModule.Types.Single(t=>t.Name=="GunGameDirector");
 var ladder=director.Methods.Single(m=>m.Name==".cctor").Body.Instructions.Where(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr).Select(i=>(string)i.Operand).Take(9);
 Check(ladder.SequenceEqual(new[]{"pistol","shotgun","smg","sniperRifle","ak50","assaultRifle","machinegun","rocket","grenadeLauncher"}),"Compiled client ladder matches the server order with SMG third, AK50 fifth and grenade launcher last");
 Check(!director.Methods.Any(m=>m.Name is "SupplyImpacts" or "ThrowImpact"),"Gun Game no longer creates or throws impact grenades");
 var classifier=director.Methods.Single(m=>m.Name=="Kind");
 Check(classifier.Body.Instructions.Any(i=>i.Operand is string s&&s=="67d0576f29f580ebc10efd08")&&classifier.Body.Instructions.Any(i=>i.Operand is string s&&s=="ak50"),"Gun Game classifies the native AK50 separately from sniper rifles");
}
if(args.Length>2)
{
 using var built=AssemblyDefinition.ReadAssembly(args[2]);
 Check(built.MainModule.Types.Single(t=>t.Name=="RaidChoice").Fields.Any(f=>f.Name=="Lab_Work_Day"),"Lab Work Day is selectable in the compiled F12 menu");
 var staffMovement=built.MainModule.Types.Single(t=>t.Name=="LabStaffMovement").Methods.Single(m=>m.Name=="Prefix");
 Check(staffMovement.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="ManualUpdate"&&m.DeclaringType.Name=="BotMover")&&!staffMovement.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BotWeaponManager"),"Unarmed staff use native movement without invoking native gun reload updates");
 var neutralBrain=built.MainModule.Types.Single(t=>t.Name=="LabStaffPeacefulBrain").Methods.Single(m=>m.Name=="Postfix");
 Check(neutralBrain.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="RemoveNodeController"),"Staff combat decision scheduler is detached after native brain initialization");
 var questGate=questClient.MainModule.Types.Single(t=>t.Name=="RaidQuestCounterGate");
 Check(questGate.Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="OwnCounter"),"Native generic kill tests are gated for the custom event quest counters");
 Check(questClient.MainModule.Types.Any(t=>t.Name=="RaidQuestItemFilter")&&questClient.MainModule.Types.Any(t=>t.FullName=="RaidRoulette.Shared.QuestIds"),"Client includes native handover filtering and the shared stable quest IDs");
 var lab=built.MainModule.Types.Single(t=>t.Name=="LabWorkDirector");
 Check(lab.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="ActivateWave"),"Third staff kill uses the native reinforcement scenario for its Raider group");
}
if(args.Length>2)
{
 using var chainClient=AssemblyDefinition.ReadAssembly(args[3]);
 var entries=chainClient.MainModule.Types.Single(t=>t.FullName=="RaidRoulette.Shared.QuestIds").Methods.Single(m=>m.Name==".cctor").Body.Instructions.Where(i=>i.Operand is string).Select(i=>(string)i.Operand).Where(s=>new[]{"stormtide","smokers_lobby","the_gift","time_warp","schizophrenia","clone_wars","ak50","boss_hunt","cultist_invasion","gun_game"}.Contains(s));
 Check(entries.First()=="stormtide"&&entries.Last()=="gun_game"&&entries.Count()==10,"Compiled client quest order starts with Stormtide and ends with Gun Game");
 var accepted=chainClient.MainModule.Types.Single(t=>t.Name=="TraderQuestDirector").Methods.Single(m=>m.Name=="Accepted");
 Check(accepted.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Take")&&accepted.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="All"),"Client event credit checks every preceding quest before advancing an accepted quest");
}
if(args.Length>2)
{
 using var tagsClient=AssemblyDefinition.ReadAssembly(args[2]);
 var guard=tagsClient.MainModule.Types.Single(t=>t.Name=="CloneDogtagIdentity").Methods.Single(m=>m.Name=="Prefix");
 Check(guard.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="CloneTagIdentity"&&m.Name=="IsClone")&&!guard.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="get_RaidContext"),"Clone tag protection uses server identity markers and works before raid-context polling updates");
 var nativeCustomTag=game.MainModule.Types.Single(t=>t.FullName=="EFT.Customization.CustomizationDogTag").Methods.Single(m=>m.Name=="GetItemClone");
 Check(nativeCustomTag.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CloneItemWithSameId"),"Installed native custom-tag creation retains the shared appearance ID that causes clone collisions");
 Check(questClient.MainModule.Types.Any(t=>t.Name=="CloneDogtagHandoverProgress")&&questClient.MainModule.Types.Any(t=>t.Name=="CloneDogtagCounterAttach"),"Verified tag hand-ins synchronize the native recovery counter both live and after profile reload");
}
if(args.Length>2)
{
 using var staffClient=AssemblyDefinition.ReadAssembly(args[2]);
 var kill=staffClient.MainModule.Types.Single(t=>t.Name=="LabStaffXpKill").Methods.Single(m=>m.Name=="Prefix");
 Check(kill.ReturnType.FullName=="System.Boolean"&&staffClient.MainModule.Types.Any(t=>t.Name=="LabStaffXpDamage"),"Staff kills and damage bypass native combat XP, boss statistics and combat-quest credit");
 var finish=staffClient.MainModule.Types.Single(t=>t.Name=="LabStaffExperience").Methods.Single(m=>m.Name=="Apply");
 Check(finish.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Award"&&m.DeclaringType.Name=="LabStaffPolicy"),"Civilian XP adjustment computes a nonnegative net award before native level changes");
 var nativeEnd=game.MainModule.Types.Single(t=>t.FullName=="EFT.BaseStatisticsManager").Methods.Single(m=>m.Name=="EndStatisticsSession"&&m.Parameters.Count==2);
 Check(nativeEnd.Body.Instructions.Count(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld&&i.Operand is FieldReference f&&f.Name=="TotalSessionExperience")==1,"Installed game has exactly one final XP store for the civilian penalty hook");
 var register=staffClient.MainModule.Types.Single(t=>t.Name=="LabStaffBehaviour").Methods.Single(m=>m.Name=="Register");
 Check(register.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="PlayerAnimatorSetWeaponId")&&register.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="SetLayerWeight"),"Staff explicitly use empty-hands animation and remove the third-person weapon overlay");
 var flee=staffClient.MainModule.Types.Single(t=>t.Name=="LabStaffBehaviour").Methods.Single(m=>m.Name=="Drive");
 Check(flee.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CalculatePath")&&flee.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="GoToByWay"),"Fleeing uses complete native navigation paths and mover corners");
 Check(staffClient.MainModule.Types.Any(t=>t.Name=="LabStaffGunfire")&&staffClient.MainModule.Types.Any(t=>t.Name=="LabStaffThreatDamage"),"Gunfire and attacks both feed the civilian danger response");
}

if(args.Length>5)
{
 using var coreServer=AssemblyDefinition.ReadAssembly(args[4]);
 using var questServer=AssemblyDefinition.ReadAssembly(args[5]);
 using var coreClient=AssemblyDefinition.ReadAssembly(args[2]);
 Check(!coreServer.MainModule.Types.Any(t=>t.Name.Contains("TraderQuest")||t.Name=="QuestItemsRouter")&&!coreServer.MainModule.Resources.Any(r=>r.Name.EndsWith("quests.json")),"Standalone event backend contains no quest definitions, quest routes or quest lifecycle patches");
 Check(!coreClient.MainModule.Types.Any(t=>t.Name.Contains("TraderQuest")||t.Name=="QuestIds"||t.Name.StartsWith("RaidQuest")||t.Name=="CloneDogtagHandoverProgress"),"Standalone event client contains no quest polling, counters or hand-in patches");
 Check(!coreServer.MainModule.AssemblyReferences.Any(r=>r.Name.StartsWith("RaidRoulette.Quests"))&&!coreClient.MainModule.AssemblyReferences.Any(r=>r.Name.StartsWith("RaidRoulette.Quests")),"Core assemblies do not depend on the optional quest pack");
 Check(questServer.MainModule.AssemblyReferences.Any(r=>r.Name=="RaidRoulette")&&questClient.MainModule.AssemblyReferences.Any(r=>r.Name=="RaidRoulette.Testing"),"Quest modules reference the shared event engine rather than duplicating it");
 Check(questServer.MainModule.Resources.Count(r=>r.Name=="RaidRoulette.Quests.quests.json")==1,"Only the optional pack embeds the stable ten-quest catalog");
 Check(!questClient.MainModule.Types.Any(t=>t.Name is "StormtideDirector" or "GunGameDirector" or "LabStaffBehaviour"),"Quest client does not duplicate event effects or AI directors");
 var dependency=questClient.MainModule.Types.Single(t=>t.Name=="QuestPackPlugin").CustomAttributes.Single(a=>a.AttributeType.Name=="BepInDependency");
 Check(dependency.ConstructorArguments[0].Value.ToString()=="com.killdg.raidroulette.testing","Quest client declares a hard BepInEx dependency on Raid Roulette");
 Check(coreClient.MainModule.Types.Any(t=>t.Name=="CloneDogtagIdentity")&&coreServer.MainModule.Types.Any(t=>t.Name=="CloneProfileMaintenancePatch"),"Clone identity and legacy repair remain available without quests");
}

using(var menuClient=AssemblyDefinition.ReadAssembly(args[2]))
{
 var menuTypes=menuClient.MainModule.Types;
 var menu=menuTypes.Single(t=>t.Name=="AdvancedMenu");
 var monitor=menuTypes.Single(t=>t.Name=="LiveRaidMonitor");
 Check(monitor.Methods.Single(m=>m.Name=="Draw").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="AdvancedMenu"&&m.Name=="get_Enabled"),"Live monitor rendering requires Advanced to be enabled");
 Check(monitor.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="AdvancedMenu"&&m.Name=="get_Enabled"),"F8 shortcut cannot expose the monitor outside Advanced");
 Check(menuTypes.Single(t=>t.Name=="RaidRoster").Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="AdvancedMenu"&&m.Name=="get_Enabled"),"Live AI roster scanning is disabled outside Advanced");
 Check(menu.Methods.Single(m=>m.Name=="Bind"&&m.Parameters.Last().ParameterType.Name=="ConfigDescription").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="Browsable"),"Advanced settings explicitly control Configuration Manager visibility");
 var plugin=menuTypes.Single(t=>t.Name=="Plugin");
 Check(plugin.Methods.Single(m=>m.Name=="Send").Body.Instructions.Any(i=>i.Operand is string text&&text=="chances")&&menuTypes.Any(t=>t.Name=="RaidChanceSettings"),"F12 chance controls send their profile to backend selection");
 Check(plugin.Methods.Single(m=>m.Name=="Awake").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="MenuAction"&&m.Name=="Bind"),"Manual next-raid choice uses a momentary action rather than a repeat toggle");
}
Console.WriteLine("TOTAL PASS "+checks+" (metadata compatibility; not an in-game test)");








