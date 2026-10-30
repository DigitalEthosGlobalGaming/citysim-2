using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Match;

/// <summary>The authoritative clock and shared-market loop.</summary>
public sealed class RoundMachine
{
	readonly MatchState state;
	readonly MatchRandom random;
	readonly HashSet<Guid> buildReady = new();
	readonly List<AuctionResult> history = new();
	readonly ContractSession contracts;
	AuctionSession auction;
	bool started;
	int auctionId;
	int miniAuction;
	int lastHospitalAttemptRound;
	double hospitalScheduledAt;
	double? buildCompleteAt;
	public PayoutResult LastPayout { get; private set; }
	public IReadOnlyList<AuctionResult> AuctionHistory => history.AsReadOnly();
	public AuctionResult LastAuction => history.LastOrDefault();
	public RoundMachine( MatchState state ) : this(state,true) { }
	RoundMachine(MatchState state,bool initialize) { this.state = state; random = new MatchRandom(state.Seed); contracts = new ContractSession(state); if (initialize) state.Goals.Initialize(); }
	internal RoundCheckpoint Capture() => new(random.State,started,auctionId,miniAuction,buildReady.ToArray(),history.ToArray(),auction?.Capture(),contracts.Capture(),LastPayout,lastHospitalAttemptRound,hospitalScheduledAt,buildCompleteAt);
	internal static RoundMachine Restore(MatchState state,RoundCheckpoint data)
	{
		if (data.LastHospitalAttemptRound < 0 || data.LastHospitalAttemptRound > state.Round || !double.IsFinite(data.HospitalScheduledAt) || data.HospitalScheduledAt < 0 || (data.BuildCompleteAt.HasValue && (!double.IsFinite(data.BuildCompleteAt.Value) || state.Phase != MatchPhase.Build)))
			throw new ArgumentException("Invalid hospital emergency checkpoint.");
		var rounds = new RoundMachine(state,false) {started=data.Started,auctionId=data.AuctionId,miniAuction=data.MiniAuction,LastPayout=data.LastPayout,lastHospitalAttemptRound=data.LastHospitalAttemptRound,hospitalScheduledAt=data.HospitalScheduledAt,buildCompleteAt=data.BuildCompleteAt};
		rounds.random.State = data.RandomState; rounds.buildReady.UnionWith(data.BuildReady); rounds.history.AddRange(data.History);
		rounds.contracts.Restore(data.Contracts); rounds.auction = data.Auction != null ? AuctionSession.Restore(state,data.Auction) : null;
		return rounds;
	}
	public ContractView GetContractView(Guid owner) { if (!started) contracts.Deal(); return contracts.View(owner); }
	public bool ContractsChosen => contracts.AllChosen;
	public bool ChooseContract(Guid owner,int draftId,string id) => !started && contracts.Choose(owner,draftId,id);
	public AuctionView GetAuctionView(Guid owner) => state.Phase == MatchPhase.Bid ? auction?.View(owner) : null;
	public bool IsBuildReady(Guid owner) => buildReady.Contains(owner);

	public void Start( double now )
	{
		if (started || state.Players.Count == 0 || state.Phase != MatchPhase.Lobby || !double.IsFinite(now)) throw new InvalidOperationException("Match cannot start.");
		contracts.Deal();
		if (!contracts.AllChosen) throw new InvalidOperationException("Every player must keep a contract before the match starts.");
		started = true; StartRound(1,now);
	}
	void StartRound(int round,double now)
	{
		miniAuction = 0; buildReady.Clear(); auction = null; buildCompleteAt = null;
		state.SetPhase(MatchPhase.RoundStart,round,now);
		state.Goals.StartRound();
		for (var i = 0; i < state.Settings.DefaultStreets; i++)
			foreach (var player in state.Players.Values) state.GiveCard(player.Id,"street");
		for (var i = 0; i < state.Settings.DefaultCommons; i++)
		{
			var card = CardDraw.Common(random);
			foreach (var player in state.Players.Values) state.GiveCard(player.Id,card);
		}
		state.BuilderDefaults(random);
		if (state.Settings.EnableAuctions) StartAuction(now);
		else state.SetPhase(MatchPhase.Bid,round,now+state.Settings.StubBidSeconds);
	}
	void StartAuction(double now)
	{
		if (miniAuction >= state.Settings.MiniAuctionCap || state.Players.Values.All(p => p.Hand.Count >= state.Settings.HandLimit)) { BeginBuild(now); return; }
		auction = new AuctionSession(state,++auctionId,++miniAuction,CardDraw.Reveal(state,random));
		state.SetPhase(MatchPhase.Bid,state.Round,-1);
		state.Events.Reveal(auction.Id);
	}
	void ResolveAuction(double now)
	{
		if (auction == null || auction.Resolved) return;
		var result = auction.Resolve(); history.Add(result);
		state.SetPhase(MatchPhase.Bid,state.Round,now+state.Settings.AuctionResolveSeconds);
		state.Events.Auction(result);
	}
	void BeginBuild(double now)
	{
		auction = null; buildReady.Clear();
		hospitalScheduledAt = now + state.Settings.BuildSeconds * state.Settings.HospitalEmergencyFraction;
		state.SetPhase(MatchPhase.Build,state.Round,now+state.Settings.BuildSeconds);
		UpdateBuildCompletion(now);
	}
	void UpdateBuildCompletion(double now)
	{
		if (state.Players.Values.All(p => p.Hand.Count == 0 || buildReady.Contains(p.Id))) buildCompleteAt ??= now + 1;
		else buildCompleteAt = null;
	}
	void AttemptHospital()
	{
		if (lastHospitalAttemptRound >= state.Round) return;
		lastHospitalAttemptRound = state.Round;
		var result = HospitalEmergency.Calculate(state,random);
		if (result.Answered) state.ChangeVp(result.Hospital.Owner,result.Award);
		state.Events.Emergency(result);
	}
	void BeginPayout(double now)
	{
		buildCompleteAt = null;
		AttemptHospital();
		state.Goals.CheckAtBuildEnd();
		state.SetPhase(MatchPhase.Payout,state.Round,now+state.Settings.PayoutSeconds);
		LastPayout = Payout.Calculate(state); Payout.Apply(state,LastPayout);
	}
	public bool BuyMarketCard(Guid owner,int id,int lot,double now)
	{
		if (state.Phase != MatchPhase.Bid || auction == null || id != auction.Id || (state.PhaseEndsAt >= 0 && now >= state.PhaseEndsAt) || !double.IsFinite(now)) return false;
		if (!auction.Buy(owner,lot)) return false;
		if (auction.AllReady) ResolveAuction(now);
		return true;
	}
	/// <summary>Starts the shared clock once. The caller must be an eligible human, validated by the game or transport.</summary>
	public bool HurryAuction(Guid owner,int id,double now)
	{
		var view = GetAuctionView(owner);
		if (!started || !double.IsFinite(now) || view == null || view.Id != id || !view.Eligible || view.Resolving || state.PhaseEndsAt >= 0) return false;
		state.SetPhase(MatchPhase.Bid,state.Round,now+state.Settings.MiniAuctionSeconds);
		return true;
	}
	public void Tick(double now)
	{
		if (!started || !double.IsFinite(now)) return;
		if (state.Phase == MatchPhase.Build)
		{
			UpdateBuildCompletion(now);
			if (buildCompleteAt.HasValue && now >= buildCompleteAt.Value && buildCompleteAt.Value < state.PhaseEndsAt)
				BeginPayout(buildCompleteAt.Value);
		}
		if (state.Phase == MatchPhase.Build && now >= hospitalScheduledAt) AttemptHospital();
		while (state.Phase != MatchPhase.EndGame && state.PhaseEndsAt >= 0 && now >= state.PhaseEndsAt)
		{
			var deadline = state.PhaseEndsAt;
			switch (state.Phase)
			{
				case MatchPhase.Bid:
					// An editor hotload may discard a private session. Never spin forever on an orphaned Bid phase.
					if (!state.Settings.EnableAuctions || auction == null) BeginBuild(deadline);
					else if (auction?.Resolved == true) StartAuction(deadline);
					else ResolveAuction(deadline);
					break;
				case MatchPhase.Build: BeginPayout(deadline); break;
				case MatchPhase.Payout:
					if (state.Round == state.Settings.Rounds)
					{
						state.ContractReveals = contracts.Reveal(); state.Events.RevealContracts(state.ContractReveals);
						FinalScoring.Apply(state); state.SetPhase(MatchPhase.EndGame,state.Round,deadline);
					}
					else StartRound(state.Round+1,deadline);
					break;
				default: return;
			}
		}
	}
	public bool Ready(Guid owner,double now)
	{
		if (!started || !state.Players.ContainsKey(owner) || !double.IsFinite(now) || (state.PhaseEndsAt >= 0 && now >= state.PhaseEndsAt)) return false;
		if (state.Phase == MatchPhase.Bid)
		{
			if (!state.Settings.EnableAuctions) { BeginBuild(now); return true; }
			if (auction == null || !auction.Ready(owner)) return false;
			if (auction.AllReady) ResolveAuction(now);
			return true;
		}
		if (state.Phase == MatchPhase.Build && now >= hospitalScheduledAt) AttemptHospital();
		if (state.Phase != MatchPhase.Build || !buildReady.Add(owner)) return false;
		state.Events.Ready(owner);
		if (buildReady.Count == state.Players.Count) BeginPayout(now);
		else UpdateBuildCompletion(now);
		return true;
	}
}
