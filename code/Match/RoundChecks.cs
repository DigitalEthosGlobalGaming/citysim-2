using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Match;

public static class RoundChecks
{
	static readonly Guid A = new("11111111-1111-1111-1111-111111111111");
	static readonly Guid B = new("22222222-2222-2222-2222-222222222222");
	public static IReadOnlyList<string> Run()
	{
		var results = new List<string>();
		void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); results.Add($"PASS {name}"); }
		MatchState Board()
		{
			var state = new MatchState(11,11,42,new MatchSettings { BaseIncome=3, RareBuildCost=0, LegendaryBuildCost=0 }); state.AddPlayer(A,"A"); state.AddPlayer(B,"B");
			for (var x = 0; x < 11; x++) state.SeedStreet(x,5);
			state.SetPhase(MatchPhase.Build,1,40); return state;
		}
		void Place(MatchState s,Guid owner,string card,int x,int y)
		{
			if (!s.GiveCard(owner,card) || !s.TryPlace(owner,card,x,y).Success) throw new InvalidOperationException($"Test setup {card} at {x},{y}");
		}
		var links = Board();
		Place(links,A,"house",2,4); Place(links,B,"shop",3,4); Place(links,A,"farm",4,4);
		var payout = Payout.Calculate(links);
		Check(payout.CoinsFor(A) == 4 && payout.CoinsFor(B) == 4,"House and Shop independently pay their owners");
		Check(links.Players[A].Coins == 8,"payout preview has no side effects");
		Check(payout.Entries.Where(e => e.Source != null).Select(e => e.Source.Order).SequenceEqual(payout.Entries.Where(e => e.Source != null).Select(e => e.Source.Order).Order()),"payout entries animate oldest building first");
		links.SetPhase(MatchPhase.Payout,2,80); links.ChangeVp(B,2);
		Check(Payout.Calculate(links).CoinsFor(A) == 5 && Payout.Calculate(links).CoinsFor(B) == 4,"catch-up goes to last place from round 2");
		links.ChangeVp(A,2);
		Check(Payout.Calculate(links).CoinsFor(A) == 5 && Payout.Calculate(links).CoinsFor(B) == 5,"tied last-place players both receive catch-up");


		var preview = Board();
		Place(preview,B,"house",4,4);
		var snapshot=System.Text.Json.JsonSerializer.Serialize(preview.Capture());
		var adjacency=preview.PreviewInteractions(A,"house",5,4);
		Check(adjacency.Any(i => i.Cell.CardId=="house" && i.Positive && i.Detail.Contains("VP on placement")),"placement preview identifies adjacent house VP");
		Check(snapshot==System.Text.Json.JsonSerializer.Serialize(preview.Capture()),"interaction preview restores the complete board");
		var pollution=preview.PreviewInteractions(A,"factory",5,4);
		Check(pollution.Any(i => i.Cell.CardId=="house" && i.Negative && i.Detail.Contains("VP at game end")),"placement preview identifies factory pollution for another owner");
		var full = Board();
        Place(full,A,"shop",5,4);
        foreach (var x in new[] {3,4,6,7}) Place(full,B,"house",x,4);
        var fullPay=Payout.Calculate(full);
        Check(fullPay.Entries.Where(e=>e.Source?.CardId=="house").Sum(e=>e.Coins)==4 && fullPay.Entries.Single(e=>e.Source?.CardId=="shop").Coins==3,"all Homes pay independently while Shop caps at three");
        var reversed=Board();
        foreach (var x in new[] {7,6,4,3}) Place(reversed,B,"house",x,4);
        Place(reversed,A,"shop",5,4);
        Check(Payout.Calculate(reversed).CoinsFor(A)==fullPay.CoinsFor(A) && Payout.Calculate(reversed).CoinsFor(B)==fullPay.CoinsFor(B),"income is independent of placement order");
        var tagged=Board(); tagged.UpdateCell(new(5,4,"shop",A,1,1)); tagged.UpdateCell(new(6,4,"housing-authority",B,2,1));
        Check(Payout.Calculate(tagged).Entries.Single(e=>e.Source?.CardId=="shop").Coins==1,"Shop counts rival Housing Authority as a Home");
        Check(BoardRules.Distance(new(0,0,"house",A,1,1),new(2,2,"shop",B,2,1)) == 2,"diagonal range includes diagonals");
        var farms=Board(); Place(farms,A,"farm",2,4); Place(farms,B,"farm",3,4); Place(farms,B,"farm",4,4);
        Check(Payout.Calculate(farms).Entries.Single(e=>e.Source?.X==3 && e.Source.CardId=="farm").Coins==1,"Farm condition pays once even with two neighbors");

		var cottage = Board();
		Place(cottage,A,"cottage",1,1);
		Check(Payout.Calculate(cottage).CoinsFor(A) == 3,"isolated Cottage has no income");
		Place(cottage,B,"park",1,2);
		Check(Payout.Calculate(cottage).CoinsFor(A) == 4,"Cottage beside park pays its owner");
		Check(PlacementScoring.Preview(cottage,"cottage",2,2) == 1,"Cottage preview counts adjacent parks");
		Place(cottage,A,"cottage",2,2);
		Check(cottage.Players[A].Vp == 1,"Cottage preview and committed VP agree");
		var houses = Board();
		Place(houses,A,"house",4,4); Place(houses,B,"house",5,4);
		Check(houses.Players[A].Vp == 0 && houses.Players[B].Vp == 1,"only the placing House owner gets adjacency VP");
		Place(houses,A,"cafe",6,4);
		Check(Payout.Calculate(houses).CoinsFor(A) == 4,"Cafe pays its owner for an adjacent house");
		var noRoad = new MatchState(11,11); noRoad.AddPlayer(A,"A"); noRoad.SetPhase(MatchPhase.Build,1,40);
		noRoad.GiveCard(A,"grant");
		Check(noRoad.PlayGrant(A,0) && noRoad.Players[A].Coins == 12 && !noRoad.Cells().Any(),"Grant consumes its card for four coins without occupying a tile");
		noRoad.GiveCard(A,"park"); noRoad.GiveCard(A,"house");
		Check(noRoad.Discard(A,0) && noRoad.Players[A].Hand.SequenceEqual(new[]{"house"}),"explicit discard removes only the selected slot");

		var idle = new MatchState(11,11,123,new MatchSettings { EnableAuctions = false, EnableGoals = false, EnableContracts = false, BaseIncome = 3 }); idle.AddPlayer(A,"A"); idle.AddPlayer(B,"B");
		var machine = new RoundMachine(idle); var payments = 0; idle.Events.PayoutResolved += _ => payments++;
		machine.Start(0);
		Check(idle.Phase == MatchPhase.Bid && idle.Players[A].Hand.SequenceEqual(idle.Players[B].Hand) && idle.Players[A].Hand.Count == 1 && idle.Players[A].Hand[0] != "street","every player receives the same free Common, without a Street, before Bid");
		machine.Tick(1);
		Check(idle.Phase == MatchPhase.Bid,"Bid stub respects its deadline");
		machine.Ready(A,1);
		Check(idle.Phase == MatchPhase.Build && idle.PhaseEndsAt == 41,"Ready skips stub Bid and starts a full Build clock");
		machine.Tick(41);
		Check(idle.Phase == MatchPhase.Payout && payments == 1 && idle.Players[A].Coins == 11,"Build deadline settles income once");
		machine.Tick(41); machine.Ready(A,42);
		Check(payments == 1,"repeated ticks and Ready during Payout cannot duplicate income");
		machine.Tick(10000);
		Check(idle.Phase == MatchPhase.EndGame && idle.Round == 8 && payments == 8,"large clock step completes exactly eight rounds");
		Check(idle.Players[A].Coins == 39 && idle.Players[B].Coins == 39,"eight idle payouts have an exact known coin total");
		Check(idle.Players[A].Hand.Count == 5,"retained defaults respect the five-card limit");
		machine.Tick(20000);
		Check(payments == 8 && idle.Players[A].Coins == 39,"EndGame is terminal and never pays again");

		var auto = new MatchState(11,11,42,new MatchSettings { EnableAuctions=false, EnableGoals=false, EnableContracts=false, DefaultCommons=0, DefaultStreets=0 });
		auto.AddPlayer(A,"A"); auto.AddPlayer(B,"B");
		var ar = new RoundMachine(auto); var autoPayments=0; auto.Events.PayoutResolved += _ => autoPayments++;
		ar.Start(0); ar.Ready(A,0);
		ar.Tick(0.5); Check(auto.Phase==MatchPhase.Build,"empty Build waits for its grace period");
		auto.GiveCard(B,"grant"); ar.Tick(0.6); ar.Tick(2);
		Check(auto.Phase==MatchPhase.Build,"a newly received card cancels the grace period");
		ar.Ready(B,2); ar.Tick(2.9); Check(auto.Phase==MatchPhase.Build,"mixed ready and empty seats wait one second");
		var restored=RoundMachine.Restore(auto,ar.Capture()); restored.Tick(3);
		Check(auto.Phase==MatchPhase.Payout && autoPayments==1,"mixed ready and empty seats pay after a restored grace period");
		restored.Tick(3); Check(autoPayments==1,"automatic payout happens once");
		var partial = new MatchState(11,11,42,new MatchSettings { EnableAuctions=false, EnableGoals=false, EnableContracts=false, DefaultCommons=0, DefaultStreets=0 });
		partial.AddPlayer(A,"A"); partial.AddPlayer(B,"B"); partial.GiveCard(B,"grant");
		var pr=new RoundMachine(partial); pr.Start(0); pr.Ready(A,0); pr.Tick(5);
		Check(partial.Phase==MatchPhase.Build,"one empty hand does not finish Build while another player holds cards");

		string PlayMatch()
		{
			var s = new MatchState(11,11,321,new MatchSettings { EnableAuctions = false, EnableGoals = false, EnableContracts = false }); s.AddPlayer(A,"A");
			for (var x = 0; x < 11; x++) s.SeedStreet(x,5);
			var r = new RoundMachine(s); r.Start(0); var clock = 0d;
			for (var round = 1; round <= 8; round++)
			{
				r.Ready(A,clock);
				foreach (var card in s.Players[A].Hand.ToArray())
				{
					if (card == "grant") { s.PlayGrant(A,s.Players[A].Hand.ToList().IndexOf(card)); continue; }
					var placed = false;
					for (var y = 0; y < 11 && !placed; y++)
					for (var x = 0; x < 11 && !placed; x++)
						if (s.PlacementError(A,card,x,y) == null) placed = s.TryPlace(A,card,x,y).Success;
				}
				r.Ready(A,clock); clock = s.PhaseEndsAt; r.Tick(clock);
			}
			Check(s.Phase == MatchPhase.EndGame && s.Cells().Count() > 11,"full solo match places defaults and finishes round eight");
			return $"{s.Players[A].Coins}:{s.Players[A].Vp}:"+string.Join(",",s.Cells().Select(c => $"{c.X}/{c.Y}/{c.CardId}"));
		}
		Check(PlayMatch() == PlayMatch(),"same seed and actions reproduce the entire solo board and economy");
		var screens = new ScreenFlow(); screens.Replace(GameScreen.Match); screens.Push(GameScreen.Pause); screens.Push(GameScreen.Settings); screens.Pop();
		Check(screens.Top == GameScreen.Pause && screens.BaseScreen == GameScreen.Match,"Back restores the previous overlay without losing the match");
		screens.Pop(); screens.Pop();
		Check(screens.Top == GameScreen.Match,"screen stack cannot pop its base screen");
		return results.AsReadOnly();
	}
}



