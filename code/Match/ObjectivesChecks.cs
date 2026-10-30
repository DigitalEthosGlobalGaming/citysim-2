using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class ObjectivesChecks
{
	static readonly Guid A = new("11111111-1111-1111-1111-111111111111"), B = new("22222222-2222-2222-2222-222222222222");
	public static IReadOnlyList<string> Run()
	{
		var passed = new List<string>();
		void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
		MatchState Board(MatchSettings settings = null,int seed = 37) { var s = new MatchState(18,18,seed,settings); s.AddPlayer(A,"A"); s.AddPlayer(B,"B"); s.SetPhase(MatchPhase.Build,1,40); return s; }
		void Put(MatchState s,string id,int x,int y,Guid? owner = null) => s.UpdateCell(new(x,y,id,owner ?? A,s.Cells().Count()+1,1));
		ObjectiveProgress Goal(MatchState s,string id,Guid? owner = null) => GoalRules.Evaluate(s,owner ?? A,GoalLibrary.Get(id));
		ObjectiveProgress Contract(MatchState s,string id,Guid? owner = null) => Contracts.Evaluate(s,owner ?? A,Contracts.Get(id));
		Check(GoalLibrary.All.Count == 9 && Contracts.All.Count == 8,"nine public goals and eight priced contracts load");
		var homes = Board(); Put(homes,"house",0,0); Put(homes,"apartment",1,0); Put(homes,"cottage",2,0); Put(homes,"house",3,1);
		Check(Goal(homes,"neighbourhood").Current == 3,"Home goal includes Cottage but excludes diagonal connections");
		Put(homes,"house",3,0,B); Check(!Goal(homes,"neighbourhood").Complete,"rival Home cannot bridge your neighbourhood");
		Put(homes,"house",2,1); Check(Goal(homes,"neighbourhood").Complete,"four orthogonally connected Home cells qualify");
		var shops = Board(); Put(shops,"shop",0,0); Put(shops,"shop",1,0); Put(shops,"shop",3,0);
		Check(!Goal(shops,"high-street").Complete,"High Street rejects a gap in the row");
		Put(shops,"shop",2,0); Check(Goal(shops,"high-street").Complete,"three consecutive row Shops qualify");
		var column = Board(); for (var y = 0; y < 3; y++) Put(column,"shop",2,y);
		Check(Goal(column,"high-street").Complete,"vertical High Street qualifies");
		var park = Board(); Put(park,"fountain",0,0); Put(park,"sapling",1,0); Put(park,"park",2,0,B);
		Check(Goal(park,"green-thumb").Current == 2,"Green Thumb counts physical owned tiles, not park weights or rival tiles");
		Put(park,"park",3,0); Put(park,"park",4,0); Check(Goal(park,"green-thumb").Complete,"shared park connects your four owned cells");
		var farms = Board(); Put(farms,"farm",0,0); Put(farms,"farm",1,0); Put(farms,"farm",2,1);
		Check(!Goal(farms,"farm-belt").Complete,"Farm Belt rejects diagonally detached farms");
		Put(farms,"farm",2,0); Check(Goal(farms,"farm-belt").Complete,"connected Farm Belt qualifies");
		var tags = Board(); for (var x = 0; x < 3; x++) Put(tags,"bank",x,0);
		Check(!Goal(tags,"civic-pride").Complete,"Civic Pride ignores Banks with Civic card type but no Civic tag");
		for (var x = 0; x < 3; x++) Put(tags,"museum",x,1);
		Check(Goal(tags,"arts-district").Complete,"three Culture buildings qualify");
		Check(Goal(tags,"civic-pride").Complete,"three Civic-tagged Museums qualify for Civic Pride");
		var collector = Board(); for (var x = 0; x < 4; x++) { Put(collector,"museum",x,0); Put(collector,"gardeners-guild",x,1); }
		Check(Goal(collector,"tag-collector").Complete,"multi-tag buildings can contribute to each of three tag groups");
		var mall = Board(); Put(mall,"street",3,3,Guid.Empty); Put(mall,"shop",2,3); Put(mall,"cafe",4,3); Put(mall,"park",4,4);
		Check(Goal(mall,"mini-mall").Current == 2,"Mini Mall rejects a diagonal park");
		Put(mall,"park",3,2,B); Check(!Goal(mall,"mini-mall").Complete,"Mini Mall rejects a rival park");
		Put(mall,"park",3,4); Check(Goal(mall,"mini-mall").Complete,"owned Shop Cafe and park beside one neutral road qualify");
		var cash = Board(); cash.ChangeCoins(A,4); Check(Goal(cash,"rainy-day").Complete,"Rainy Day threshold is twelve coins");
		cash.ChangeCoins(A,-1); Check(!Goal(cash,"rainy-day").Complete,"Rainy Day uses current coins rather than a past peak");

		var tied = Board(); tied.Goals.Initialize(new[] {"rainy-day","neighbourhood","green-thumb"});
		tied.ChangeCoins(A,4); tied.ChangeCoins(B,4); var events = 0; tied.Events.GoalsClaimed += _ => events++;
		Check(tied.Players[A].Vp == 0 && tied.Goals.History.Count == 0,"meeting a condition does not claim it during Build");
		var claimed = tied.Goals.CheckAtBuildEnd();
		Check(claimed.Claims.Single().Awards.Count == 2 && tied.Players[A].Vp == 3 && tied.Players[B].Vp == 3,"same-phase qualifiers each receive three VP");
		Check(tied.Goals.Active[0].Claim != null && tied.Goals.CheckAtBuildEnd().Claims.Count == 0 && events == 1,"claimed card stays visible and the same checkpoint is idempotent");
		tied.Goals.StartRound(); Check(tied.Goals.Active[0].GoalId != "rainy-day" && tied.Goals.Active.Skip(1).Select(g => g.GoalId).SequenceEqual(new[] {"neighbourhood","green-thumb"}),"next round replaces only the claimed slot");
		var sole = Board(); sole.Goals.Initialize(new[] {"rainy-day","neighbourhood","green-thumb"}); sole.ChangeCoins(A,4);
		sole.SetPhase(MatchPhase.Bid,1,40); Check(sole.Goals.CheckAtBuildEnd().Claims.Count == 0,"Bid is not a goal settlement checkpoint");
		sole.SetPhase(MatchPhase.Build,1,40); sole.Goals.CheckAtBuildEnd(); Check(sole.Players[A].Vp == 5 && sole.Players[B].Vp == 0,"sole claimant receives five VP");
		var seeded = Board(); seeded.Goals.Initialize(); var seededAgain = Board(); seededAgain.Goals.Initialize();
		Check(seeded.Goals.Active.Count == 3 && seeded.Goals.Active.Select(g => g.GoalId).Distinct().Count() == 3 && seeded.Goals.Active.SequenceEqual(seededAgain.Goals.Active),"initial three-goal draw is distinct and seeded");
		var recycle = Board(new MatchSettings { PublicGoalCount = 9, Rounds = 24 }); recycle.Goals.Initialize(); recycle.ChangeCoins(A,4);
		for (var round = 1; round <= 24; round++) { recycle.SetPhase(MatchPhase.Build,round,40); recycle.Goals.CheckAtBuildEnd(); recycle.Goals.StartRound(); }
		Check(recycle.Goals.Active.Select(g => g.GoalId).Distinct().Count() == 9 && recycle.Goals.History.Count == 24,"exhausted goal deck recycles without duplicate active slots");

		var thresholds = Board(); for (var x = 0; x < 9; x++) { Put(thresholds,"cottage",x,0); Put(thresholds,"park",x,1); }
		Check(!Contract(thresholds,"suburbia").Complete && !Contract(thresholds,"greenbelt").Complete,"nine Home or Nature buildings do not meet their ten-building contracts");
		Put(thresholds,"cottage",9,0); Put(thresholds,"park",9,1); Check(Contract(thresholds,"suburbia").Complete && Contract(thresholds,"greenbelt").Complete,"ten Home and Nature buildings meet their contracts");
		thresholds.ChangeCoins(A,6); Check(!Contract(thresholds,"hoarder").Complete,"Hoarder rejects fourteen coins"); thresholds.ChangeCoins(A,1); Check(Contract(thresholds,"hoarder").Complete,"Hoarder accepts fifteen coins");
		for (var x = 0; x < 4; x++) { Put(thresholds,"museum",x,2); Put(thresholds,"factory",x,4); }
		Check(!Contract(thresholds,"cultured").Complete && !Contract(thresholds,"industrialist").Complete,"four Culture or Industry buildings are insufficient");
		Put(thresholds,"museum",4,2); Put(thresholds,"factory",4,4); Check(Contract(thresholds,"cultured").Complete && Contract(thresholds,"industrialist").Complete,"five Culture or Industry buildings qualify");
		var landlord = Board(); for (var x = 0; x < 3; x++) { Put(landlord,"house",x*3,1); Put(landlord,"house",x*3,0); }
		Check(Contract(landlord,"landlord").Current == 0,"Landlord ignores your own neighbouring Homes");
		for (var x = 0; x < 3; x++) Put(landlord,"cottage",x*3,2,B);
		Put(landlord,"house",1,1,B); Check(Contract(landlord,"landlord").Current == 3,"Landlord counts each own Home once even with two rival neighbours");
		var mayor = Board(); for (var x = 0; x < 4; x++) Put(mayor,"bank",x,0);
		Check(Contract(mayor,"mayor").Current == 0,"Mayor ignores Civic card types without the Civic tag");
		Put(mayor,"museum",3,2); Put(mayor,"museum",4,2); Check(Contract(mayor,"mayor").Current == 1,"Mayor counts different tagged definitions, not repeated copies");
		Put(mayor,"town-hall",0,2); Put(mayor,"chamber-of-commerce",1,2); Put(mayor,"gardeners-guild",2,2);
		Check(Contract(mayor,"mayor").Complete,"four distinct Civic-tagged types qualify");
		var entrepreneur = Board();
		for (var i = 0; i < 3; i++) { var x = 1+i*6; Put(entrepreneur,"shop",x,1); Put(entrepreneur,"apartment",x,0); Put(entrepreneur,"house",x+1,0); Put(entrepreneur,"cafe",x,2); }
		Check(Contract(entrepreneur,"entrepreneur").Current == 3,"two Home buildings qualify each Shop independently");
		var unpaid = Board(); Put(unpaid,"shop",1,1); for (var x = 0; x < 3; x++) Put(unpaid,"cafe",x,0);
		Check(Contract(unpaid,"entrepreneur").Current == 0,"Cafes are not Home buildings for Entrepreneur");

		MatchState Lobby(int seed = 37,MatchSettings settings = null) { var s = new MatchState(11,11,seed,settings ?? new MatchSettings {EnableAuctions=false}); s.AddPlayer(A,"A"); s.AddPlayer(B,"B"); return s; }
		var lobby = Lobby(); var rounds = new RoundMachine(lobby); var view = rounds.GetContractView(A); var other = rounds.GetContractView(B);
		Check(view.Owner == A && view.Choices.Count == 2 && view.Choices.Distinct().Count() == 2 && view.Selected == null && rounds.GetContractView(Guid.Empty) == null,"contract projection contains only the requested owner's two distinct choices");
		var same = new RoundMachine(Lobby()); Check(view.Choices.SequenceEqual(same.GetContractView(A).Choices) && other.Choices.SequenceEqual(same.GetContractView(B).Choices),"contract deals reproduce by seed and seat");
		var blocked = false; try { rounds.Start(0); } catch (InvalidOperationException) { blocked = true; }
		rounds.Tick(100); Check(blocked && lobby.Phase == MatchPhase.Lobby && lobby.Round == 0 && lobby.Players[A].Hand.Count == 0,"clock and defaults wait for every contract choice");
		Check(!rounds.ChooseContract(A,view.DraftId+1,view.Choices[0]) && !rounds.ChooseContract(A,view.DraftId,Contracts.All.First(c => !view.Choices.Contains(c.Id)).Id),"stale draft and unoffered choices are rejected");
		Check(rounds.ChooseContract(A,view.DraftId,view.Choices[0]) && !rounds.ChooseContract(A,view.DraftId,view.Choices[1]) && !rounds.ContractsChosen,"selection locks once while other owner still waits");
		Check(rounds.GetContractView(A).Choices.Count == 0 && rounds.GetContractView(A).Selected == view.Choices[0] && rounds.GetContractView(B).Selected == null && lobby.ContractReveals.Count == 0,"owner selection discards the offer while public model reveals no contracts");
		rounds.ChooseContract(B,other.DraftId,other.Choices[0]); rounds.Start(0);
		Check(rounds.ContractsChosen && !rounds.ChooseContract(B,other.DraftId,other.Choices[1]) && lobby.Phase == MatchPhase.Bid,"all kept contracts unlock the first round and cannot change after start");

		var finalSettings = new MatchSettings { Rounds=2, EnableAuctions=false, ContractChoices=8, PublicGoalCount=3, DefaultCommons=0, DefaultStreets=0 };
		var finish = Lobby(settings:finalSettings); finish.Goals.Initialize(new[] {"rainy-day","neighbourhood","green-thumb"}); var loop = new RoundMachine(finish);
		loop.ChooseContract(A,finish.Seed,"hoarder"); loop.ChooseContract(B,finish.Seed,"suburbia"); loop.Start(0); finish.ChangeCoins(A,7);
		loop.Ready(A,0); loop.Ready(A,0); loop.Ready(B,0);
		Check(finish.Phase == MatchPhase.Payout && finish.Goals.History.Count == 1 && finish.Goals.History[0].Awards.Single().Vp == 5,"ready path checks goals before payout income");
		Check(finish.ContractReveals.Count == 0 && finish.FinalScore == null,"contracts remain secret through a nonfinal payout");
		loop.Tick(5); Check(finish.Round == 2 && finish.Goals.Active.All(g => g.Claim == null) && finish.Goals.Active.All(g => g.GoalId != "rainy-day"),"round transition refreshes the claimed goal");
		finish.GiveCard(A,"park"); loop.Ready(A,5); loop.Tick(finish.PhaseEndsAt); Check(finish.Phase == MatchPhase.Payout,"deadline path settles Build without ready requests");
		loop.Tick(finish.PhaseEndsAt); var final = finish.FinalScore; var winner = final.Players.First(p => p.Player == A);
		Check(finish.Phase == MatchPhase.EndGame && finish.ContractReveals.Count == 2 && finish.ContractReveals.First(r => r.Player == A).Vp == 5 && finish.ContractReveals.First(r => r.Player == B).Vp == 0,"end reveal awards the printed contract VP and zero for unmet conditions");
		Check(winner.Lines.Count(l => l.SecretContract) == 1 && winner.Lines.Where(l => l.Reason.StartsWith("Public goal")).Sum(l => l.Vp) == 5 && winner.Lines.Sum(l => l.Vp) == finish.Players[A].Vp,"explained score separates public goals and contracts without double counting");
		var vpBefore = finish.Players[A].Vp; loop.Tick(10000); Check(ReferenceEquals(final,FinalScoring.Apply(finish)) && finish.Players[A].Vp == vpBefore,"final contract settlement is idempotent");
		return passed.AsReadOnly();
	}
}

