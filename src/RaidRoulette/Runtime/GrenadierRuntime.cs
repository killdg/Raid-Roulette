using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace RaidRoulette.Runtime
{
    public static class GrenadierRuntime
    {
        private static GrenadierService? service;
        public static void Initialize(GrenadierService value) => service = value;
        public static bool Available => service?.Available == true;

        public static Task<IEnumerable<BotBase?>> TransformResponse(MongoId session, Task<IEnumerable<BotBase?>> task) =>
             RaidRulesRuntime.Get(session)?.EventId == "grenadier" && service != null ? Transform(session, task, service) : task;
        private static async Task<IEnumerable<BotBase?>> Transform(MongoId session, Task<IEnumerable<BotBase?>> task, GrenadierService svc)
        {
            var result = new List<BotBase?>();
            foreach (var bot in await task.ConfigureAwait(false))
            {
                if (bot == null)
                {
                    result.Add(null);
                    continue;
                }

                try
                {
                    var copy = svc.CopyForResponse(bot);
                    result.Add(copy);
                    if (!ReferenceEquals(copy, bot))
                    {
                        RaidDiagnostics.Log(
                            session,
                            "grenade-supply",
                            $"profile={copy.Id} role={copy.Info?.Settings?.Role} grenades={copy.Inventory?.Items?.Count(i => i.Template == new MongoId(GrenadierService.Smoke))}"
                        );
                    }
                }
                catch (Exception e)
                {
                    result.Add(bot);
                    RaidDiagnostics.Log(session, "grenade-supply-failed", e.Message);
                }
            }

            return result;
        }
    }
}
