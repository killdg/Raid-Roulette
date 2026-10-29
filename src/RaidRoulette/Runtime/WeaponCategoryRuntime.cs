using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace RaidRoulette.Runtime;
public static class WeaponCategoryRuntime
{
 private static WeaponCategoryService? service;
 public static void Initialize(WeaponCategoryService value)=>service=value;
 public static bool Available(string id)=>service?.Available(id)==true;
 public static Task<IEnumerable<BotBase?>> TransformResponse(MongoId session,Task<IEnumerable<BotBase?>> task)
 {
  string? id=RaidRulesRuntime.Get(session)?.EventId;
  return WeaponCategoryService.Handles(id)&&service!=null?Transform(task,id!,service):task;
 }
 private static async Task<IEnumerable<BotBase?>> Transform(Task<IEnumerable<BotBase?>> task,string id,WeaponCategoryService svc)
  =>(await task.ConfigureAwait(false)).Select(bot=>bot==null?null:svc.CopyForResponse(bot,id)).ToArray();
}
