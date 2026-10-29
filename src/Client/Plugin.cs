using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RaidRoulette.TestingClient;

public enum RaidChoice
{
    Random, Normal, Loot_Surge, Boss_Hunt, Boss_Convention, High_Value_Loot,
    Raider_Assault, Cultist_Invasion, Scav_Concentrate, Pmc_Sweep, Get_Handy,
    I_Have_A_Shot, Marksman, Extreme_Overwatch, Explosive_Scav_Units, Outbreak,
    Geared, Overloaded, Matrix_Glitch, No_Safe_Space, Posers_Everywhere, Fake_Cult, Overrun, Clone_Wars, The_Gift, Smokers_Lobby, Time_Warp, Schizophrenia, Gun_Game, Stormtide, AK50, Grenadier, Lab_Work_Day
}
[BepInPlugin("com.killdg.raidroulette.testing", "Raid Roulette", "2.0.39")]
public sealed class Plugin : BaseUnityPlugin
{
    private static Plugin? instance;
    internal static bool ConfirmedCultistRaid => instance?.lastRaid?.Value?.Contains(": cultist_invasion") == true;
    internal static bool TraceEnabled => instance?.diagnostics?.Value == true;
    internal static void Trace(string text) { try { instance?.Logger.LogInfo(text); } catch {} }
    internal static ConfigEntry<bool> GhostReview = null!;
    internal static Player? Local => Singleton<GameWorld>.Instantiated && !(Singleton<GameWorld>.Instance is HideoutGameWorld)
        ? Singleton<GameWorld>.Instance.MainPlayer : null;
    private ConfigEntry<RaidChoice> choice = null!;
    private ConfigEntry<bool> force = null!;
    private readonly RaidChanceSettings chances=new();
    private string? queuedEvent,queueId;
    private volatile bool queueConsumed;
    private ConfigEntry<string> status = null!, serverSelection = null!, lastRaid = null!;
    private string? pendingSelection, pendingRaid;
    private float nextStatusCheck, nextDirectorError;
    private volatile bool busy;
    private string? pendingStatus;
    private readonly System.Collections.Generic.List<Harmony> patchGroups=new();
    private readonly AutoApplyTracker autoApply = new();
    private readonly LiveBotDiagnostics liveDiagnostics = new();
    private readonly LabWorkDirector labWork = new();
    private readonly LocalRaidDirector director = new();
    private readonly SmokersDirector smokers = new();
    private readonly TimeWarpDirector timeWarp = new();
    private readonly RaidEventNotifier raidNotice = new();
    private readonly BossHuntDirector bossHunt = new();
    private readonly SchizophreniaDirector schizophrenia = new();
    private readonly StormtideDirector stormtide = new();
    private readonly GunGameDirector gunGame = new();
    private readonly GrenadierDirector grenadier = new();
    private readonly RaidRoster roster = new();
    private readonly LiveRaidMonitor monitor = new();
    private readonly GiftDirector gift = new();
    internal static string RaidContext => instance?.lastRaid?.Value ?? "";
    internal static int StartupTarget,StartupCap;
    internal static string HuntBossRole="";
    internal static string[] OverwatchZones=Array.Empty<string>();
    internal static RooftopSlot[] OverwatchPoints=Array.Empty<RooftopSlot>();
    internal static int OverwatchPerZone,OverwatchCap;
    internal static ConfigEntry<int> WaveInterval = null!, WaveSize = null!, AliveCap = null!;
    internal static ConfigEntry<bool> BossRoaming = null!;
    private ConfigEntry<bool> diagnostics = null!;
    private void Awake()
    {
        instance = this;
        ConfigurationImport.Import(Config,message=>Logger.LogInfo(message));
        AdvancedMenu.Initialize(Config);
        chances.Bind(Config,()=>autoApply.Changed());
        GunGameRespawn.Bind(Config);
        LabStaffExperience.Bind(Config);
        stormtide.Bind(Config);
        raidNotice.Bind(Config);
        roster.Bind(Config,()=>director.Status+timeWarp.Status+bossHunt.Status+schizophrenia.Status+stormtide.Status+gunGame.Status+(GiftDirector.Active?"\n"+gift.Status:""));
        monitor.Bind(Config,roster,()=>director.Status+timeWarp.Status+bossHunt.Status+schizophrenia.Status+stormtide.Status+gunGame.Status+(GiftDirector.Active?"\n"+gift.Status+"\n"+gift.Details:""));
        choice = AdvancedMenu.Bind(Config,"1. Next raid", "Event", RaidChoice.Random, "Choose a type, then use Queue selected raid once. It applies to the next generated raid only. Unsupported maps fall back to Normal. Random uses your percentages.");
        force = MenuAction.Bind(Config,"1. Next raid","Queue selected raid once","Queue the selected type for ONE raid only. Map/asset restrictions still apply; then automatic chances resume. Queues last for this server session.",()=>
        {queuedEvent=choice.Value==RaidChoice.Random?"random":choice.Value.ToString().ToLowerInvariant();queueId=Guid.NewGuid().ToString("N");autoApply.Changed();},new ConfigurationManagerAttributes{HideDefaultButton=true,CustomDrawer=entry=>{if(GUILayout.Button("Queue next raid"))entry.BoxedValue=true;}});

        status = AdvancedMenu.Bind(Config,"1. Next raid", "Selection status", "Not contacted yet", new ConfigDescription("Read the backend acknowledgement here. Editing this text has no effect.", null, new ConfigurationManagerAttributes { ReadOnly = true }));
        serverSelection = AdvancedMenu.Bind(Config,"9. Advanced / Selection", "Server selection", "Waiting for backend", new ConfigDescription("The selection currently acknowledged by the server. Checked automatically.", null, new ConfigurationManagerAttributes { ReadOnly = true }));
        lastRaid = AdvancedMenu.Bind(Config,"9. Advanced / Selection", "Last generated raid", "Not checked yet", new ConfigDescription("Actual selected event for the last generated raid; does not verify that AI spawned.", null, new ConfigurationManagerAttributes { ReadOnly = true }));
        autoApply.Changed(); // synchronize the saved force toggle and event on game launch
        choice.SettingChanged += (_, _) => autoApply.Changed();
        diagnostics = AdvancedMenu.Bind(Config,"9. Advanced / Diagnostics", "Log spawned bot roles and weapons", false, "Diagnostic build: sample registered AI once per second and record actual roles/weapon IDs in BepInEx/LogOutput.log. Does not change bots. Enable for Cultist Invasion and Explosive Scav Units tests.");
        GhostReview = AdvancedMenu.Bind(Config,"9. Advanced / Local player testing", "Ghost Review", false,
            "Local solo review mode: invisible to ordinary AI targeting, damage shield, unlimited sprint/jump and arm stamina, and 1.5x sprint speed. Switch off to restore ordinary behavior. AI overhaul detection and scripted/environmental deaths are not guaranteed. Always starts off when launching the game.");
        GhostReview.Value = false;
        WaveInterval = AdvancedMenu.Bind(Config,"4. Overrun", "Wave interval seconds", 300, new ConfigDescription("Reinforcements every this many seconds after the raid starts. Set 60 for a short test. Frozen at raid start.", new AcceptableValueRange<int>(60,900)));
        WaveSize = AdvancedMenu.Bind(Config,"4. Overrun", "Bots per wave", 0, new ConfigDescription("0 = unique ordinary Scavs/PMCs observed during the first 60 seconds, minimum 1. Bosses/followers excluded. Positive values use a fixed count. Frozen at raid start.", new AcceptableValueRange<int>(0,40)));
        AliveCap = AdvancedMenu.Bind(Config,"4. Overrun", "Maximum alive AI", 35, new ConfigDescription("Counts all living AI. Reinforcement requests wait for capacity; native spawning limits still apply. Frozen at raid start.", new AcceptableValueRange<int>(5,80)));
        BossRoaming = AdvancedMenu.Bind(Config,"9. Advanced / Boss Convention", "Experimental idle roaming", true, "Only Boss Convention: ask idle bosses to choose another native patrol point after standing still. Combat takes priority. No teleporting or forced straight-line movement.");
        status.Value="Initializing client; waiting for current backend acknowledgement";
        serverSelection.Value="Waiting for backend";lastRaid.Value="Not checked yet";
        int ready=0,failed=0;
        foreach(var patch in typeof(Plugin).Assembly.GetTypes().Where(t=>t.IsDefined(typeof(HarmonyPatch),false)).OrderBy(t=>t==typeof(StormtideNativeAudio)?1:0).ThenBy(t=>t.Name))
        {
            var group=new Harmony("com.killdg.raidroulette.testing."+patch.Name);
            try{group.CreateClassProcessor(patch).Patch();patchGroups.Add(group);ready++;Logger.LogInfo("[RR-INIT] enabled="+patch.Name);}
            catch(Exception e)
            {
                failed++;Logger.LogError("[RR-INIT] failed="+patch.Name+" "+e);
                try{group.UnpatchSelf();}catch(Exception cleanup){Logger.LogError("[RR-INIT] cleanup-failed="+patch.Name+" "+cleanup.GetBaseException().Message);}
            }
        }
        Logger.LogInfo($"Raid event patches enabled. [RR-INIT] ready={ready} failed={failed}; F12 > Raid Roulette. Not a Fika build.");
    }
    private void OnGUI(){monitor.Draw();GunGameRespawn.Draw();}
    private void Update()
    {
        AdvancedMenu.Tick();
        if(!AdvancedMenu.Enabled&&GhostReview.Value)GhostReview.Value=false;
        if(queueConsumed){queueConsumed=false;queuedEvent=null;queueId=null;}
        GunGameRespawn.Tick();
        monitor.Tick();
        raidNotice.Tick();
        if (pendingStatus != null) { status.Value = pendingStatus; Logger.LogInfo(pendingStatus); pendingStatus = null; }
        if (autoApply.Observe(Time.unscaledTime)) status.Value = "Selection changed — waiting to send...";
        if (autoApply.Ready(Time.unscaledTime, busy)) Send(true);
        if (pendingSelection != null) { if (serverSelection.Value != pendingSelection) Logger.LogInfo("Server selection: " + pendingSelection); serverSelection.Value = pendingSelection; pendingSelection = null; }
        if (pendingRaid != null) { if (lastRaid.Value != pendingRaid) Logger.LogInfo("Last generated raid: " + pendingRaid); lastRaid.Value = pendingRaid; pendingRaid = null; }
        if (!busy && !autoApply.HasPending && Time.unscaledTime >= nextStatusCheck) Send(false);
        try{labWork.Tick();}catch(Exception e){if(Time.unscaledTime>=nextDirectorError){nextDirectorError=Time.unscaledTime+30;Plugin.Trace("[RR-LAB] update error="+e.GetBaseException().Message);}} gift.Tick(); smokers.Tick(); timeWarp.Tick(); bossHunt.Tick(); schizophrenia.Tick(); stormtide.Tick(); gunGame.Tick(); grenadier.Tick();
        roster.Tick();
        if (AdvancedMenu.Enabled&&diagnostics.Value) liveDiagnostics.Tick(Logger, lastRaid.Value);
        try { director.Tick(); } catch(Exception e) { if(Time.unscaledTime>=nextDirectorError){nextDirectorError=Time.unscaledTime+30;Logger.LogWarning("[RR-WAVE] director-error: "+e.GetBaseException().Message);} }
        var player = Local;
        if (player == null || !player.IsYourPlayer || !GhostReview.Value || player.Physical == null) return;
        Refill(player.Physical.Stamina);
        Refill(player.Physical.HandsStamina);
    }
    private static void Refill(Stamina stamina)
    {
        if (stamina == null) return;
        stamina.Current = stamina.TotalCapacity.Value;
        stamina.Overuse = 0;
    }
    private void Send(bool apply)
    {
        if (busy) return;
        busy = true;
        var revision = autoApply.Revision;
        if (apply) autoApply.Dispatched(revision);
        nextStatusCheck = Time.unscaledTime + 10f;
        if (apply)
        {
            status.Value = "Contacting backend...";
            Logger.LogInfo("Sending chance settings; one-shot choice="+(queuedEvent??"none"));
        }
        string expectedEvent=queuedEvent??"random";
        bool queue=queuedEvent!=null;
        var request=new JObject{["action"]=apply?(queue?"apply":"settings"):"status",["event"]=expectedEvent,["repeat"]=false,["allowDisabled"]=true,["queueId"]=queueId,["chances"]=JObject.FromObject(chances.Snapshot())}.ToString();
        Task.Run(async () =>
        {
            try
            {
                var text = await Backend.Post(request);
                var data = JObject.Parse(text);
                // Accept raw mod responses, or the standard SPT response envelope.
                if (data["data"] is JObject body) data = body;
                if (revision == autoApply.Revision)
                {
                    bool ok = data["ok"]?.Value<bool>() == true;
                    string message = data["message"]?.ToString() ?? "No acknowledgement";
                    if (apply && queue && ok && !EventSelection.Matches(expectedEvent, message)) ok = false;
                    if(ok&&queue&&data["queuePending"]?.Value<bool>()==false)queueConsumed=true;
                    pendingStatus=ok?(queue&&data["queuePending"]?.Value<bool>()==true?"Next raid queued: "+expectedEvent:"Automatic raid percentages active"):"Rejected: "+message;
                    int end = message.IndexOf(". Map-", StringComparison.Ordinal);
                    pendingSelection = end < 0 ? message : message.Substring(0, end);
                    pendingRaid = data["lastRaid"]?.ToString() ?? "Not reported";
                    StartupTarget=data["startupTarget"]?.Value<int>()??0;
                    StartupCap=data["startupCap"]?.Value<int>()??0;
                    HuntBossRole=data["huntBossRole"]?.ToString()??"";
                    OverwatchPerZone=data["overwatchPerZone"]?.Value<int>()??0;
                    OverwatchCap=data["overwatchCap"]?.Value<int>()??0;
                    OverwatchZones=data["overwatchZones"]?.ToObject<string[]>()??Array.Empty<string>();
                    OverwatchPoints=data["overwatchPoints"]?.ToObject<RooftopSlot[]>()??Array.Empty<RooftopSlot>();
                }
            }
            catch (Exception e) { if (revision == autoApply.Revision) pendingStatus = "Backend request failed: " + e.GetBaseException().Message + ". Check that preview.2 is installed and backend is running."; }
            finally { busy = false; }
        });
    }
    internal static bool IsLocal(IPlayer? player) => player != null && Local != null && player.ProfileId == Local.ProfileId;
    internal static bool Hidden(IPlayer? player) => ((GhostReview.Value || GunGameRespawn.Protected) && IsLocal(player)) || GiftDirector.IsRunner(player);
    private void OnDestroy() { try{labWork.Stop();GunGameRespawn.Stop();gunGame.Stop();stormtide.Dispose();schizophrenia.Stop();timeWarp.Stop();}finally{foreach(var group in patchGroups)try{group.UnpatchSelf();}catch(Exception e){Logger.LogWarning("[RR-INIT] shutdown-cleanup "+e.GetBaseException().Message);}} }
}
internal static class Backend
{
    // Reuse the launcher's SPT session, address and TLS handling. No hard-coded localhost URL.
    public static async Task<string> Post(string json,string path="/client/raidroulette/testing")
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SPT.Common.Http.RequestHandler", false)).FirstOrDefault(t => t != null)
            ?? throw new InvalidOperationException("SPT RequestHandler was not loaded");
        var method = type.GetMethod("PostJsonAsync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null)
            ?? throw new MissingMethodException("SPT RequestHandler.PostJsonAsync(string, string) not found");
        var task = (Task<string>)method.Invoke(null, new object[] { path, json })!;
        var result = await task.ConfigureAwait(false);
        return result;
    }
}
[HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.IsEnemy), new[] { typeof(IPlayer) })]
internal static class IsEnemyPatch
{
    private static bool Prefix(BotsGroup __instance,IPlayer __0, ref bool __result) { if (!Plugin.Hidden(__0)||(BossHuntDirector.IsGroup(__instance)&&Plugin.IsLocal(__0))) return true; __result = false; return false; }
}
[HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.AddEnemy), new[] { typeof(IPlayer), typeof(EBotEnemyCause) })]
internal static class AddEnemyPatch
{
    private static bool Prefix(BotsGroup __instance,IPlayer __0, ref bool __result) { if (!Plugin.Hidden(__0)||(BossHuntDirector.IsGroup(__instance)&&Plugin.IsLocal(__0))) return true; __result = false; return false; }
}
[HarmonyPatch(typeof(EnemyInfo), "get_IsVisible")]
internal static class VisionPatch
{
    private static void Postfix(EnemyInfo __instance, ref bool __result) { if (Plugin.Hidden(__instance.Person)&&!BossHuntDirector.IsTarget(__instance.Owner,__instance.Person)) __result = false; }
}
[HarmonyPatch(typeof(EnemyInfo), "get_CanShoot")]
internal static class ShootPatch
{
    private static void Postfix(EnemyInfo __instance, ref bool __result) { if (Plugin.Hidden(__instance.Person)&&!BossHuntDirector.IsTarget(__instance.Owner,__instance.Person)) __result = false; }
}
[HarmonyPatch(typeof(BotMemory), "get_GoalEnemy")]
internal static class GoalPatch
{
    private static void Postfix(BotMemory __instance,ref EnemyInfo? __result) { if (GiftDirector.IsRunner(__instance._owner?.GetPlayer) || (__result != null && Plugin.Hidden(__result.Person)&&!BossHuntDirector.IsTarget(__instance._owner,__result.Person))) __result = null; }
}
[HarmonyPatch(typeof(Player), nameof(Player.ApplyDamageInfo))]
internal static class DamagePatch
{
    private static bool Prefix(Player __instance) => !((Plugin.GhostReview.Value || GunGameRespawn.Protected) && Plugin.IsLocal(__instance));
}
[HarmonyPatch(typeof(Player), nameof(Player.ReceiveDamage))]
internal static class ReceivePatch
{
    private static bool Prefix(Player __instance) => !((Plugin.GhostReview.Value || GunGameRespawn.Protected) && Plugin.IsLocal(__instance));
}
[HarmonyPatch(typeof(MovementContext), "get_SprintSpeed")]
internal static class SpeedPatch
{
    private static void Postfix(MovementContext __instance, ref float __result)
    {
        var player = Plugin.Local;
        if (player != null && player.IsYourPlayer && ReferenceEquals(player.MovementContext, __instance)) __result *= Plugin.GhostReview.Value ? 1.5f : 1f;
    }
}
[HarmonyPatch(typeof(BotMemory), "get_HaveEnemy")]
internal static class HasEnemyPatch
{
    private static void Postfix(BotMemory __instance, ref bool __result)
    {
        // GoalEnemy is filtered by GoalPatch; keep the paired presence check consistent.
        if ((Plugin.GhostReview.Value || GunGameRespawn.Protected || GiftDirector.Active) && __instance.GoalEnemy == null) __result = false;
    }
}


















