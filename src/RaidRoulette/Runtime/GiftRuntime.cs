using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace RaidRoulette.Runtime
{
    public static class GiftRuntime
    {
        private static GiftService? service;
        public static void Initialize(GiftService value) => service = value;
        public static bool Available => service?.Available == true;
        public static bool StaffAvailable => service?.StaffAvailable == true;

        public static void ConfigureMap(LocationBase map) => service?.ConfigureMap(map);
        public static void ConfigureLab(LocationBase map) => service?.ConfigureLab(map);
        public static Task<IEnumerable<BotBase?>> TransformResponse(MongoId session, Task<IEnumerable<BotBase?>> task)
        {
            if (RaidRulesRuntime.Get(session)?.EventId is not ("the_gift" or "lab_work_day") || service == null)
            {
                return task;
            }

            return Transform(task, service, session);
        }

        private static async Task<IEnumerable<BotBase?>> Transform(Task<IEnumerable<BotBase?>> task, GiftService gift, MongoId session)
        {
            var original = await task.ConfigureAwait(false);
            var result = new List<BotBase?>();
            foreach (var bot in original)
            {
                if (bot == null)
                {
                    result.Add(null);
                    continue;
                }

                try
                {
                    var copy = gift.CopyForResponse(bot, RaidRulesRuntime.Get(session)?.EventId == "lab_work_day");
                    result.Add(copy);
                    if (!ReferenceEquals(copy, bot) && RaidRulesRuntime.Get(session)is { } raid && raid.EventId == "the_gift")
                    {
                        foreach (var coin in copy.Inventory?.Items?.Where(i => i.Template == new MongoId(GiftService.Bitcoin)) ?? [])
                        {
                            raid.RunnerBitcoins.TryAdd(coin.Id.ToString(), 0);
                        }
                    }

                    if (!ReferenceEquals(copy, bot))
                    {
                        RaidDiagnostics.Log(
                            session,
                            "gift-prize",
                            $"profile={copy.Id} items={copy.Inventory?.Items?.Count} bitcoins={copy.Inventory?.Items?.Count(i => i.Template == new MongoId(GiftService.Bitcoin))}"
                        );
                    }
                }
                catch (Exception e)
                {
                    result.Add(bot);
                    RaidDiagnostics.Log(session, "gift-prize-failed", e.Message);
                }
            }

            return result;
        }
    }
}
