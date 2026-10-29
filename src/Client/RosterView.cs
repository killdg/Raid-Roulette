using System;
using System.Collections.Generic;
using System.Linq;
namespace RaidRoulette.TestingClient;
public sealed class RosterEntry
{
    public readonly string Id,Name,Role,Side;
    public readonly int Level;
    public readonly bool? Alive;
    public readonly float? Distance;
    public RosterEntry(string id,string name,string role,string side,int level,bool? alive,float? distance)
    {Id=id;Name=name;Role=role;Side=side;Level=level;Alive=alive;Distance=distance;}
}
public static class RosterView
{
    public static RosterPage Page(RosterEntry[] entries,string filter,bool aliveOnly,int requested,int pageSize)
    {
        if(pageSize<1)throw new ArgumentOutOfRangeException(nameof(pageSize));
        var filtered=entries.Where(e=>Matches(e,filter,aliveOnly)).ToArray();
        int pages=Math.Max(1,(filtered.Length+pageSize-1)/pageSize);
        int page=Math.Max(1,Math.Min(pages,requested));
        return new RosterPage(filtered.Skip((page-1)*pageSize).Take(pageSize).ToArray(),page,pages,filtered.Length);
    }

    public static bool Matches(RosterEntry row,string filter,bool aliveOnly)
        =>(!aliveOnly||row.Alive==true)&&(string.IsNullOrWhiteSpace(filter)||new[]{row.Id,row.Name,row.Role,row.Side}.Any(value=>value.IndexOf(filter.Trim(),StringComparison.OrdinalIgnoreCase)>=0));
    public static string Summary(IEnumerable<RosterEntry> entries)
    {
        var rows=entries.ToArray();
        return $"Registered: {rows.Length} | Alive: {rows.Count(r=>r.Alive==true)} | Dead: {rows.Count(r=>r.Alive==false)} | Loading: {rows.Count(r=>!r.Alive.HasValue)}\n"+
            string.Join(", ",rows.GroupBy(r=>r.Role).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(g=>$"{g.Key}: {g.Count(r=>r.Alive==true)} alive / {g.Count()} registered"));
    }
}

public sealed class RosterPage
{
    public readonly RosterEntry[] Rows;
    public readonly int Number,Pages,Total;
    public RosterPage(RosterEntry[] rows,int number,int pages,int total){Rows=rows;Number=number;Pages=pages;Total=total;}
}
