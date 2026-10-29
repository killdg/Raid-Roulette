using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
using Customization=SPTarkov.Server.Core.Models.Eft.Common.Tables.Customization;
namespace RaidRoulette.Services;

public sealed class GiftService(TemplateTable templates,BotTable bots,ICloner cloner)
{
    public const string Hat="5a43943586f77416ad2f06e2",Bitcoin="59faff1d86f7746c51718c9c",Paper=RaidRoulette.Shared.TrailItems.ToiletPaper;
    public const string Roubles="5449016a4bdc2d6f028b456f",Dollars="5696686a4bdc2da3298b456a",Euros="569668774bdc2da2298b4568";
    public const string Blicky="66015072e9f84d5680039678",BlickyMagazine="66015dc4aaad2f54cb04c56a",ToyRounds="6601546f86889319850bd566";
    public const string StaffVest="5fd4c5477a8d854fa0105061";
    public bool StaffAvailable=>Available&&templates.Items.ContainsKey(new MongoId(StaffVest))&&bots.Types.TryGetValue("bosssanitar",out var medical)&&medical?.BotAppearance?.Body?.Count>0&&medical.BotAppearance.Feet?.Count>0;
    private double Price(string id)=>templates.Handbook?.Items?.FirstOrDefault(x=>x.Id.ToString()==id)?.Price??0;
    public bool Available=>bots.Types.ContainsKey("gifter") && new[]{Hat,Bitcoin,Paper,Roubles,Dollars,Euros,Blicky,BlickyMagazine,ToyRounds}.All(id=>templates.Items.ContainsKey(new MongoId(id))) && Price(Dollars)>0 && Price(Euros)>0 && Price(Dollars)+Price(Euros)<200000;
    public void ConfigureMap(LocationBase map)
    {
        var zone=RaidRulesService.Zones(map).OrderBy(_=>Random.Shared.Next()).FirstOrDefault();
        if(zone==null)return;
        map.BossLocationSpawn??=[];
        map.BossLocationSpawn.RemoveAll(s=>s.BossName?.Equals("gifter",StringComparison.OrdinalIgnoreCase)==true);
        map.BossLocationSpawn.Add(new BossLocationSpawn {BossName="gifter",BossEscortType="assault",BossEscortAmount="0",BossChance=100,BossZone=zone,BossDifficulty="normal",BossEscortDifficulty="normal",Time=15,ForceSpawn=true,IgnoreMaxBots=false,Supports=[],TriggerId="",TriggerName="",SptId="rr-the-gift"});
    }
    public void ConfigureLab(LocationBase map)
    {
        var zones=RaidRulesService.Zones(map).OrderBy(_=>Random.Shared.Next()).Take(4).ToArray();map.BossLocationSpawn??=[];
        int extras=zones.Length*3;map.BotMax+=extras;if(map.BotMaxPvE>0)map.BotMaxPvE+=extras;
        int batch=0;foreach(var zone in zones)map.BossLocationSpawn.Add(new(){BossName="gifter",BossEscortType="gifter",BossEscortAmount="2",BossChance=100,BossZone=zone,BossDifficulty="normal",BossEscortDifficulty="normal",Time=60+8*batch++,ForceSpawn=true,IgnoreMaxBots=true,Supports=[],TriggerId="",TriggerName="",SptId="rr-lab-staff"});
    }
    public BotBase CopyForResponse(BotBase original,bool staff=false)
    {
        if(original.Info?.Settings?.Role?.Equals("gifter",StringComparison.OrdinalIgnoreCase)!=true)return original;
        var copy=cloner.Clone(original)!;
        var oldItems=copy.Inventory?.Items??throw new InvalidOperationException("Gift runner has no inventory.");
        var equipment=copy.Inventory!.Equipment??throw new InvalidOperationException("Gift runner has no equipment root.");
        var root=oldItems.Single(i=>i.Id==equipment);root.Id=new MongoId();root.ParentId=null;
        var bag=oldItems.FirstOrDefault(i=>i.SlotId=="Backpack" && templates.Items.TryGetValue(i.Template,out var tpl) && tpl.Properties?.Grids?.Any()==true)
            ??throw new InvalidOperationException("Santa template did not provide a backpack.");
        bag.Id=new MongoId();bag.ParentId=root.Id.ToString();
        var grid=templates.Items[bag.Template].Properties!.Grids!.OrderByDescending(g=>(g.Properties?.CellsH??0)*(g.Properties?.CellsV??0)).First();
        int width=grid.Properties?.CellsH??0,height=grid.Properties?.CellsV??0;
        if(width<1 || height<1)throw new InvalidOperationException("Gift backpack has no usable grid.");
        var items=new List<Item>{root,bag,new(){Id=new MongoId(),Template=new MongoId(Hat),ParentId=root.Id.ToString(),SlotId="Headwear",Upd=new(){SpawnedInSession=true}}};
        // A complete toy pistol lets native firearm initialization establish readiness.
        // Event client targeting filters still keep this runner out of combat.
        var blicky=new Item{Id=new MongoId(),Template=new MongoId(Blicky),ParentId=root.Id.ToString(),SlotId="Holster",Upd=new(){SpawnedInSession=true}};
        var magazine=new Item{Id=new MongoId(),Template=new MongoId(BlickyMagazine),ParentId=blicky.Id.ToString(),SlotId="mod_magazine",Upd=new(){SpawnedInSession=true}};
        items.Add(blicky);items.Add(magazine);
        items.Add(new Item{Id=new MongoId(),Template=new MongoId(ToyRounds),ParentId=magazine.Id.ToString(),SlotId="cartridges",Location=0,Upd=new(){StackObjectsCount=19,SpawnedInSession=true}});
        items.Add(new Item{Id=new MongoId(),Template=new MongoId(ToyRounds),ParentId=blicky.Id.ToString(),SlotId="patron_in_weapon",Upd=new(){StackObjectsCount=1,SpawnedInSession=true}});
        var pockets=oldItems.FirstOrDefault(i=>i.SlotId=="Pockets");
        if(pockets!=null){pockets.Id=new MongoId();pockets.ParentId=root.Id.ToString();items.Add(pockets);}
        double dollarRate=Price(Dollars),euroRate=Price(Euros);
        if(dollarRate<=0 || euroRate<=0)throw new InvalidOperationException("Currency handbook prices unavailable.");
        int target=Random.Shared.Next(200000,400000);
        int dollars=Math.Max(1,(int)Math.Floor(target*Random.Shared.Next(5,16)/100d/dollarRate));
        int euros=Math.Max(1,(int)Math.Floor(target*Random.Shared.Next(5,16)/100d/euroRate));
        int roubles=(int)Math.Ceiling(target-dollars*dollarRate-euros*euroRate);
        int bitcoins=Random.Shared.Next(0,4);
        var prizes=new List<(string Template,int Count)>{(Roubles,roubles),(Dollars,Math.Max(1,dollars)),(Euros,Math.Max(1,euros))};
        for(int i=0;i<bitcoins;i++)prizes.Add((Bitcoin,1));
        prizes.Add((Paper,1)); // preload a real paper model for the later loose-item trail
        var occupied=new bool[width,height];
        foreach(var prize in prizes)
        {
            var tpl=templates.Items[new MongoId(prize.Template)];int w=tpl.Properties?.Width??1,h=tpl.Properties?.Height??1;
            bool placed=false;
            for(int y=0;y<=height-h&&!placed;y++)for(int x=0;x<=width-w&&!placed;x++)
            {
                bool free=true;for(int dx=0;dx<w;dx++)for(int dy=0;dy<h;dy++)free&=!occupied[x+dx,y+dy];
                if(!free)continue;
                for(int dx=0;dx<w;dx++)for(int dy=0;dy<h;dy++)occupied[x+dx,y+dy]=true;
                items.Add(new Item{Id=new MongoId(),Template=tpl.Id,ParentId=bag.Id.ToString(),SlotId=grid.Name,Location=new{x,y,r=0,isSearched=true},Upd=new(){StackObjectsCount=prize.Count,SpawnedInSession=true}});placed=true;
            }
            if(!placed)throw new InvalidOperationException("Gift backpack cannot fit the configured prize.");
        }
        if(staff)items=items.Where(i=>i.Id==root.Id||i.SlotId=="Pockets").ToList();
        copy.Id=new MongoId();copy.Inventory=new(){Items=items,Equipment=root.Id};
        // Use ordinary Scav clothing under the hat when that appearance pool exists.
        if(bots.Types.TryGetValue("assault",out var scav) && scav?.BotAppearance is {} look)
        {
            MongoId? Pick(Dictionary<MongoId,double>? pool)=>pool?.Count>0?pool.Keys.ElementAt(Random.Shared.Next(pool.Count)):(MongoId?)null;
            copy.Customization??=new();
            copy.Customization.Head=Pick(look.Head)??copy.Customization.Head;
            copy.Customization.Body=Pick(look.Body)??copy.Customization.Body;
            copy.Customization.Feet=Pick(look.Feet)??copy.Customization.Feet;
            copy.Customization.Hands=Pick(look.Hands)??copy.Customization.Hands;
            copy.Customization.Voice=Pick(look.Voice)??copy.Customization.Voice;
        }
        copy.Info!.Nickname=staff?"Lab Staff":"The Gift";copy.Info.LowerNickname=copy.Info.Nickname.ToLowerInvariant();copy.Info.MainProfileNickname=copy.Info.Nickname;
        copy.Info.Settings!.StandingForKill=0;copy.Info.Settings.AggressorBonus=0;
        if(staff)
        {
            if(!templates.Items.ContainsKey(new MongoId(StaffVest)))throw new InvalidOperationException("Native Security vest is unavailable for Lab staff.");
            items.Add(new Item{Id=new MongoId(),Template=new MongoId(StaffVest),ParentId=root.Id.ToString(),SlotId="TacticalVest",Upd=new(){SpawnedInSession=true}});
            if(bots.Types.TryGetValue("bosssanitar",out var medic)&&medic?.BotAppearance is {} medical)
            {
                MongoId? Pick(Dictionary<MongoId,double>? pool)=>pool?.Count>0?pool.Keys.ElementAt(Random.Shared.Next(pool.Count)):(MongoId?)null;
                copy.Customization??=new();copy.Customization.Body=Pick(medical.Body)??copy.Customization.Body;
                copy.Customization.Feet=Pick(medical.Feet)??copy.Customization.Feet;copy.Customization.Hands=Pick(medical.Hands)??copy.Customization.Hands;
            }
            copy.Info.Settings.Experience=0;copy.Info.Settings.UseSimpleAnimator=false;
        }
        return copy;
    }
}

