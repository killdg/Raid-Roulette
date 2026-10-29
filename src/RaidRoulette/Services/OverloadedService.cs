using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
namespace RaidRoulette.Services;

public sealed class OverloadedService(TemplateTable templates,GlobalTable globals,ICloner cloner)
{
    private sealed record Gun(TemplateItem Template,List<Item> Preset,TemplateItem[] Magazines);
    private static int Capacity(TemplateItem t)=>(int)(t.Properties?.Cartridges??[]).Select(c=>c.MaxCount??0).DefaultIfEmpty().Max();
    private static bool RealMagazine(TemplateItem t)=>Capacity(t)>0&&t.Name?.Contains("infected",StringComparison.OrdinalIgnoreCase)!=true;
    private Gun[]? arsenal;
    private Gun[] Arsenal=>arsenal??=templates.Items.Values.Where(t=>!string.IsNullOrEmpty(t.Properties?.WeapClass)&&t.Properties.IsStationaryWeapon!=true&&t.Properties.DefAmmo.HasValue&&templates.Items.ContainsKey(t.Properties.DefAmmo.Value)).Select(t=>
    {
        var preset=globals.ItemPresets.Values.FirstOrDefault(p=>p.Encyclopedia==t.Id)??globals.ItemPresets.Values.FirstOrDefault(p=>p.Items?.Any(i=>i.Template==t.Id)==true);
        var mags=(t.Properties!.Slots??[]).Where(s=>s.Name=="mod_magazine").SelectMany(s=>s.Properties?.Filters??[]).SelectMany(f=>f.Filter??[]).Distinct().Where(templates.Items.ContainsKey).Select(id=>templates.Items[id]).Where(RealMagazine).ToArray();
        return new Gun(t,preset?.Items??[],mags);
    }).Where(g=>g.Preset.Any(i=>i.Template==g.Template.Id)&&g.Magazines.Length>0).ToArray();
    public int SidearmCapacity=>Arsenal.Where(g=>g.Template.Properties!.WeapClass is "pistol" or "revolver").SelectMany(g=>g.Magazines).Select(Capacity).DefaultIfEmpty().Max();
    public bool Available(int minimum)=>Arsenal.Any(g=>g.Template.Properties!.WeapClass is not ("pistol" or "revolver")&&g.Magazines.Any(m=>Capacity(m)>=minimum))&&SidearmCapacity>0;
    public BotBase CopyForResponse(BotBase original,int minimum)
    {
        string role=original.Info?.Settings?.Role??"";
        if(!RaidRulesService.IsScav(role)&&!role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase)&&!role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase))return original;
        var primary=Arsenal.Where(g=>g.Template.Properties!.WeapClass is not ("pistol" or "revolver")&&g.Magazines.Any(m=>Capacity(m)>=minimum)).ToArray();
        int sideCapacity=SidearmCapacity;
        var pistols=Arsenal.Where(g=>g.Template.Properties!.WeapClass is "pistol" or "revolver"&&g.Magazines.Any(m=>Capacity(m)==sideCapacity)).ToArray();
        if(primary.Length==0||pistols.Length==0)throw new InvalidOperationException("No complete eligible primary/sidearm presets exist for Overloaded.");
        var copy=cloner.Clone(original)!;var items=copy.Inventory?.Items??throw new InvalidOperationException("NPC inventory missing.");
        var equipment=copy.Inventory!.Equipment??throw new InvalidOperationException("NPC equipment root missing.");
        bool second=items.Any(i=>i.ParentId==equipment.ToString()&&i.SlotId=="SecondPrimaryWeapon");
        // Remove cached/generated guns, undersized spare magazines and their descendants.
        var remove=items.Where(i=>templates.Items.TryGetValue(i.Template,out var t)&&(!string.IsNullOrEmpty(t.Properties?.WeapClass)||Capacity(t)>0)).Select(i=>i.Id.ToString()).ToHashSet();
        bool changed;do{changed=false;foreach(var i in items)if(i.ParentId!=null&&remove.Contains(i.ParentId))changed|=remove.Add(i.Id.ToString());}while(changed);
        items.RemoveAll(i=>remove.Contains(i.Id.ToString()));
        void Equip(string slot,Gun gun,int capacity,bool exact)
        {
            var kit=cloner.Clone(gun.Preset)!;var root=kit.First(i=>i.Template==gun.Template.Id);
            var discard=kit.Where(i=>i.ParentId==root.Id.ToString()&&(i.SlotId=="mod_magazine"||(gun.Template.Properties!.Chambers??[]).Any(c=>c.Name==i.SlotId))).Select(i=>i.Id.ToString()).ToHashSet();
            bool more;do{more=false;foreach(var i in kit)if(i.ParentId!=null&&discard.Contains(i.ParentId))more|=discard.Add(i.Id.ToString());}while(more);
            kit.RemoveAll(i=>discard.Contains(i.Id.ToString()));
            var ids=kit.ToDictionary(i=>i.Id.ToString(),_=>new MongoId());string oldRoot=root.Id.ToString();
            foreach(var i in kit)
            {
                string old=i.Id.ToString();i.Id=ids[old];
                if(old==oldRoot){i.ParentId=equipment.ToString();i.SlotId=slot;i.Location=null;i.Upd??=new();i.Upd.SpawnedInSession=true;}
                else if(i.ParentId!=null&&ids.TryGetValue(i.ParentId,out var parent))i.ParentId=parent.ToString();
                else throw new InvalidOperationException("Weapon preset has an invalid attachment hierarchy.");
            }
            var magazine=gun.Magazines.Where(m=>exact?Capacity(m)==capacity:Capacity(m)>=capacity).OrderBy(Capacity).First();
            var mag=new Item{Id=new MongoId(),Template=magazine.Id,ParentId=root.Id.ToString(),SlotId="mod_magazine",Upd=new(){SpawnedInSession=true}};
            kit.Add(mag);var cartridges=magazine.Properties!.Cartridges!.OrderByDescending(c=>c.MaxCount).First();
            kit.Add(new(){Id=new MongoId(),Template=gun.Template.Properties!.DefAmmo!.Value,ParentId=mag.Id.ToString(),SlotId=cartridges.Name,Location=0,Upd=new(){StackObjectsCount=Capacity(magazine),SpawnedInSession=true}});
            var chamber=gun.Template.Properties.Chambers?.FirstOrDefault();
            if(chamber!=null)kit.Add(new(){Id=new MongoId(),Template=gun.Template.Properties.DefAmmo.Value,ParentId=root.Id.ToString(),SlotId=chamber.Name,Upd=new(){StackObjectsCount=1,SpawnedInSession=true}});
            items.AddRange(kit);
        }
        Equip("FirstPrimaryWeapon",primary[Random.Shared.Next(primary.Length)],minimum,false);
        if(second)Equip("SecondPrimaryWeapon",primary[Random.Shared.Next(primary.Length)],minimum,false);
        Equip("Holster",pistols[Random.Shared.Next(pistols.Length)],sideCapacity,true);
        copy.Inventory.FastPanel=copy.Inventory.FastPanel?.Where(p=>!remove.Contains(p.Value.ToString())).ToDictionary(p=>p.Key,p=>p.Value);
        copy.Inventory.FavoriteItems=copy.Inventory.FavoriteItems?.Where(i=>!remove.Contains(i.ToString())).ToArray();
        return copy;
    }
}
