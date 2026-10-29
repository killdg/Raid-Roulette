using System;
using System.Globalization;
namespace RaidRoulette.TestingClient;

public sealed class RaidEventNotice
{
    private string? eventId;
    private bool consumed;
    public void Begin(){eventId=null;consumed=false;}
    public void Observe(string context)
    {
        if(consumed||string.IsNullOrWhiteSpace(context))return;
        int separator=context.IndexOf(": ",StringComparison.Ordinal);
        if(separator<0)return;
        var parts=context.Substring(separator+2).Split(new[]{' ','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length==0)return;
        string id=parts[0];
        if(id.Length>0)eventId=id;
    }
    public string? Consume()
    {
        if(consumed||eventId==null)return null;
        consumed=true;
        string title=eventId switch
        {
            "schizophrenia"=>"S̴̜͆c̷̯̈h̴̗̃ï̸͉z̶̻͊o̶̖͝p̷͇͘h̷̢̃r̴̦̂ê̶̯n̴͝ͅi̴̭̚a̵͊ͅ",
            "ak50"=>"AK50", "normal"=>"Normal", "pmc_sweep"=>"PMC Sweep", "geared"=>"GEARED", "high_value_loot"=>"High-Value Loot",
            _=>CultureInfo.InvariantCulture.TextInfo.ToTitleCase(eventId.Replace('_',' '))
        };
        return "Raid Event : "+title;
    }
}


