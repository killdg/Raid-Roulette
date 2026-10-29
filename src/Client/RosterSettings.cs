using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
namespace RaidRoulette.TestingClient;

// Standard ConfigEntry values work even when a Configuration Manager ignores
// custom drawers. Display refreshes do not trigger a config-file write.
public sealed class RosterSettings
{
    public const int PageSize=10;
    private readonly ConfigFile config;
    public readonly ConfigEntry<string> Filter,Overview,Roster,Context,WaveStatus,Notice;
    public readonly ConfigEntry<bool> AliveOnly,Expanded;
    private readonly List<ConfigurationManagerAttributes> details=new();
    public readonly ConfigEntry<int> Page;
    public readonly ConfigEntry<string>[] Identity=new ConfigEntry<string>[PageSize];
    public readonly ConfigEntry<string>[] State=new ConfigEntry<string>[PageSize];
    private const string Section="9. Advanced / Live raid information";
    public RosterSettings(ConfigFile config,Action? visibilityChanged=null,Action<ConfigEntryBase>? foldoutDrawer=null)
    {
        this.config=config;
        Expanded=config.Bind(Section,"00 Show live raid details",false,new ConfigDescription("Expand or collapse the live raid roster below. The F8 monitor keeps working while collapsed.",null,new ConfigurationManagerAttributes{CustomDrawer=foldoutDrawer,HideDefaultButton=true,Order=-100}));
        Filter=Field("01 Raid filter","","Match AI name, role, side or profile ID. All registered AI across the map are eligible, regardless of distance.");
        AliveOnly=Field("02 Raid alive only",false,"Hide dead/loading actors. Dead actors disappear when the game unregisters them.");
        Page=Field("03 Raid page",1,"Ten AI per page. The displayed page is clamped to the filtered result count.",new AcceptableValueRange<int>(1,1000));
        Overview=Text("AI overview","No active raid","Live counts, not an action button. Updated every second. Reset only clears this text until the next refresh.");
        Roster=Text("AI roster","No active raid","Live page information, not an Open button. Use 03 Raid page and 01 Raid filter to browse all registered AI.");
        Context=Text("Raid event","No active raid","Actual last generated raid context.");
        WaveStatus=Text("Wave and Gift status","No active raid","Overrun queue / Gift trail counters when applicable.");
        Notice=Text("Roster notice","","Refresh errors and status. Values are observational; changing a displayed value does not control an AI.");
        for(int i=0;i<PageSize;i++)
        {
            string key="Raid bot "+(i+1).ToString("00");
            Identity[i]=Text(key+" identity","—","Nickname, native role, faction and short ID. Full ID is included in the corresponding status row.");
            State[i]=Text(key+" status","—","Alive/dead/loading, level, distance from the player, full profile ID.");
        }
        Expanded.SettingChanged+=(_,_)=>{foreach(var attribute in details)attribute.Browsable=Expanded.Value;visibilityChanged?.Invoke();};
    }
    private ConfigEntry<T> Field<T>(string key,T value,string description,AcceptableValueBase? range=null,bool readOnly=false)
    {
        var attributes=new ConfigurationManagerAttributes{Browsable=Expanded.Value,ReadOnly=readOnly,HideDefaultButton=readOnly};details.Add(attributes);
        return config.Bind(Section,key,value,new ConfigDescription(description,range,attributes));
    }
    private ConfigEntry<string> Text(string key,string value,string description)=>Field(key,value,"Live raid roster. "+description,null,true);
    public void Publish(RosterEntry[] entries,string context,string waveStatus,string notice,bool inRaid)
    {
        var selected=RosterView.Page(entries,Filter.Value,AliveOnly.Value,Page.Value,PageSize);
        bool previous=config.SaveOnConfigSet;config.SaveOnConfigSet=false;
        try
        {
            Set(Overview,inRaid?"Raid AI | "+RosterView.Summary(entries):"No active raid");
            Set(Roster,$"Raid roster | Page {selected.Number}/{selected.Pages} | Matching {selected.Total} | Registered {entries.Length}");
            Set(Context,inRaid?context:"No active raid");Set(WaveStatus,inRaid?waveStatus:"No active raid");Set(Notice,notice);
            if(Page.Value!=selected.Number)Page.Value=selected.Number;
            for(int i=0;i<PageSize;i++)
            {
                if(i>=selected.Rows.Length){Set(Identity[i],"—");Set(State[i],"—");continue;}
                var row=selected.Rows[i];string shortId=row.Id.Length>6?row.Id.Substring(row.Id.Length-6):row.Id;
                Set(Identity[i],$"{row.Name} | {row.Role}/{row.Side} | #{shortId}");
                string state=row.Alive==true?"Alive":row.Alive==false?"Dead":"Loading";
                Set(State[i],$"{state} | Lv {row.Level} | {(row.Distance.HasValue?row.Distance.Value.ToString("0")+"m":"?m")} | {row.Id}");
            }
        }
        finally{config.SaveOnConfigSet=previous;}
    }
    private static void Set(ConfigEntry<string> entry,string value){if(entry.Value!=value)entry.Value=value;}
}
