using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;
using CitySim.Match;

namespace CitySim.Ai;

public enum BotStyle { Balanced, Housing, Trade, Nature, Industry, Culture }

public static class BotStyles
{
    public static BotStyle ForSeat(int matchSeed,int seat)
    {
        var styles=Enum.GetValues<BotStyle>();
        var random=new Random(unchecked(matchSeed ^ 19349663));
        for(var i=styles.Length-1;i>0;i--) {var j=random.Next(i+1);(styles[i],styles[j])=(styles[j],styles[i]);}
        return styles[Math.Abs(seat)%styles.Length];
    }
    public static Tag TagFor(BotStyle style) => style switch {
        BotStyle.Housing=>Tag.Home, BotStyle.Trade=>Tag.Trade, BotStyle.Nature=>Tag.Nature,
        BotStyle.Industry=>Tag.Industry, BotStyle.Culture=>Tag.Culture, _=>Tag.None
    };
}

/// <summary>Values visible board results through the real rules. Never reads rival hands or contracts.</summary>
internal sealed class BotEvaluation
{
    readonly Guid owner;
    readonly BotDifficulty difficulty;
    readonly BotStyle style;
    readonly ContractDef contract;
    internal BotEvaluation(Guid owner,BotDifficulty difficulty,BotStyle style,ContractDef contract)
    { this.owner=owner; this.difficulty=difficulty; this.style=style; this.contract=contract; }

    internal double Horizon(MatchState board) => Math.Min(board.Settings.Rounds-board.Round+1,difficulty switch { BotDifficulty.Easy=>1.5,BotDifficulty.Normal=>3.5,_=>8.0 });
    internal double CashValue(double cash,MatchState board)
    {
        // Cash beyond plausible future purchases has only its actual spare-coin value.
        var futureMarkets=Math.Max(0,board.Settings.Rounds-board.Round);
        var useful=Math.Min(Math.Max(0,cash),futureMarkets*6);
        return cash/board.Settings.CoinsPerEndVp + useful*0.12;
    }
    internal double Affinity(string cardId)
    {
        var tag=BotStyles.TagFor(style);
        return tag!=Tag.None && CardLibrary.Get(cardId).Tags.HasFlag(tag) ? 0.22 : 0;
    }

    internal double Score(MatchState board)
    {
        var me=board.Players[owner];
        var horizon=Math.Max(0,Horizon(board));
        var payout=Payout.Calculate(board);
        var entries=payout.Entries.Where(e=>e.Player==owner).ToArray();
        var coins=entries.Sum(e=>e.Coins)*horizon;
        var vp=me.Vp+entries.Sum(e=>e.Vp)*horizon;
        var own=board.Cells().Where(c=>c.Owner==owner).ToArray();
        // A Vineyard's skipped payouts and Startup's ramp are evaluated at their real ages.
        foreach(var cell in own)
        foreach(var effect in CardLibrary.Get(cell.CardId).Effects.Where(e=>e.Rule is "delayed-coins" or "ramping-coins"))
        {
            double Income(int age) => effect.Rule=="delayed-coins" ? age>=effect.Cap ? effect.Amount:0 : Math.Min(effect.Cap,(age+1)*effect.Amount);
            coins-=Income(cell.Age)*horizon;
            for(var t=0;t<(int)Math.Ceiling(horizon);t++) coins+=Income(cell.Age+t)*Math.Min(1,horizon-t);
        }
        // Forest maturity is a fixed, one-time owner award, independent of park size.
        foreach(var cell in own)
            {
                var growth=CardLibrary.Get(cell.CardId).Effects.FirstOrDefault(e=>e.Hook==EffectHook.Growth && e.Rule=="forest");
                if(growth==null || cell.Age>=growth.Cap || growth.Cap-cell.Age>horizon) continue;
                vp+=growth.Amount;
            }
        if(board.RushHourActive(owner)) coins-=entries.Where(e=>e.Reason=="Rush Hour").Sum(e=>e.Coins)*Math.Max(0,horizon-1);
        var finalScores=FinalScoring.Calculate(board);
        var final=finalScores.Players.Single(p=>p.Player==owner);
        vp+=final.Lines.Where(l=>l.Source!=null).Sum(l=>l.Vp);
        var competitionWeight=0.25+0.75*(board.Round-1)/Math.Max(1.0,board.Settings.Rounds-1);
        vp+=final.Lines.Where(l=>l.Reason is "Longest Road" or "Biggest Park Owner" or "Full House").Sum(l=>l.Vp)*competitionWeight;
        // Progress has value even before it crosses an award threshold.
        if(board.Settings.EnableGoals)
        foreach(var goal in board.Goals.Active.Where(g=>g.Claim==null))
        {
            var def=GoalLibrary.Get(goal.GoalId); var progress=GoalRules.Evaluate(board,owner,def);
            var fraction=Math.Clamp(progress.Current/(double)Math.Max(1,progress.Target),0,1);
            vp+=board.Settings.GoalVp*Math.Pow(fraction,1.5)*0.65;
        }
        if(contract!=null)
        {
            var p=Contracts.Evaluate(board,owner,contract);
            var fraction=Math.Clamp(p.Current/(double)Math.Max(1,p.Target),0,1);
            vp+=contract.Vp*(fraction>=1 ? 1:0.55*Math.Pow(fraction,1.4));
        }
        // Receiving free cards next round is valuable while there is time to use them.
        var futureRounds=Math.Max(0,horizon-1);
        var cells=board.Cells().ToArray();
        var room=Math.Min(1,(board.Width*board.Height-cells.Length)/Math.Max(1.0,board.Players.Count*futureRounds*2));
        vp+=own.Count(c=>c.CardId is "warehouse" or "builders-yard")*Math.Min(3,futureRounds)*0.45*room;
        foreach(var cell in own)
        foreach(var effect in CardLibrary.Get(cell.CardId).Effects)
        {
            coins+=effect.Rule switch {
                "park-coins"=>effect.Amount*futureRounds*0.6*room,
                "contractor-coins"=>effect.Amount*futureRounds*0.8*room,
                "first-building-beside-own"=>effect.Amount*futureRounds*0.7*room,
                "warehouse-street-coins"=>effect.Amount*futureRounds*0.7*room,
                _=>0
            };
            if(effect.Rule=="roundabout-vp")
            {
                var open=new[]{(0,-1),(1,0),(0,1),(-1,0)}.Count(d=>board.InBounds(cell.X+d.Item1,cell.Y+d.Item2) && board.Get(cell.X+d.Item1,cell.Y+d.Item2)==null);
                vp+=open*Math.Min(1,futureRounds*0.15)*effect.Amount*room;
            }
            if(effect.Rule=="street-building-vp")
            {
                // Forecast only future construction on empty edge neighbours. Buildings
                // already beside this Street have either paid once or predate the road.
                var open=new[]{(0,-1),(1,0),(0,1),(-1,0)}.Count(d=>board.InBounds(cell.X+d.Item1,cell.Y+d.Item2) && board.Get(cell.X+d.Item1,cell.Y+d.Item2)==null);
                vp+=open*Math.Min(1,futureRounds*0.12)*effect.Amount*room*0.5;
            }
        }
        // Emergencies are uniformly selected from visible Homes. Forecast subsequent
        // rounds only, since this round's emergency may already have occurred.
        var homes=cells.Where(BoardRules.IsHome).ToArray();
        var hospitals=cells.Where(c=>c.CardId=="hospital").ToArray();
        if(homes.Length>0 && hospitals.Any(c=>c.Owner==owner))
        {
            var effect=CardLibrary.Get("hospital").Effects.Single(e=>e.Rule=="hospital-emergency");
            var covered=homes.Count(home=>hospitals.Where(h=>BoardRules.Distance(home,h)<=effect.Range).OrderBy(h=>BoardRules.Distance(home,h)).ThenBy(h=>h.Order).FirstOrDefault()?.Owner==owner);
            vp+=covered/(double)homes.Length*Math.Max(0,horizon-1)*effect.Amount;
        }
        var rivalWeight=difficulty switch {BotDifficulty.Easy=>0.01,BotDifficulty.Normal=>0.04,_=>0.08};
        var rival=finalScores.Players.Where(p=>p.Player!=owner).Select(p=>p.EarnedVp+p.Lines.Where(l=>l.Source!=null).Sum(l=>l.Vp)+payout.Entries.Where(e=>e.Player==p.Player).Sum(e=>e.Vp)*horizon).DefaultIfEmpty(0).Max();
        return vp+CashValue(me.Coins+coins,board)-rival*rivalWeight;
    }

    internal double ContractFit(MatchState board,ContractDef offer)
    {
        var progress=Contracts.Evaluate(board,owner,offer);
        var styleTag=BotStyles.TagFor(style);
        var rounds=Math.Max(1,board.Settings.Rounds);
        var tagShare=offer.Tag==Tag.None ? 0 : CardLibrary.All.Where(c=>c.Type is not (CardType.Event or CardType.Road)).Count(c=>c.Tags.HasFlag(offer.Tag))/(double)CardLibrary.All.Count(c=>c.Type is not (CardType.Event or CardType.Road));
        var expected=offer.Kind switch {
            ContractKind.TagCount=>progress.Current+(rounds*2.5)*tagShare*(styleTag==offer.Tag ? 1.3:1),
            ContractKind.Coins=>offer.Target*0.8,
            ContractKind.DistinctCivic=>4.5,
            ContractKind.NearbyHomeShops=>style is BotStyle.Trade or BotStyle.Housing ? 2.8:2.1,
            ContractKind.RivalNeighbours=>3.4,
            _=>progress.Current
        };
        return offer.Vp*Math.Pow(Math.Clamp(expected/offer.Target,0,1),1.5);
    }

    internal static MatchState PublicClone(MatchState source,Guid owner,string addedCard=null,int purchaseCost=0)
    {
        var players=source.Players.Values.Select(p=>new PlayerCheckpoint(p.Id,p.Name,p.Coins,p.Vp,0,
            p.Id==owner ? p.Hand.Concat(addedCard==null?Array.Empty<string>():new[]{addedCard}).ToArray():Array.Empty<string>())).ToArray();
        var goals=new GoalsCheckpoint(false,0,1,source.Goals.Active.ToArray(),source.Goals.History.ToArray(),Array.Empty<string>(),Array.Empty<string>());
        var copy=MatchState.Restore(new MatchCheckpoint(2,source.Width,source.Height,0,source.Settings,source.Round,MatchPhase.Build,
            source.PhaseEndsAt,source.NextPlacementOrder-1,source.LastPaidRound,source.Cells().ToArray(),source.Claims.ToArray(),players,goals,Array.Empty<ContractReveal>(),null,
            source.TriggerUsageSnapshot(),source.ActivitySnapshot(),source.BuilderChoiceSnapshot().Where(c=>c.Player==owner).ToArray()));
        if(!copy.ChangeCoins(owner,-purchaseCost)) throw new InvalidOperationException("Unaffordable public-state evaluation.");
        return copy;
    }
}
