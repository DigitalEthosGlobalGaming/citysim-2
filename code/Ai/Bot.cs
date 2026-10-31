using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CitySim.Cards;
using CitySim.Match;

namespace CitySim.Ai;

public enum BotDifficulty { Easy, Normal, Hard }
// Retained for historical simulation callers; live matches use BotStyle.
public enum BotStrategy { Balanced, Tag, ParkRush, EconomyFirst }

/// <summary>Seeded opponent with bounded planning steps and separate decision/pacing randomness.</summary>
public sealed class Bot
{
    readonly Guid player;
    readonly MatchState state;
    readonly RoundMachine rounds;
    readonly BotDifficulty difficulty;
    readonly Random decisions, timing;
    readonly BotStyle style;
    readonly Func<bool> finishBuildingQuickly;
    bool finishingBuild;
    MatchPhase lastPhase=(MatchPhase)(-1);
    int lastRound=-1,lastAuction=-1;
    double actAt;
    BotSearch buildSearch,marketSearch;
    PlanningStamp searchStamp;
    OwnLotBid[] lots;
    int lotCursor;
    readonly List<(OwnLotBid Lot,double Value)> offers=new();
    BotEvaluation evaluation;
    readonly record struct PlanningStamp(int Round,int Order,int Claims,long Hand,int Coins);
    PlanningStamp Stamp=>new(state.Round,state.NextPlacementOrder,state.Claims.Count,state.Players[player].HandVersion,state.Players[player].Coins);
    public BotDifficulty Difficulty=>difficulty;
    public BotStyle Style=>style;
    public int RejectedActions { get; private set; }
    public int DecisionsCompleted { get; private set; }
    public int SearchRestarts { get; private set; }
    public double MaximumPlanningStepMilliseconds { get; private set; }
    public double LastPlanningStepMilliseconds { get; private set; }
    void Record(bool accepted) { if(!accepted) RejectedActions++; }

    public Bot(Guid player,MatchState state,RoundMachine rounds,BotDifficulty difficulty,int seed,
        BotStrategy strategy=BotStrategy.Balanced,Func<bool> finishBuildingQuickly=null,BotStyle? style=null)
    {
        if(!state.Players.ContainsKey(player)) throw new ArgumentException("Unknown player",nameof(player));
        if(!Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
        if(!Enum.IsDefined(strategy) || style.HasValue && !Enum.IsDefined(style.Value)) throw new ArgumentOutOfRangeException(nameof(strategy));
        this.player=player;this.state=state;this.rounds=rounds;this.difficulty=difficulty;this.finishBuildingQuickly=finishBuildingQuickly;
        this.style=style ?? strategy switch {BotStrategy.Tag=>BotStyle.Industry,BotStrategy.ParkRush=>BotStyle.Nature,BotStrategy.EconomyFirst=>BotStyle.Trade,_=>BotStyle.Balanced};
        decisions=new(seed);timing=new(unchecked(seed ^ 83492791));
    }
    BotEvaluation Evaluation => evaluation ??= new(player,difficulty,style,
        rounds.GetContractView(player)?.Selected is { } contract ? Contracts.Get(contract):null);
    public void ChooseContract()
    {
        var view=rounds.GetContractView(player);
        if(view==null || view.Selected!=null || view.Choices.Count==0) return;
        var evaluator=new BotEvaluation(player,difficulty,style,null);
        var ranked=view.Choices.Select(id=>(Id:id,Value:evaluator.ContractFit(state,Contracts.Get(id))))
            .OrderByDescending(c=>c.Value).ToArray();
        var pick=ranked[BotChoice.PickIndex(ranked.Select(c=>c.Value).ToArray(),difficulty,decisions)];
        rounds.ChooseContract(player,view.DraftId,pick.Id);evaluation=null;
    }
    void ClearSearch()
    {
        buildSearch=null;marketSearch=null;lots=null;offers.Clear();lotCursor=0;
    }
    public void Update(double now)
    {
        if(!double.IsFinite(now) || !state.Players.ContainsKey(player)) return;
        var view=rounds.GetAuctionView(player);var auction=view?.Id ?? -1;
        var fast=state.Phase==MatchPhase.Build && finishBuildingQuickly?.Invoke()==true;
        if(lastPhase!=state.Phase || lastRound!=state.Round || lastAuction!=auction)
        {
            lastPhase=state.Phase;lastRound=state.Round;lastAuction=auction;
            ClearSearch();evaluation=null;finishingBuild=fast;actAt=now+Delay(now,true);
        }
        else if(finishingBuild!=fast) {finishingBuild=fast;actAt=now+Delay(now);}
        if(state.Phase is not (MatchPhase.Bid or MatchPhase.Build) || state.PhaseEndsAt>=0 && now>=state.PhaseEndsAt) return;
        if(state.Phase==MatchPhase.Bid)
        {
            if(view==null || !view.Eligible || view.Resolving || view.Ready) return;
            MarketStep(view,now);return;
        }
        if(rounds.IsBuildReady(player)) return;
        BuildStep(now);
    }
    double Delay(double now,bool initial=false)
    {
        var remaining=state.PhaseEndsAt<0 ? state.Settings.MiniAuctionSeconds:Math.Max(0,state.PhaseEndsAt-now);
        var seconds=state.Phase==MatchPhase.Build
            ? finishingBuild ? 0.15+timing.NextDouble()*0.20 : initial ? 2+timing.NextDouble()*2 : 1.2+timing.NextDouble()*1.4
            : 0.35+timing.NextDouble()*0.3;
        return Math.Min(seconds,remaining*(state.Phase==MatchPhase.Build?0.15:0.2));
    }
    void Step(BotSearch search)
    {
        var started=Stopwatch.GetTimestamp();search.Step();
        LastPlanningStepMilliseconds=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        MaximumPlanningStepMilliseconds=Math.Max(MaximumPlanningStepMilliseconds,LastPlanningStepMilliseconds);
    }
    void MarketStep(AuctionView view,double now)
    {
        if(lots==null || searchStamp!=Stamp)
        {
            searchStamp=Stamp;marketSearch=null;lotCursor=0;offers.Clear();
            lots=view.Lots.Where(l=>l.Allowed && (long)l.Lot.MinimumBid+state.Settings.BuildCost(CardLibrary.Get(l.Lot.CardId))<=view.AvailableCoins).ToArray();
        }
        if(lotCursor<lots.Length)
        {
            var lot=lots[lotCursor];var card=CardLibrary.Get(lot.Lot.CardId);
            if(card.Id is "grant" or "rush-hour" or "ribbon-cutting")
            {
                var amount=card.Id switch {
                    "grant"=>card.Effects.Single(e=>e.Rule=="coins").Amount,
                    "rush-hour"=>state.RushHourActive(player)?0:Math.Min(4,state.Cells().Count(c=>c.Owner==player && c.CardId=="shop")+state.Players[player].Hand.Count(id=>id=="shop")),
                    _=>Math.Min(4,state.BuildingsPlacedThisRound(player)+state.Players[player].Hand.Count(id=>CardLibrary.Get(id).Type is not (CardType.Road or CardType.Event)))
                };
                var coins=state.Players[player].Coins;
                var value=Evaluation.CashValue(coins-lot.Lot.MinimumBid+amount,state)-Evaluation.CashValue(coins,state);
                offers.Add((lot,value));lotCursor++;
            }
            else
            {
                marketSearch ??= new(BotEvaluation.PublicClone(state,player,card.Id,lot.Lot.MinimumBid),player,Evaluation,difficulty,card.Id,baseline:Evaluation.Score(state));
                Step(marketSearch);
                if(marketSearch.Complete) {offers.Add((lot,marketSearch.Best?.Value ?? double.NegativeInfinity));marketSearch=null;lotCursor++;}
            }
        }
        var deadline=state.PhaseEndsAt>=0 && state.PhaseEndsAt-now<=0.25;
        if(now<actAt || lotCursor<lots.Length && !deadline) return;
        if(deadline && marketSearch?.Best is { } partial) offers.Add((lots[lotCursor],partial.Value));
        var choices=offers.Where(o=>double.IsFinite(o.Value) && o.Value>0.05).ToList();
        choices.Add((null,0)); // Passing is a rated option; bad purchases never enter the draw.
        var chosen=choices[BotChoice.PickIndex(choices.Select(o=>o.Value).ToArray(),difficulty,decisions)];
        if(chosen.Lot!=null)
        {
            Record(rounds.BuyMarketCard(player,view.Id,chosen.Lot.Lot.Index,now));
        }
        else Record(rounds.Ready(player,now));
        DecisionsCompleted++;
    }
    void BuildStep(double now)
    {
        var owner=state.Players[player];
        if(owner.Hand.Count==0) {if(now>=actAt) Record(rounds.Ready(player,now));return;}
        state.ChooseBuilderDefault(player,state.Cells().Count(c=>c.Owner==player && CardLibrary.Get(c.CardId).Type==CardType.Road)>=3);
        if(buildSearch==null || searchStamp.Round!=Stamp.Round || searchStamp.Hand!=Stamp.Hand || searchStamp.Coins!=Stamp.Coins)
        {
            if(buildSearch!=null) SearchRestarts++;
            searchStamp=Stamp;buildSearch=new(BotEvaluation.PublicClone(state,player),player,Evaluation,difficulty);
        }
        Step(buildSearch);
        var deadline=state.PhaseEndsAt-now<=0.5;
        if(now<actAt || !buildSearch.Complete && !deadline) return;
        var chosen=buildSearch.Choose(decisions,move=>move.Valid(state,player));
        // Other players can occupy a planned site while this bot thinks. Keep useful
        // completed work, but choose only a move that is still legal on the live board.
        if(chosen!=null && chosen.Value>=-0.05 && chosen.Move.Valid(state,player))
        {
            Record(chosen.Move.Apply(state,player));DecisionsCompleted++;
        }
        else if(buildSearch.Complete || deadline)
        {
            if(owner.Hand.Count>=state.Settings.HandTarget)
            {
                // Free capacity for future purchases; keep useful blocked cards when there is room.
                var slot=Enumerable.Range(0,owner.Hand.Count).OrderBy(i=>CardLibrary.Get(owner.Hand[i]).Rarity).First();
                Record(state.Discard(player,slot));
            }
            else Record(rounds.Ready(player,now));
        }
        buildSearch=null;actAt=now+Delay(now);
    }
}

/// <summary>Round-robin group budget; individual planning steps are also resumable.</summary>
internal static class BotFrameScheduler
{
    internal static int Run(int count,ref int cursor,double budget,Action<int> update,Func<double> elapsed)
    {
        if(count<=0) {cursor=0;return 0;}
        var start=((cursor%count)+count)%count;var done=0;
        do {update((start+done)%count);done++;} while(done<count && elapsed()<budget);
        cursor=(start+(done==count?1:done))%count;return done;
    }
}
