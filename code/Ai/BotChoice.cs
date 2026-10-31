using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Ai;

/// <summary>Seeded, rating-weighted decisions shared by market, contract and build choices.</summary>
public static class BotChoice
{
    public const string PolicyVersion="2026-10-30.5-weighted-choices";
    public static double BestChoiceChance(BotDifficulty difficulty) => difficulty switch {
        BotDifficulty.Easy=>0.55, BotDifficulty.Normal=>0.75, BotDifficulty.Hard=>0.90,
        _=>throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    /// <summary>Best-rated ties share the accuracy budget. Other credible options are
    /// weighted by rating; clearly inferior and nonfinite options have no chance.
    /// A sole credible option is selected with certainty.</summary>
    public static double[] Probabilities(IReadOnlyList<double> ratings,BotDifficulty difficulty)
    {
        var accuracy=BestChoiceChance(difficulty);
        var probabilities=new double[ratings.Count];
        var finite=Enumerable.Range(0,ratings.Count).Where(i=>double.IsFinite(ratings[i])).ToArray();
        if(finite.Length==0) return probabilities;
        var best=finite.Max(i=>ratings[i]);
        var fraction=difficulty switch {BotDifficulty.Easy=>0.65,BotDifficulty.Normal=>0.40,_=>0.25};
        var minimumGap=difficulty switch {BotDifficulty.Easy=>1.0,BotDifficulty.Normal=>0.75,_=>0.5};
        var gap=Math.Max(minimumGap,Math.Abs(best)*fraction);
        var top=finite.Where(i=>Math.Abs(ratings[i]-best)<=1e-8).ToArray();
        var other=finite.Where(i=>ratings[i]>=best-gap && best-ratings[i]>1e-8).ToArray();
        var topBudget=other.Length==0?1:accuracy;
        foreach(var i in top) probabilities[i]=topBudget/top.Length;
        // Subtracting the best keeps exponentials finite. A higher-rated alternative
        // always has greater weight than a lower-rated one.
        var temperature=Math.Max(0.25,gap*0.5);
        var weights=other.Select(i=>Math.Exp((ratings[i]-best)/temperature)).ToArray();
        var sum=weights.Sum();
        for(var j=0;j<other.Length;j++) probabilities[other[j]]=(1-topBudget)*weights[j]/sum;
        return probabilities;
    }

    public static int PickIndex(IReadOnlyList<double> ratings,BotDifficulty difficulty,Random random)
    {
        var probabilities=Probabilities(ratings,difficulty);
        var remaining=random.NextDouble();var last=-1;
        for(var i=0;i<probabilities.Length;i++)
        {
            if(probabilities[i]<=0) continue;
            last=i;remaining-=probabilities[i];if(remaining<0) return i;
        }
        return last;
    }
}
