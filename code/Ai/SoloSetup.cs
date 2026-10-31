using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Ai;

public sealed record OpponentSetup(int Seat, string Name, BotDifficulty Difficulty);
public sealed record SoloSetupData(int OpponentCount, OpponentSetup[] Opponents);

/// <summary>Pure, copy-on-write setup. Display names never seed gameplay decisions.</summary>
public static class SoloSetup
{
    public const int MaximumOpponents = 4;
    public const int MaximumNameLength = 24;
    static readonly string[] Names = {
        "Alex", "Bailey", "Blake", "Casey", "Charlie", "Cleo", "Dakota", "Drew",
        "Eden", "Ellis", "Emery", "Finley", "Frankie", "Harper", "Hazel", "Indigo",
        "Jamie", "Jesse", "Kai", "Kit", "Lane", "Lee", "Morgan", "Noel", "Oakley",
        "Parker", "Quinn", "Remy", "Riley", "Robin", "Sage", "Sam", "Sky", "Taylor"
    };

    public static Guid PlayerId(int seat)
    {
        if (seat < 0 || seat >= MaximumOpponents) throw new ArgumentOutOfRangeException(nameof(seat));
        return new Guid($"{seat+2:x8}-0000-0000-0000-{seat+2:x12}");
    }
    public static string CleanName(string value)
    {
        var name=new string((value ?? "").Where(c=>!char.IsControl(c)).ToArray()).Trim();
        return name[..Math.Min(MaximumNameLength,name.Length)];
    }

    public static SoloSetupData Create(int seed) => Normalize(null,seed);
    public static SoloSetupData Normalize(SoloSetupData value,int seed)
    {
        var random=new Random(seed);
        var names=Names.OrderBy(_=>random.Next()).ToArray();
        var slots=new OpponentSetup[MaximumOpponents];
        var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(var i=0;i<MaximumOpponents;i++)
        {
            var old=value?.Opponents?.FirstOrDefault(p=>p != null && p.Seat==i);
            var name=CleanName(old?.Name);
            if(string.IsNullOrEmpty(name)) name=names.First(n=>!used.Contains(n));
            used.Add(name);
            slots[i]=new(i,name,old!=null && Enum.IsDefined(old.Difficulty)?old.Difficulty:BotDifficulty.Normal);
        }
        return new(Math.Clamp(value?.OpponentCount ?? 2,1,MaximumOpponents),slots);
    }
    public static SoloSetupData Resize(SoloSetupData value,int count) => value with { OpponentCount=Math.Clamp(count,1,MaximumOpponents),Opponents=value.Opponents.ToArray() };
    public static SoloSetupData Edit(SoloSetupData value,int seat,string name,BotDifficulty difficulty)
    {
        if(seat<0 || seat>=MaximumOpponents || !Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(seat));
        return value with { Opponents=value.Opponents.Select(p=>p.Seat==seat ? p with {Name=CleanName(name),Difficulty=difficulty}:p).ToArray() };
    }
    public static SoloSetupData Randomize(SoloSetupData value,int seed)
    {
        var fresh=Create(seed);
        return value with { Opponents=value.Opponents.Select((p,i)=>p with {Name=fresh.Opponents[i].Name}).ToArray() };
    }
    public static OpponentSetup[] StartRoster(SoloSetupData value,int fallbackSeed)
    {
        var clean=Normalize(value,fallbackSeed);
        return clean.Opponents.Take(clean.OpponentCount).Select(p=>p with { }).ToArray();
    }
}
