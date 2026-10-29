using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
namespace RaidRoulette.TestingClient;

public static class ConfigurationImport
{
 public static bool Import(ConfigFile config,Action<string>? log=null)
 {
  try
  {
   string target=Path.GetFullPath(config.ConfigFilePath);
   bool Sections(string path)=>File.Exists(path)&&File.ReadLines(path).Any(line=>line.TrimStart().StartsWith("[",StringComparison.Ordinal));
   if(Sections(target))return false;
   string folder=Path.GetDirectoryName(target)!;
   if(!Directory.Exists(folder))return false;
   var candidates=Directory.GetFiles(folder,"com.*.raidroulette.testing.cfg").Where(path=>!string.Equals(Path.GetFullPath(path),target,StringComparison.OrdinalIgnoreCase)&&Sections(path)).ToArray();
   if(candidates.Length!=1){if(candidates.Length>1)log?.Invoke("[RR-CONFIG] Multiple existing preference files found; automatic import skipped.");return false;}
   File.Copy(candidates[0],target,true);config.Reload();
   log?.Invoke("[RR-CONFIG] Existing Raid Roulette preferences imported; original file retained.");return true;
  }
  catch(Exception e){log?.Invoke("[RR-CONFIG] Preference import unavailable: "+e.GetBaseException().Message);return false;}
 }
}
