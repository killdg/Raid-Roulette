using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
using RaidRoulette.Runtime;
namespace RaidRoulette.Services;
public static class WeaponEscrow
{
 public static List<Item> Snapshot(BotBaseInventory inventory,ICloner clone)
 {
  var items=inventory.Items??[];string equipment=inventory.Equipment?.ToString()??throw new InvalidOperationException("Player equipment root is missing");
  var selected=items.Where(i=>i.ParentId==equipment&&i.SlotId is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster").Select(i=>i.Id.ToString()).ToHashSet();
  bool more;do{more=false;foreach(var i in items)if(i.ParentId!=null&&selected.Contains(i.ParentId))more|=selected.Add(i.Id.ToString());}while(more);
  return clone.Clone(items.Where(i=>selected.Contains(i.Id.ToString())).ToList())!;
 }
 public static bool DeliverOnce(GunGameLedger state,List<Item> current,ICloner clone,Action<List<Item>> send)
 {
  if(state.Returned)return false;
  var originals=state.OriginalIds.ToHashSet();current.RemoveAll(i=>originals.Contains(i.Id.ToString()));
  var returned=clone.Clone(state.ReturnItems)!;var ids=returned.ToDictionary(i=>i.Id.ToString(),_=>new MongoId());
  foreach(var item in returned){string previous=item.Id.ToString();item.Id=ids[previous];if(item.ParentId!=null&&ids.TryGetValue(item.ParentId,out var parent))item.ParentId=parent.ToString();else{item.ParentId=null;item.SlotId=null;item.Location=null;}}
  if(returned.Count>0)send(returned);state.Returned=true;return true;
 }
}

