using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using RaidRoulette.Shared;
namespace RaidRoulette.TestingClient;

internal sealed class RaidChanceSettings
{
 private ConfigEntry<RaidChanceMode> mode=null!;
 private ConfigEntry<float> frequency=null!;
 private ConfigEntry<string> summary=null!;
 private readonly Dictionary<string,ConfigEntry<float>> entries=new();
 internal void Bind(ConfigFile config,Action changed)
 {
  mode=config.Bind("2. Raid chances","Chance mode",RaidChanceMode.OverallChance,"OverallChance uses the overall event rate and each nonzero event's relative share. IndividualPercentages uses exact raid percentages; total must be at most 100. Unallocated or map-ineligible chances become Normal.");
  frequency=config.Bind("2. Raid chances","Any event chance (%)",40f,new ConfigDescription("OverallChance mode only: 40 means 60% Normal and 40% split across nonzero event shares before map eligibility. Unsupported shares become Normal.",new AcceptableValueRange<float>(0,100)));
  summary=config.Bind("2. Raid chances","Chance status","",new ConfigDescription("Validation of your percentages. Invalid changes are not applied by the server.",null,new ConfigurationManagerAttributes{ReadOnly=true,HideDefaultButton=true}));
  var defaults=new HashSet<string>{"the_gift","clone_wars","smokers_lobby","time_warp","stormtide","schizophrenia","gun_game","boss_hunt","cultist_invasion","posers_everywhere"};
  foreach(var option in Enum.GetValues(typeof(RaidChoice)).Cast<RaidChoice>().Where(c=>c!=RaidChoice.Random))
  {
   string id=option.ToString().ToLowerInvariant();float initial=id=="normal"?60:defaults.Contains(id)?4:0;
   entries[id]=config.Bind("3. Raid percentages",option.ToString().Replace('_',' ')+" (%)",initial,new ConfigDescription("IndividualPercentages: exact chance out of 100. OverallChance: relative event share (Normal entry ignored). Zero excludes this event from automatic selection. Unsupported map/asset shares become Normal. Explicit next-raid choices can still select zero-share events.",new AcceptableValueRange<float>(0,100)));
   entries[id].SettingChanged+=(_,_)=>{Refresh();changed();};
  }
  mode.SettingChanged+=(_,_)=>{Refresh();changed();};frequency.SettingChanged+=(_,_)=>{Refresh();changed();};Refresh();
 }
 internal RaidChanceProfile Snapshot()=>new(){Mode=mode.Value.ToString(),EventChance=frequency.Value,Percentages=entries.ToDictionary(p=>p.Key,p=>(double)p.Value.Value)};
 private void Refresh()
 {
  var p=Snapshot();bool valid=RaidChances.Valid(p,entries.Keys,out var error);
  summary.Value=!valid?error:p.Mode=="OverallChance"?$"{100-p.EventChance:0.##}% Normal + {p.EventChance:0.##}% event pool; unsupported shares go to Normal.":$"Configured {p.Percentages.Values.Sum():0.##}% / 100%; unused and unsupported chances go to Normal.";
 }
}
