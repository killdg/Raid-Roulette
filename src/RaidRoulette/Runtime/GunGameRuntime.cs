using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using RaidRoulette.Services;

namespace RaidRoulette.Runtime
{
    public sealed class GunGameRequest : IRequestData
    {
        public string ProfileId { get; set; } = "";
        public string Action { get; set; } = "";
        public string Token { get; set; } = "";
        public string Victim { get; set; } = "";
        public string Kind { get; set; } = "";
        public List<string> ItemIds { get; set; } = [];
        public int Attempt { get; set; }
        public string DeathId { get; set; } = "";
    }

    public sealed class GunGameLedger
    {
        public string Token { get; set; } = "";
        public string Event { get; set; } = "";
        public List<Item> ReturnItems { get; set; } = [];
        public List<string> OriginalIds { get; set; } = [];
        public bool Returned { get; set; }
        public int Stage { get; set; }
        public bool Knife { get; set; }
        public bool Won { get; set; }
        public int Rewarded { get; set; }
        public int Attempt { get; set; }
        public HashSet<string> Restarts { get; set; } = [];

        public bool Restart(string deathId)
        {
            if (string.IsNullOrWhiteSpace(deathId) || deathId.Length > 100 || !Restarts.Add(deathId))
            {
                return false;
            }

            Stage = 0;
            Knife = Won = false;
            Attempt++;
            return true;
        }

        public HashSet<string> Victims { get; set; } = [];
        public List<Item> Rewards { get; set; } = [];
        public HashSet<string> MailedRewards { get; set; } = [];

        public bool RecordKill(string victim, string kind, int? attempt = null)
        {
            if (attempt.HasValue && attempt.Value != Attempt)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(victim) || !Victims.Add(victim))
            {
                return false;
            }

            if (kind == "knife")
            {
                Knife = true;
                return true;
            }

            if (!Won && Stage < EventArsenal.Ladder.Length && kind == EventArsenal.Ladder[Stage])
            {
                if (Stage == 8)
                {
                    Won = true;
                }
                else
                {
                    Stage++;
                }

                return true;
            }

            return false;
        }

        public int EligibleReward => Won ? (Knife ? 3 : 1) : 0;

        public void IssueRewards()
        {
            while (Rewarded < EligibleReward)
            {
                Rewards.Add(new() { Id = new MongoId(), Template = new MongoId("59faff1d86f7746c51718c9c"), Upd =
                     new() { StackObjectsCount = 1, SpawnedInSession = true } });
                Rewarded++;
            }
        }
    }

    public static class GunGameRuntime
    {
        private static EventArsenal? arsenal;
        private static SaveServer save = null!;
        private static MailSendService mail = null!;
        private static JsonUtil json = null!;
        private static ICloner clone = null!;
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private const string Key = "raidRouletteWeaponEscrow";
        public static void Initialize(EventArsenal a, SaveServer s, MailSendService m, JsonUtil j, ICloner c)
        {
            arsenal = a;
            save = s;
            mail = m;
            json = j;
            clone = c;
        }

        public static bool Available(string id) => arsenal != null && (id == "ak50" ? arsenal.Available("ak50")
            : EventArsenal.Ladder.All(arsenal.Available));
        public static Task<IEnumerable<BotBase?>> TransformResponse(MongoId session, Task<IEnumerable<BotBase?>> task)
        {
            string? id = RaidRulesRuntime.Get(session)?.EventId;
            return arsenal != null && id is "gun_game" or "ak50" ? Transform(task, id) : task;
        }

        private static async Task<IEnumerable<BotBase?>> Transform(Task<IEnumerable<BotBase?>> task, string id) =>
             (await task.ConfigureAwait(false)).Select(bot => bot == null ? null : arsenal!.CopyAi(bot, id == "ak50" ? "ak50"
            : "pistol", true)).ToArray();
        private static GunGameLedger? Read(SptProfile profile)
        {
            if (!profile.ExtensionData!.TryGetValue(Key, out var raw) || raw == null)
            {
                return null;
            }

            return raw as GunGameLedger ?? json.Deserialize<GunGameLedger>(json.Serialize(raw));
        }

        private static void Write(SptProfile profile, GunGameLedger state) => profile.ExtensionData![Key] = state;
        public static async Task<object> Handle(MongoId session, GunGameRequest request)
        {
            await Gate.WaitAsync();
            try
            {
                var profile = save.GetProfile(session);
                var state = Read(profile);
                var pmc = profile.CharacterData!.PmcData!;
                if (request.Action == "recover")
                {
                    if (state != null && !state.Returned)
                    {
                        Return(profile, state, session);
                        await save.SaveProfileAsync(session);
                    }

                    return new
                    {
                        ok = true
                    };
                }

                if (request.Action == "begin")
                {
                    if (request.ProfileId != pmc.Id.ToString())
                    {
                        throw new InvalidOperationException("Weapon-swap events currently require the active PMC profile");
                    }

                    string id = RaidRulesRuntime.Get(session)?.EventId ?? "";
                    if (id is not ("gun_game" or "ak50"))
                    {
                        throw new InvalidOperationException("Raid is not a weapon-swap event");
                    }

                    if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 80)
                    {
                        throw new InvalidOperationException("Invalid raid token");
                    }

                    if (state != null && state.Token == request.Token && state.Returned)
                    {
                        throw new InvalidOperationException("This weapon return has already completed");
                    }

                    if (state == null || state.Token != request.Token)
                    {
                        if (state != null && !state.Returned)
                        {
                            Return(profile, state, session);
                        }

                        var items = pmc.Inventory!.Items!;
                        var equipment = pmc.Inventory.Equipment.ToString();
                        var roots = items.Where(i => i.ParentId == equipment && i
                            .SlotId is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster").Select(i => i.Id.ToString()).ToHashSet();
                        var ids = new HashSet<string>(roots);
                        bool more;
                        do
                        {
                            more = false;
                            foreach (var i in items)
                            {
                                if (i.ParentId != null && ids.Contains(i.ParentId))
                                {
                                    more |= ids.Add(i.Id.ToString());
                                }
                            }
                        }
                        while (more);
                        state = new()
                        {
                            Token = request.Token,
                            Event = id,
                            ReturnItems = WeaponEscrow.Snapshot(pmc.Inventory, clone),
                            OriginalIds = ids.ToList()
                        };
                        // Persist the original snapshot and remove it from the authoritative profile before issuing replacements.
                        items.RemoveAll(i => ids.Contains(i.Id.ToString()));
                        pmc.Inventory.FastPanel = pmc.Inventory.FastPanel?.Where(p => !ids.Contains(p.Value.ToString())).ToDictionary(p =>
                             p.Key, p => p.Value);
                        pmc.Inventory.FavoriteItems = pmc.Inventory.FavoriteItems?.Where(i => !ids.Contains(i.ToString())).ToArray();
                        pmc.InsuredItems = pmc.InsuredItems?.Where(i => !i.ItemId.HasValue || !ids.Contains(i.ItemId.Value.ToString()))
                            .ToList();
                        Write(profile, state);
                    }

                    await save.SaveProfileAsync(session);
                    return new
                    {
                        ok = true,
                        stage = state.Stage,
                        won = state.Won,
                        knife = state.Knife,
                        attempt = state.Attempt,
                        originalIds = state.OriginalIds,
                        populationCap = Testing.TestingControl.PopulationCap,
                        populationTarget = Testing.TestingControl.PopulationTarget,
                        fenceRespawn = Quests.FenceEvents.GunGameRespawnMode(session),
                        kits = (state.Event == "ak50" ? new[]
                        {
                            "ak50"
                        }

                        : EventArsenal.Ladder).Select(arsenal!.Kit).ToArray()
                    };
                }

                if (state == null || state.Token != request.Token || state.Returned)
                {
                    throw new InvalidOperationException("No active weapon escrow for this raid");
                }

                if (request.Action == "kill")
                {
                    if (state.Event != "gun_game")
                    {
                        throw new InvalidOperationException("Not Gun Game");
                    }

                    state.RecordKill(request.Victim, request.Kind, request.Attempt);
                    state.IssueRewards();
                    if (state.Won)
                    {
                        EventIntegration.GunGameWin(session);
                    }
                }

                if (request.Action == "respawn")
                {
                    if (state.Event != "gun_game")
                    {
                        throw new InvalidOperationException("Respawn requires Gun Game");
                    }

                    if (string.IsNullOrWhiteSpace(request.DeathId) || request.DeathId.Length > 100)
                    {
                        throw new InvalidOperationException("Invalid respawn identifier");
                    }

                    state.Restart(request.DeathId);
                }

                if (request.Action == "mail_rewards")
                {
                    var ids = request.ItemIds.ToHashSet();
                    var pending = state.Rewards.Where(i => ids.Contains(i.Id.ToString()) && !state.MailedRewards.Contains(i.Id
                        .ToString())).ToList();
                    foreach (var item in pending)
                    {
                        string marker = "reward:" + state.Token + ":" + item.Id;
                        if (!HasMail(profile, marker))
                        {
                            mail.SendDirectNpcMessageToPlayer(
                                session,
                                "54cb50c76803fa8b248b4571",
                                MessageType.NpcTraderMessage,
                                "Your Gun Game reward. Your inventory was full.",
                                clone.Clone(new List<Item> { item }),
                                604800,
                                new SystemData { ExtensionData = new() { ["rrDelivery"] = marker } }
                            );
                        }

                        state.MailedRewards.Add(item.Id.ToString());
                    }
                }

                Write(profile, state);
                await save.SaveProfileAsync(session);
                return new
                {
                    ok = true,
                    stage = state.Stage,
                    won = state.Won,
                    knife = state.Knife,
                    attempt = state.Attempt,
                    rewards = state.Rewards.Where(i => !state.MailedRewards.Contains(i.Id.ToString())).ToArray()
                };
            }
            finally
            {
                Gate.Release();
            }
        }

        private static void Return(SptProfile profile, GunGameLedger state, MongoId session)
        {
            if (state.Returned)
            {
                return;
            }

            var inventory = profile.CharacterData!.PmcData!.Inventory!;
            var originalIds = state.OriginalIds.ToHashSet();
            inventory.FastPanel = inventory.FastPanel?.Where(p => !originalIds.Contains(p.Value.ToString())).ToDictionary(p =>
                 p.Key, p => p.Value);
            inventory.FavoriteItems = inventory.FavoriteItems?.Where(i => !originalIds.Contains(i.ToString())).ToArray();
            WeaponEscrow.DeliverOnce(
                state,
                inventory.Items!,
                clone,
                returned =>
            {
                string marker = "return:" + state.Token;
                if (!HasMail(profile, marker))
                {
                    mail.SendDirectNpcMessageToPlayer(
                        session,
                        "54cb50c76803fa8b248b4571",
                        MessageType.NpcTraderMessage,
                        "A little package for you. Your original weapon and attachments have been returned after " + state.Event
                            .Replace('_', ' ') + ".",
                        returned,
                        604800,
                        new SystemData { ExtensionData = new() { ["rrDelivery"] = marker } }
                    );
                }
            }
            );
            Write(profile, state);
        }

        private static bool IsMarker(SystemData? data, string marker) => data?.ExtensionData != null && data.ExtensionData
            .TryGetValue("rrDelivery", out var raw) && raw?.ToString() == marker;
        private static bool HasMail(SptProfile profile, string marker) => profile.DialogueRecords?.Values.Any(d => d
            .Messages?.Any(m => IsMarker(m.SystemData, marker)) == true) == true;
        public static async Task Finish(MongoId session)
        {
            if (arsenal == null)
            {
                return;
            }

            await Gate.WaitAsync();
            try
            {
                var profile = save.GetProfile(session);
                var state = Read(profile);
                if (state != null && !state.Returned)
                {
                    Return(profile, state, session);
                    await save.SaveProfileAsync(session);
                }
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
