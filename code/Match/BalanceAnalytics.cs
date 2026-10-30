using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record BalanceEvent(int SchemaVersion,string BalanceVersion,Guid RecordingId,Guid MatchId,
	long Sequence,DateTime TimestampUtc,int Round,string Phase,string Event,object Data);

/// <summary>Observes committed authority events only. Payloads use match-local seats, never account IDs or names.</summary>
public sealed class BalanceAnalytics : IDisposable
{
	// Bump when card definitions or scoring change; schema changes have a separate version.
	public const string BalanceVersion="2026-10-30.2";
	readonly MatchState state;
	readonly RoundMachine rounds;
	readonly Action<BalanceEvent> write;
	readonly Action<Exception> failed;
	readonly Dictionary<Guid,int> seats;
	readonly Guid recordingId=Guid.NewGuid(),matchId;
	long sequence;
	bool disabled,disposed,completed;
	public BalanceAnalytics(MatchState state,RoundMachine rounds,Guid matchId,string mode,
		IEnumerable<Guid> bots,Action<BalanceEvent> write,Action<Exception> failed=null,string difficulty=null)
	{
		this.state=state; this.rounds=rounds; this.matchId=matchId; this.write=write; this.failed=failed;
		seats=state.Players.Keys.Select((id,index)=>(id,index)).ToDictionary(p=>p.id,p=>p.index);
		var botIds=new HashSet<Guid>(bots ?? Array.Empty<Guid>());
		Emit("recording_started",new { Mode=mode,Difficulty=difficulty,Resumed=state.Round>0,Seed=state.Seed,state.Width,state.Height,
			Settings=state.Settings,Cards=CardLibrary.All,Players=state.Players.Keys.Select(id=>new {Seat=Seat(id),IsBot=botIds.Contains(id)}).ToArray(),InitialState=Players(),InitialBoard=state.Cells().Select(Cell).ToArray() });
		var e=state.Events;
		e.CardPurchased+=Purchased; e.CardReceived+=Received; e.CardDiscarded+=Discarded;
		e.PlacementResolved+=Placed; e.EventPlayed+=Played; e.BuildingReplaced+=Replaced;
		e.LotsRevealed+=Offers; e.AuctionResolved+=MarketResolved; e.PhaseChanged+=Phase;
		e.PayoutResolved+=Payout; e.BuildingGrown+=Grown; e.MatchScored+=Scored;
		if (state.FinalScore!=null) Scored(state.FinalScore);
		else if (state.Phase==MatchPhase.Bid) Offers(0);
	}
	int? Seat(Guid id) => seats.TryGetValue(id,out var seat) ? seat : null;
	object Players() => state.Players.Values.Select(p=>new {Seat=Seat(p.Id),p.Coins,p.Vp,Hand=p.Hand.ToArray()}).ToArray();
	object Cell(BoardCell cell) => cell==null ? null : new {Seat=Seat(cell.Owner),cell.CardId,cell.X,cell.Y,cell.Order};
	void Emit(string name,object data)
	{
		if (disabled || disposed || completed) return;
		try { write(new(1,BalanceVersion,recordingId,matchId,++sequence,DateTime.UtcNow,state.Round,state.Phase.ToString(),name,data)); }
		catch (Exception error) { disabled=true; failed?.Invoke(error); }
	}
	void Purchased(CardPurchase p) => Emit("card_purchased",new {Seat=Seat(p.Player),p.CardId,p.Price,p.MarketId,p.Lot,CoinsAfter=state.Players[p.Player].Coins});
	void Received(HandCardChange p) => Emit("card_received",new {Seat=Seat(p.Player),p.CardId});
	void Discarded(HandCardChange p) => Emit("card_discarded",new {Seat=Seat(p.Player),p.CardId});
	void Placed(PlacementEffects p) => Emit("card_played",new {Kind="placement",Cell=Cell(p.Cell),BuildCost=state.Settings.BuildCost(CardLibrary.Get(p.Cell.CardId)),CoinsAfter=state.Players[p.Cell.Owner].Coins,Awards=p.Awards.Select(a=>new {Seat=Seat(a.Player),a.Coins,a.Vp,a.Reason,Source=Cell(a.Source)}).ToArray()});
	void Played(CardEventResult p) => Emit("card_played",new {Kind="event",Seat=Seat(p.Player),p.CardId,p.Coins,X=p.Claim?.X,Y=p.Claim?.Y});
	void Replaced(BoardReplacement p) => Emit("building_replaced",new {Before=Cell(p.Before),After=Cell(p.After)});
	void Grown(GrowthResult p) => Emit("building_grown",new {Before=Cell(p.Before),After=Cell(p.After),Awards=p.Park.Awards.Select(a=>new {Seat=Seat(a.Player),a.Coins,a.Vp,a.Reason,Source=Cell(a.Source)}).ToArray()});
	void Offers(int _)
	{
		foreach (var player in state.Players.Keys)
		{
			var view=rounds?.GetAuctionView(player); if (view==null || view.Resolving) continue;
			Emit("market_offered",new {Seat=Seat(player),MarketId=view.Id,view.Number,view.Eligible,view.Ready,view.AvailableCoins,
				Lots=view.Lots.Select(l=>new {l.Lot.Index,l.Lot.CardId,Price=l.Lot.MinimumBid,l.Allowed}).ToArray()});
		}
	}
	void MarketResolved(AuctionResult result) => Emit("market_resolved",new {MarketId=result.AuctionId,
		Players=result.Accounts.Select(p=>new {Seat=Seat(p.Player),p.Paid,Passed=p.Paid==0}).ToArray()});
	void Phase(PhaseChange p) => Emit("phase_started",new {Players=Players()});
	void Payout(PayoutResult result) => Emit("payout_resolved",new {Entries=result.Entries.Select(p=>new {Seat=Seat(p.Player),p.Coins,p.Vp,p.Reason,Source=Cell(p.Source),Partner=Cell(p.Partner)}).ToArray(),Players=Players()});
	void Scored(EndScore score)
	{
		Emit("match_completed",new {Players=score.Players.Select(p=>new {Seat=Seat(p.Player),p.TotalVp,p.EarnedVp,p.BonusVp,p.Coins,
			Won=score.Winners.Contains(p.Player),Hand=state.Players[p.Player].Hand.ToArray(),
			Lines=p.Lines.Select(l=>new {l.Reason,l.Vp,Source=Cell(l.Source),l.SecretContract}).ToArray()}).ToArray(),Board=state.Cells().Select(Cell).ToArray()});
		completed=true;
	}
	public void Dispose()
	{
		if (disposed) return;
		Emit("recording_stopped",new {Reason="authority_detached",Players=Players()}); disposed=true;
		var e=state.Events;
		e.CardPurchased-=Purchased; e.CardReceived-=Received; e.CardDiscarded-=Discarded;
		e.PlacementResolved-=Placed; e.EventPlayed-=Played; e.BuildingReplaced-=Replaced;
		e.LotsRevealed-=Offers; e.AuctionResolved-=MarketResolved; e.PhaseChanged-=Phase;
		e.PayoutResolved-=Payout; e.BuildingGrown-=Grown; e.MatchScored-=Scored;
	}
}

