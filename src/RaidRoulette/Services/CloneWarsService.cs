using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Utils.Cloners;

namespace RaidRoulette.Services;

public sealed record CloneWarsSnapshot(string Nickname,int? Level,int? Experience,
    Customization Appearance,Skills Skills,BotBaseInventory Kit);

public sealed class CloneWarsService(ICloner cloner)
{
    public CloneWarsSnapshot Capture(BotBase player)
    {
        if(player.Info is null || string.IsNullOrEmpty(player.Info.Nickname) || player.Customization is null || player.Skills is null || player.Inventory?.Equipment is null)
            throw new InvalidOperationException("The PMC profile is missing appearance, skills, nickname or equipment.");
        var root=player.Inventory.Equipment.Value;
        var items=player.Inventory.Items??[];
        if(items.Count(i=>i.Id==root)!=1)throw new InvalidOperationException("The PMC equipment root is missing or duplicated.");
        var children=items.Where(i=>!string.IsNullOrEmpty(i.ParentId)).GroupBy(i=>i.ParentId!).ToDictionary(g=>g.Key,g=>g.ToArray());
        var included=new HashSet<MongoId>();var selected=new List<Item>();var pending=new Queue<Item>();
        pending.Enqueue(items.Single(i=>i.Id==root));
        while(pending.Count>0)
        {
            var item=pending.Dequeue();
            if(!included.Add(item.Id))throw new InvalidOperationException("Duplicate/cyclic equipment item IDs in the PMC kit.");
            selected.Add(item);
            if(children.TryGetValue(item.Id.ToString(),out var next))foreach(var child in next)pending.Enqueue(child);
        }
        var kit=new BotBaseInventory {
            Equipment=root,Items=cloner.Clone(selected)!,
            FastPanel=player.Inventory.FastPanel?.Where(p=>included.Contains(p.Value)).ToDictionary(p=>p.Key,p=>p.Value),
            FavoriteItems=player.Inventory.FavoriteItems?.Where(included.Contains).ToArray()
        };
        return new(player.Info.Nickname,player.Info.Level,player.Info.Experience,
            cloner.Clone(player.Customization)!,cloner.Clone(player.Skills)!,kit);
    }
    public BotBase CopyForResponse(CloneWarsSnapshot snapshot,BotBase original)
    {
        string role=original.Info?.Settings?.Role??"";
        if(!(role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase)||role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase)))return original;
        var copy=cloner.Clone(original)??throw new InvalidOperationException("Could not copy generated PMC.");
        Apply(snapshot,copy,new BotGenerationDetails {IsPmc=true,Role=role,Side=original.Info?.Side??""});
        return copy;
    }
    public bool Apply(CloneWarsSnapshot snapshot,BotBase bot,BotGenerationDetails details)
    {
        if(details.IsPlayerScav || !details.IsPmc || !(details.Role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase)||details.Role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase)))return false;
        if(bot.Info?.Settings is null)throw new InvalidOperationException("Generated PMC has no AI role settings.");
        // Construct everything before replacing the generated profile. Original player and
        // frozen snapshot are never handed to the AI or mutated by this method.
        var kit=cloner.Clone(snapshot.Kit)!;
        var appearance=cloner.Clone(snapshot.Appearance)!;var skills=cloner.Clone(snapshot.Skills)!;
        var kitItems=kit.Items??throw new InvalidOperationException("Frozen kit has no items.");
        var oldRoot=kit.Equipment!.Value;
        var ids=kitItems.ToDictionary(i=>i.Id,i=>i.SlotId=="Dogtag"&&i.ParentId==oldRoot.ToString()?new MongoId(RaidRoulette.Shared.CloneTagIdentity.NewId()):new MongoId());
        var newRoot=ids[oldRoot];var profileId=new MongoId();
        foreach(var item in kitItems)
        {
            var oldId=item.Id;item.Id=ids[oldId];
            if(oldId==oldRoot){item.ParentId=null;continue;}
            if(string.IsNullOrEmpty(item.ParentId) || !MongoId.IsValidMongoId(item.ParentId) || !ids.TryGetValue(new MongoId(item.ParentId),out var parent))
                throw new InvalidOperationException("Equipment contains an unresolved parent reference.");
            item.ParentId=parent.ToString();
        }
        kit.Equipment=newRoot;
        kit.FastPanel=kit.FastPanel?.ToDictionary(p=>p.Key,p=>ids[p.Value]);
        kit.FavoriteItems=kit.FavoriteItems?.Select(i=>ids[i]).ToArray();
        // Retain the native generated PMC dogtag if the player has no equipped tag.
        // Foreign dogtags carried inside containers are copied intact.
        if(!kitItems.Any(i=>i.ParentId==newRoot.ToString() && i.SlotId=="Dogtag"))
        {
            var tag=bot.Inventory?.Items?.FirstOrDefault(i=>i.SlotId=="Dogtag");
            tag=tag!=null?cloner.Clone(tag)!:new Item{Id=new MongoId(),Template=new MongoId(details.Role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase)?"59f32bb586f774757e1e8442":"59f32c3b86f77472a31742f0"),SlotId="Dogtag"};
            tag.Id=new MongoId(RaidRoulette.Shared.CloneTagIdentity.NewId());tag.ParentId=newRoot.ToString();kitItems.Add(tag);
        }
        foreach(var tag in kitItems.Where(i=>i.ParentId==newRoot.ToString() && i.SlotId=="Dogtag"))
        {
            tag.Upd??=new();tag.Upd.Dogtag??=new();
            tag.Upd.SpawnedInSession=true;
            tag.Upd.Dogtag.Nickname=snapshot.Nickname;tag.Upd.Dogtag.Level=snapshot.Level;
            tag.Upd.Dogtag.ProfileId=profileId.ToString();tag.Upd.Dogtag.AccountId=bot.Aid?.ToString();
        }
        bot.Id=profileId;bot.Customization=appearance;bot.Skills=skills;bot.Inventory=kit;
        bot.Info.Nickname=snapshot.Nickname;bot.Info.LowerNickname=snapshot.Nickname.ToLowerInvariant();
        bot.Info.MainProfileNickname=snapshot.Nickname;bot.Info.Level=snapshot.Level;bot.Info.Experience=snapshot.Experience;
        // Keep generated faction, difficulty, health, affiliations and AI settings. Role
        // changes would break native spawn caches keyed by USEC/BEAR.
        return true;
    }
}
