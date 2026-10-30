using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;
using CitySim.Match;

namespace CitySim.Ai;

public enum BotDifficulty { Easy, Normal, Hard }
public enum BotStrategy { Balanced, Tag, ParkRush, EconomyFirst }

/// <summary>Local, seeded rival. Decisions use public board information and this player's private views only.</summary>
public sealed class Bot
{
    readonly Guid player;
    readonly MatchState state;
    readonly RoundMachine rounds;
    readonly BotDifficulty difficulty;
    readonly Random random;
    readonly BotStrategy strategy;
    MatchPhase lastPhase = (MatchPhase)(-1);
    int lastRound = -1, lastAuction = -1;
    double actAt;
    bool bidSubmitted;
    List<OwnLotBid> bidLots;
    readonly List<(OwnLotBid lot,double value)> evaluatedLots = new();
    int bidCursor, bidAvailable;
    long bidHandVersion;
    void ClearBidWork() { bidLots=null; evaluatedLots.Clear(); bidCursor=0; }
    public int RejectedActions { get; private set; }
    void Record(bool accepted) { if (!accepted) RejectedActions++; }
    readonly record struct Candidate(string Card, int Slot, int X, int Y, double Rough);
    readonly record struct Choice(string Card, int Slot, int X, int Y, double Value);
    readonly record struct RenovationChoice(int EventSlot,int Slot,int X,int Y,double Value);
    int CandidateLimit => difficulty switch { BotDifficulty.Easy => 5, BotDifficulty.Normal => 10, _ => 16 };
    double Noise => difficulty switch { BotDifficulty.Easy => 2.0, BotDifficulty.Normal => 0.65, _ => 0.1 };
    double Horizon => difficulty switch { BotDifficulty.Easy => 1.0, BotDifficulty.Normal => 2.0, _ => 3.5 };
    double Greed => difficulty switch { BotDifficulty.Easy => 0.35, BotDifficulty.Normal => 0.6, _ => 0.85 };

    public Bot(Guid player, MatchState state, RoundMachine rounds, BotDifficulty difficulty, int seed, BotStrategy strategy = BotStrategy.Balanced)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.rounds = rounds ?? throw new ArgumentNullException(nameof(rounds));
        if (!state.Players.ContainsKey(player)) throw new ArgumentException("Unknown player", nameof(player));
        if (!Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (!Enum.IsDefined(strategy)) throw new ArgumentOutOfRangeException(nameof(strategy));
        this.player = player; this.difficulty = difficulty; this.strategy = strategy; random = new Random(seed);
    }

    public void ChooseContract()
    {
        var view = rounds.GetContractView(player);
        if (view == null || view.Selected != null || view.Choices.Count == 0) return;
        var best = view.Choices.Select(id => Contracts.Get(id)).OrderByDescending(c =>
            c.Vp / (double)Math.Max(1, c.Target) + (c.Kind == ContractKind.Coins ? 0.35 : 0) + random.NextDouble() * Noise * 0.1).First();
        rounds.ChooseContract(player, view.DraftId, best.Id);
    }

    public void Update(double now)
    {
        if (!double.IsFinite(now) || !state.Players.ContainsKey(player)) return;
        var view = rounds.GetAuctionView(player);
        var auctionId = view?.Id ?? -1;
        if (state.Phase != lastPhase || state.Round != lastRound || auctionId != lastAuction)
        {
            lastPhase = state.Phase; lastRound = state.Round; lastAuction = auctionId; bidSubmitted = false; ClearBidWork();
            actAt = now + Delay(now);
        }
        if (state.Phase is not (MatchPhase.Bid or MatchPhase.Build) || (state.PhaseEndsAt >= 0 && now >= state.PhaseEndsAt) || now < actAt) return;
        if (state.Phase == MatchPhase.Bid)
        {
            if (view == null || !view.Eligible || view.Resolving || view.Ready) { ClearBidWork(); return; }
            if (!bidSubmitted)
            {
                if (!BidStep(view, now)) { actAt=now; return; }
                bidSubmitted=true;
            }
            else Record(rounds.Ready(player, now));
        }
        else if (!rounds.IsBuildReady(player)) Build(now);
        actAt = now + Delay(now);
    }

    double Delay(double now)
    {
        // Reserve time for both bid submission and readiness, even in very short phases.
        var remaining = state.PhaseEndsAt < 0 ? state.Settings.MiniAuctionSeconds : Math.Max(0, state.PhaseEndsAt - now);
        return Math.Min(0.35 + random.NextDouble() * 0.3, remaining * 0.2);
    }

    double StrategyBonus(string card)
    {
        var def = CardLibrary.Get(card);
        return strategy switch {
            BotStrategy.Tag => def.Tags.HasFlag(Tag.Industry) ? 1.8 : 0,
            BotStrategy.ParkRush => def.ParkSize > 0 ? 1.8 + def.ParkSize * 0.4 : 0,
            BotStrategy.EconomyFirst => def.Effects.Any(e => e.Hook == EffectHook.Payout) ? 0.8 : 0,
            _ => 0
        };
    }

    // One complete lot evaluation per update. Never submit a partial bid until work is complete
    // or the deadline is close; each evaluated lot uses the normal candidate budget.
    bool BidStep(AuctionView view, double now)
    {
        if (bidLots == null || bidHandVersion != state.Players[player].HandVersion || bidAvailable != view.AvailableCoins)
        {
            ClearBidWork();
            bidHandVersion=state.Players[player].HandVersion; bidAvailable=view.AvailableCoins;
            bidLots=view.Lots.Where(l => l.Allowed && (long)l.Lot.MinimumBid + state.Settings.BuildCost(CardLibrary.Get(l.Lot.CardId)) <= view.AvailableCoins).ToList();
        }
        if (bidCursor < bidLots.Count)
        {
            var lot=bidLots[bidCursor++];
            evaluatedLots.Add((lot,EvaluateCard(lot.Lot.CardId,lot.Lot.MinimumBid)+StrategyBonus(lot.Lot.CardId)-lot.Lot.MinimumBid/(double)state.Settings.CoinsPerEndVp));
        }
        if (bidCursor < bidLots.Count && (state.PhaseEndsAt < 0 || state.PhaseEndsAt-now > 0.15)) return false;
        if (evaluatedLots.Count > 0)
        {
            var best=evaluatedLots.OrderByDescending(x => x.value + (random.NextDouble()-0.5)*Noise).First();
            if (best.value > 0)
            {
                var minimum=best.lot.Lot.MinimumBid;
                if (view.AvailableCoins>=minimum) Record(rounds.BuyMarketCard(player,view.Id,best.lot.Lot.Index,now));
            }
        }
        ClearBidWork();
        return true;
    }

    void Build(double now)
    {
        // Retain road access early, then use the Yard to diversify the next round's default.
        state.ChooseBuilderDefault(player,state.Cells().Count(c=>c.Owner==player && Cards.CardLibrary.Get(c.CardId).Type==Cards.CardType.Road)>=3);
        var hand = state.Players[player].Hand;
        if (hand.Count == 0) { Record(rounds.Ready(player, now)); return; }
        var grant = Enumerable.Range(0, hand.Count).FirstOrDefault(i => hand[i] == "grant", -1);
        if (grant >= 0) { Record(state.PlayGrant(player, grant)); return; }
        var choice = BestPlacement(hand.Select((card, slot) => (card, slot)).Where(x=>x.card is not ("ribbon-cutting" or "rush-hour")), state);
        var renovation=BestRenovation(state);
        if (renovation is { } upgrade && upgrade.Value>0 && (choice == null || upgrade.Value>choice.Value.Value))
        {
            Record(state.TryRenovate(player,upgrade.EventSlot,upgrade.Slot,upgrade.X,upgrade.Y).Success); return;
        }
        if (choice is { } best)
        {
            if (best.Card == "land-claim") Record(state.TryClaim(player, best.Slot, best.X, best.Y).Success);
            else Record(state.TryPlace(player, best.Card, best.X, best.Y).Success);
            return;
        }
		var ribbon=Enumerable.Range(0,hand.Count).FirstOrDefault(i=>hand[i]=="ribbon-cutting",-1);
		if (ribbon>=0) { Record(state.PlayEvent(player,ribbon)); return; }
		var rush=Enumerable.Range(0,hand.Count).FirstOrDefault(i=>hand[i]=="rush-hour",-1);
		if (rush>=0 && !state.RushHourActive(player)) { Record(state.PlayEvent(player,rush)); return; }
        // Preserve blocked cards for a later round unless they prevent admission to an auction.
        if (hand.Count >= state.Settings.HandTarget) Record(state.Discard(player, 0));
        else Record(rounds.Ready(player, now));
    }

    double EvaluateCard(string card, int purchaseCost)
    {
        if (card=="renovation") { var renovationBoard=PublicClone(card); renovationBoard.ChangeCoins(player,-purchaseCost); return BestRenovation(renovationBoard)?.Value ?? 0; }
        if (card == "grant") return CardLibrary.Get(card).Effects.First(e => e.Rule == "coins").Amount / (double)state.Settings.CoinsPerEndVp + 1;
		if (card == "ribbon-cutting") { var effect=CardLibrary.Get(card).Effects.First(e=>e.Rule=="round-building-coins"); return Math.Min(effect.Cap,(state.BuildingsPlacedThisRound(player)+1)*effect.Amount) / (double)state.Settings.CoinsPerEndVp + 0.5; }
		if (card == "rush-hour") return Math.Min(4,state.Cells().Count(c=>c.Owner==player && c.CardId=="shop")) / (double)state.Settings.CoinsPerEndVp + 0.3;
        // Add only our candidate card to a public-state clone, never rival hands or hidden deck state.
        var clone = PublicClone(card);
        clone.ChangeCoins(player,-purchaseCost);
        var choice = BestPlacement(new[] { (card, clone.Players[player].Hand.Count - 1) }, clone);
        return choice?.Value ?? 0;
    }

    Choice? BestPlacement(IEnumerable<(string card,int slot)> cards, MatchState board)
    {
        var shortlist = new List<Candidate>();
        foreach (var (card, slot) in cards)
        {
            if (card == "grant") continue;
            for (var y = 0; y < board.Height; y++) for (var x = 0; x < board.Width; x++)
            {
                if (board.CardPlayError(player, card, x, y) != null) continue;
                var neighbours = board.Neighbours(x,y).ToArray();
                var own = neighbours.Count(n => n.Owner == player);
                var rough = neighbours.Length * 0.3 + own * 0.2 + StrategyBonus(card) + (random.NextDouble() - 0.5) * Noise;
                if (card != "land-claim")
                {
                    rough += PlacementScoring.Preview(board,card,x,y,player);
                    // Give fixed end-game awards a place in the shortlist as well as the exact evaluation below.
                    rough += CardLibrary.Get(card).Effects.Where(e => e.Hook == EffectHook.EndGame && e.Rule == "vp").Sum(e => e.Amount);
                }
                if (card == "street")
                {
                    // Roads open adjacent building sites, especially while land is still vacant.
                    var open = 0;
                    foreach (var (dx,dy) in new[] { (0,-1), (1,0), (0,1), (-1,0) })
                        if (board.InBounds(x+dx,y+dy) && board.Get(x+dx,y+dy) == null) open++;
                    rough += open * 0.35 + neighbours.Count(n => CardLibrary.Get(n.CardId).NeedsRoad) * 0.3;
                }
                shortlist.Add(new(card, slot, x, y, rough));
            }
        }
        if (shortlist.Count == 0) return null;
        var top = shortlist.OrderByDescending(c => c.Rough).Take(CandidateLimit).ToArray();
        var baseline = Payout.Calculate(board).CoinsFor(player);
        var endBaseline = FinalScoring.CardEffects(board).Where(l => l.Player == player).Sum(l => l.Vp);
        var contract = rounds.GetContractView(player)?.Selected;
        var beforeContract = contract == null ? 0 : Contracts.Evaluate(board, player, Contracts.Get(contract)).Current;
        var goals = board.Goals.Active.Where(g => g.Claim == null).Select(g => GoalLibrary.Get(g.GoalId)).ToArray();
        var goalBefore = goals.Select(g => GoalRules.Evaluate(board,player,g).Current).ToArray();
        Choice? best = null;
        foreach (var candidate in top)
        {
            if (candidate.Card == "land-claim")
            {
                var claimValue = 0.25 + candidate.Rough * 0.15;
                if (best == null || claimValue > best.Value.Value) best = new(candidate.Card,candidate.Slot,candidate.X,candidate.Y,claimValue);
                continue;
            }
            var clone = PublicClone(null, board);
            var beforeVp = clone.Players[player].Vp;
            var beforeCoins = clone.Players[player].Coins;
            if (!clone.TryPlace(player,candidate.Card,candidate.X,candidate.Y).Success) continue;
            var immediateVp = clone.Players[player].Vp - beforeVp;
            var endVp = FinalScoring.CardEffects(clone).Where(l => l.Player == player).Sum(l => l.Vp) - endBaseline;
            var income = Payout.Calculate(clone).CoinsFor(player) - baseline;
            var remaining = Math.Max(1, Math.Min(Horizon, state.Settings.Rounds - state.Round + 1));
            var contractGain = contract == null ? 0 : Math.Max(0, Contracts.Evaluate(clone,player,Contracts.Get(contract)).Current - beforeContract);
            var goalGain = 0.0;
            for (var i = 0; i < goals.Length; i++)
            {
                var progress = GoalRules.Evaluate(clone, player, goals[i]);
                goalGain += Math.Max(0, progress.Current - goalBefore[i]) * state.Settings.GoalVp / (double)Math.Max(1, progress.Target);
            }
            var immediateCoins = clone.Players[player].Coins - beforeCoins;
            var value = 0.5 + immediateVp + endVp + (immediateCoins + income * remaining) * (strategy == BotStrategy.EconomyFirst ? 1.8 : 1.0) / state.Settings.CoinsPerEndVp + contractGain * 0.7 + goalGain * 0.6 + candidate.Rough * 0.2 + StrategyBonus(candidate.Card);
            if (best == null || value > best.Value.Value) best = new(candidate.Card,candidate.Slot,candidate.X,candidate.Y,value);
        }
        return best;
    }

    RenovationChoice? BestRenovation(MatchState board)
    {
        var hand=board.Players[player].Hand;
        var eventSlot=Enumerable.Range(0,hand.Count).FirstOrDefault(i=>hand[i]=="renovation",-1);
        if (eventSlot<0) return null;
        var candidates=new List<(int Slot,BoardCell Cell,int Vp)>();
        foreach (var target in board.Cells().Where(c=>c.Owner==player))
        for (var slot=0;slot<hand.Count;slot++)
            if (board.PreviewRenovation(player,eventSlot,slot,target.X,target.Y) is { } vp) candidates.Add((slot,target,vp));
        if (candidates.Count==0) return null;
        var beforeIncome=Payout.Calculate(board).CoinsFor(player);
        var beforeEnd=FinalScoring.CardEffects(board).Where(l=>l.Player==player).Sum(l=>l.Vp);
        RenovationChoice? best=null;
        foreach (var candidate in candidates.OrderByDescending(c=>c.Vp).ThenBy(c=>c.Cell.Order).Take(CandidateLimit))
        {
            var clone=PublicClone(null,board); var before=clone.Players[player]; var vp=before.Vp; var coins=before.Coins;
            if (!clone.TryRenovate(player,eventSlot,candidate.Slot,candidate.Cell.X,candidate.Cell.Y).Success) continue;
            var horizon=Math.Max(1,Math.Min(Horizon,board.Settings.Rounds-board.Round+1));
            var value=0.5+clone.Players[player].Vp-vp+FinalScoring.CardEffects(clone).Where(l=>l.Player==player).Sum(l=>l.Vp)-beforeEnd+
                (clone.Players[player].Coins-coins+(Payout.Calculate(clone).CoinsFor(player)-beforeIncome)*horizon)/board.Settings.CoinsPerEndVp;
            if (best==null || value>best.Value.Value) best=new(eventSlot,candidate.Slot,candidate.Cell.X,candidate.Cell.Y,value);
        }
        return best;
    }

    MatchState PublicClone(string addedCard, MatchState source = null)
    {
        source ??= state;
        var cells = source.Cells().ToArray();
        var players = source.Players.Values.Select(p => new PlayerCheckpoint(p.Id,p.Name,p.Coins,p.Vp,0,
            p.Id == player ? p.Hand.Concat(addedCard == null ? Array.Empty<string>() : new[] { addedCard }).ToArray() : Array.Empty<string>())).ToArray();
        var goals = new GoalsCheckpoint(false,0,1,source.Goals.Active.ToArray(),source.Goals.History.ToArray(),Array.Empty<string>(),Array.Empty<string>());
        return MatchState.Restore(new MatchCheckpoint(2,source.Width,source.Height,0,source.Settings,source.Round,MatchPhase.Build,
            source.PhaseEndsAt,source.NextPlacementOrder-1,source.LastPaidRound,cells,source.Claims.ToArray(),players,goals,Array.Empty<ContractReveal>(),null,source.TriggerUsageSnapshot(),source.ActivitySnapshot(),source.BuilderChoiceSnapshot().Where(c=>c.Player==player).ToArray()));
    }
}

/// <summary>Round-robin frame budget. A single update runs to completion and may exceed the budget.</summary>
internal static class BotFrameScheduler
{
    internal static int Run(int count,ref int cursor,double budget,Action<int> update,Func<double> elapsed)
    {
        if (count<=0) { cursor=0; return 0; }
        var start=((cursor%count)+count)%count; var done=0;
        do { update((start+done)%count); done++; } while (done<count && elapsed()<budget);
        cursor=(start+(done==count ? 1 : done))%count;
        return done;
    }
}
