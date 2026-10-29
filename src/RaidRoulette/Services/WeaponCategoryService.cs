using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
namespace RaidRoulette.Services;

public sealed class WeaponCategoryService(TemplateTable templates,GlobalTable globals,ICloner cloner)
{
    public static bool Handles(string? id)=>id is "get_handy" or "i_have_a_shot" or "marksman";
    private static bool Allowed(string id,string? cls)=>id switch {"get_handy"=>cls is "pistol" or "revolver","i_have_a_shot"=>cls=="shotgun","marksman"=>cls=="sniperRifle",_=>false};
    private bool HasCartridges(Item i)=>templates.Items.TryGetValue(i.Template,out var t)&&t.Properties?.Cartridges?.Any(c=>c.MaxCount>0)==true;
    private IEnumerable<List<Item>> Presets(string id)=>globals.ItemPresets.Values.Select(p=>p.Items??[]).Where(kit=>kit.Any(g=>templates.Items.TryGetValue(g.Template,out var t)&&Allowed(id,t.Properties?.WeapClass)&&t.Properties?.IsStationaryWeapon!=true&&t.Properties?.DefAmmo.HasValue==true&&templates.Items.ContainsKey(t.Properties.DefAmmo.Value))&&kit.Any(HasCartridges));
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,List<Item>[]> presetCache=new();
    private List<Item>[] Choices(string id)=>presetCache.GetOrAdd(id,key=>Presets(key).ToArray());
    public bool Available(string id)=>Choices(id).Length>0;
    public BotBase CopyForResponse(BotBase original,string id)
    {
        var role=original.Info?.Settings?.Role??"";
        if(!RaidRulesService.IsScav(role)&&!role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase)&&!role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase))return original;
        var copy=cloner.Clone(original)!;var items=copy.Inventory?.Items??throw new InvalidOperationException("AI inventory missing.");
        var equipment=copy.Inventory!.Equipment??throw new InvalidOperationException("AI equipment missing.");
        var choices=Choices(id);if(choices.Length==0)throw new InvalidOperationException("No complete weapon presets for "+id);
        var remove=items.Where(i=>templates.Items.TryGetValue(i.Template,out var t)&&(!string.IsNullOrEmpty(t.Properties?.WeapClass)||t.Properties?.Cartridges?.Any()==true)).Select(i=>i.Id.ToString()).ToHashSet();
        bool changed;do{changed=false;foreach(var i in items)if(i.ParentId!=null&&remove.Contains(i.ParentId))changed|=remove.Add(i.Id.ToString());}while(changed);
        items.RemoveAll(i=>remove.Contains(i.Id.ToString()));
        // Replace carried grenades in place, retaining their native inventory locations and count.

        var flashTemplate=templates.Items.Values.FirstOrDefault(t=>t.Name?.Equals("grenade_zarya",StringComparison.OrdinalIgnoreCase)==true)
            ??templates.Items.Values.FirstOrDefault(t=>t.Properties?.ThrowType!=null&&t.Name?.Contains("zarya",StringComparison.OrdinalIgnoreCase)==true)
            ??throw new InvalidOperationException("Zarya flashbang template unavailable.");
        foreach(var item in items.Where(i=>templates.Items.TryGetValue(i.Template,out var t)&&t.Properties?.ThrowType!=null)){item.Template=flashTemplate.Id;item.Upd??=new();item.Upd.StackObjectsCount=1;}
        var kit=cloner.Clone(choices[Random.Shared.Next(choices.Length)])!;
        var root=kit.First(g=>templates.Items.TryGetValue(g.Template,out var t)&&Allowed(id,t.Properties?.WeapClass));
        var gun=templates.Items[root.Template];
        // Native presets normally contain no ammo; replace any cartridge/chamber children before filling.
        var oldAmmo=kit.Where(i=>kit.Any(parent=>parent.Id.ToString()==i.ParentId&&templates.Items.TryGetValue(parent.Template,out var t)&&((t.Properties?.Cartridges??[]).Any(c=>c.Name==i.SlotId)||(t.Properties?.Chambers??[]).Any(c=>c.Name==i.SlotId)))).Select(i=>i.Id).ToHashSet();
        kit.RemoveAll(i=>oldAmmo.Contains(i.Id));
        var ids=kit.ToDictionary(i=>i.Id.ToString(),_=>new MongoId());var oldRoot=root.Id.ToString();
        foreach(var i in kit){var old=i.Id.ToString();i.Id=ids[old];if(old==oldRoot){i.ParentId=equipment.ToString();i.SlotId=id=="get_handy"?"Holster":"FirstPrimaryWeapon";i.Location=null;}else if(i.ParentId!=null&&ids.TryGetValue(i.ParentId,out var parent))i.ParentId=parent.ToString();else throw new InvalidOperationException("Invalid weapon preset hierarchy.");}
        foreach(var holder in kit.ToArray())
        {
            var props=templates.Items[holder.Template].Properties;
            foreach(var slot in (props?.Cartridges??[]).Concat(props?.Chambers??[]))
            {
                int capacity=(int)(slot.MaxCount??1);if(capacity<=0)continue;
                kit.Add(new(){Id=new MongoId(),Template=gun.Properties!.DefAmmo!.Value,ParentId=holder.Id.ToString(),SlotId=slot.Name,Location=0,Upd=new(){StackObjectsCount=capacity,SpawnedInSession=true}});
            }
        }
        items.AddRange(kit);
        copy.Inventory.FastPanel=copy.Inventory.FastPanel?.Where(p=>!remove.Contains(p.Value.ToString())).ToDictionary(p=>p.Key,p=>p.Value);
        copy.Inventory.FavoriteItems=copy.Inventory.FavoriteItems?.Where(i=>!remove.Contains(i.ToString())).ToArray();
        return copy;
    }
}



