using Killdg.DeveloperTools;
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

namespace RaidRoulette.TestingClient
{
    public enum RaidChoice
    {
        Random,
        Normal,
        Boss_Run_Down,
        Boss_Convention,
        Locked_Loot,
        Raider_Assault,
        Cultist_Invasion,
        Scav_Concentrate,
        Pmc_Sweep,
        Get_Handy,
        I_Have_A_Shot,
        Marksman,
        Explosive_Scav_Units,
        Outbreak,
        Geared,
        Guarded,
        Overloaded,
        Matrix_Glitch,
        No_Safe_Space,
        Posers_Everywhere,
        Fake_Cult,
        Overrun,
        Clone_Wars,
        The_Thief,
        Smokers_Lobby,
        Time_Warp,
        Schizophrenia,
        Gun_Game,
        Stormtide,
        AK50,
        Grenadier,
        Lab_Work_Day,
        Blue_Day,
        Snowball_Trials,
        Trials,
        Extreme_Trials,
        Guard_Trials,
        Total_Trial,
        Spring,
        Summer,
        Autumn,
        Winter,
        Wrong_Door,
        Infrastructure_Decay
    }

    [BepInDependency(DeveloperSettings.PluginId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("com.killdg.raidroulette.testing", "Raid Roulette", "2.0.146")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin? instance;
        internal static bool ConfirmedCultistRaid => instance?.lastRaid?.Value?.Contains(": cultist_invasion") == true;
        internal static bool TraceEnabled => DeveloperSettings.Available && instance?.diagnostics?.Value == true;

        internal static void Trace(string text)
        {
            try
            {
                if (TraceEnabled)
                {
                    instance?.Logger.LogInfo(text);
                }
            }
            catch
            {
            }
        }

        internal static ConfigEntry<bool> GhostReview = null!;
        internal static Player? Local => Singleton<GameWorld>.Instantiated && !(Singleton<GameWorld>
            .Instance is HideoutGameWorld) ? Singleton<GameWorld>.Instance.MainPlayer : null;

        private ConfigEntry<RaidChoice> choice = null!;
        private ConfigEntry<bool> force = null!;
        private ConfigEntry<bool> modifiersEnabled = null!;
        private ConfigEntry<bool> traderEventActivation = null!;
        private readonly RaidChanceSettings chances = new();
        private string? queuedEvent;
        private string? queueId;
        private volatile bool queueConsumed;
        private ConfigEntry<string> status = null!;
        private ConfigEntry<string> serverSelection = null!;
        private ConfigEntry<string> lastRaid = null!;
        private string? pendingSelection;
        private string? pendingRaid;
        private int pendingRaidWorldRevision;
        private int worldRevision;
        private int confirmedWorldRevision = -1;
        private bool raidStarted;
        private float nextStatusCheck;
        private GameWorld? statusWorld;
        private int networkFailures;
        private FeatureUpdateRunner featureUpdates = null!;
        private volatile bool busy;
        private string? pendingStatus;
        private readonly System.Collections.Generic.List<Harmony> patchGroups = new();
        private readonly AutoApplyTracker autoApply = new();
        private readonly LiveBotDiagnostics liveDiagnostics = new();
        private readonly LabWorkDirector labWork = new();
        private readonly LocalRaidDirector director = new();
        private readonly SmokersDirector smokers = new();
        private readonly TimeWarpDirector timeWarp = new();
        private readonly RaidEventNotifier raidNotice = new();
        private readonly BossHuntDirector bossHunt = new();
        private readonly GauntletDirector gauntlet = new();
        private readonly SchizophreniaDirector schizophrenia = new();
        private readonly StormtideDirector stormtide = new();
        private readonly GunGameDirector gunGame = new();
        private readonly InfrastructureDecayDirector infrastructureDecay = new();
        private readonly GrenadierDirector grenadier = new();
        private readonly RaidBehaviorDirector raidBehaviors = new();
        private readonly HighValueRoomDirector highValueRooms = new();
        private readonly DeveloperQuickTravel quickTravel = new();
        private readonly DoorTeleportDirector doorTeleports = new();
        private readonly RaidRoster roster = new();
        private readonly LiveRaidMonitor monitor = new();
        private readonly GiftDirector gift = new();
        // A status response from the menu describes the previous raid. Require
        // a response requested after deployment before enabling raid effects.
        // GameWorld is created during loading, before the backend generates this raid.
        internal static string RaidContext => instance == null || (instance.statusWorld != null && (!instance.raidStarted
            || instance.confirmedWorldRevision != instance.worldRevision)) ? "" : instance.lastRaid?.Value ?? "";

        internal static int StartupTarget;
        internal static bool FenceSelectionActive => instance?.modifiersEnabled?.Value == true && instance
            .traderEventActivation?.Value == true;

        internal static void RefreshFenceMenus()
        {
            if (instance != null)
            {
                instance.nextStatusCheck = 0;
            }
        }

        internal static int StartupCap;
        internal static string HuntBossRole = "";
        internal static string[] OverwatchZones = Array.Empty<string>();
        internal static RooftopSlot[] OverwatchPoints = Array.Empty<RooftopSlot>();
        internal static int OverwatchPerZone;
        internal static int OverwatchCap;
        internal static ConfigEntry<int> WaveInterval = null!;
        internal static ConfigEntry<int> WaveSize = null!;
        internal static ConfigEntry<int> AliveCap = null!;
        internal const bool BossRoaming = true;
        private ConfigEntry<bool> diagnostics = null!;
        private void Awake()
        {
            DeveloperSettings.Initialize("Raid Roulette");
            instance = this;
            ConfigurationImport.Import(Config, message => Logger.LogInfo(message));
            RaidChoiceNames.Migrate(Config);
            AdvancedMenu.Initialize(Config);
            quickTravel.Bind(Config);
            featureUpdates = new FeatureUpdateRunner((name, error) => Logger.LogWarning("[RR-UPDATE] feature=" + name + " " + error));
            featureUpdates.Add("Quick travel", quickTravel.Tick);
            featureUpdates.Add("Wrong Door", doorTeleports.Tick);
            featureUpdates.Add("Infrastructure Decay", infrastructureDecay.Tick);
            featureUpdates.Add("Lab Work Day", labWork.Tick);
            featureUpdates.Add("The Thief", gift.Tick);
            featureUpdates.Add("Smokers Lobby", smokers.Tick);
            featureUpdates.Add("Time Warp", timeWarp.Tick);
            featureUpdates.Add("Boss Run Down", bossHunt.Tick);
            featureUpdates.Add("Snowball and Trials", gauntlet.Tick);
            featureUpdates.Add("Schizophrenia", schizophrenia.Tick);
            featureUpdates.Add("Stormtide", stormtide.Tick);
            featureUpdates.Add("Gun Game", gunGame.Tick);
            featureUpdates.Add("Grenadier", grenadier.Tick);
            featureUpdates.Add("Raid AI behavior", raidBehaviors.Tick);
            featureUpdates.Add("Locked Loot", highValueRooms.Tick);
            featureUpdates.Add("AI roster", roster.Tick);
            featureUpdates.Add("AI waves", director.Tick);
            featureUpdates.Add(
                "Spawn diagnostics",
                () =>
            {
                if (AdvancedMenu.Enabled && diagnostics.Value)
                {
                    liveDiagnostics.Tick(Logger, lastRaid.Value);
                }
            }
            );
            highValueRooms.Bind(Config);
            traderEventActivation = Config.Bind(
                "0. General",
                "Trader Event Activation",
                false,
                "Use Fence\u0027s RR assignments instead of random/manual raid selection. Accept one, then enter " +
                "an RR-marked eligible PMC map. Outside a raid, accepting another RR ticket replaces the " +
                "queued one. All assignments can be accepted again after each assigned raid. Trials " +
                "requires clearing all required waves for success; incomplete runs fail. Other events " +
                "finish after one assigned raid. With no accepted eligible assignment, the raid is Normal. " +
                "Turn this off to use the manual picker. Reopen Fence after changing this setting."
            );
            FenceMapMarkers.Enabled = traderEventActivation.Value;
            traderEventActivation.SettingChanged += (_, _) =>
            {
                FenceMapMarkers.Enabled = traderEventActivation.Value;
                autoApply.Changed();
            };
            modifiersEnabled = Config.Bind(
                "0. General",
                "Enable Raid Roulette",
                true,
                "Enable standalone random and queued raid modifiers. Off leaves ordinary raids Normal while "
                    + "Task Edition and Advanced Tasks can still select their mission events. Applies to the next "
                    + "raid; the shared engine stays loaded."
            );
            modifiersEnabled.SettingChanged += (_, _) =>
            {
                if (!modifiersEnabled.Value)
                {
                    queuedEvent = null;
                    queueId = null;
                }

                autoApply.Changed();
            };
            chances.Bind(Config, () => autoApply.Changed());
            GunGameRespawn.Bind(Config);
            LabStaffExperience.Bind(Config);
            stormtide.Bind(Config);
            infrastructureDecay.Bind(Config);
            raidNotice.Bind(Config);
            roster.Bind(
                Config,
                () => director.Status + "\n" + gauntlet.Status + timeWarp.Status + bossHunt.Status + schizophrenia.Status
                    + stormtide.Status + gunGame.Status + (GiftDirector.Active ? "\n" + gift.Status : "")
            );
            monitor.Bind(
                Config,
                roster,
                () => director.Status + "\n" + gauntlet.Status + timeWarp.Status + bossHunt.Status + schizophrenia.Status
                    + stormtide.Status + gunGame.Status + (GiftDirector.Active ? "\n" + gift.Status + "\n" + gift.Details : "")
            );
            choice = AdvancedMenu.Bind(
                Config,
                "1. Next raid",
                "Event",
                RaidChoice.Random,
                new ConfigDescription(
                    "Search or scroll the event list, then queue it for one raid. Unsupported maps become Normal.",
                    null,
                    new ConfigurationManagerAttributes { CustomDrawer = RaidEventPicker.Draw }
                )
            );
            force = MenuAction.Bind(
                Config,
                "1. Next raid",
                "Queue selected raid once",
                "Queue the selected type for ONE raid only. Map/asset restrictions still apply; then "
                    + "automatic chances resume. Queues last for this server session.",
                () =>
            {
                if (!modifiersEnabled.Value)
                {
                    status.Value = "Enable Raid Roulette before queuing a standalone event.";
                    return;
                }

                if (traderEventActivation.Value)
                {
                    status.Value = "Fence controls raid selection. Disable Trader Event Activation to queue a manual event.";
                    Logger.LogWarning(status.Value);
                    return;
                }

                queuedEvent = choice.Value == RaidChoice.Random ? "random" : RaidChoiceNames.EventId(choice.Value.ToString());
                queueId = Guid.NewGuid().ToString("N");
                autoApply.Changed();
            },
                new ConfigurationManagerAttributes { HideDefaultButton = true, CustomDrawer = entry =>
            {
                if (GUILayout.Button("Queue next raid"))
                {
                    entry.BoxedValue = true;
                }
            } }
            );
            var killWaveTag = new ConfigurationManagerAttributes
            {
                HideDefaultButton = true,
                CustomDrawer = entry =>
                {
                    if (GUILayout.Button("Kill current Trials wave"))
                    {
                        entry.BoxedValue = true;
                    }
                }
            };
            AdvancedMenu.Adopt(killWaveTag);
            MenuAction.Bind(
                Config,
                "9. Advanced / Trials testing",
                "Kill current Trials wave",
                "Testing only: kill bound Trials actors through native health. Does not skip pending spawns " +
                "or count as player kills. Excludes Snowball.",
                GauntletDirector.KillWaveForTesting,
                killWaveTag
            );
            status = AdvancedMenu.Bind(
                Config,
                "1. Next raid",
                "Selection status",
                "Not contacted yet",
                new ConfigDescription(
                    "Read the backend acknowledgement here. Editing this text has no effect.",
                    null,
                    new ConfigurationManagerAttributes { ReadOnly = true }
                )
            );
            serverSelection = AdvancedMenu.Bind(
                Config,
                "9. Advanced / Selection",
                "Server selection",
                "Waiting for backend",
                new ConfigDescription(
                    "The selection currently acknowledged by the server. Checked automatically.",
                    null,
                    new ConfigurationManagerAttributes { ReadOnly = true }
                )
            );
            lastRaid = AdvancedMenu.Bind(
                Config,
                "9. Advanced / Selection",
                "Last generated raid",
                "Not checked yet",
                new ConfigDescription(
                    "Actual selected event for the last generated raid; does not verify that AI spawned.",
                    null,
                    new ConfigurationManagerAttributes { ReadOnly = true }
                )
            );
            autoApply.Changed(); // synchronize the saved force toggle and event on game launch
            choice.SettingChanged += (_, _) => autoApply.Changed();
            diagnostics = AdvancedMenu.Bind(
                Config,
                "9. Advanced / Diagnostics",
                "Log spawned bot roles and weapons",
                false,
                "Diagnostic build: sample registered AI once per second and record actual roles/weapon IDs "
                    + "in BepInEx/LogOutput.log. Does not change bots. Enable for Cultist Invasion and Explosive " + "Scav Units tests."
            );
            GhostReview = AdvancedMenu.Bind(
                Config,
                "9. Advanced / Local player testing",
                "Ghost Review",
                false,
                "Local solo review mode: invisible to ordinary AI targeting, damage shield, unlimited "
                    + "sprint/jump and arm stamina, and 1.5x sprint speed. Switch off to restore ordinary "
                    + "behavior. AI overhaul detection and scripted/environmental deaths are not guaranteed. "
                    + "Always starts off when launching the game."
            );
            GhostReview.Value = false;
            WaveInterval = AdvancedMenu.Bind(
                Config,
                "4. Overrun",
                "Replacement cooldown seconds",
                3,
                AdvancedMenu.Diagnostic(
                    "Minimum delay between population replacement requests. Overrun triggers below half the map "
                        + "target, rather than on a fixed wave timer. Frozen at raid start.",
                    new AcceptableValueRange<int>(1, 30)
                )
            );
            WaveSize = AdvancedMenu.Bind(
                Config,
                "4. Overrun",
                "Bots per wave",
                0,
                AdvancedMenu.Diagnostic(
                    "0 = unique ordinary Scavs/PMCs observed during the first 60 seconds, minimum 1. "
                        + "Bosses/followers excluded. Positive values use a fixed count. Frozen at raid start.",
                    new AcceptableValueRange<int>(0, 40)
                )
            );
            AliveCap = AdvancedMenu.Bind(
                Config,
                "4. Overrun",
                "Maximum alive AI",
                35,
                AdvancedMenu.Diagnostic(
                    "Counts all living AI. Reinforcement requests wait for capacity; native spawning limits still apply. Frozen at raid start.",



                    new AcceptableValueRange<int>(5, 80)
                )
            );
            status.Value = "Initializing client; waiting for current backend acknowledgement";
            serverSelection.Value = "Waiting for backend";
            lastRaid.Value = "Not checked yet";
            int ready = 0;
            int failed = 0;
            foreach (var patch in typeof(Plugin).Assembly.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false))
                .OrderBy(t => t == typeof(StormtideNativeAudio) ? 1 : 0).ThenBy(t => t.Name))
            {
                var group = new Harmony("com.killdg.raidroulette.testing." + patch.Name);
                try
                {
                    group.CreateClassProcessor(patch).Patch();
                    patchGroups.Add(group);
                    ready++;
                    Logger.LogInfo("[RR-INIT] enabled=" + patch.Name);
                }
                catch (Exception e)
                {
                    failed++;
                    Logger.LogError("[RR-INIT] failed=" + patch.Name + " " + e);
                    try
                    {
                        group.UnpatchSelf();
                    }
                    catch (Exception cleanup)
                    {
                        Logger.LogError("[RR-INIT] cleanup-failed=" + patch.Name + " " + cleanup.GetBaseException().Message);
                    }
                }
            }

            Logger.LogInfo($"Raid event patches enabled. [RR-INIT] ready={ready} failed={failed}; F12 > Raid Roulette. Not a Fika build.");
        }

        private void LateUpdate()
        {
            labWork.LateTick();
            infrastructureDecay.LateTick();
            stormtide.LateTick();
            schizophrenia.LateTick();
        }

        private void OnGUI()
        {
            monitor.Draw();
            highValueRooms.DrawMarkers();
            GunGameRespawn.Draw();
            schizophrenia.Draw();
        }

        private void Update()
        {
            AdvancedMenu.Tick();
            var observedWorld = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (observedWorld is HideoutGameWorld)
            {
                observedWorld = null;
            }

            if (!ReferenceEquals(observedWorld, statusWorld))
            {
                statusWorld = observedWorld;
                raidStarted = false;
                worldRevision++;
                nextStatusCheck = 0;
            }

            if (statusWorld != null && !raidStarted && Singleton<AbstractGame>.Instantiated && Singleton<AbstractGame>.Instance
                .Status == GameStatus.Started)
            {
                // Invalidate any response requested during loading. The normal
                // coalesced status request now confirms the deployed raid.
                raidStarted = true;
                worldRevision++;
                nextStatusCheck = 0;
            }

            if (!AdvancedMenu.Enabled && GhostReview.Value)
            {
                GhostReview.Value = false;
            }

            if (queueConsumed)
            {
                queueConsumed = false;
                queuedEvent = null;
                queueId = null;
            }

            GunGameRespawn.Tick();
            monitor.Tick();
            raidNotice.Tick();
            if (pendingStatus != null)
            {
                string update = pendingStatus;
                pendingStatus = null;
                if (update.StartsWith("Backend request failed:", StringComparison.Ordinal))
                {
                    networkFailures++;
                    nextStatusCheck = Time.unscaledTime + (float)NetworkCadence.Retry(networkFailures, 10);
                }
                else
                {
                    networkFailures = 0;
                }

                if (status.Value != update)
                {
                    status.Value = update;
                    Logger.LogInfo(update);
                }
            }

            if (autoApply.Observe(Time.unscaledTime))
            {
                status.Value = "Selection changed â€” waiting to send...";
            }

            if (autoApply.Ready(Time.unscaledTime, busy))
            {
                Send(true);
            }

            if (pendingSelection != null)
            {
                if (serverSelection.Value != pendingSelection)
                {
                    Logger.LogInfo("Server selection: " + pendingSelection);
                }

                serverSelection.Value = pendingSelection;
                pendingSelection = null;
            }

            if (pendingRaid != null)
            {
                if (pendingRaidWorldRevision == worldRevision)
                {
                    if (lastRaid.Value != pendingRaid)
                    {
                        Logger.LogInfo("Last generated raid: " + pendingRaid);
                    }

                    lastRaid.Value = pendingRaid;
                    confirmedWorldRevision = worldRevision;
                }

                pendingRaid = null;
            }

            if (!busy && !autoApply.HasPending && Time.unscaledTime >= nextStatusCheck)
            {
                Send(false);
            }

            featureUpdates.Tick(Time.unscaledTime);
            var player = Local;
            if (player == null || !player.IsYourPlayer || !GhostReview.Value || player.Physical == null)
            {
                return;
            }

            Refill(player.Physical.Stamina);
            Refill(player.Physical.HandsStamina);
        }

        private static void Refill(Stamina stamina)
        {
            if (stamina == null)
            {
                return;
            }

            stamina.Current = stamina.TotalCapacity.Value;
            stamina.Overuse = 0;
        }

        private void Send(bool apply)
        {
            if (busy)
            {
                return;
            }

            busy = true;
            int requestWorldRevision = worldRevision;
            var revision = autoApply.Revision;
            if (apply)
            {
                autoApply.Dispatched(revision);
            }

            nextStatusCheck = Time.unscaledTime + (float)NetworkCadence.Roulette(modifiersEnabled.Value, statusWorld != null);
            if (apply)
            {
                status.Value = "Contacting backend...";
                Logger.LogInfo("Sending chance settings; one-shot choice=" + (queuedEvent ?? "none"));
            }

            string expectedEvent = queuedEvent ?? "random";
            bool queue = modifiersEnabled.Value && queuedEvent != null;
            bool standaloneEnabled = modifiersEnabled.Value;
            var request = new JObject
            {
                ["action"] = apply ? (queue ? "apply" : "settings") : "status",
                ["event"] = expectedEvent,
                ["repeat"] = false,
                ["allowDisabled"] = true,
                ["eventsEnabled"] = standaloneEnabled,
                ["traderEventActivation"] = traderEventActivation.Value,
                ["queueId"] = queueId,
                ["chances"] = JObject.FromObject(chances.Snapshot())
            }.ToString();
            Task.Run(async () =>
            {
                try
                {
                    var text = await Backend.Post(request);
                    var data = JObject.Parse(text);
                    // Accept raw mod responses, or the standard SPT response envelope.
                    if (data["data"] is JObject body)
                    {
                        data = body;
                    }

                    if (revision == autoApply.Revision)
                    {
                        bool ok = data["ok"]?.Value<bool>() == true;
                        FenceMapMarkers.Notices = data["fenceMarkers"]?.ToObject<FenceMapNotice[]>() ?? Array.Empty<FenceMapNotice>();
                        string message = data["message"]?.ToString() ?? "No acknowledgement";
                        if (apply && queue && ok && !EventSelection.Matches(expectedEvent, message))
                        {
                            ok = false;
                        }

                        if (ok && queue && data["queuePending"]?.Value<bool>() == false)
                        {
                            queueConsumed = true;
                        }

                        pendingStatus = ok ? (!standaloneEnabled ? "Raid Roulette disabled; task packs remain active" : queue
                            && data["queuePending"]?.Value<bool>() == true ? "Next raid queued: " + expectedEvent
                            : "Automatic raid percentages active") : "Rejected: " + message;
                        int end = message.IndexOf(". Map-", StringComparison.Ordinal);
                        pendingSelection = end < 0 ? message : message.Substring(0, end);
                        pendingRaidWorldRevision = requestWorldRevision;
                        pendingRaid = data["lastRaid"]?.ToString() ?? "Not reported";
                        StartupTarget = data["startupTarget"]?.Value<int>() ?? 0;
                        StartupCap = data["startupCap"]?.Value<int>() ?? 0;
                        HuntBossRole = data["huntBossRole"]?.ToString() ?? "";
                        OverwatchPerZone = data["overwatchPerZone"]?.Value<int>() ?? 0;
                        OverwatchCap = data["overwatchCap"]?.Value<int>() ?? 0;
                        OverwatchZones = data["overwatchZones"]?.ToObject<string[]>() ?? Array.Empty<string>();
                        OverwatchPoints = data["overwatchPoints"]?.ToObject<RooftopSlot[]>() ?? Array.Empty<RooftopSlot>();
                    }
                }
                catch (Exception e)
                {
                    if (revision == autoApply.Revision)
                    {
                        pendingStatus = "Backend request failed: " + e.GetBaseException().Message
                            + ". Check that the matching server mod is installed and SPT.Server is running.";
                    }
                }
                finally
                {
                    busy = false;
                }
            });
        }

        internal static bool IsLocal(IPlayer? player) => player != null && Local != null && player.ProfileId == Local.ProfileId;
        internal static bool Hidden(IPlayer? player) => ((GhostReview.Value || GunGameRespawn.Protected)
            && IsLocal(player)) || GiftDirector.IsRunner(player);
        private void OnDestroy()
        {
            try
            {
                doorTeleports.Stop();
                infrastructureDecay.Reset();
                labWork.Stop();
                gauntlet.Stop();
                GunGameRespawn.Stop();
                gunGame.Stop();
                stormtide.Dispose();
                schizophrenia.Stop();
                timeWarp.Stop();
            }
            finally
            {
                foreach (var group in patchGroups)
                {
                    try
                    {
                        group.UnpatchSelf();
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning("[RR-INIT] shutdown-cleanup " + e.GetBaseException().Message);
                    }
                }
            }
        }
    }

    internal static class Backend
    {
        // Reuse the launcher's SPT session, address and TLS handling. No hard-coded localhost URL.
        private static MethodInfo? cachedPostMethod;
        private static readonly object methodGate = new();
        private static MethodInfo ResolvePostMethod()
        {
            lock (methodGate)
            {
                if (cachedPostMethod != null)
                {
                    return cachedPostMethod;
                }

                var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly
                    .GetType("SPT.Common.Http.RequestHandler", false)).FirstOrDefault(candidate => candidate != null)
                    ?? throw new InvalidOperationException("SPT RequestHandler was not loaded");
                cachedPostMethod = type.GetMethod(
                    "PostJsonAsync",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null
                ) ?? throw new MissingMethodException("SPT RequestHandler.PostJsonAsync(string, string) not found");
                return cachedPostMethod;
            }
        }

        public static async Task<string> Post(string json, string path = "/client/raidroulette/testing")
        {
            var method = ResolvePostMethod();
            var task = (Task<string>)method.Invoke(null, new object[] { path, json })!;
            var result = await task.ConfigureAwait(false);
            return result;
        }
    }

    [HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.IsEnemy), new[] { typeof(IPlayer) })]
    internal static class IsEnemyPatch
    {
        private static bool Prefix(BotsGroup __instance, IPlayer __0, ref bool __result)
        {
            if (GauntletDirector.Allies(__instance, __0))
            {
                __result = false;
                return false;
            }

            if (!Plugin.Hidden(__0) || CampaignActors.CanTarget(__instance, __0) || (BossHuntDirector.IsGroup(__instance)
                && Plugin.IsLocal(__0)))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.AddEnemy), new[] { typeof(IPlayer), typeof(EBotEnemyCause) })]
    internal static class AddEnemyPatch
    {
        private static bool Prefix(BotsGroup __instance, IPlayer __0, ref bool __result)
        {
            if (GauntletDirector.Allies(__instance, __0))
            {
                __result = false;
                return false;
            }

            if (!Plugin.Hidden(__0) || CampaignActors.CanTarget(__instance, __0) || (BossHuntDirector.IsGroup(__instance)
                && Plugin.IsLocal(__0)))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(EnemyInfo), "get_IsVisible")]
    internal static class VisionPatch
    {
        private static void Postfix(EnemyInfo __instance, ref bool __result)
        {
            if (GauntletPursuit.IsHunter(__instance.Owner) && __instance.Person is Player other && GauntletPursuit
                .IsHunter(other.AIData?.BotOwner))
            {
                __result = false;
                return;
            }

            if (Plugin.Hidden(__instance.Person) && !CampaignActors.CanTarget(__instance.Owner, __instance.Person)
                && !BossHuntDirector.IsTarget(__instance.Owner, __instance.Person))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(EnemyInfo), "get_CanShoot")]
    internal static class ShootPatch
    {
        private static void Postfix(EnemyInfo __instance, ref bool __result)
        {
            if (GauntletPursuit.IsHunter(__instance.Owner) && __instance.Person is Player other && GauntletPursuit
                .IsHunter(other.AIData?.BotOwner))
            {
                __result = false;
                return;
            }

            if (Plugin.Hidden(__instance.Person) && !CampaignActors.CanTarget(__instance.Owner, __instance.Person)
                && !BossHuntDirector.IsTarget(__instance.Owner, __instance.Person))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(BotMemory), "get_GoalEnemy")]
    internal static class GoalPatch
    {
        private static void Postfix(BotMemory __instance, ref EnemyInfo? __result)
        {
            if (GiftDirector.IsRunner(__instance._owner?.GetPlayer) || (__result != null && Plugin.Hidden(__result.Person)
                && !CampaignActors.CanTarget(__instance._owner, __result.Person) && !BossHuntDirector.IsTarget(__instance._owner,
                 __result.Person)))
            {
                __result = null;
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ApplyDamageInfo))]
    internal static class DamagePatch
    {
        private static bool Prefix(Player __instance) => !((Plugin.GhostReview.Value || GunGameRespawn.Protected) && Plugin
            .IsLocal(__instance));
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ReceiveDamage))]
    internal static class ReceivePatch
    {
        private static bool Prefix(Player __instance) => !((Plugin.GhostReview.Value || GunGameRespawn.Protected) && Plugin
            .IsLocal(__instance));
    }

    [HarmonyPatch(typeof(MovementContext), "get_SprintSpeed")]
    internal static class SpeedPatch
    {
        private static void Postfix(MovementContext __instance, ref float __result)
        {
            var player = Plugin.Local;
            if (player != null && player.IsYourPlayer && ReferenceEquals(player.MovementContext, __instance))
            {
                __result *= Plugin.GhostReview.Value ? 1.5f : 1f;
            }
        }
    }

    [HarmonyPatch(typeof(BotMemory), "get_HaveEnemy")]
    internal static class HasEnemyPatch
    {
        private static void Postfix(BotMemory __instance, ref bool __result)
        {
            // GoalEnemy is filtered by GoalPatch; keep the paired presence check consistent.
            if ((Plugin.GhostReview.Value || GunGameRespawn.Protected || GiftDirector.Active) && __instance.GoalEnemy == null)
            {
                __result = false;
            }
        }
    }
}
