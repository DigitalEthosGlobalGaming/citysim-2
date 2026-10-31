using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;
using CitySim.Match;

namespace CitySim.Ai;

internal enum BotMoveKind { Place, Event, Claim, Renovate }
internal sealed record BotMove(BotMoveKind Kind,string Card,int Slot,int X=0,int Y=0,int ReplacementSlot=-1,double Rough=0)
{
    internal bool Apply(MatchState board,Guid player) => Kind switch {
        BotMoveKind.Event=>board.PlayEvent(player,Slot),
        BotMoveKind.Claim=>board.TryClaim(player,Slot,X,Y).Success,
        BotMoveKind.Renovate=>board.TryRenovate(player,Slot,ReplacementSlot,X,Y).Success,
        _=>board.TryPlace(player,Card,X,Y).Success
    };
    internal bool Valid(MatchState board,Guid player)
    {
        var hand=board.Players[player].Hand;
        if(Slot<0 || Slot>=hand.Count || hand[Slot]!=(Kind==BotMoveKind.Renovate ? "renovation":Card)) return false;
        return Kind switch {
            BotMoveKind.Event=>Card!="rush-hour" || !board.RushHourActive(player),
            BotMoveKind.Claim=>board.CardPlayError(player,Card,X,Y)==null,
            BotMoveKind.Renovate=>board.RenovationError(player,Slot,ReplacementSlot,X,Y)==null,
            _=>board.PlacementError(player,Card,X,Y)==null
        };
    }
}
internal sealed record BotMoveScore(BotMove Move,double Value,MatchState After);

/// <summary>One step scans at most 64 sites or scores one move; no whole-board deep search in Update.</summary>
internal sealed class BotSearch
{
    readonly MatchState board;
    readonly Guid owner;
    readonly BotEvaluation evaluation;
    readonly BotDifficulty difficulty;
    readonly bool lookahead;
    readonly int limit;
    readonly double baseline;
    readonly IEnumerator<BotMove> generator;
    readonly List<BotMove> rough=new();
    readonly List<BotMoveScore> scores=new();
    List<BotMove> shortlist;
    int cursor,beam;
    BotSearch continuation;
    BotMoveScore[] beams;
    internal bool Complete { get; private set; }
    internal int EvaluatedMoves { get; private set; }
    internal IReadOnlyList<BotMoveScore> Scores=>scores;
    internal BotMoveScore Best => scores.OrderByDescending(s=>s.Value).FirstOrDefault();

    internal BotSearch(MatchState board,Guid owner,BotEvaluation evaluation,BotDifficulty difficulty,string onlyCard=null,
        bool lookahead=true,double? baseline=null,int? limit=null)
    {
        this.board=board;this.owner=owner;this.evaluation=evaluation;this.difficulty=difficulty;this.lookahead=lookahead;
        this.baseline=baseline ?? evaluation.Score(board);
        this.limit=limit ?? (difficulty switch {BotDifficulty.Easy=>6,BotDifficulty.Normal=>10,_=>14});
        generator=Generate(onlyCard).GetEnumerator();
    }
    internal void Step()
    {
        if(Complete) return;
        if(shortlist==null)
        {
            for(var i=0;i<64;i++)
            {
                if(!generator.MoveNext())
                {
                    // Keep the best site for each card, then fill remaining slots by merit.
                    shortlist=rough.GroupBy(m=>m.Card).Select(g=>g.OrderByDescending(m=>m.Rough).First())
                        .Concat(rough.OrderByDescending(m=>m.Rough)).DistinctBy(m=>(m.Kind,m.Slot,m.X,m.Y,m.ReplacementSlot)).Take(limit).ToList();
                    break;
                }
                if(generator.Current!=null) rough.Add(generator.Current);
            }
            return;
        }
        if(cursor<shortlist.Count)
        {
            var action=shortlist[cursor++];
            var copy=BotEvaluation.PublicClone(board,owner);
            if(action.Apply(copy,owner))
            {
                var territory=action.Kind==BotMoveKind.Claim ? Math.Min(0.3,action.Rough*0.05):0;
                scores.Add(new(action,evaluation.Score(copy)-baseline+evaluation.Affinity(action.Card)+territory,copy));
                EvaluatedMoves++;
            }
            return;
        }
        if(lookahead && difficulty==BotDifficulty.Hard)
        {
            beams ??= scores.OrderByDescending(s=>s.Value).Where(s=>s.After.Players[owner].Hand.Count>0).Take(2).ToArray();
            if(beam<beams.Length)
            {
                var first=beams[beam];
                continuation ??= new(first.After,owner,evaluation,difficulty,lookahead:false,baseline:baseline,limit:4);
                continuation.Step();
                if(continuation.Complete)
                {
                    if(continuation.Best is { } second)
                    {
                        var index=scores.FindIndex(s=>s.Move==first.Move);
                        scores[index]=first with { Value=first.Value+Math.Max(0,second.Value-first.Value)*0.65 };
                    }
                    continuation=null;beam++;
                }
                return;
            }
        }
        Complete=true;
    }

    internal BotMoveScore Choose(Random random,Func<BotMove,bool> valid=null)
    {
        var groups=scores.Where(s=>double.IsFinite(s.Value) && s.Value>=-0.05 && (valid==null || valid(s.Move)))
            .GroupBy(s=>s.Move.Card).Select(g=>g.ToArray()).ToArray();
        if(groups.Length==0) return null;
        // Rate cards first so a card with many possible sites does not receive more
        // lottery tickets. Then choose among that card's rated legal placements.
        var card=groups[BotChoice.PickIndex(groups.Select(g=>g.Max(s=>s.Value)).ToArray(),difficulty,random)];
        return card[BotChoice.PickIndex(card.Select(s=>s.Value).ToArray(),difficulty,random)];
    }

    IEnumerable<BotMove> Generate(string onlyCard)
    {
        var hand=board.Players[owner].Hand;
        var cells=board.Cells().ToArray();
        var homeCells=cells.Where(BoardRules.IsHome).ToArray();
        var shops=cells.Where(c=>c.CardId=="shop").ToArray();
        var eventSlot=hand.ToList().IndexOf("renovation");
        for(var slot=0;slot<hand.Count;slot++)
        {
            var card=CardLibrary.Get(hand[slot]);
            if(onlyCard!=null && card.Id!=onlyCard) continue;
            if(card.Id is "grant" or "rush-hour" or "ribbon-cutting")
            {
                if(card.Id=="rush-hour" && (board.RushHourActive(owner) || !shops.Any(c=>c.Owner==owner))) continue;
                if(card.Id=="ribbon-cutting" && board.BuildingsPlacedThisRound(owner)==0) continue;
                yield return new(BotMoveKind.Event,card.Id,slot,Rough:2);continue;
            }
            if(card.Id=="renovation") continue;
            for(var y=0;y<board.Height;y++)
            for(var x=0;x<board.Width;x++)
            {
                if(board.Get(x,y)!=null || board.CardPlayError(owner,card.Id,x,y)!=null) {yield return null;continue;}
                var neighbours=board.Neighbours(x,y).ToArray();
                var own=neighbours.Count(c=>c.Owner==owner);
                var nearbyHomes=homeCells.Count(c=>Math.Max(Math.Abs(c.X-x),Math.Abs(c.Y-y))<=2);
                var shopNear=shops.Any(c=>Math.Max(Math.Abs(c.X-x),Math.Abs(c.Y-y))<=2);
                var buildings=neighbours.Count(c=>CardLibrary.Get(c.CardId).Type!=CardType.Road);
                double estimate=0.15*own+evaluation.Affinity(card.Id);
                foreach(var effect in card.Effects)
                {
                    estimate+=effect.Rule switch {
                        "adjacent-home-tag-vp"=>neighbours.Count(BoardRules.IsHome)*effect.Amount,
                        "adjacent-park-vp"=>neighbours.Count(c=>BoardRules.IsPark(c) || c.CardId=="lake")*effect.Amount,
                        "beside-road-vp"=>neighbours.Any(c=>CardLibrary.Get(c.CardId).Type==CardType.Road)?effect.Amount:0,
                        "beside-nature-vp"=>neighbours.Any(c=>CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Nature))?effect.Amount:0,
                        "park-score"=>effect.Amount,
                        "shop-condition"=>shopNear ? effect.Amount:0,
                        "nearby-any-homes"=>Math.Min(effect.Cap,nearbyHomes)*0.65,
                        "cafe-neighbours"=>Math.Min(effect.Cap,neighbours.Count(c=>BoardRules.IsHome(c) || c.CardId=="shop"))*0.6,
                        "adjacent-farm-condition"=>neighbours.Any(BoardRules.IsFarm)?0.7:0,
                        "coins"=>effect.Amount*0.45,
                        "vp"=>effect.Amount,
                        "nearby-building-vp"=>Math.Min(effect.Cap,cells.Count(c=>CardLibrary.Get(c.CardId).Type!=CardType.Road && Math.Max(Math.Abs(c.X-x),Math.Abs(c.Y-y))<=effect.Range)),
                        _=>0
                    };
                }
                if(card.ParkSize>0) estimate+=0.5+card.ParkSize*0.2;
                if(card.Id=="street")
                {
                    var access=4-neighbours.Length;
                    estimate+=access*0.12+(hand.Any(id=>CardLibrary.Get(id).NeedsRoad || CardLibrary.Get(id).Effects.Any(e=>e.Rule=="beside-road-vp"))?0.4:0);
                }
                if(card.Id=="land-claim") estimate=neighbours.Count(c=>c.Owner!=owner)*0.5+buildings*0.25;
                yield return new(card.Id=="land-claim"?BotMoveKind.Claim:BotMoveKind.Place,card.Id,slot,x,y,Rough:estimate);
            }
        }
        if(eventSlot>=0 && onlyCard is null or "renovation")
        for(var slot=0;slot<hand.Count;slot++)
        {
            var card=CardLibrary.Get(hand[slot]);
            if(card.Type is CardType.Road or CardType.Event) continue;
            foreach(var target in cells.Where(c=>c.Owner==owner))
            {
                if(board.RenovationError(owner,eventSlot,slot,target.X,target.Y)!=null) { yield return null;continue; }
                var fixedVp=card.Effects.Where(e=>e.Rule=="vp").Sum(e=>e.Amount);
                var original=CardLibrary.Get(target.CardId);
                var cost=original.Effects.Where(e=>e.Rule=="coins" && e.Hook==EffectHook.Payout).Sum(e=>e.Amount)*0.5;
                yield return new(BotMoveKind.Renovate,card.Id,eventSlot,target.X,target.Y,slot,fixedVp-cost+evaluation.Affinity(card.Id));
            }
        }
    }
}
