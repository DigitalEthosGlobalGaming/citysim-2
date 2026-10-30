using System;

namespace CitySim.Match;

// Host-only persistence DTOs. These must never enter ordinary sync properties or client snapshots.
internal sealed record PlayerCheckpoint(Guid Id,string Name,int Coins,int Vp,long HandVersion,string[] Hand);
internal sealed record GoalsCheckpoint(bool Initialized,int CheckedRound,uint RandomState,PublicGoal[] Active,GoalClaim[] History,string[] Deck,string[] Discard);
internal sealed record ContractCheckpoint(Guid Player,string[] Choices,string Selected);
internal sealed record ContractsCheckpoint(uint RandomState,ContractCheckpoint[] Players);
internal sealed record BidCheckpoint(Guid Player,int[] Amounts,bool Ready);
internal sealed record AuctionCheckpoint(int Id,int Number,AuctionLot[] Lots,bool Resolved,BidCheckpoint[] Bids);
internal sealed record TriggerUsageCheckpoint(int SourceOrder,int Round,int Count);
internal sealed record MatchCheckpoint(int Version,int Width,int Height,int Seed,MatchSettings Settings,int Round,MatchPhase Phase,double PhaseEndsAt,
	int PlacementOrder,int LastPaidRound,BoardCell[] Cells,TileClaim[] Claims,PlayerCheckpoint[] Players,GoalsCheckpoint Goals,ContractReveal[] ContractReveals,EndScore FinalScore,TriggerUsageCheckpoint[] TriggerUsage = null,RoundActivity[] Activity = null,BuilderChoice[] BuilderChoices = null);
internal sealed record RoundCheckpoint(uint RandomState,bool Started,int AuctionId,int MiniAuction,Guid[] BuildReady,AuctionResult[] History,
	AuctionCheckpoint Auction,ContractsCheckpoint Contracts,PayoutResult LastPayout,int LastHospitalAttemptRound=0,double HospitalScheduledAt=0,double? BuildCompleteAt=null);
internal sealed record HostCheckpoint(int Version,Guid MatchId,long Revision,MatchCheckpoint Match,RoundCheckpoint Rounds,double Clock,CommandSequence[] Sequences);
