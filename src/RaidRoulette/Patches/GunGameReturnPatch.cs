using RaidRoulette.Runtime;
using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
namespace RaidRoulette.Patches;
[HarmonyPatch(typeof(MatchController),nameof(MatchController.EndLocalRaidAsync))]
internal static class GunGameReturnPatch
{
 private static void Postfix(MongoId sessionId,ref Task __result)=>__result=Finish(__result,sessionId);
 private static async Task Finish(Task native,MongoId session){await native.ConfigureAwait(false);await GunGameRuntime.Finish(session);}
}
