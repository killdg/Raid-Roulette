using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace RaidRoulette.Runtime;
public static class OverloadedRuntime
{
    private static OverloadedService? service;
    public static void Initialize(OverloadedService value)=>service=value;
    public static bool Available(int minimum)=>service?.Available(minimum)==true;
    public static Task<IEnumerable<BotBase?>> TransformResponse(MongoId session,Task<IEnumerable<BotBase?>> task)
        =>RaidRulesRuntime.Get(session)?.EventId=="overloaded"&&service!=null?Transform(session,task,service):task;
    private static async Task<IEnumerable<BotBase?>> Transform(MongoId session,Task<IEnumerable<BotBase?>> task,OverloadedService svc)
    {
        var result=new List<BotBase?>();
        foreach(var bot in await task.ConfigureAwait(false))
        {
            if(bot==null){result.Add(null);continue;}
            // Fail the request rather than silently returning guns that violate this event.
            var copy=svc.CopyForResponse(bot,RaidRulesRuntime.Config.V2.MinimumMagazineCapacity);result.Add(copy);
            if(!ReferenceEquals(copy,bot))RaidDiagnostics.Log(session,"overloaded-equipped",$"profile={copy.Id} role={copy.Info?.Settings?.Role} primaryMin={RaidRulesRuntime.Config.V2.MinimumMagazineCapacity} sidearmCapacity={svc.SidearmCapacity}");
        }
        return result;
    }
}
