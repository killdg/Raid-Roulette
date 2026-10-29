using SPTarkov.Server.Core.Models.Common;
namespace RaidRoulette.Runtime;

// Optional campaign contract. The event engine never loads quest definitions itself.
public interface IEventCampaign
{
 string? Select(MongoId session,string? map);
 void Begin(MongoId session,string map,string eventId);
 void GunGameWin(MongoId session);
}
public static class EventIntegration
{
 private static IEventCampaign? campaign;
 public static bool Ready{get;internal set;}
 public static bool Active=>campaign!=null;
 public static void Register(IEventCampaign value)
 {
  if(!Ready)throw new InvalidOperationException("Raid Roulette must be enabled and loaded before its quest pack.");
  if(campaign!=null)throw new InvalidOperationException("An event campaign is already registered.");
  campaign=value??throw new ArgumentNullException(nameof(value));
 }
 public static string? Select(MongoId session,string? map)=>campaign?.Select(session,map);
 public static void Begin(MongoId session,string map,string eventId)=>campaign?.Begin(session,map,eventId);
 public static void GunGameWin(MongoId session)=>campaign?.GunGameWin(session);
}
