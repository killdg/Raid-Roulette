using Mono.Cecil;

using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(args[0] + "/BepInEx/core");
resolver.AddSearchDirectory(args[0] + "/EscapeFromTarkov_Data/Managed");
AssemblyDefinition ReadAssembly(string path)
{
    return AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
}

using var questClient = ReadAssembly(args[3]);
var root = args[0];
using var game = ReadAssembly(root + "/EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll");
int checks = 0;
void Check(bool good, string name)
{
    if (!good)
    {
        throw new Exception(name);
    }

    checks++;
    Console.WriteLine("PASS " + name);
}

foreach (var item in new (string Type, string Method, string Return, int Count)[]
{
    ("RainController", "SetupSummer", "System.Void", 0),
    ("RainController", "SetupSpring", "System.Void", 0),
    ("RainController", "SetupAutumn", "System.Void", 0),
    ("RainController", "SetupAutumnLate", "System.Void", 0),
    ("RainController", "OnDestroy", "System.Void", 0),
    ("EFT.BotSpawner", "CanSpawnRole", "System.Boolean", 1),
    ("EFT.BotSpawner", "SpawnBotsInZoneOnPositions", "System.Void", 4),
    ("EFT.BotSpawner", "method_10", "System.Void", 4),
    ("BotWeaponManager", "get_IsReady", "System.Boolean", 0),
    ("EFT.Player", "Teleport", "System.Void", 2),
    ("EFT.Player", "Move", "System.Void", 1),
    ("EFT.Interactive.WorldInteractiveObject", "GetRotationAxis", "UnityEngine.Vector3", 2),
    ("EFT.InteractionContextHelper", "GetAvailableInteractionState", "EFT.UI.AvailableInteractionState", 6),
    ("EFT.InteractionContextHelper", "smethod_10", "System.Void", 4),
    ("EFT.Player", "Say", "System.Void", 6),
    ("EFT.Player", "OnPhraseTold", "System.Void", 4),
    ("EFT.Interactive.Door", "KickOpen", "System.Void", 2),
    ("EFT.GameWorld", "WorldInteractiveObjects", "System.Collections.Generic.IEnumerable`1<EFT.Interactive.WorldInteractiveObject>", 0),
    ("EFT.BaseStatisticsManager", "BeginStatisticsSession", "System.Void", 0),
    ("EFT.BaseStatisticsManager", "EndStatisticsSession", "System.Void", 2),
    ("EFT.BaseStatisticsManager", "OnEnemyKill", "System.Void", 18),
    ("EFT.BaseStatisticsManager", "OnEnemyDamage", "System.Void", 13),
    ("EFT.Player", "OnMakingShot", "System.Void", 2),
    ("EFT.UI.SessionEnd.SessionResultExperienceCount", "Show", "System.Void", 3),
    ("EFT.Profile", "SetUpDogTagSlot", "System.Void", 2),
    ("EFT.Quests.TaskConditionCounter", "set_Value", "System.Void", 1),
    ("EFT.Quests.ConditionCounterManager", "LoadConditionCounters", "System.Void", 0),
    ("BotWeaponManager", "Activate", "System.Void", 0),
    ("ShootData", "Activate", "System.Void", 0),
    ("StandartBotBrain", "Activate", "System.Void", 0),
    ("BotWeaponSelector", "TakeMainWeapon", "System.Void", 0),
    ("AICoreController", "RemoveNodeController", "System.Void", 1),
    ("EFT.Quests.ConditionCounterManager", "Test", "System.Void", 3),
    ("EFT.Quests.ConditionsConnectorsManager`1", "GetItemsForConditionIEnumerable",
         "System.Collections.Generic.IEnumerable`1<EFT.InventoryLogic.Item>", 2),
    ("EFT.BotMemory", "set_GoalEnemy", "System.Void", 1),
    ("BotGroupEnemyInfo", "set_EnemyLastPosition", "System.Void", 1),
    ("EFT.UI.SessionEnd.SessionResultExitStatus", "Show", "System.Void", 7),
    ("EFT.Communications.NotificationManager", "DisplayMessageNotification", "System.Void", 4),
    ("EFT.GameDateTime", "ResetForce", "System.Void", 1),
    ("EFT.GameDateTime", "Calculate", "System.DateTime", 0),
    ("BotGrenadeController", "SetThrowParams", "System.Void", 1),
    ("BotGrenadeController", "SetThrowData", "System.Boolean", 1),
    ("BotGrenadeController", "DoThrow", "System.Boolean", 0),
    ("BotPathController", "SetCurPath", "System.Void", 1),
    ("BotPathController", "Stop", "System.Void", 0),
    ("BotItemTaker", "HaveItemToTake", "System.Boolean", 0),
    ("BotItemTaker", "Take", "System.Void", 1),
    ("BotItemTaker", "PickupAction", "System.Void", 4),
    ("BotItemTaker", "RefreshClosestItems", "System.Void", 0),
    ("BotMover", "Stop", "System.Void", 0),
    ("BotMover", "MovementPause", "System.Void", 2),
    ("BotMover", "MovePlayer", "System.Void", 0),
    ("EFT.BotOwner", "UpdateManual", "System.Void", 0),
    ("BotExfiltrationData", "WannaLeave", "System.Boolean", 0),
    ("BotGiftData", "HaveUngiftedPlayerNear", "System.Boolean", 0),
    ("BotGiftData", "UpdateGiftTarget", "System.Void", 0),
    ("BotGiftData", "CalcGiftsOnStart", "System.Void", 0),
    ("BotItemDropper", "RefreshItemToDrop", "System.Void", 0),
    ("BotsGroup", "IsEnemy", "System.Boolean", 1),
    ("BotsGroup", "AddEnemy", "System.Boolean", 2),
    ("EnemyInfo", "get_IsVisible", "System.Boolean", 0),
    ("EnemyInfo", "get_CanShoot", "System.Boolean", 0),
    ("EFT.BotMemory", "get_GoalEnemy", "EnemyInfo", 0),
    ("EFT.BotMemory", "get_HaveEnemy", "System.Boolean", 0),
    ("EFT.Player", "ApplyDamageInfo", "System.Void", 4),
    ("EFT.Player", "ReceiveDamage", "System.Void", 5),
    ("EFT.MovementContext", "get_SprintSpeed", "System.Single", 0),
    ("BossLocationSpawn", "Init", "System.Void", 0),
    ("BossSpawnScenario", "Stop", "System.Void", 0),
    ("PatrollingData", "BossGoNextPoint", "System.Void", 0),
    ("PatrollingData", "Unpause", "System.Void", 0),
    ("BotBossSpawn", "SpawnBossMain", "System.Threading.Tasks.Task", 6),
    ("BossSpawnScenario", "Run", "System.Void", 2),
    ("BossSpawnScenario", "ActivateWave", "System.Void", 1),
    ("BossSpawnScenario", "ActivateWavesAfterGameStarted", "System.Void", 1),
    ("BotBossSpawn", "TrySpawn", "System.Boolean", 6),
    ("BotBossSpawn", "SpawnBossAndFollowers", "System.Threading.Tasks.Task", 6),
    ("BotZonesLeaveController", "IsZoneBlockFor", "System.Boolean", 2),
    ("BotZonesLeaveController", "UnBlockZoneFor", "System.Void", 2)
}

)
{
    var type = game.MainModule.Types.Single(t => t.FullName == item.Type);
    Check(
        type.Methods.Count(m => m.Name == item.Method && m.ReturnType.FullName == item.Return && m.Parameters.Count == item.Count) == 1,
        "Game patch signature " + item.Type + "." + item.Method
    );
}

var stamina = game.MainModule.Types.Single(t => t.Name == "Stamina");
foreach (var field in new[]
{
    "Current",
    "Overuse",
    "TotalCapacity"
}

)
{
    Check(stamina.Fields.Any(f => f.Name == field && f.IsPublic), "Public stamina field " + field);
}

var player = game.MainModule.Types.Single(t => t.FullName == "EFT.Player");
Check(player.Fields.Any(f => f.Name == "Physical" && f.IsPublic), "Player Physical API");
var update = game.MainModule.Types.Single(t => t.FullName == "EFT.BotOwner").Methods.Single(m => m.Name == "UpdateManual");
Check(
    update.Body.Instructions.Count(i => i.Operand is MethodReference m && m.DeclaringType.Name == "BotWeaponManager"
        && m.Name == "get_IsReady") == 1,
    "Installed game has exactly one activation readiness call to replace"
);
Check(
    update.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType
        .FullName == "UnityEngine.AI.NavMesh" && m.Name == "SamplePosition"),
    "Native activation still owns the navigation check"
);
if (args.Length > 1 && File.Exists(args[1]))
{
    using var common = ReadAssembly(args[1]);
    var handler = common.MainModule.Types.Single(t => t.FullName == "SPT.Common.Http.RequestHandler");
    Check(
        handler.Methods.Any(m => m.Name == "PostJsonAsync" && m.IsStatic && m.IsPublic && m.Parameters.Select(p => p
            .ParameterType.FullName).SequenceEqual(new[] { "System.String", "System.String" }) && m.ReturnType
            .FullName == "System.Threading.Tasks.Task`1<System.String>"),
        "SPT launcher-session async HTTP API (reference 4.1.0)"
    );
}
else
{
    Console.WriteLine("NOTE SPT.Common HTTP reference unavailable; unchanged reflection HTTP adapter was not rechecked.");
}

if (args.Length > 2)
{
    using var client = ReadAssembly(args[2]);
    var activation = client.MainModule.Types.Single(x => x.Name == "GiftUnarmedActivation");
    Check(
        activation.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatch" && a.ConstructorArguments.Any(v => v
            .Value?.ToString() == "UpdateManual")),
        "Activation patch targets UpdateManual rather than an inlineable getter"
    );
    Check(
        activation.Methods.Any(m => m.Name == "Transpiler") && !activation.Methods.Any(m => m.Name == "Postfix"),
        "Caller transpiler replaces the previous getter postfix"
    );
    var readiness = activation.Methods.Single(m => m.Name == "ReadyForActivation");
    Check(
        readiness.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Isinst && i.Operand is TypeReference t
            && t.Name == "EmptyHandsController"),
        "Readiness exception requires actual empty hands"
    );
    Check(
        !readiness.Body.Instructions.Any(i => i.Operand is MethodReference m && (m.Name == "set_IsReady" || m
            .Name == "set_BotState" || m.Name == "method_10")),
        "Readiness helper does not mutate weapon readiness or force bot activation"
    );
    Check(
        !client.MainModule.AssemblyReferences.Any(x => x.Name.StartsWith("Fika")),
        "Local client has no Fika assembly dependency"
    );
    var choices = client.MainModule.Types.Single(x => x.Name == "RaidChoice");
    Check(
        choices.Fields.Any(x => x.Name == "Clone_Wars") && choices.Fields.Any(x => x.Name == "The_Thief"),
        "Compiled menu exposes Clone Wars and The Thief"
    );
    Check(
        client.MainModule.Types.Any(x => x.Name == "LiveRaidMonitor"),
        "Standalone monitor is compiled into the client"
    );
    var huntDirector = client.MainModule.Types.Single(x => x.Name == "BossHuntDirector");
    Check(
        huntDirector.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i
            .Operand is MethodReference m && m.DeclaringType.Name == "BotMover" && m.Name == "GoToByWay"),
        "Boss Hunt pursues through the native path mover"
    );
    Check(
        !huntDirector.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i
            .Operand is MethodReference m && (m.Name == "Teleport" || m.Name == "SetVisible" || m.Name == "set_timeScale")),
        "Boss Hunt does not teleport actors, fake visibility or alter simulation speed"
    );
    foreach (var hook in new[]
    {
        "GiftBeforeMovement",
        "GiftMovement"
    }

    )
    {
        var patch = client.MainModule.Types.Single(t => t.Name == hook);
        Check(
            patch.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m
                && m.DeclaringType.Name == "BossHuntDirector" && m.Name == "Drive"),
            "Hunter movement is refreshed by native " + hook + " hook"
        );
    }

    Check(
        huntDirector.Methods.Single(m => m.Name == "Drive").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .Name == "MovementResume"),
        "Hunter movement refresh clears native idle pauses before moving"
    );
    var psych = client.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    IEnumerable<TypeDefinition> PsychTypes(TypeDefinition type) => new[]
    {
        type
    }.Concat(type.NestedTypes.SelectMany(PsychTypes));
    var psychCalls = PsychTypes(psych).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body
        .Instructions).Where(i => i.Operand is MethodReference).Select(i => (MethodReference)i.Operand).ToArray();
    Check(choices.Fields.Any(f => f.Name == "Schizophrenia"), "Schizophrenia is selectable in the client menu");
    Check(
        psychCalls.Any(m => m.Name == "set_bones") && !psych.Methods.Single(m => m.Name == "CreateVisual").Body
            .Instructions.Any(i => i.Operand is MethodReference m && m.Name == "BakeMesh") && psychCalls.Any(m => m
            .Name == "GetRenderersNonAlloc"),
        "Hallucination bodies borrow native rendered geometry; only held weapon attachments use baked snapshots"
    );
    Check(
        !psychCalls.Any(m => m.Name is "Instantiate" or "ThrowItem" or "CreateItem" or "CreatePlayer" or "ApplyDamageInfo"
            or "ApplyShot" or "CreateGrenade"),

        "Hallucination director does not clone actors, create inventory items or apply damage"
    );
    var addedComponents = psychCalls.OfType<GenericInstanceMethod>().Where(m => m.Name == "AddComponent")
        .SelectMany(m => m.GenericArguments).Select(t => t.FullName).ToArray();
    Check(
        addedComponents.Length > 0 && addedComponents.All(t =>
             t is "UnityEngine.MeshFilter" or "UnityEngine.MeshRenderer" or "UnityEngine.SkinnedMeshRenderer"
                 or "UnityEngine.AudioSource" or "UnityEngine.ParticleSystem" or "RaidRoulette.TestingClient.PsychPanelInput"),

        "Hallucination objects contain only mesh rendering and audio components, without collision or AI"
    );
    Check(
        psychCalls.Any(m => m.DeclaringType.Name == "Effects" && m.Name == "Emit") && psychCalls.Any(m => m.DeclaringType
            .Name == "Effects" && m.Name == "EmitGrenade"),
        "Phantom impacts and grenade bursts use native visual effects APIs"
    );
    Check(
        psychCalls.Any(m => m.Name == "GetGrenadePinSound") && psychCalls.Any(m => m.Name == "GetFuzeSound"),
        "Tripwire hallucinations use native pin and fuze sound assets"
    );
    Check(
        !psych.Methods.Single(m => m.Name == "TickCore").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .Name.StartsWith("FindObject")),
        "Hallucination frame tick no longer performs periodic scene-wide asset discovery"
    );
    Check(
        psychCalls.Any(m => m.DeclaringType.Name == "StillnessScare" && m.Name == "Tick") && psych.Methods.Any(m => m
            .Name == "StationaryGrenade"),
        "Stationary-player scare is wired to audio-only grenade cues"
    );
    Check(
        psych.Methods.Single(m => m.Name == "Stop").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Destroy"),
        "Hallucination director destroys owned visuals and audio during cleanup"
    );
    var allowedRefs = new[]
    {
        "netstandard",
        "Assembly-CSharp",
        "UnityEngine.CoreModule",
        "UnityEngine.AIModule",
        "0Harmony",
        "BepInEx",
        "UnityEngine.IMGUIModule",
        "Newtonsoft.Json",
        "UnityEngine.AudioModule",
        "UnityEngine.AssetBundleModule",
        "UnityEngine.PhysicsModule",
        "Comfort",
        "AnimationSystem.Types",
        "ItemComponent.Types",
        "UnityEngine.ParticleSystemModule",
        "UnityEngine.AnimationModule",
        // Native map-button badges use the UI and font modules shipped with Tarkov.
        "UnityEngine.UI",
        "UnityEngine.UIModule",
        "UnityEngine.TextRenderingModule",
        "Unity.TextMeshPro"
    };
    Check(
        client.MainModule.AssemblyReferences.All(r => allowedRefs.Contains(r.Name)),
        "Client assembly references only audited Tarkov, Unity and base SPT/BepInEx libraries"
    );
    var gunGame = client.MainModule.Types.Single(t => t.Name == "GunGameDirector");
    var gunCalls = PsychTypes(gunGame).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body
        .Instructions).Select(i => i.Operand).OfType<MethodReference>().ToArray();
    Check(
        new[] { "Gun_Game", "Stormtide", "AK50", "Grenadier" }.All(name => choices.Fields.Any(f => f.Name == name)),
        "All four new events are selectable in the compiled client"
    );
    Check(
        gunCalls.Any(m => m.Name == "FlatItemsToTree") && gunCalls.Any(m => m.Name == "RemoveItemWithoutRestrictions")
            && gunCalls.Any(m => m.Name == "Proceed" && m.Parameters.Count > 0 && m.Parameters[0].ParameterType.Name == "Weapon"),
        "Gun Game builds native weapon trees and swaps inventory/hands through native APIs"
    );
    Check(
        gunCalls.Any(m => m.Name == "LoadBundlesAndCreatePools"),
        "Weapon-swap events preload native model bundles before changing hands"
    );
    Check(
        gunCalls.Any(m => m.Name == "SetFireMode"),
        "Automatically equipped weapons select full auto when supported"
    );
    Check(
        gunCalls.Any(m => m.Name == "PlayUISound") && gunCalls.Any(m => m.Name == "DisplayMessageNotification"),
        "Gun Game uses native achievement audio and bottom notification APIs"
    );
    var respawn = client.MainModule.Types.Single(t => t.Name == "GunGameRespawn");
    var respawnCalls = PsychTypes(respawn).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body
        .Instructions).Select(i => i.Operand).OfType<MethodReference>().ToArray();
    Check(
        respawnCalls.Any(m => m.Name == "FullRestoreBodyPart") && respawnCalls.Any(m => m.Name == "RemoveNegativeEffects")
            && respawnCalls.Any(m => m.Name == "ChangeHydration"),
        "Respawn restores native body parts, removes negative effects and replenishes hydration"
    );
    Check(
        respawnCalls.Any(m => m.Name == "SamplePosition") && respawnCalls.Any(m => m.Name == "CheckCapsule")
            && respawnCalls.Any(m => m.Name == "Teleport"),
        "Respawn placement uses native points with NavMesh and collision checks before teleporting"
    );
    var placement = respawn.Methods.Single(method => method.Name == "FindPoint");
    Check(
        placement.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method && method.Name == "Concat"),
        "Respawn combines native point pools instead of retaining PMC points exclusively"
    );
    Check(
        placement.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method && method
            .DeclaringType.Name == "GauntletSpawnSafety" && method.Name == "HasUnlockedExit"),
        "Respawn rejects routes blocked by locked doors"
    );
    Check(
        respawnCalls.Any(m => m.Name == "RestartAfterDeath") && respawnCalls.Any(m => m.Name == "get_LocalWeaponReady"),
        "Respawn resets the saved attempt and waits for a loaded pistol before releasing controls"
    );
    Check(
        !respawnCalls.Any(m => m.DeclaringType.Name == "GameTimer" && m.Name.StartsWith("set_")) && !respawnCalls.Any(m =>
             m.DeclaringType.FullName == "UnityEngine.Time" && m.Name == "set_timeScale"),
        "Respawning never resets the raid timer or pauses the AI simulation"
    );
    var killPrefix = client.MainModule.Types.Single(t => t.Name == "GunGameLethalRespawn").Methods.Single(m => m.Name == "Prefix");
    Check(
        killPrefix.ReturnType.FullName == "System.Boolean" && killPrefix.Body.Instructions.Any(i => i
            .Operand is MethodReference m && m.Name == "Intercept"),
        "Respawn intercepts the lethal health event before native death teardown"
    );
    var clockEffect = client.MainModule.Types.Single(t => t.Name == "SchizophreniaClock");
    var clockCalls = clockEffect.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i
        .Operand).OfType<MethodReference>().ToArray();
    Check(
        clockCalls.Any(m => m.Name == "ResetForce") && clockCalls.All(m => !(m.DeclaringType.Name == "GameTimer" && m.Name
            .StartsWith("set_"))) && clockEffect.Fields.Any(f => f.Name == "consumed"),
        "Once-per-raid Schizophrenia time jump owns only the world clock, not the countdown"
    );
    Check(
        new[] { "TimeFactor", "TimeFactorMod" }.All(name => clockEffect.Methods.Single(m => m.Name == "Stop").Body
            .Instructions.Any(i => i.Operand is FieldReference f && f.Name == name || i.Operand is MethodReference m && m
            .Name == "set_" + name)),
        "Time-jump cleanup restores both original world-clock speed factors"
    );
    Check(
        client.MainModule.Types.Any(t => t.Name == "StormtideNativeAudio") && client.MainModule.Types.Single(t => t
            .Name == "StormtideDirector").Methods.Any(m => m.Name == "Stop"),
        "Stormtide managed-audio attenuation and restoration are compiled"
    );
    var thunder = client.MainModule.Types.Single(t => t.Name == "StormtideThunder");
    var thunderCalls = PsychTypes(thunder).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body
        .Instructions).Select(i => i.Operand).OfType<MethodReference>().ToArray();
    var assets = client.MainModule.Types.Single(t => t.Name == "StormAudioAssets");
    var recordingNames = assets.Methods.Single(m => m.IsConstructor).Body.Instructions.Where(i => i.OpCode.Code == Mono
        .Cecil.Cil.Code.Ldstr).Select(i => (string)i.Operand).ToArray();
    Check(
        recordingNames.SequenceEqual(new[] { "Thunder1.wav", "ThunderReplacement.wav", "Thunder3.wav", "HeavyRain.wav" }),
        "Compiled audio pool contains Thunder 1, the replacement, Thunder 3 and heavy rain"
    );
    Check(
        thunderCalls.Any(m => m.DeclaringType.Name == "ThunderSchedule" && m.Name == "Tick"),
        "Automatic strikes use the raid-elapsed probabilistic schedule"
    );
    var assetCalls = PsychTypes(assets).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body
        .Instructions).Select(i => i.Operand).OfType<MethodReference>().ToArray();
    Check(
        assetCalls.Any(m => m.DeclaringType.Name == "AudioClip" && m.Name == "Create") && assetCalls.Any(m => m
            .DeclaringType.Name == "Task" && m.Name == "Run") && assetCalls.Any(m => m.DeclaringType.Name == "WavePcm" && m
                .Name == "Read"),
        "Stormtide decodes bundled recordings off the main thread and uploads owned Unity clips"
    );
    Check(
        thunderCalls.Any(m => m.Name == "set_shadows") && !thunderCalls.Any(m => m
            .Name is "ApplyDamageInfo" or "ApplyShot" or "CreateGrenade"),
        "Lightning has no shadow rendering or gameplay damage path"
    );
    Check(
        thunder.Methods.Single(m => m.Name == "Stop").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .Name == "Destroy") && assets.Methods.Single(m => m.Name == "Dispose").Body.Instructions.Any(i => i
            .Operand is MethodReference m && m.Name == "Destroy"),
        "Thunder cleanup removes its source/light and shared assets release recording clips on shutdown"
    );
    Check(
        !client.MainModule.Types.Any(t => t.Name == "ThunderSound"),
        "Recorded thunder fully replaces the previous synthetic sound generator"
    );
    Check(
        PsychTypes(psych).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i
            .Operand is string message && message == "Did you hear that?"),
        "Schizophrenia includes the requested once-per-raid whisper notification"
    );
    var stormTargets = client.MainModule.Types.Single(t => t.Name == "StormtideNativeAudio").Methods.Single(m => m.Name == "TargetMethods");
    Check(
        !stormTargets.Body.Instructions.Any(i => i.Operand is string name && name is "Play" or "PlayScheduled")
            && !stormTargets.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "GetTypes"),
        "Storm audio targets an explicit volume-method allowlist, never playback stubs or a broad type scan"
    );
    foreach (string sourceName in new[]
    {
        "BetterSource",
        "SuperSourceDistant"
    }

    )
    {
        var nativeVolume = game.MainModule.Types.Single(t => t.Name == sourceName).Methods.Single(m => m.Name == "UpdateSourceVolume");
        Check(
            nativeVolume.HasBody && nativeVolume.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "set_volume"),
            sourceName + " volume target has a concrete native volume-update body"
        );
    }

    var startup = client.MainModule.Types.Single(t => t.Name == "Plugin").Methods.Single(m => m.Name == "Awake");
    Check(
        startup.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "CreateClassProcessor") && !startup
            .Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "PatchAll"),
        "Client startup isolates Harmony patch classes instead of rolling back the entire client"
    );
    Check(
        startup.Body.ExceptionHandlers.Count >= 2,
        "Patch initialization and failed-patch cleanup have independent exception guards"
    );
    Check(
        gunCalls.Any(m => m.Name == "UpdateHandsController") && gunCalls.Any(m => m.Name == "set_IsWeaponReady"),
        "Gun Game refreshes AI weapon/controller readiness after the actual native hands callback"
    );
    Check(
        gunCalls.Any(m => m.Name == "FastForwardCurrentState") && gunCalls.Any(m => m.Name == "SetAnimatorValues"),
        "Newly drawn firearms finish their spawn state and synchronize chamber/magazine animator values"
    );
    var capture = client.MainModule.Types.Single(t => t.Name == "GunGameDamageCapture");
    Check(
        capture.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m
            && m.Name == "ObserveDamage"),
        "Gun Game captures direct health damage before rocket explosion deaths notify listeners"
    );
    var health = game.MainModule.Types.Single(t => t.FullName == "EFT.HealthSystem.ActiveHealthController");
    Check(
        health.Methods.Any(m => m.Name == "ApplyDamage" && m.Parameters.Count == 3 && m.Parameters[2].ParameterType
            .FullName == "EFT.Ballistics.DamageInfo"),
        "Installed health controller exposes the exact three-argument damage capture target"
    );
    var localDirector = client.MainModule.Types.Single(x => x.Name == "LocalRaidDirector");
    Check(
        localDirector.Methods.Single(m => m.Name == "get_NativePoints").Body.Instructions.Any(i => i
            .Operand is FieldReference f && f.DeclaringType.FullName == "EFT.Game.Spawning.SpawnSystem" && f.Name == "_spawnPoints"),
        "Rooftop lookup reads the global native spawn collection rather than the PMC-only zone list"
    );
    var warp = client.MainModule.Types.Single(x => x.Name == "TimeWarpDirector");
    Check(choices.Fields.Any(x => x.Name == "Time_Warp"), "Time Warp is selectable in the compiled menu");
    Check(
        warp.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).All(i => i
            .Operand is not MethodReference m || !(m.DeclaringType.FullName == "UnityEngine.Time" && m
            .Name == "set_timeScale") && !(m.DeclaringType.FullName == "EFT.GameTimer" && m.Name.StartsWith("set_"))),
        "Time Warp never changes simulation timeScale or raid timer properties"
    );
    Check(
        warp.Methods.Single(m => m.Name == "Stop").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.FullName == "EFT.GameDateTime" && m.Name == "ResetForce"),
        "Time Warp restoration rebases the original clock timeline"
    );
    Check(
        choices.Fields.Any(x => x.Name == "Smokers_Lobby") && client.MainModule.Types.Any(x => x.Name == "SmokersDirector"),
        "Smokers Lobby menu and native throw director are compiled into client"
    );
    Check(
        client.MainModule.Types.Any(x => x.Name == "GiftRouteOwnership") && client.MainModule.Types.Any(x => x.Name == "GiftNoPickup"),
        "Route ownership and pickup guards are compiled into the client"
    );
    var plugin = client.MainModule.Types.Single(x => x.Name == "Plugin");
    Check(
        plugin.Methods.Single(x => x.Name == "OnGUI").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.Name == "LiveRaidMonitor" && m.Name == "Draw"),
        "Plugin OnGUI draws the standalone monitor independently of F12"
    );
    Check(client.MainModule.Types.Any(x => x.Name == "RaidRoster"), "Compiled F12 roster is included");
    Check(choices.Fields.Any(x => x.Name == "Overrun"), "Compiled menu exposes Overrun");
    Check(
        !client.MainModule.Types.Any(x => x.Name.Contains("Admin")),
        "Compiled local client contains no admin controller"
    );
    Check(
        client.MainModule.Types.Any(x => x.Name == "CultistZoneFix") && client.MainModule.Types.Any(x => x.Name == "DamagePatch"),
        "Cultist fix and Ghost Review retained"
    );
}

if (args.Length > 2)
{
    using var renamed = ReadAssembly(args[2]);
    Check(renamed.Name.Name == "RaidRoulette.Testing", "Client assembly uses the Raid Roulette name");
    var plugin = renamed.MainModule.Types.Single(t => t.Name == "Plugin");
    var identity = plugin.CustomAttributes.Single(a => a.AttributeType.Name == "BepInPlugin");
    Check(
        identity.ConstructorArguments[0].Value.ToString() == "com.killdg.raidroulette.testing" && identity
            .ConstructorArguments[1].Value.ToString() == "Raid Roulette",
        "BepInEx menu and configuration identity use Raid Roulette"
    );
    var director = renamed.MainModule.Types.Single(t => t.Name == "GunGameDirector");
    var ladder = director.Methods.Single(m => m.Name == ".cctor").Body.Instructions.Where(i => i.OpCode.Code == Mono
        .Cecil.Cil.Code.Ldstr).Select(i => (string)i.Operand).Take(9);
    Check(
        ladder.SequenceEqual(new[] { "pistol", "shotgun", "smg", "sniperRifle", "ak50", "assaultRifle", "machinegun",
             "rocket", "grenadeLauncher" }),
        "Compiled client ladder matches the server order with SMG third, AK50 fifth and grenade launcher last"
    );
    Check(
        !director.Methods.Any(m => m.Name is "SupplyImpacts" or "ThrowImpact"),
        "Gun Game no longer creates or throws impact grenades"
    );
    var classifier = director.Methods.Single(m => m.Name == "Kind");
    Check(
        !director.Fields.Any(f => f.Name == "nextRecover") && director.Fields.Any(f => f.Name == "recoveryNeeded")
            && director.Fields.Any(f => f.Name == "recoveryQueued"),
        "Weapon escrow recovery uses lifecycle receipts instead of an idle periodic timer"
    );
    Check(
        !classifier.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "op_Implicit" && m.DeclaringType
            .FullName == "EFT.MongoID"),
        "Source-less damage classification never constructs a native MongoID from an empty string"
    );
    Check(
        classifier.Body.Instructions.Any(i => i.Operand is string s && s == "67d0576f29f580ebc10efd08") && classifier.Body
            .Instructions.Any(i => i.Operand is string s && s == "ak50"),
        "Gun Game classifies the native AK50 separately from sniper rifles"
    );
}

if (args.Length > 2)
{
    using var built = ReadAssembly(args[2]);
    Check(
        built.MainModule.Types.Single(t => t.Name == "RaidChoice").Fields.Any(f => f.Name == "Lab_Work_Day"),
        "Lab Work Day is selectable in the compiled F12 menu"
    );
    var staffMovement = built.MainModule.Types.Single(t => t.Name == "LabStaffMovement").Methods.Single(m => m.Name == "Prefix");
    Check(
        staffMovement.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "ManualUpdate" && m
            .DeclaringType.Name == "BotMover") && !staffMovement.Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.Name == "BotWeaponManager"),
        "Unarmed staff use native movement without invoking native gun reload updates"
    );
    var neutralBrain = built.MainModule.Types.Single(t => t.Name == "LabStaffPeacefulBrain").Methods.Single(m => m.Name == "Postfix");
    var nativeLook = game.MainModule.Types.Single(t => t.Name == "LookSensor");
    Check(
        nativeLook.Methods.Any(m => m.Name == "UpdateLook" && m.Parameters.Count == 1) && nativeLook.Fields.Any(f => f
            .Name == "_botOwner" && f.FieldType.FullName == "EFT.BotOwner") && nativeLook.Fields.Any(f => f
            .Name == "_isTaskCompleted" && f.FieldType.FullName == "System.Boolean"),
        "Native civilian vision hook resolves its exact owner and task-completion fields"
    );
    var staffVision = built.MainModule.Types.Single(t => t.Name == "LabStaffVision").Methods.Single(m => m.Name == "Prefix");
    Check(
        staffVision.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "IsStaff") && staffVision.Body
            .Instructions.Any(i => i.Operand is MethodReference m && m.Name == "SetValue"),
        "Only recognized civilians bypass weapon-root combat vision and complete their periodic task"
    );
    Check(
        neutralBrain.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "RemoveNodeController"),
        "Staff combat decision scheduler is detached after native brain initialization"
    );
    var questGate = questClient.MainModule.Types.Single(t => t.Name == "RaidQuestCounterGate");
    Check(
        questGate.Methods.Single(m => m.Name == "Prefix").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .Name == "OwnCounter"),
        "Native generic kill tests are gated for the custom event quest counters"
    );
    Check(
        questClient.MainModule.Types.Any(t => t.Name == "RaidQuestItemFilter") && questClient.MainModule.Types.Any(t => t
            .FullName == "RaidRoulette.Shared.QuestIds"),
        "Client includes native handover filtering and the shared stable quest IDs"
    );
    var lab = built.MainModule.Types.Single(t => t.Name == "LabWorkDirector");
    Check(
        lab.Methods.Single(m => m.Name == "Tick").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "ActivateWave"),
        "Third staff kill uses the native reinforcement scenario for its Raider group"
    );
}

if (args.Length > 2)
{
    using var chainClient = ReadAssembly(args[3]);
    var entries = chainClient.MainModule.Types.Single(t => t.FullName == "RaidRoulette.Shared.QuestIds").Methods
        .Single(m => m.Name == ".cctor").Body.Instructions.Where(i => i.Operand is string).Select(i => (string)i.Operand)
        .Where(s => new[] { "stormtide", "smokers_lobby", "the_gift", "time_warp", "schizophrenia", "clone_wars", "ak50",
         "boss_hunt", "cultist_invasion", "gun_game" }.Contains(s));
    Check(
        entries.First() == "stormtide" && entries.Last() == "gun_game" && entries.Count() == 10,
        "Compiled client quest order starts with Stormtide and ends with Gun Game"
    );
    var accepted = chainClient.MainModule.Types.Single(t => t.Name == "TraderQuestDirector").Methods.Single(m => m.Name == "Accepted");
    Check(
        accepted.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Take") && accepted.Body
            .Instructions.Any(i => i.Operand is MethodReference m && m.Name == "All"),
        "Client event credit checks every preceding quest before advancing an accepted quest"
    );
}

if (args.Length > 2)
{
    using var tagsClient = ReadAssembly(args[2]);
    var guard = tagsClient.MainModule.Types.Single(t => t.Name == "CloneDogtagIdentity").Methods.Single(m => m.Name == "Prefix");
    Check(
        guard.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "CloneTagIdentity" && m
            .Name == "IsClone") && !guard.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_RaidContext"),
        "Clone tag protection uses server identity markers and works before raid-context polling updates"
    );
    var nativeCustomTag = game.MainModule.Types.Single(t => t.FullName == "EFT.Customization.CustomizationDogTag")
        .Methods.Single(m => m.Name == "GetItemClone");
    Check(
        nativeCustomTag.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "CloneItemWithSameId"),
        "Installed native custom-tag creation retains the shared appearance ID that causes clone collisions"
    );
    Check(
        questClient.MainModule.Types.Any(t => t.Name == "CloneDogtagHandoverProgress") && questClient.MainModule.Types
            .Any(t => t.Name == "CloneDogtagCounterAttach"),
        "Verified tag hand-ins synchronize the native recovery counter both live and after profile reload"
    );
}

if (args.Length > 2)
{
    using var staffClient = ReadAssembly(args[2]);
    var kill = staffClient.MainModule.Types.Single(t => t.Name == "LabStaffXpKill").Methods.Single(m => m.Name == "Prefix");
    Check(
        kill.ReturnType.FullName == "System.Boolean" && staffClient.MainModule.Types.Any(t => t.Name == "LabStaffXpDamage"),
        "Staff kills and damage bypass native combat XP, boss statistics and combat-quest credit"
    );
    var finish = staffClient.MainModule.Types.Single(t => t.Name == "LabStaffExperience").Methods.Single(m => m.Name == "Apply");
    Check(
        finish.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Award" && m.DeclaringType.Name == "LabStaffPolicy"),
        "Civilian XP adjustment computes a nonnegative net award before native level changes"
    );
    var nativeEnd = game.MainModule.Types.Single(t => t.FullName == "EFT.BaseStatisticsManager").Methods.Single(m => m
        .Name == "EndStatisticsSession" && m.Parameters.Count == 2);
    Check(
        nativeEnd.Body.Instructions.Count(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && i.Operand is FieldReference f
            && f.Name == "TotalSessionExperience") == 1,
        "Installed game has exactly one final XP store for the civilian penalty hook"
    );
    var register = staffClient.MainModule.Types.Single(t => t.Name == "LabStaffBehaviour").Methods.Single(m => m.Name == "Register");
    Check(
        register.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "PlayerAnimatorSetWeaponId")
            && register.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "SetLayerWeight"),
        "Staff explicitly use empty-hands animation and remove the third-person weapon overlay"
    );
    var flee = staffClient.MainModule.Types.Single(t => t.Name == "LabStaffBehaviour").Methods.Single(m => m.Name == "Drive");
    Check(
        flee.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "CalculatePath") && flee.Body
            .Instructions.Any(i => i.Operand is MethodReference m && m.Name == "GoToByWay"),
        "Fleeing uses complete native navigation paths and mover corners"
    );
    Check(
        staffClient.MainModule.Types.Any(t => t.Name == "LabStaffGunfire") && staffClient.MainModule.Types.Any(t => t
            .Name == "LabStaffThreatDamage"),
        "Gunfire and attacks both feed the civilian danger response"
    );
}

if (args.Length > 5)
{
    using var coreServer = ReadAssembly(args[4]);
    using var questServer = ReadAssembly(args[5]);
    using var coreClient = ReadAssembly(args[2]);
    Check(
        !coreServer.MainModule.Types.Any(t => t.Name.Contains("TraderQuest") || t.Name == "QuestItemsRouter")
            && !coreServer.MainModule.Resources.Any(r => r.Name.EndsWith("quests.json")),
        "Standalone event backend contains no quest definitions, quest routes or quest lifecycle patches"
    );
    Check(
        !coreClient.MainModule.Types.Any(t => t.Name.Contains("TraderQuest") || t.Name == "QuestIds" || t.Name
            .StartsWith("RaidQuest") || t.Name == "CloneDogtagHandoverProgress"),
        "Standalone event client contains no quest polling, counters or hand-in patches"
    );
    Check(
        !coreServer.MainModule.AssemblyReferences.Any(r => r.Name.StartsWith("RaidRoulette.Quests")) && !coreClient
            .MainModule.AssemblyReferences.Any(r => r.Name.StartsWith("RaidRoulette.Quests")),
        "Core assemblies do not depend on the optional quest pack"
    );
    Check(
        questServer.MainModule.AssemblyReferences.Any(r => r.Name == "RaidRoulette") && questClient.MainModule
            .AssemblyReferences.Any(r => r.Name == "RaidRoulette.Testing"),
        "Quest modules reference the shared event engine rather than duplicating it"
    );
    Check(
        questServer.MainModule.Resources.Count(r => r.Name == "RaidRoulette.Quests.quests.json") == 1,
        "Only the optional pack embeds the stable ten-quest catalog"
    );
    Check(
        !questClient.MainModule.Types.Any(t => t.Name is "StormtideDirector" or "GunGameDirector" or "LabStaffBehaviour"),
        "Quest client does not duplicate event effects or AI directors"
    );
    var dependency = questClient.MainModule.Types.Single(t => t.Name == "AdvancedPlugin").CustomAttributes.Single(a =>
         a.AttributeType.Name == "BepInDependency" && a.ConstructorArguments[0].Value.ToString() == "com.killdg.raidroulette.testing");
    Check(
        dependency.ConstructorArguments[0].Value.ToString() == "com.killdg.raidroulette.testing",
        "Quest client declares a hard BepInEx dependency on Raid Roulette"
    );
    Check(
        coreClient.MainModule.Types.Any(t => t.Name == "CloneDogtagIdentity") && coreServer.MainModule.Types.Any(t => t
            .Name == "CloneProfileMaintenancePatch"),
        "Clone identity and legacy repair remain available without quests"
    );
}

using (var menuClient = ReadAssembly(args[2]))
{
    var menuTypes = menuClient.MainModule.Types;
    var menu = menuTypes.Single(t => t.Name == "AdvancedMenu");
    var monitor = menuTypes.Single(t => t.Name == "LiveRaidMonitor");
    Check(
        monitor.Methods.Single(m => m.Name == "Draw").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.Name == "AdvancedMenu" && m.Name == "get_Enabled"),
        "Live monitor rendering requires Advanced to be enabled"
    );
    Check(
        monitor.Methods.Single(m => m.Name == "Tick").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.Name == "AdvancedMenu" && m.Name == "get_Enabled"),
        "F8 shortcut cannot expose the monitor outside Advanced"
    );
    Check(
        menuTypes.Single(t => t.Name == "RaidRoster").Methods.Single(m => m.Name == "Tick").Body.Instructions.Any(i => i
            .Operand is MethodReference m && m.DeclaringType.Name == "AdvancedMenu" && m.Name == "get_Enabled"),
        "Live AI roster scanning is disabled outside Advanced"
    );
    Check(
        menu.Methods.Single(m => m.Name == "Bind" && m.Parameters.Last().ParameterType.Name == "ConfigDescription").Body
            .Instructions.Any(i => i.Operand is FieldReference f && f.Name == "Browsable"),
        "Advanced settings explicitly control Configuration Manager visibility"
    );
    var plugin = menuTypes.Single(t => t.Name == "Plugin");
    Check(
        plugin.Methods.Single(m => m.Name == "Send").Body.Instructions.Any(i => i.Operand is string text
            && text == "chances") && menuTypes.Any(t => t.Name == "RaidChanceSettings"),
        "F12 chance controls send their profile to backend selection"
    );
    Check(
        plugin.Methods.Single(m => m.Name == "Awake").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.Name == "MenuAction" && m.Name == "Bind"),
        "Manual next-raid choice uses a momentary action rather than a repeat toggle"
    );
}

using (var updatedMenu = ReadAssembly(args[2]))
{
    var menuTypes = updatedMenu.MainModule.Types;
    var chanceSettings = menuTypes.Single(t => t.Name == "RaidChanceSettings");
    Check(
        menuTypes.Any(type => type.Name == "RaidPreset") && chanceSettings.Methods.Single(method => method.Name == "Refresh").HasBody,
        "Chance settings expose presets with custom-only percentage rows"
    );
    var notifier = menuTypes.Single(t => t.Name == "RaidEventNotifier");
    Check(
        notifier.Fields.Any(f => f.Name == "startNotice") && notifier.Fields.Any(f => f.Name == "notice"),
        "Start and end raid notices keep independent once-only state"
    );
    var noticeBind = notifier.Methods.Single(m => m.Name == "Bind");
    Check(
        noticeBind.Body.Instructions.Any(i => i.Operand is string text && text == "Show raid event before raid"),
        "Optional before-raid notice is present in the compiled menu"
    );
    var storm = menuTypes.Single(t => t.Name == "StormtideDirector");
    Check(
        !storm.Fields.Any(f => f.Name == "thunderEnabled"),
        "Stormtide no longer has a separate thunder opt-out flag"
    );
    Check(
        storm.Methods.Single(m => m.Name == "Core").Body.Instructions.Any(i => i.Operand is string text && text == ": stormtide"),
        "Always-enabled storm effects remain gated to the Stormtide event"
    );
    var plugin = menuTypes.Single(t => t.Name == "Plugin");
    Check(
        plugin.Fields.Single(f => f.Name == "BossRoaming").IsLiteral && (bool)plugin.Fields.Single(f => f.Name == "BossRoaming").Constant,
        "Boss Convention idle roaming is permanently enabled"
    );
    Check(
        plugin.Methods.Single(m => m.Name == "Send").Body.Instructions.Any(i => i.Operand is string text && text == "eventsEnabled"),
        "The client sends its master toggle to the backend"
    );
}

using (var backport = ReadAssembly(args[2]))
{
    var storm = backport.MainModule.Types.Single(t => t.Name == "StormtideDirector");
    Check(
        backport.MainModule.Types.Any(t => t.Name == "LightningLightFlicker") && !backport.MainModule.AssemblyReferences
            .Any(r => r.Name.StartsWith("ReturnOfThunderstorms")),
        "Stormtide contains an independent light-flicker backport"
    );
    Check(
        storm.Methods.Single(m => m.Name == "Core").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.Name == "LightningLightFlicker" && m.Name == "Discover"),
        "Stormtide discovers lights only through its active event director"
    );
    Check(
        storm.Methods.Single(m => m.Name == "Stop").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .DeclaringType.Name == "LightningLightFlicker" && m.Name == "Stop"),
        "Stopping Stormtide restores flickering lights"
    );
    Check(
        backport.MainModule.Types.Single(t => t.Name == "Plugin").Methods.Single(m => m.Name == "LateUpdate").Body
            .Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "StormtideDirector" && m.Name == "LateTick"),
        "RR applies the light pulse after native frame updates"
    );
}

Console.WriteLine("TOTAL PASS " + checks + " (metadata compatibility; not an in-game test)");
using (var psychClient = ReadAssembly(args[2]))
{
    var types = psychClient.MainModule.Types;
    var psych = types.Single(t => t.Name == "SchizophreniaDirector");
    var clock = types.Single(t => t.Name == "SchizophreniaClock");
    Check(
        clock.Methods.Single(m => m.Name == "Tick").Parameters.Last().ParameterType.FullName == "System.Boolean",
        "Time jump explicitly requires enclosure permission"
    );
    var indoor = types.Single(t => t.Name == "SchizophreniaInterior");
    Check(
        indoor.Methods.Single(m => m.Name == "Enclosed").Body.Instructions.Any(i => i.Operand is MethodReference r && r
            .Name == "get_Environment"),
        "Room scare checks native indoor volumes as well as bounded sight rays"
    );
    Check(
        game.MainModule.Types.Single(t => t.FullName == "EFT.UI.GUISounds").Fields.Any(f => f.Name == "_mainMenuMusic" && f
            .FieldType.FullName == "UnityEngine.AudioClip[]"),
        "Installed Tarkov exposes its native cached soundtrack"
    );
    Check(
        psych.Methods.Any(m => m.Name == "TryMusicDecoy") && psych.Methods.Any(m => m.Name == "TickMusic"),
        "Stationary music decoy is wired to the existing hallucination director"
    );
    Check(
        psych.Methods.Single(m => m.Name == "TickMusic").Body.Instructions.Any(i => i.Operand is MethodReference r && r
            .DeclaringType.Name == "PsychMusicPolicy" && r.Name == "Gain"),
        "Decoy playback applies the tested five-second fade envelope"
    );
}

Console.WriteLine("FINAL TOTAL PASS " + checks);
using (var expanded = ReadAssembly(args[2]))
{
    var types = expanded.MainModule.Types;
    var director = types.Single(t => t.Name == "SchizophreniaDirector");
    Check(
        types.Any(t => t.Name == "PsychPanelInput") && director.Methods.Any(m => m.Name == "Panel"),
        "F2 diagnostic panel has a native input node rather than clicks passing through to firing"
    );
    var world = types.Single(t => t.Name == "PsychWorldScares");
    var move = world.Methods.Single(m => m.Name == "StepShuffle").Body.Instructions.Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        move.Any(m => m.DeclaringType.Name == "ItemManipulator" && m.Name == "Move") && move.Any(m => m
            .Name == "RunNetworkTransaction") && !move.Any(m => m.Name.Contains("WithoutRestrictions")),
        "Backpack reposition uses simulated native moves and native transactions, with no direct remove/add"
    );
    Check(
        world.Methods.Single(m => m.Name == "StepShuffle").Body.Instructions.Any(i => i.Operand is MethodReference r && r
            .Name == "CreateItemAddress"),
        "Backpack fallback moves use grid addresses"
    );
    Check(
        director.Methods.Single(m => m.Name == "TryDoorPulse").Body.Instructions.Any(i => i.Operand is MethodReference r
            && r.Name == "Visible"),
        "Automatic door scare requires camera visibility and line of sight"
    );
    Check(
        director.Methods.Single(m => m.Name == "FalseDeath").Body.Instructions.Any(i => i.Operand is MethodReference r && r
            .Name == "PlayEndGameSound") && !director.Methods.Single(m => m.Name == "FalseDeath").Body.Instructions.Any(i => i
            .Operand is MethodReference r && r.Name.Contains("StopGame")),
        "False-death scare invokes native end audio without ending the raid"
    );
    Check(
        game.MainModule.Types.Single(t => t.FullName == "EFT.Interactive.Door").Methods.Any(m => m.Name == "Interact" && m
            .Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "InteractionResult"),
        "Manual door apparition hook matches the installed native overload"
    );
    var air = director.Methods.Single(m => m.Name == "FastPlaneDirection").Body.Instructions.Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        air.Any(m => m.Name == "CreateVisual") && !air.Any(m => m.Name is "CreateFromPool" or "AirdropEvent" or "CreateItem"),
        "Phantom aircraft borrows rendered meshes without creating an airdrop simulation"
    );
    Check(
        director.Methods.Single(m => m.Name == "Burst").Body.Instructions.Any(i => i.Operand is MethodReference r && r
            .DeclaringType.Name == "CollisionModule" && r.Name == "set_enabled"),
        "Cosmetic pyrotechnics explicitly disable particle collisions"
    );
}

Console.WriteLine("EXPANSION TOTAL PASS " + checks);
using (var glassClient = ReadAssembly(args[2]))
{
    var types = glassClient.MainModule.Types;
    var glass = types.Single(t => t.Name == "PsychGlassIllusion");
    var calls = glass.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        !calls.Any(m => m.Name is "Break" or "BreakWindow" or "MakeHit" or "BreakByHit" or "CleanPieces" or "set_FirstHitPosition")
            && !calls.Any(m => m.DeclaringType.Name.EndsWith("Collider") && m.Name == "set_enabled"),
        "Glass hallucination never changes native break state or collider enable state"
    );
    Check(
        calls.Any(m => m.Name == "set_forceRenderingOff") && calls.Any(m => m.DeclaringType.Name == "ComputeBuffer" && m
            .Name == "SetData"),
        "Glass illusion supports native renderer and GPU window visibility paths"
    );
    Check(
        glass.Methods.Single(m => m.Name == "Restore").Body.Instructions.Any(i => i.Operand is MethodReference r && r
            .Name == "RestoreIntact"),
        "Restoration consults genuine native glass damage before enabling intact GPU glass"
    );
    var manager = game.MainModule.Types.Single(t => t.Name == "WindowsManager");
    Check(
        new[] { "_breakerIdInstanceId", "_renderData", "_instancesEnable" }.All(n => manager.Fields.Any(f => f.Name == n))
            && game.MainModule.Types.Single(t => t.FullName == "WindowsManagerUtilities.GeometryComputeBuffers").Fields
            .Any(f => f.Name == "_instancesEnable" && f.FieldType.Name == "ComputeBuffer"),
        "Installed GPU window visibility fields match the audited adapter"
    );
    Check(
        types.Single(t => t.Name == "SchizophreniaDirector").Methods.Single(m => m.Name == "TickCore").Body.Instructions
            .Any(i => i.Operand is GenericInstanceMethod r && r.Name == "FindActiveEffects"),
        "Injury boost reads active native health effects"
    );
    Check(
        types.Single(t => t.Name == "SchizophreniaDirector").Methods.Single(m => m.Name == "Panel").Body.Instructions
            .Any(i => i.Operand is string s && s.Contains("Shatter nearby glass")),
        "F2 panel includes a manual glass shatter and restore action"
    );
}

Console.WriteLine("GLASS TOTAL PASS " + checks);
using (var fixedClient = ReadAssembly(args[2]))
{
    var director = fixedClient.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    var calls = director.Methods.Single(m => m.Name == "PanelTick").Body.Instructions.Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        calls.Any(m => m.DeclaringType.Name == "PlayerOwner" && m.Name == "get_InputTree") && !calls.Any(m => m
            .Name == "FindObjectOfType"),
        "F2 attaches to the local player's actual IInputTree instead of searching for a scene component"
    );
    Check(
        calls.Any(m => m.Name == "Contains") && calls.Any(m => m.DeclaringType.Name == "InputNode" && m.Name == "Add"),
        "Panel verifies tree attachment and has a local owner fallback for mocked input trees"
    );
    Check(
        director.Methods.Single(m => m.Name == "FalseDeath").Body.Instructions.Any(i => i.Operand is MethodReference r && r
            .Name == "GetEndGameClip") && director.Methods.Single(m => m.Name == "FalseDeath").Body.Instructions.Any(i => i
            .Operand is MethodReference r && r.Name == "DeathNoticeDelay"),
        "False-death reassurance uses the native loss clip duration"
    );
}

Console.WriteLine("INPUT FIX TOTAL PASS " + checks);
using (var actionClient = ReadAssembly(args[2]))
{
    var types = actionClient.MainModule.Types;
    var medical = types.Single(t => t.Name == "PsychMedicalIllusion");
    var calls = medical.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        calls.Any(m => m.Name == "Evaluate") && !calls.Any(m => m
            .Name is "CreateController" or "CreateControllerAsync" or "DoMedEffect" or "CreateItem" or "Proceed"),
        "Medical illusion evaluates cosmetic clip graphs without healing controllers, real items or treatment calls"
    );
    Check(
        medical.Methods.Single(m => m.Name == "Clear").Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == "SetActive"),
        "Medical cleanup immediately deactivates owned rigs before destruction"
    );
    Check(
        types.Any(t => t.Name == "PsychMedicalCleanup") && game.MainModule.Types.Single(t => t.Name == "Player" && t
            .Namespace == "EFT").NestedTypes.Single(t => t.Name == "MedsController").NestedTypes.Single(t => t
            .Name == "MedsInHandsOperation").Methods.Any(m => m.Name == "HideWeapon"),
        "Medical cancellation cleanup has a native operation target in this build"
    );
    var nativeActionPlayer = game.MainModule.Types.Single(t => t.Name == "Player" && t.Namespace == "EFT");
    Check(
        nativeActionPlayer.Methods.Any(m => m.Name == "ThrowGrenade" && m.Parameters.Any(p => p.Name == "simulate")),
        "Jukebox trigger audits native real-versus-simulated grenade throws"
    );
    var firearm = nativeActionPlayer.NestedTypes.Single(t => t.Name == "FirearmController");
    Check(
        firearm.Methods.Any(m => m.Name == "ReloadMag") && firearm.Methods.Any(m => m.Name == "QuickReloadMag"),
        "Magazine illusion targets native normal and quick reload methods"
    );
}

Console.WriteLine("ACTION MEDICAL TOTAL PASS " + checks);
using (var repaired = ReadAssembly(args[2]))
{
    var types = repaired.MainModule.Types;
    var worldScares = types.Single(t => t.Name == "PsychWorldScares");
    Check(
        worldScares.Methods.Single(m => m.Name == "ClearSight").Body.Instructions.Any(i => i
            .Operand is GenericInstanceMethod r && r.Name == "GetComponentInParent"),
        "Sight checks can ignore the local player's own body colliders"
    );
    var med = types.Single(t => t.Name == "PsychMedicalIllusion");
    Check(
        med.Methods.Single(m => m.Name == "Create").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "BodySkins"),
        "Medical ghosts bind the player's actual hand skin rather than the held-item object"
    );
    Check(
        med.Methods.Single(m => m.Name == "Create").Body.Instructions.Any(i => i.Operand is string s && s == "item_syringe_use"),
        "Medical clip selection names the actual syringe animation"
    );
    Check(
        worldScares.Methods.Single(m => m.Name == "StepShuffle").Body.Instructions.Any(i => i.Operand is MethodReference r
            && r.Name == "Swap"),
        "Whole-backpack rearrangement supports native swaps without temporary equipment transfers"
    );
    Check(
        types.Any(t => t.Name == "PsychRapidGunSound") && game.MainModule.Types.Single(t => t.Name == "WeaponSoundPlayer")
            .Methods.Any(m => m.Name == "FireBullet"),
        "Rapid gun audio patch targets the native audio-only bullet method"
    );
    Check(
        types.Single(t => t.Name == "PsychRapidGunSound").Methods.Single(m => m.Name == "Prefix").ReturnType.FullName == "System.Boolean",
        "Gun audio override can suppress only the original audio call after replacement starts"
    );
}

Console.WriteLine("REPAIR TOTAL PASS " + checks);
using (var placement = ReadAssembly(args[2]))
{
    var types = placement.MainModule.Types;
    var med = types.Single(t => t.Name == "PsychMedicalIllusion");
    Check(
        med.Methods.Single(m => m.Name == "LateTick").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Evaluate"),
        "Medical cosmetic clip graphs evaluate during the late frame pass"
    );
    Check(
        types.Single(t => t.Name == "SchizophreniaDirector").Methods.Single(m => m.Name == "LateTick").Body.Instructions
            .Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "PsychMedicalIllusion" && m.Name == "LateTick"),
        "Director dispatches medical late frame sampling"
    );
}

Console.WriteLine("MEDICAL PLACEMENT TOTAL PASS " + checks);
using (var repaired = ReadAssembly(args[2]))
{
    var director = repaired.MainModule.Types.Single(type => type.Name == "SchizophreniaDirector");
    var actorCalls = director.Methods.Single(method => method.Name == "ActorRenderers").Body.Instructions.Select(i => i
        .Operand).OfType<MethodReference>();
    Check(
        actorCalls.Any(method => method.Name == "get_ControllerGameObject"),
        "Actor visuals include held-item hierarchy outside the clothing renderer list"
    );
    var medical = repaired.MainModule.Types.Single(type => type.Name == "PsychMedicalIllusion");
    Check(
        medical.Methods.Single(method => method.Name == "LateTick").Body.Instructions.Any(i => i
            .Operand is MethodReference method && method.Name == "TransformPoint"),
        "Medical ghost positions use a camera-relative destination after clip sampling"
    );
    var gestureCalls = director.Methods.Single(method => method.Name == "Beckon").Body.Instructions.Select(i => i
        .Operand).OfType<MethodReference>().ToArray();
    Check(
        gestureCalls.All(method => method.Name != "ShowGesture"),
        "Jukebox invitation does not command a real donor bot to gesture"
    );
    var gunCalls = director.Methods.Single(method => method.Name == "OverrideGunSound").Body.Instructions.Select(i => i
        .Operand).OfType<MethodReference>().ToArray();
    Check(
        gunCalls.Any(method => method.Name == "Play") && gunCalls.All(method => method
            .Name is not ("InitiateShot" or "CreateItem" or "ConsumeAmmo")),
        "Alternate gun playback stays on the audio-only path"
    );
}

Console.WriteLine("VISUAL REPAIR TOTAL PASS " + checks);
using (var medicalClient = ReadAssembly(args[2]))
{
    var medical = medicalClient.MainModule.Types.Single(type => type.Name == "PsychMedicalIllusion");
    var createCalls = medical.Methods.Single(method => method.Name == "Create").Body.Instructions.Select(instruction =>
         instruction.Operand).OfType<MethodReference>().ToArray();
    Check(
        createCalls.Any(method => method.DeclaringType.Name == "AnimationClipPlayable" && method.Name == "Create"),
        "Medical copies use isolated Unity clip players"
    );
    var createInstructions = medical.Methods.Single(method => method.Name == "Create").Body.Instructions;
    Check(
        createInstructions.Any(instruction => instruction.Operand is MethodReference method && method
            .Name == "set_fireEvents" && instruction.Previous?.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_0),
        "Cosmetic medical animators disable native animation events"
    );
    Check(
        medical.Methods.Single(method => method.Name == "Clear").Body.Instructions.Any(instruction => instruction
            .Operand is MethodReference method && method.DeclaringType.Name == "PlayableGraph" && method.Name == "Destroy"),
        "Surgery cleanup destroys the independent clip graphs"
    );
}

Console.WriteLine("MEDICAL CLIP GRAPH TOTAL PASS " + checks);
using (var encounterClient = ReadAssembly(args[2]))
{
    var director = encounterClient.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    Check(
        !director.Methods.Any(m => m.Name == "DoorGhostAt" || m.Name == "HeadFlame" || m.Name == "SkyFlares" || m
            .Name == "IncomingFirework"),
        "Release candidate removes door boss, head flame, sky flare and ground firework effects"
    );
    Check(
        director.Methods.Single(m => m.Name == "Stop").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .Name == "remove_OnPlayerDeadStatic"),
        "Stopping hallucinations unsubscribes kill tracking"
    );
    var fade = director.Methods.Single(m => m.Name == "ApplyVisualFade").Body.Instructions.Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        fade.Any(m => m.DeclaringType.Name == "PsychFadePolicy" && m.Name == "Alpha"),
        "Visual decoys use the checked fade envelope"
    );
}

Console.WriteLine("ENCOUNTER FINAL TOTAL PASS " + checks);
using (var performanceClient = ReadAssembly(args[2]))
{
    var director = performanceClient.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    var calls = director.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        !calls.Any(m => m.DeclaringType.Namespace == "UnityEngine" && (m.Name == "FindObjectsOfType" || m
            .Name == "FindObjectOfType" || m.Name == "FindObjectsOfTypeAll")),
        "Hallucination director no longer performs global Unity object searches"
    );
    var cache = director.Methods.Single(m => m.Name == "CacheLoadedAssetsIncrementally").Body.Instructions;
    Check(
        cache.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "Stopwatch" && m.Name == "get_Elapsed"),
        "Loaded bundle inspection has an elapsed-time frame budget"
    );
    Check(
        director.Methods.Single(m => m.Name == "OverrideGunSound").Body.Instructions.All(i => !(i
            .Operand is MethodReference m && m.DeclaringType.Name == "Resources")),
        "Rapid gun audio uses cached alternatives without a global search fallback"
    );
}

Console.WriteLine("PERFORMANCE FINAL TOTAL PASS " + checks);
using (var soundRepairClient = ReadAssembly(args[2]))
{
    var director = soundRepairClient.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    var bank = director.Methods.Single(m => m.Name == "CacheSoundBank").Body.Instructions;
    Check(
        bank.Any(i => i.Operand is FieldReference f && f.DeclaringType.Name == "SoundBank" && f.Name == "Environments")
            && bank.Any(i => i.Operand is FieldReference f && f.DeclaringType.Name == "DistanceVarity" && f.Name == "Clips"),
        "Sound cache follows native bank environment/distance clip references"
    );
    Check(
        director.Methods.Single(m => m.Name == "OverrideGunSound").Body.Instructions.Any(i => i
            .Operand is MethodReference m && m.Name == "get_WeaponSoundPlayer"),
        "Rapid gun audio identifies the local native firearm sound player directly"
    );
    Check(
        director.Methods.Any(m => m.Name == "TestGunSound") && director.Methods.Single(m => m.Name == "Panel").Body
            .Instructions.Any(i => i.Operand is string text && text.Contains("Preview alternate gun sound")),
        "F2 has an audio-only alternate gun sound test"
    );
}

Console.WriteLine("AUDIO REPAIR FINAL TOTAL PASS " + checks);
using (var weaponRepairClient = ReadAssembly(args[2]))
{
    var director = weaponRepairClient.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    var assembly = director.Methods.Single(m => m.Name == "AddAssembledWeapon").Body.Instructions;
    Check(
        assembly.Any(i => i.Operand is MethodReference m && m.Name == "BakeMesh"),
        "Phantom weapons snapshot skinned attachment meshes in assembled pose"
    );
    Check(
        director.Methods.Single(m => m.Name == "UpdateVisual").Body.Instructions.Any(i => i.Operand is MethodReference m
            && m.Name == "UpdateWeaponAnchor"),
        "Phantom weapon assembly follows its copied hand each update"
    );
    Check(
        director.Methods.Single(m => m.Name == "Destroy").Body.Instructions.Any(i => i.Operand is FieldReference f && f
            .Name == "OwnedMesh"),
        "Phantom cleanup releases owned baked weapon meshes"
    );
}

Console.WriteLine("WEAPON ASSEMBLY FINAL TOTAL PASS " + checks);
using (var loadingRepairClient = ReadAssembly(args[2]))
{
    var director = loadingRepairClient.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    Check(
        director.Methods.Single(m => m.Name == "TickAudioPreparation").Body.Instructions.Any(i => i
            .Operand is MethodReference m && m.Name == "LoadAudioData"),
        "Audio preparation explicitly requests native clip data before requiring Loaded state"
    );
    var discovery = director.Methods.Single(m => m.Name == "TickFireworkAudioDiscovery").Body.Instructions.Select(i =>
         i.Operand).OfType<MethodReference>().ToArray();
    Check(
        discovery.Any(m => m.Name == "LoadAssetAsync") && !discovery.Any(m => m.Name == "LoadAllAssets" || m
            .Name == "FindObjectsOfTypeAll"),
        "Firework fallback asynchronously requests named native audio assets without a whole-object or load-all scan"
    );
    Check(
        director.Methods.Single(m => m.Name == "ClearAudioPreparation").Body.Instructions.Any(i => i
            .Operand is FieldReference f && f.Name == "gunPreviewPending"),
        "Stopping the event cancels a pending automatic sound preview"
    );
}

Console.WriteLine("AUDIO LOADING FINAL TOTAL PASS " + checks);
using (var grenadeRepairClient = ReadAssembly(args[2]))
{
    var director = grenadeRepairClient.MainModule.Types.Single(t => t.Name == "SchizophreniaDirector");
    var grenadeRootCalls = director.Methods.Single(m => m.Name == "GrenadeRenderRoot").Body.Instructions;
    Check(
        grenadeRootCalls.Any(i => i.Operand is FieldReference field && field.Name == "GrenadeItself") && grenadeRootCalls
            .Any(i => i.Operand is MethodReference method && method.Name == "get_WeaponObject"),
        "Grenade visuals follow native throwable and held-object references outside the wrapper hierarchy"
    );
    Check(
        director.Methods.Single(m => m.Name == "Grenade").Body.Instructions.Any(i => i.Operand is MethodReference method
            && method.Name == "CreateVisual"),
        "Grenade scare still creates render-only copies rather than a real throwable"
    );
}

Console.WriteLine("GRENADE ROOT FINAL TOTAL PASS " + checks);
using (var releaseClient = ReadAssembly(args[2]))
{
    var types = releaseClient.MainModule.Types;
    var behavior = types.Single(t => t.Name == "RaidBehaviorDirector");
    var mortarCalls = new[]
    {
        behavior
    }.Concat(behavior.NestedTypes).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body
        .Instructions).Select(i => i.Operand).OfType<MethodReference>().ToArray();
    Check(
        mortarCalls.Any(m => m.Name == "StartEventShelling") && mortarCalls.Any(m => m.Name == "StartShellingPosition"),
        "Blue Day uses native fixed and flare-callable mortar scheduling"
    );
    Check(
        types.Single(t => t.Name == "LocalRaidDirector").Methods.Any(m => m.Name == "FillEventPopulation"),
        "Outbreak and Overrun share live population replenishment"
    );
    var loot = types.Single(t => t.Name == "HighValueRoomDirector");
    Check(
        loot.Methods.Single(m => m.Name == "Populate").Body.Instructions.Any(i => i.Operand is MethodReference m && m
            .Name == "SpawnLootItem"),
        "Keyed rooms create real native loot rather than visual copies"
    );
    Check(
        loot.Methods.Single(m => m.Name == "Unlocked").Body.Instructions.Any(i => i.Operand is FieldReference m && m.Name == "KeyId"),
        "High-value rooms require a keyed door"
    );
    var panel = types.Single(t => t.Name == "SchizophreniaDirector").Methods.Single(m => m.Name == "Panel").Body
        .Instructions.Select(i => i.Operand).OfType<string>().ToArray();
    Check(
        !panel.Any(text => text.Contains("boss scare") || text.Contains("Flaming AI") || text.Contains("Sky flares")
            || text.Contains("visible attacker") || text.Contains("Lightkeeper") || text.Contains("fills view")),
        "Removed scares are absent from the F2 panel"
    );
    Check(
        types.Single(t => t.Name == "LockedLootNativeUnlock").CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatch"),
        "Locked Loot hooks the native unlock coroutine entry point"
    );
}

Check(
    game.MainModule.Types.Single(t => t.FullName == "EFT.InventoryLogic.Grid").Fields.Any(f => f.Name == "_filters"
        && f.FieldType.FullName.EndsWith("ItemFilter[]")),
    "Native backpack filter field exists for isolated raid-only overrides"
);
using (var pickupAssembly = ReadAssembly(args[2]))
{
    var pickup = pickupAssembly.MainModule.Types.Single(t => t.Name == "LockedLootPickup");
    var calls = pickup.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand)
        .OfType<MethodReference>().ToArray();
    Check(
        calls.Any(m => m.Name == "Move") && calls.Any(m => m.Name == "CanExecute") && calls.Any(m => m.Name == "OwnsSpawnedItem"),
        "Case take fallback is restricted to event loot and native validated backpack moves"
    );
}

using (var lifecycleAssembly = ReadAssembly(args[2]))
{
    var populate = lifecycleAssembly.MainModule.Types.Single(t => t.Name == "HighValueRoomDirector").Methods
        .Single(m => m.Name == "Populate");
    var calls = populate.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
    Check(
        calls.Any(m => m.Name == "DestroyLoot") && !calls.Any(m => m.DeclaringType.FullName == "UnityEngine.Object" && m
            .Name == "Destroy"),
        "Replaced loot uses native pool return without destroying pooled objects"
    );
    var kill = game.MainModule.Types.Single(t => t.FullName == "EFT.Interactive.LootItem").Methods.Single(m => m.Name == "Kill");
    Check(
        kill.Body.Instructions.Any(i => i.Operand is MethodReference method && method.Name == "ReturnToPool"),
        "Native LootItem.Kill returns the object to Tarkov's asset pool"
    );
}

Console.WriteLine("RELEASE CANDIDATE FINAL TOTAL PASS " + checks);
using (var gauntletClient = ReadAssembly(args[2]))
{
    var types = gauntletClient.MainModule.Types;
    var director = types.Single(type => type.Name == "GauntletDirector");
    Check(
        types.Single(type => type.Name == "RaidChoice").Fields.Any(field => field.Name == "Extreme_Trials"),
        "All three gauntlet choices are compiled into the selector"
    );
    Check(
        director.Methods.Single(method => method.Name == "Stop").Body.Instructions.Any(instruction => instruction
            .Operand is MethodReference method && method.Name == "remove_OnPlayerDeadStatic"),
        "Gauntlet cleanup unsubscribes the native death callback"
    );
    Check(
        director.Methods.Single(method => method.Name == "HelmetsAdjacent").Body.Instructions.Any(instruction =>
             instruction.Operand is MethodReference method && method.DeclaringType.Name == "GauntletPolicy" && method.Name == "Adjacent"),
        "Helmet easter egg uses real inventory grid adjacency"
    );
    Check(types.Any(type => type.Name == "GauntletPursuit"), "Multiple hunters have independent pursuit routes");
    Check(
        !types.Any(type => type.Name == "FencePreventNativeHandIn"),
        "Fence does not rewrite quest SetStatus transitions and re-enter the native condition loop"
    );
    foreach (string name in new[]
    {
        "FenceLoadingStatusNotification",
        "FenceLoadingConditionNotification"
    }

    )
    {
        var notificationGate = types.Single(type => type.Name == name);
        Check(
            notificationGate.CustomAttributes.Any(attribute => attribute.AttributeType.Name == "HarmonyPatch")
                && notificationGate.Methods.Single(method => method.Name == "Prefix").ReturnType.FullName == "System.Boolean",
            name + " gates presentation without rewriting a quest state"
        );
    }

    foreach (string patchName in new[]
    {
        "PsychBtrAim",
        "PsychBtrTurretSafety",
        "PsychBtrShotSafety"
    }

    )
    {
        Check(
            types.Any(type => type.Name == patchName && type.CustomAttributes.Any(attribute => attribute.AttributeType
                .Name == "HarmonyPatch")),
            patchName + " declares its native BTR hook"
        );
    }

    var btrShot = types.Single(type => type.Name == "PsychBtrShotSafety").Methods.Single(method => method.Name == "Prefix");
    Check(
        btrShot.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method && method.DeclaringType
            .Name == "ShootData" && method.Name == "EndShoot"),
        "BTR firing guard releases existing fire before rejecting a new shot"
    );
    var pursuitTick = types.Single(type => type.Name == "GauntletPursuit").Methods.Single(method => method.Name == "Tick");
    Check(
        pursuitTick.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method && method
            .DeclaringType.Name == "ZryachiyEnemyInfo" && method.Name == "SetGetHitFrom"),
        "Trials hunters remove Lighthouse retaliation-only firing restriction through native hostility"
    );
}

using (var menuRepair = ReadAssembly(args[2]))
{
    var types = menuRepair.MainModule.Types;
    foreach (var location in new[]
    {
        (Type: "RaidEventPicker", Method: "Draw"),
        (Type: "RaidChanceSettings", Method: "DrawMix")
    }

    )
    {
        var method = types.Single(type => type.Name == location.Type).Methods.Single(method => method.Name == location.Method);
        Check(
            method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "BeginScrollView"),
            location.Type + " has its own bounded scroll view"
        );
        Check(
            method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "TextField"),
            location.Type + " exposes text search/input"
        );
    }

    Check(
        types.Single(type => type.Name == "RaidChanceSettings").Methods.Single(method => method.Name == "Snapshot").Body
            .Instructions.Any(instruction => instruction.Operand is MethodReference call && call.DeclaringType
            .Name == "EventMixPolicy" && call.Name == "Create"),
        "Custom menu uses tested independent Normal-chance and event-weight calculation"
    );
}

using (var layoutRepair = ReadAssembly(args[2]))
{
    foreach (var location in new[]
    {
        (Type: "RaidChanceSettings", Method: "DrawMix"),
        (Type: "RaidEventPicker", Method: "Draw")
    }

    )
    {
        var method = layoutRepair.MainModule.Types.Single(type => type.Name == location.Type).Methods.Single(method =>
             method.Name == location.Method);
        foreach (string group in new[]
        {
            "Horizontal",
            "Vertical",
            "ScrollView"
        }

        )
        {
            int Count(string name) => method.Body.Instructions.Count(instruction => instruction
                .Operand is MethodReference call && call.DeclaringType.Name == "GUILayout" && call.Name == name);
            Check(Count("Begin" + group) == Count("End" + group), location.Type + " balances " + group + " layout groups");
        }
    }
}

using (var exclusivityRepair = ReadAssembly(args[2]))
{
    foreach (string name in new[]
    {
        "GauntletOrdinaryWaveGate",
        "GauntletQueuedActivationGate"
    }

    )
    {
        var prefix = exclusivityRepair.MainModule.Types.Single(type => type.Name == name).Methods.Single(method => method.Name == "Prefix");
        Check(
            prefix.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "Managed"),
            name + " checks native creation-data ownership"
        );
        Check(
            prefix.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "StopSpawn"),
            name + " cancels excluded native creation data"
        );
    }
}

using (var fencePresentation = ReadAssembly(args[2]))
{
    var nativeAccept = game.MainModule.Types.Single(type => type.FullName == "EFT.Quests.QuestControllerClientBackend")
        .Methods.Single(method => method.Name == "AcceptQuest" && method.Parameters.Count == 2);
    Check(
        nativeAccept.ReturnType is GenericInstanceType resultTask && resultTask.ElementType
            .FullName == "System.Threading.Tasks.Task`1" && resultTask.GenericArguments.Single().Name == "IResult",
        "Fence accept presentation wraps the native async result"
    );
    var presentation = fencePresentation.MainModule.Types.Single(type => type.Name == "FenceSelectionPresentation");
    Check(
        presentation.CustomAttributes.Any(attribute => attribute.AttributeType.Name == "HarmonyPatch"),
        "Fence selection presentation targets a native method"
    );
    var stateMachine = presentation.NestedTypes.Single(type => type.Name.Contains("FinishAccept"));
    var calls = stateMachine.Methods.Single(method => method.Name == "MoveNext").Body.Instructions
        .Select(instruction => instruction.Operand).OfType<MethodReference>().ToArray();
    Check(calls.Any(call => call.Name == "get_Succeed"), "Fence UI reconciliation requires a successful accept reply");
    Check(
        calls.Any(call => call.Name == "SetStatus" && call.Parameters.Any(parameter => parameter.ParameterType.Name == "EQuestStatus")),
        "Fence UI reconciliation uses native quest presentation state"
    );
}

// These checks validate native API contracts and presentation-only scope, not
// visible gameplay, glyph availability or runtime animation.
using (var extractRepair = ReadAssembly(args[2]))
{
    var nativePanel = game.MainModule.Types.Single(type => type.FullName == "EFT.UI.ExtractionTimersPanel");
    Check(
        nativePanel.Methods.Any(method => method.Name == "ShowTimer" && method.Parameters.Count == 2),
        "Extract illusion targets native two-argument ShowTimer"
    );
    var nativeRow = game.MainModule.Types.Single(type => type.FullName == "EFT.UI.BattleTimer.ExitTimerPanel");
    Check(nativeRow.Fields.Any(field => field.Name == "_pointName"), "Extract illusion uses native destination label");
    var effect = extractRepair.MainModule.Types.Single(type => type.Name == "PsychExtractionMenu");
    Check(
        !effect.Methods.Where(method => method.HasBody).SelectMany(method => method.Body.Instructions).Any(instruction =>
             instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && instruction.Operand is FieldReference field && field
            .DeclaringType.Name.Contains("Exfiltration")),
        "Extract illusion does not write native extraction fields"
    );
}

using (var patrolAssembly = ReadAssembly(args[2]))
{
    var drive = patrolAssembly.MainModule.Types.Single(type => type.Name == "LabStaffBehaviour")
        .Methods.Single(method => method.Name == "DriveHub");
    Check(drive.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method
        && method.DeclaringType.Name == "LabStaffHubs" && method.Name == "TryPatrol"),
        "Lab staff movement selects cross-office patrol routes");
    var planner = patrolAssembly.MainModule.Types.Single(type => type.Name == "LabStaffHubs")
        .Methods.Single(method => method.Name == "TryPatrol");
    Check(planner.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method
        && method.Name == "CalculatePath")
        && planner.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method
            && method.Name == "HasUnlockedExit"),
        "Cross-office patrol validates native navigation and locked door exits");
}

using (var staffLife = ReadAssembly(args[2]))
{
    var staff = staffLife.MainModule.Types.Single(type => type.Name == "LabStaffBehaviour");
    var chatter = staff.Methods.Single(method => method.Name == "Converse");
    Check(chatter.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method
        && method.DeclaringType.FullName == "EFT.Player" && method.Name == "Say"),
        "Staff proximity chatter uses native player speech");
    var patrol = staff.Methods.Single(method => method.Name == "DriveHub");
    Check(patrol.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method
        && method.Name == "MovementPause")
        && !patrol.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method
            && method.Name == "Teleport"),
        "Staff rests pause native movement without teleporting");
}
