using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace RaidRoulette.Runtime;
public static class SmokersRuntime
{
    private static SmokersService? service;
    public static void Initialize(SmokersService value)=>service=value;
    public static bool Available=>service?.Available==true;
    public static Task<IEnumerable<BotBase?>> TransformResponse(MongoId session,Task<IEnumerable<BotBase?>> task)
        =>RaidRulesRuntime.Get(session)?.EventId=="smokers_lobby"&&service!=null?Transform(session,task,service):task;
    private static async Task<IEnumerable<BotBase?>> Transform(MongoId session,Task<IEnumerable<BotBase?>> task,SmokersService svc)
    {
        var result=new List<BotBase?>();
        foreach(var bot in await task.ConfigureAwait(false))
        {
            if(bot==null){result.Add(null);continue;}
            try{var copy=svc.CopyForResponse(bot);result.Add(copy);if(!ReferenceEquals(copy,bot))RaidDiagnostics.Log(session,"smoke-supply",$"profile={copy.Id} role={copy.Info?.Settings?.Role} smokes={copy.Inventory?.Items?.Count(i=>i.Template==new MongoId(SmokersService.Smoke))}");}
            catch(Exception e){result.Add(bot);RaidDiagnostics.Log(session,"smoke-supply-failed",e.Message);}
        }
        return result;
    }
}
