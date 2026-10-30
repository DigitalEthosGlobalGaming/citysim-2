using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class CoreCardChecks
{
	static readonly Guid A = new("11111111-1111-1111-1111-111111111111"), B = new("22222222-2222-2222-2222-222222222222");
	public static IReadOnlyList<string> Run()
	{
		var passed = new List<string>();
		void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add($"PASS {name}"); }
		MatchState Board(int size = 11,MatchSettings settings = null)
		{
			var s = new MatchState(size,size,1,settings ?? new MatchSettings { RareBuildCost = 0, LegendaryBuildCost = 0 }); s.AddPlayer(A,"A"); s.AddPlayer(B,"B");
			for (var x = 0; x < size; x++) s.SeedStreet(x,size/2);
			s.SetPhase(MatchPhase.Build,1,40); return s;
		}
		BoardCell Place(MatchState s,Guid owner,string card,int x,int y)
		{
			if (!s.GiveCard(owner,card)) throw new InvalidOperationException("Cannot deal "+card);
			var result = s.TryPlace(owner,card,x,y); if (!result.Success) throw new InvalidOperationException($"Setup {card} {x},{y}: {result.Reason}"); return result.Cell;
		}
		var park = Board();
		Place(park,A,"park",1,1); Check(park.Players[A].Vp == 1,"isolated size-one Park scores one VP");
		Place(park,B,"wild-garden",3,1);
		var preview = PlacementScoring.Preview(park,"fountain",2,1,A);
		var beforeA = park.Players[A].Vp; var beforeB = park.Players[B].Vp;
		var fountain = Place(park,A,"fountain",2,1);
		Check(preview == 1 && park.Players[A].Vp-beforeA == 1 && park.Players[B].Vp == beforeB,"Fountain joins distinct regions and awards only its owner the marginal point");
		Check(ParkScoring.Region(park,fountain).Count == 3 && ParkScoring.Region(park,fountain).Sum(ParkScoring.SizeOf) == 4,"Fountain weighs two but occupies one park tile");
		Place(park,A,"park",4,2); Check(ParkScoring.Regions(park).Count == 2,"diagonal park tiles do not connect");
		var cap = Board(15); var deltas = new List<int>();
		for (var x = 0; x < 11; x++) { var old = cap.Players[A].Vp; Place(cap,A,"park",x,0); deltas.Add(cap.Players[A].Vp-old); }
		Check(deltas[0] == 1 && deltas[1] == 0 && deltas[2] == 1 && deltas[9] == 0 && deltas[10] == 0 && cap.Players[A].Vp == 5,"park extensions score marginally through and beyond the cap");
		var loop = Board(); foreach (var point in new[] {(1,1),(1,2),(2,2),(3,2),(3,1)}) Place(loop,A,"park",point.Item1,point.Item2);
		var loopBefore = loop.Players[A].Vp; Place(loop,A,"park",2,1);
		Check(loop.Players[A].Vp == loopBefore,"a bridge touching the same region twice counts it once");
		var cappedGrowth = Board(15); Place(cappedGrowth,A,"sapling",0,0); for (var x = 1; x < 10; x++) Place(cappedGrowth,B,"park",x,0);
		var growthA = cappedGrowth.Players[A].Vp; var growthB = cappedGrowth.Players[B].Vp;
		for (var round = 1; round <= 2; round++) { cappedGrowth.SetPhase(MatchPhase.Payout,round,round*40); Payout.Apply(cappedGrowth,Payout.Calculate(cappedGrowth)); }
		Check(cappedGrowth.Players[A].Vp == growthA && cappedGrowth.Players[B].Vp == growthB,"growth at the cap awards neither owner");

		var sapling = Board(); var grownEvents = 0; sapling.Events.BuildingGrown += _ => grownEvents++;
		Place(sapling,A,"sapling",1,1);
		sapling.SetPhase(MatchPhase.Payout,1,40); var first = Payout.Calculate(sapling); Payout.Apply(sapling,first);
		Check(sapling.Get(1,1).Age == 1 && ParkScoring.SizeOf(sapling.Get(1,1)) == 1 && grownEvents == 0,"Sapling stays small after its first payout");
		var coinsAfterFirst = sapling.Players[A].Coins;
		Check(!Payout.Apply(sapling,first) && sapling.Players[A].Coins == coinsAfterFirst && sapling.Get(1,1).Age == 1,"duplicate payout cannot add coins or advance growth");
		sapling.SetPhase(MatchPhase.Payout,2,80); Payout.Apply(sapling,Payout.Calculate(sapling));
		Check(sapling.Get(1,1).Age == 2 && Growth.IsMature(sapling.Get(1,1)) && ParkScoring.SizeOf(sapling.Get(1,1)) == 3,"Sapling becomes a size-three Forest after exactly two payouts");
		Check(sapling.Players[A].Vp == 2 && grownEvents == 1,"growth scores the park once");
		sapling.SetPhase(MatchPhase.Payout,3,120); Payout.Apply(sapling,Payout.Calculate(sapling));
		Check(grownEvents == 1 && sapling.Players[A].Vp == 2 && !CardLibrary.TryGet("forest",out _),"mature Forest neither regrows nor becomes a new deck card");

		var guild = Board(); Place(guild,A,"gardeners-guild",0,0);
		var purse = guild.Players[A].Coins;
		Place(guild,A,"park",1,0); Place(guild,A,"fountain",2,0); Place(guild,A,"sapling",3,0);
		Check(guild.Players[A].Coins == purse+3,"Gardener's Guild triggers on Park, Fountain and Sapling placement");
		Place(guild,B,"wild-garden",4,0);
		Check(guild.Players[A].Coins == purse+3,"rival park placement does not trigger your Guild");
		guild.SetPhase(MatchPhase.Payout,1,40); Payout.Apply(guild,Payout.Calculate(guild));
		guild.SetPhase(MatchPhase.Payout,2,80); var income = Payout.Calculate(guild).CoinsFor(A); var beforeGrowth = guild.Players[A].Coins; Payout.Apply(guild,Payout.Calculate(guild));
		Check(guild.Players[A].Coins == beforeGrowth+income,"growth is a park addition, not a new Guild-triggering placement");

		var architect = Board(); Place(architect,A,"street",0,0); Place(architect,A,"architects-studio",0,1);
		Check(architect.Players[A].Coins == 10,"Architect can trigger on itself as the first building beside an owned road");
		architect.SetPhase(MatchPhase.Build,2,80); var architectCoins = architect.Players[A].Coins;
		Place(architect,A,"street",1,0); Place(architect,A,"park",1,1); Place(architect,A,"park",2,1);
		Check(architect.Players[A].Coins == architectCoins+2,"roads do not use the Architect's first-building trigger, and it pays only once per round");
		architect.SetPhase(MatchPhase.Build,3,120); var nextCoins = architect.Players[A].Coins;
		Place(architect,A,"park",9,9); Place(architect,A,"park",3,1);
		Check(architect.Players[A].Coins == nextCoins,"a first building without an owned neighbour consumes the Architect opportunity");

		var industry = Board(); Place(industry,A,"factory",1,4); Place(industry,A,"bank",0,0); Place(industry,A,"bank",1,0); Place(industry,A,"bank",2,0);
		Check(Payout.Calculate(industry).CoinsFor(A) == 10,"Factory and three stackable Banks pay their flat income");
		var civic = Board(); Place(civic,A,"town-hall",1,4); Place(civic,A,"bank",0,0); Place(civic,A,"gardeners-guild",2,0);
		Check(Payout.Calculate(civic).Entries.All(e => e.Reason != "Town Hall"),"Bank's Civic card type does not count as a Civic tag");
		Place(civic,A,"museum",2,4);
		Check(Payout.Calculate(civic).Entries.Single(e => e.Reason == "Town Hall").Coins == 2,"Town Hall includes itself when the third Civic-tagged building arrives");
		var chamber = Board(15); Place(chamber,A,"chamber-of-commerce",1,6); Place(chamber,A,"bank",0,0);
		Check(Payout.Calculate(chamber).Entries.Single(e => e.Reason == "Chamber of Commerce").Coins == 1,"Chamber counts itself and pays per pair of Trade tags");
		for (var x = 2; x < 12; x++) Place(chamber,A,"shop",x,6);
		Check(Payout.Calculate(chamber).Entries.Single(e => e.Reason == "Chamber of Commerce").Coins == 4,"Chamber income respects its data cap");

		var apartments = Board(); Place(apartments,A,"house",3,4); Place(apartments,A,"house",4,4); Place(apartments,B,"shop",5,4); Place(apartments,A,"apartment",6,4);
		Check(Payout.Calculate(apartments).Entries.Any(e => e.Source?.CardId == "apartment" && e.Coins == 2),"Apartment independently earns two coins near a Shop");
		var adjacency = Board(); Place(adjacency,B,"house",1,4);
		Check(PlacementScoring.Preview(adjacency,"apartment",2,4,A) == 2,"Apartment adjacency preview pays two VP per neighbouring house");
		Place(adjacency,A,"apartment",2,4); Check(adjacency.Players[A].Vp == 2,"Apartment committed points match preview");

		var claims = Board(); claims.GiveCard(A,"land-claim"); var claimEvents = 0; claims.Events.ClaimsChanged += () => claimEvents++;
		Check(claims.TryClaim(A,0,0,0).Success && claims.Get(0,0) == null && claims.Claims.Count == 1,"Land Claim reserves an empty tile without placing a building");
		claims.GiveCard(B,"park"); Check(!claims.TryPlace(B,"park",0,0).Success && claims.Players[B].Hand.Count == 1,"rivals cannot build on a reservation or lose their card trying");
		Place(claims,A,"park",0,0); Check(claims.Claims.Count == 0 && claimEvents == 2,"the reserving owner may build and consumes the marker");
		claims.GiveCard(A,"land-claim"); claims.TryClaim(A,0,1,0); claims.SetPhase(MatchPhase.RoundStart,2,80);
		Check(claims.Claims.Count == 0,"Land Claim expires at the next round start");
		claims.SetPhase(MatchPhase.Build,2,100); Check(claims.TryPlace(B,"park",1,0).Success,"the former reservation is available to rivals next round");

		var scored = new MatchState(8,8,1,new MatchSettings { RareBuildCost = 0, LegendaryBuildCost = 0 }); scored.AddPlayer(A,"A"); scored.AddPlayer(B,"B"); for (var x = 0; x < 8; x++) scored.SeedStreet(x,3); scored.SeedStreet(3,4); scored.SetPhase(MatchPhase.Build,1,40);
		Place(scored,A,"street",0,0); Place(scored,A,"street",1,0); Place(scored,A,"street",2,0); Place(scored,B,"street",7,7);
		Place(scored,A,"house",1,2); Place(scored,B,"house",2,2); Place(scored,A,"shop",3,2); Place(scored,A,"cafe",4,2);
		Place(scored,A,"park",4,1); Place(scored,B,"park",4,0); Place(scored,A,"fountain",5,1);
		Place(scored,A,"museum",6,2); Place(scored,A,"monument",6,1); Place(scored,A,"factory",2,4); Place(scored,A,"street",3,5);
		Place(scored,B,"house",2,5); Place(scored,A,"house",1,4); Place(scored,A,"bank",7,0); Place(scored,A,"chamber-of-commerce",7,2); Place(scored,A,"town-hall",5,2); Place(scored,A,"architects-studio",0,2);
		Check(scored.Players[A].Vp == 2 && scored.Players[B].Vp == 1,"known final-score fixture has exact earned VP before settlement");
		scored.SetPhase(MatchPhase.Payout,1,40); Payout.Apply(scored,Payout.Calculate(scored));
		Check(scored.Players[A].Coins > 0 && scored.Players[B].Coins > 0,"known fixture pays income");
		var final = FinalScoring.Calculate(scored); var scoreA = final.Players.Single(p => p.Player == A); var scoreB = final.Players.Single(p => p.Player == B);
		Check(scoreA.TotalVp == 32 && scoreB.TotalVp == 4,"explained final score matches prototype income and competition fixture");
		Check(scoreA.Lines.Single(l => l.Reason == "Museum").Vp == 4 && scoreA.Lines.Single(l => l.Reason == "Monument").Vp == 6 && scoreA.Lines.Single(l => l.Reason == "Cafe").Vp == 2,"Museum excludes itself; Monument and park-side Cafe end bonuses score");
		Check(scoreA.Lines.Where(l => l.Reason == "Factory pollution").Sum(l => l.Vp) == -1 && scoreB.Lines.Where(l => l.Reason == "Factory pollution").Sum(l => l.Vp) == -1,"pollution affects the factory owner's houses as well as rivals");
		Check(scoreA.Lines.Single(l => l.Reason == "Full House").Vp == 2 && scoreB.Lines.Single(l => l.Reason == "Full House").Vp == 2,"tied competitive bonuses split their point pool");
		Check(final.Winners.SequenceEqual(new[] {A}) && final.Players.All(p => p.Lines.Sum(l => l.Vp) == p.TotalVp),"winner and every displayed score reconcile to the explanation lines");
		FinalScoring.Apply(scored); FinalScoring.Apply(scored);
		Check(scored.Players[A].Vp == scoreA.TotalVp && scored.Players[B].Vp == scoreB.TotalVp,"end scoring is idempotent");
		var roads = Board(); foreach (var p in new[] {(2,1),(2,2),(1,2),(3,2),(2,3)}) roads.UpdateCell(new(p.Item1,p.Item2,"street",A,100+p.Item1+10*p.Item2,1));
		Check(RoadScoring.Longest(roads,A) == 3,"a five-road cross has a longest simple chain of three, not five");
		var cycle = Board(); foreach (var p in new[] {(1,1),(2,1),(2,2),(1,2),(3,1)}) cycle.UpdateCell(new(p.Item1,p.Item2,"street",A,100+p.Item1+10*p.Item2,1));
		Check(RoadScoring.Longest(cycle,A) == 5,"cyclic road networks are searched for the exact longest chain");
		var graphCorrect = true;
		for (var mask = 1; mask < 512 && graphCorrect; mask++)
		{
			var graph = new MatchState(3,3); graph.AddPlayer(A,"A"); var order = 0;
			for (var i = 0; i < 9; i++) if ((mask & (1<<i)) != 0) graph.UpdateCell(new(i%3,i/3,"street",A,++order,1));
			var visited = new HashSet<int>();
			int Brute(BoardCell cell) { visited.Add(cell.Order); var longest = graph.Neighbours(cell.X,cell.Y).Where(n => !visited.Contains(n.Order)).Select(Brute).DefaultIfEmpty(0).Max(); visited.Remove(cell.Order); return 1+longest; }
			var expected = graph.Cells().Select(Brute).Max(); graphCorrect = RoadScoring.Longest(graph,A) == expected;
		}
		Check(graphCorrect,"road chain solver matches independent brute force for all 511 nonempty 3×3 boards");
		var neutral = new MatchState(3,3); neutral.AddPlayer(A,"A"); neutral.SeedStreet(1,0); neutral.SetPhase(MatchPhase.Build,1,40); Place(neutral,A,"street",0,0); Place(neutral,A,"street",2,0);
		Check(RoadScoring.Longest(neutral,A) == 1,"neutral starting roads cannot join owned chains for the bonus");
		var parkOwner = Board(); Place(parkOwner,A,"fountain",0,0); Place(parkOwner,A,"fountain",1,0); Place(parkOwner,B,"park",2,0); Place(parkOwner,B,"park",3,0); Place(parkOwner,B,"park",4,0);
		Check(FinalScoring.Calculate(parkOwner).Players.Single(p => p.Player == B).Lines.Single(l => l.Reason == "Biggest Park Owner").Vp == 6,"park-owner bonus counts owned physical tiles, not Fountain size weights");
		var museumCap = Board(); Place(museumCap,A,"museum",1,4); for (var x = 0; x < 9; x++) Place(museumCap,A,"fountain",x,0);
		Check(FinalScoring.Calculate(museumCap).Players.Single(p => p.Player == A).Lines.Single(l => l.Reason == "Museum").Vp == 8,"Museum end points stop at the data cap");
		var cottageHome = Board(); Place(cottageHome,A,"cottage",1,4); Place(cottageHome,B,"house",2,4); Place(cottageHome,A,"cafe",0,4); cottageHome.UpdateCell(new(1,2,"street",A,100,1)); Place(cottageHome,A,"factory",1,3);
		Check(cottageHome.Players[B].Vp == 1 && Payout.Calculate(cottageHome).Entries.Any(e => e.Source?.CardId == "cafe" && e.Coins > 0),"Cottage remains a Home for adjacency and Cafe effects");
		Check(FinalScoring.Calculate(cottageHome).Players.Single(p => p.Player == A).Lines.Any(l => l.Reason == "Factory pollution" && l.Vp == -1),"Cottage is affected by pollution as a Home building");
		var empty = Board(); var emptyScore = FinalScoring.Calculate(empty);
		Check(emptyScore.Players.All(p => p.Lines.Where(l => l.Reason != "Spare coins" && l.Reason != "Earned during match").All(l => l.Vp == 0)),"all-zero measures do not award free competitive bonuses");
		empty.ChangeCoins(A,1); Check(FinalScoring.Calculate(empty).Winners.SequenceEqual(new[] {A}),"coins break a tied final VP total");
		var three = new MatchState(7,7); var third = new Guid("33333333-3333-3333-3333-333333333333");
		three.AddPlayer(A,"A"); three.AddPlayer(B,"B"); three.AddPlayer(third,"C"); for (var x = 0; x < 7; x++) three.SeedStreet(x,1); three.SetPhase(MatchPhase.Build,1,40);
		Place(three,A,"house",0,0); Place(three,B,"house",2,0); Place(three,third,"house",4,0);
		Check(FinalScoring.Calculate(three).Players.All(p => p.Lines.Single(l => l.Reason == "Full House").Vp == 1),"a four-point bonus split three ways rounds down equally");
		return passed.AsReadOnly();
	}
}



