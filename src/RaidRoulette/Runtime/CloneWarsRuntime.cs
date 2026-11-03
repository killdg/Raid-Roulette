using System.Collections.Concurrent;
using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace RaidRoulette.Runtime
{
    public static class CloneWarsRuntime
    {
        private static readonly ConcurrentDictionary<MongoId, CloneWarsSnapshot> Snapshots = new();
        private static readonly ConcurrentDictionary<MongoId, byte> Warned = new();
        private static CloneWarsService? service;
        private static Func<MongoId, BotBase?> profile = _ => null;
        private static Action<string> log = _ =>
        {
        };
        public static void Initialize(CloneWarsService cloneService, Func<MongoId, BotBase?> resolver, Action<string> logger)
        {
            service = cloneService;
            profile = resolver;
            log = logger;
            Snapshots.Clear();
            Warned.Clear();
        }

        public static bool Capture(MongoId session)
        {
            Clear(session);
            try
            {
                var player = profile(session) ?? throw new InvalidOperationException("PMC profile was not found for this session.");
                var snapshot = service?.Capture(player) ?? throw new InvalidOperationException("Clone service is not initialized.");
                Snapshots[session] = snapshot;
                log($"[RR-CLONE] stage=snapshot session={session} nickname={snapshot.Nickname} level={snapshot.Level} " +
                    $"kitItems={snapshot.Kit.Items?.Count}; player save remains unchanged");
                return true;
            }
            catch (Exception e)
            {
                log("[RR-CLONE] stage=snapshot-failed " + e.Message);
                return false;
            }
        }

        public static Task<IEnumerable<BotBase?>> TransformResponse(MongoId session, Task<IEnumerable<BotBase?>> task)
        {
            if (service == null || !Snapshots.TryGetValue(session, out var snapshot))
            {
                return task;
            }

            return CopyResponse(task, snapshot, session, service);
        }

        private static async Task<IEnumerable<BotBase?>> CopyResponse(Task<IEnumerable<BotBase?>> task,
             CloneWarsSnapshot snapshot, MongoId session, CloneWarsService cloneService)
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
                    var copy = cloneService.CopyForResponse(snapshot, bot);
                    result.Add(copy);
                    if (!ReferenceEquals(copy, bot))
                    {
                        RaidDiagnostics.Log(
                            session,
                            "clone-applied",
                            $"profile={copy.Id} role={copy.Info?.Settings?.Role} nickname={copy.Info?.Nickname} level={copy.Info?.Level} items={copy.Inventory?.Items?.Count}"
                        );
                    }
                }
                catch (Exception e)
                {
                    result.Add(bot);
                    if (Warned.TryAdd(session, 0))
                    {
                        log("[RR-CLONE] stage=apply-failed; retaining generated PMC: " + e.Message);
                    }
                }
            }

            return result;
        }

        public static void Clear(MongoId session)
        {
            Snapshots.TryRemove(session, out _);
            Warned.TryRemove(session, out _);
        }
    }
}
