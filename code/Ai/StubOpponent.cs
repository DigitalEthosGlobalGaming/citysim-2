using System;
using System.Linq;
using CitySim.Match;

namespace CitySim.Ai;

/// <summary>M2 test rival: fixed first-affordable-lot bids and first-legal-cell building. M6 replaces this with AI.</summary>
public sealed class StubOpponent
{
	readonly Guid player;
	readonly MatchState state;
	readonly RoundMachine rounds;
	int auctionId = -1;
	double actAt;
	public StubOpponent(Guid player,MatchState state,RoundMachine rounds) { this.player = player; this.state = state; this.rounds = rounds; }
	public void Update(double now)
	{
		if (state.Phase == MatchPhase.Bid)
		{
			var view = rounds.GetAuctionView(player);
			if (view == null || !view.Eligible || view.Resolving || view.Ready) return;
			if (view.Id != auctionId) { auctionId = view.Id; actAt = now+Math.Min(state.Settings.StubThinkSeconds,state.Settings.MiniAuctionSeconds*0.5); }
			if (now < actAt) return;
			var lot = view.Lots.FirstOrDefault(l => l.Allowed && l.Lot.MinimumBid <= Math.Min(state.Settings.StubOpponentBid,view.AvailableCoins));
			if (lot != null) rounds.BuyMarketCard(player,view.Id,lot.Lot.Index,now);
			if (lot == null) rounds.Ready(player,now);
			actAt = now+state.Settings.StubThinkSeconds;
		}
		else if (state.Phase == MatchPhase.Build && now >= actAt && !rounds.IsBuildReady(player))
		{
			var hand = state.Players[player].Hand;
			if (hand.Count == 0) { rounds.Ready(player,now); return; }
			var card = hand[0];
			if (card == "grant") state.PlayGrant(player,0);
			else
			{
				var placed = false;
				for (var y = 0; y < state.Height && !placed; y++)
				for (var x = 0; x < state.Width && !placed; x++)
					if (state.CardPlayError(player,card,x,y) == null) placed = card == "land-claim" ? state.TryClaim(player,0,x,y).Success : state.TryPlace(player,card,x,y).Success;
				if (!placed) state.Discard(player,0);
			}
			actAt = now+state.Settings.StubThinkSeconds;
		}
	}
}
