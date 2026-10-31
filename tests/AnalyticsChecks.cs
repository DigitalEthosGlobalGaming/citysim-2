using System.Text.Json;
using CitySim.Match;

public static class AnalyticsChecks
{
	public static void Run()
	{
		void Check(bool condition,string message) { if (!condition) throw new Exception("Analytics: "+message); }
		var human=Guid.NewGuid(); var bot=Guid.NewGuid(); var matchId=Guid.NewGuid();
		var settings=new MatchSettings {EnableContracts=false,EnableGoals=false,DefaultStreets=0,DefaultCommons=0,HandTarget=3,HandLimit=5};
		var state=new MatchState(12,12,123,settings); state.AddPlayer(human,"PRIVATE HUMAN NAME"); state.AddPlayer(bot,"PRIVATE BOT NAME");
		var rounds=new RoundMachine(state); var records=new List<BalanceEvent>();
		using var analytics=new BalanceAnalytics(state,rounds,matchId,"solo",new[]{bot},records.Add,
			botConfigurations:new Dictionary<Guid,BotAnalyticsConfiguration> {{bot,new("Hard","Nature")}});
		var started=JsonSerializer.SerializeToElement(records[0].Data);
		Check(started.GetProperty("Players")[0].GetProperty("BotConfiguration").ValueKind==JsonValueKind.Null &&
			started.GetProperty("Players")[1].GetProperty("BotConfiguration").GetProperty("Difficulty").GetString()=="Hard" &&
			started.GetProperty("Players")[1].GetProperty("BotConfiguration").GetProperty("Style").GetString()=="Nature","individual difficulty and style are recorded on the bot seat only");
		rounds.Start(0); var view=rounds.GetAuctionView(human);
		Check(records.Count(e=>e.Event=="market_offered")==2,"offers recorded for each seat");
		Check(!rounds.BuyMarketCard(human,view.Id+1,0,0),"invalid purchase rejected");
		Check(!records.Any(e=>e.Event=="card_purchased"),"rejected actions do not count");
		Check(rounds.BuyMarketCard(human,view.Id,0,0),"valid purchase");
		Check(records.Count(e=>e.Event=="card_purchased")==1 && !records.Any(e=>e.Event=="market_resolved"),"purchase persisted before market resolves");
		Check(!rounds.BuyMarketCard(human,view.Id,0,0) && records.Count(e=>e.Event=="card_purchased")==1,"duplicate purchase does not count twice");
		state.SetPhase(MatchPhase.Build,1,100); state.GiveCard(human,"park");
		Check(state.TryPlace(human,"park",3,3).Success,"placement");
		state.GiveCard(human,"grant"); Check(state.PlayEvent(human,state.Players[human].Hand.Count-1),"event play");
		Check(state.Discard(human,0),"discard");
		Check(records.Count(e=>e.Event=="card_played")==2 && records.Count(e=>e.Event=="card_discarded")==1,"plays and discards tracked");
		var json=JsonSerializer.Serialize(records);
		Check(!json.Contains(human.ToString()) && !json.Contains(bot.ToString()) && !json.Contains("PRIVATE"),"no account IDs or player names in payloads");
		Check(records.Select(e=>e.Sequence).SequenceEqual(Enumerable.Range(1,records.Count).Select(n=>(long)n)),"contiguous event sequence");
		analytics.Dispose(); var count=records.Count; state.GiveCard(human,"street");
		Check(records.Last().Event=="recording_stopped" && records.Count==count,"detaches cleanly");
		var failures=0;
		using (var broken=new BalanceAnalytics(state,rounds,Guid.NewGuid(),"solo",null,_=>throw new IOException("disk full"),_=>failures++))
		{
			Check(state.GiveCard(human,"park"),"storage failure leaves gameplay working");
			Check(failures==1,"failed sink disabled after one warning");
		}
		var completed=new List<BalanceEvent>();
		using (var final=new BalanceAnalytics(state,rounds,matchId,"solo",null,completed.Add))
		{
			FinalScoring.Apply(state); FinalScoring.Apply(state);
		}
		Check(completed.Count(e=>e.Event=="match_completed")==1 && completed.Last().Event=="match_completed","one terminal completed record");
		var resumed=new List<BalanceEvent>();
		using (var next=new BalanceAnalytics(state,rounds,matchId,"multiplayer",null,resumed.Add)) { }
		Check(resumed[0].MatchId==records[0].MatchId && resumed[0].RecordingId!=records[0].RecordingId,"migration segments share match ID and have unique recording IDs");
		Console.WriteLine("Analytics checks passed: offers, committed actions, anonymity, lifecycle and storage failure isolation.");
	}
}
