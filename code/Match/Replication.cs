using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Match;

public sealed record PublicPlayer(Guid Id,string Name,int Coins,int Vp,int HandCount,bool BuildReady,bool ContractReady);
public sealed record PublicAuction(int Id,int Round,int Number,int Cap,bool Resolving,AuctionLot[] Lots,AuctionParticipant[] Players);
/// <summary>No seed, shuffle state, secret submissions, offers or hands belong in this broadcast.</summary>
public sealed record PublicMatchSnapshot(int Width,int Height,MatchSettings Settings,int Round,MatchPhase Phase,double PhaseEndsAt,
	PublicPlayer[] Players,BoardCell[] Cells,TileClaim[] Claims,PublicGoal[] Goals,GoalClaim[] GoalHistory,PublicAuction Auction,
	AuctionResult LastAuction,PayoutResult LastPayout,ContractReveal[] ContractReveals,EndScore FinalScore,RoundActivity[] Activity=null);
public sealed record OwnerMatchSnapshot(Guid Owner,long HandVersion,string[] Hand,ContractView Contract,AuctionView Auction,bool BuilderCommon=false);
public sealed record RecipientSnapshot(int Version,Guid MatchId,long Revision,double Clock,PublicMatchSnapshot Public,OwnerMatchSnapshot Own);

/// <summary>Only recipient-filtered state becomes a client's model. Rival hands stay empty.</summary>
public sealed class MatchReplica
{
	readonly HashSet<Guid> retired = new();
	public MatchState State { get; private set; }
	public RecipientSnapshot Latest { get; private set; }
	public bool Apply(Guid viewer,RecipientSnapshot packet)
	{
		if (packet == null || packet.Version != 2 || packet.MatchId == Guid.Empty || packet.Revision < 0 || !double.IsFinite(packet.Clock) || packet.Own != null && packet.Own.Owner != viewer) return false;
		if (retired.Contains(packet.MatchId) || Latest?.MatchId == packet.MatchId && packet.Revision <= Latest.Revision) return false;
		if (Latest?.MatchId != packet.MatchId)
		{
			if (Latest != null) retired.Add(Latest.MatchId);
			State=new MatchState(packet.Public.Width,packet.Public.Height,0,packet.Public.Settings);
		}
		Latest=packet; State.ApplyReplica(packet.Public,packet.Own); return true;
	}
}

public static class MatchProjection
{
	public static RecipientSnapshot For(MatchState state,RoundMachine rounds,Guid viewer,Guid matchId,long revision,double clock)
	{
		var players = state.Players.Values.Select(p => new PublicPlayer(p.Id,p.Name,p.Coins,p.Vp,p.Hand.Count,
			rounds.IsBuildReady(p.Id),!state.Settings.EnableContracts || rounds.GetContractView(p.Id)?.Selected != null)).ToArray();
		var auction = rounds.GetAuctionView(viewer);
		// Spectators still see the public lots, never a participant's private projection.
		var publicAuction = auction ?? state.Players.Keys.Select(rounds.GetAuctionView).FirstOrDefault(v => v != null);
		var visible = publicAuction == null ? null : new PublicAuction(publicAuction.Id,publicAuction.Round,publicAuction.Number,publicAuction.Cap,publicAuction.Resolving,
			publicAuction.Lots.Select(l => l.Lot).ToArray(),publicAuction.Players.ToArray());
		var data = new PublicMatchSnapshot(state.Width,state.Height,state.Settings,state.Round,state.Phase,state.PhaseEndsAt,players,state.Cells().ToArray(),state.Claims.ToArray(),
			state.Goals.Active.ToArray(),state.Goals.History.ToArray(),visible,rounds.LastAuction,rounds.LastPayout,state.ContractReveals.ToArray(),state.FinalScore,state.ActivitySnapshot());
		OwnerMatchSnapshot own = null;
		if (state.Players.TryGetValue(viewer,out var owner))
		{
			var contract = rounds.GetContractView(viewer);
			// ContractSession's internal draft token derives from its hidden seed. The wire token is scoped by MatchId instead.
			if (contract != null) contract = contract with { DraftId = matchId.GetHashCode() };
			own = new(viewer,owner.HandVersion,owner.Hand.ToArray(),contract,auction,state.BuilderCommon(viewer));
		}
		return new(2,matchId,revision,clock,data,own);
	}
}

/// <summary>An authenticated transport supplies caller; a packet can never name the acting player.</summary>
public enum MatchCommandKind { Bid, Ready, Place, Discard, PlayGrant, Claim, KeepContract, PlayEvent, BuilderDefault, Renovate, HurryAuction, BuyMarketCard }
public sealed record MatchCommand(Guid MatchId,long Sequence,int Round,MatchPhase Phase,MatchCommandKind Kind,
	int AuctionId=0,int Slot=-1,long HandVersion=0,string CardId=null,int X=0,int Y=0,int Amount=0,int ReplacementSlot=-1);
public sealed record CommandReply(long Sequence,bool Accepted,string Reason);
internal sealed record CommandSequence(Guid Player,long Sequence);

public sealed class MatchAuthority
{
	readonly MatchState state;
	readonly RoundMachine rounds;
	readonly Dictionary<Guid,long> sequences = new();
	public Guid MatchId { get; }
	public MatchAuthority(Guid matchId,MatchState state,RoundMachine rounds) { MatchId=matchId; this.state=state; this.rounds=rounds; }
	internal long LastSequence(Guid player) => sequences.GetValueOrDefault(player);
	internal CommandSequence[] CaptureSequences() => sequences.Select(p => new CommandSequence(p.Key,p.Value)).ToArray();
	internal void RestoreSequences(IEnumerable<CommandSequence> previous) { sequences.Clear(); foreach (var p in previous) sequences[p.Player]=p.Sequence; }
	public CommandReply Execute(Guid caller,MatchCommand request,double hostClock)
	{
		CommandReply Denied(string reason) => new(request?.Sequence ?? 0,false,reason);
		CommandReply Result(bool accepted,string reason) => new(request.Sequence,accepted,accepted ? "" : reason);
		if (request == null || request.MatchId != MatchId || !state.Players.TryGetValue(caller,out var player)) return Denied("This match or player is no longer active.");
		if (request.Sequence <= 0 || request.Sequence <= sequences.GetValueOrDefault(caller)) return Denied("Request already handled.");
		sequences[caller]=request.Sequence;
		if (!double.IsFinite(hostClock) || request.Round != state.Round || request.Phase != state.Phase) return Denied("The phase has changed.");
		if (request.Kind == MatchCommandKind.KeepContract)
			return Result(request.AuctionId == MatchId.GetHashCode() && rounds.ChooseContract(caller,state.Seed,request.CardId),"Contract must be one of your current offered choices.");
		if (request.Kind == MatchCommandKind.BuilderDefault)
			return Result(request.Amount is 0 or 1 && state.ChooseBuilderDefault(caller,request.Amount==1),"Choose a default while you own a Builders' Yard.");
		if (request.Kind == MatchCommandKind.BuyMarketCard)
			return Result(rounds.BuyMarketCard(caller,request.AuctionId,request.Slot,hostClock),"Card could not be purchased.");
		if (request.Kind == MatchCommandKind.HurryAuction)
			return Result(rounds.HurryAuction(caller,request.AuctionId,hostClock),"The auction clock could not be started.");
		if (request.Kind == MatchCommandKind.Ready)
		{
			if (state.Phase == MatchPhase.Bid && rounds.GetAuctionView(caller)?.Id != request.AuctionId) return Denied("The auction has changed.");
			return Result(rounds.Ready(caller,hostClock),"Ready could not be changed.");
		}
		if (state.Phase != MatchPhase.Build || hostClock >= state.PhaseEndsAt || rounds.IsBuildReady(caller)) return Denied("Building is closed.");
		if (request.HandVersion != player.HandVersion || request.Slot < 0 || request.Slot >= player.Hand.Count || player.Hand[request.Slot] != request.CardId)
			return Denied("Your hand changed. Select the card again.");
		if (request.Kind == MatchCommandKind.Renovate)
		{
			var error=state.RenovationError(caller,request.Slot,request.ReplacementSlot,request.X,request.Y);
			if (error != null) return Denied(error);
			return Result(state.TryRenovate(caller,request.Slot,request.ReplacementSlot,request.X,request.Y).Success,"Renovation could not be completed.");
		}
		if (request.Kind is MatchCommandKind.Place or MatchCommandKind.Claim)
		{
			var error=request.Kind==MatchCommandKind.Place ? state.PlacementError(caller,request.CardId,request.X,request.Y) : request.CardId=="land-claim" ? state.CardPlayError(caller,request.CardId,request.X,request.Y) : "Select Land Claim first.";
			if (error!=null) return Denied(error); // Network denials go only to the caller, not the host's local feedback.
		}
		var accepted = request.Kind switch
		{
			MatchCommandKind.Place => state.TryPlace(caller,request.CardId,request.X,request.Y).Success,
			MatchCommandKind.Claim => state.TryClaim(caller,request.Slot,request.X,request.Y).Success,
			MatchCommandKind.Discard => state.Discard(caller,request.Slot),
			MatchCommandKind.PlayGrant => state.PlayGrant(caller,request.Slot), MatchCommandKind.PlayEvent => state.PlayEvent(caller,request.Slot), _ => false
		};
		return new(request.Sequence,accepted,accepted ? "" : "That action is not valid here.");
	}
}
