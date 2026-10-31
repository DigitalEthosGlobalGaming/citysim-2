using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CitySim.Cards;

namespace CitySim.Match;

public static class LocalBalanceChecks
{
 public static IReadOnlyList<string> Run()
 {
  var passed=new List<string>();void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);passed.Add("PASS "+why);}
  var a=new Guid("11111111-0000-0000-0000-000000000001");var b=new Guid("22222222-0000-0000-0000-000000000002");var c=new Guid("33333333-0000-0000-0000-000000000003");
  MatchState Board(int round=1){var s=new MatchState(15,15,71,new MatchSettings {EnableGoals=false,EnableContracts=false});s.AddPlayer(a,"A");s.AddPlayer(b,"B");s.AddPlayer(c,"C");s.SetPhase(MatchPhase.Build,round,40);return s;}
  void Put(MatchState s,Guid owner,string id,int x,int y){s.GiveCard(owner,id);var p=s.TryPlace(owner,id,x,y);if(!p.Success)throw new InvalidOperationException(id+": "+p.Reason);}
  void Road(MatchState s,Guid owner,int x,int y,int order)=>s.UpdateCell(new(x,y,"street",owner,order,1));
  Check(CardLibrary.All.Count(x=>x.NeedsRoad)==15,"fifteen building cards require roads after removing fourteen requirements");
  foreach(var id in new[]{"house","housing-authority","farm","orchard","vineyard","eco-council","cafe","school","museum","hospital","tourist-office","startup","contractor","architects-studio"}){
   var s=Board();s.GiveCard(a,id);Check(s.PlacementError(a,id,7,7)==null,id+" is legal without a road");
  }
  var streets=Board();Road(streets,a,4,5,100);Road(streets,a,5,4,101);Road(streets,b,6,5,102);Road(streets,Guid.Empty,5,6,103);
  var before=JsonSerializer.Serialize(streets.Capture());var preview=streets.PreviewOutlook(c,"house",5,5);
  Check(preview.Awards.Where(x=>x.Reason=="Street · building placed").Count()==2 && preview.Awards.Where(x=>x.Player==a).Sum(x=>x.Vp)==1 && preview.Awards.Where(x=>x.Player==b).Sum(x=>x.Vp)==1,"a new building pays each adjacent Street owner once, excluding the city");
  Check(preview.Awards.Where(x=>x.Player==c).Sum(x=>x.Vp)==1 && before==JsonSerializer.Serialize(streets.Capture()),"the builder's road bonus is one point and preview does not mutate the match");
  Put(streets,c,"house",5,5);
  Check(streets.Players[a].Vp==1 && streets.Players[b].Vp==1 && streets.Players[c].Vp==1,"committed building applies both Street-owner and builder awards exactly once");
  for(int round=1;round<=3;round++){streets.SetPhase(MatchPhase.Payout,round,40);Payout.Apply(streets,Payout.Calculate(streets));}
  Check(streets.Players[a].Vp==1 && streets.Players[b].Vp==1 && streets.Players[c].Vp==1,"three payouts never repeat Street or road-adjacency VP");
  var retro=Board();Put(retro,a,"house",1,1);var start=retro.Players[a].Vp;Put(retro,a,"street",1,0);
  Check(retro.Players[a].Vp==start,"placing a Street beside an existing building awards no retroactive VP");
  var own=Board();Road(own,a,1,0,100);Put(own,a,"house",1,1);
  Check(own.Players[a].Vp==2,"a builder who owns the Street receives both separate one-point bonuses");
  foreach(var id in new[]{"house","cafe","school","museum","hospital","tourist-office"}){
   var s=Board();Road(s,Guid.Empty,3,2,101);Road(s,b,2,3,102);s.UpdateCell(new(4,3,"avenue",Guid.Empty,103,1));
   var award=PlacementScoring.Calculate(s,new(3,3,id,a,200,1));Check(award.Awards.Where(x=>x.Reason=="Beside road").Sum(x=>x.Vp)==1,id+" receives only one road bonus beside multiple roads");
  }
  var diagonal=Board();Road(diagonal,b,2,2,100);Put(diagonal,a,"house",3,3);Check(diagonal.Players[a].Vp==0 && diagonal.Players[b].Vp==0,"diagonal roads award neither builder nor Street owner");
  var park=Board();Put(park,b,"farm",4,4);Put(park,c,"vineyard",6,4);var ap=park.Players[a].Vp;Put(park,a,"fountain",5,4);
  Check(park.Players[a].Vp-ap==3,"Fountain awards two base VP and one bonus beside multiple rivals' Nature cards");
  Put(park,a,"park",3,3);Check(park.Players[a].Vp-ap==4,"a diagonal Nature card does not trigger the park bonus");
  var big=Board();for(var x=0;x<12;x++)Put(big,a,"park",x,0);
  Check(big.Players[a].Vp==23,"large parks keep awarding one base plus one Nature-neighbour VP beyond ten tiles");
  var lake=Board();Put(lake,a,"park",4,4);Put(lake,b,"lake",5,4);Put(lake,a,"fountain",6,4);
  Check(ParkScoring.Region(lake,lake.Get(4,4)).Count==3 && ParkScoring.Region(lake,lake.Get(4,4)).Sum(ParkScoring.SizeOf)==3,"Lake connects park tiles and Fountain counts as one physical tile");
  var growth=Board();Road(growth,b,1,0,100);Put(growth,a,"sapling",1,1);var paid=growth.Players[b].Vp;
  for(var r=1;r<=3;r++){growth.SetPhase(MatchPhase.Payout,r,40);Payout.Apply(growth,Payout.Calculate(growth));}
  Check(growth.Players[a].Vp==3 && growth.Players[b].Vp==paid && ParkScoring.SizeOf(growth.Get(1,1))==1,"Forest growth grants two VP once without another Street award or extra park tiles");
  var late=Board(7);var snap=JsonSerializer.Serialize(late.Capture());var bank=late.PreviewOutlook(a,"bank",7,7);var startup=late.PreviewOutlook(a,"startup",7,7);var tax=late.PreviewOutlook(a,"tax-office",7,7);
  Check(bank.PayoutsLeft==2 && bank.NextIncome==2 && bank.RemainingIncome==4 && startup.NextIncome==1 && startup.RemainingIncome==3,"late income previews show actual Bank and Startup payout horizons");
  Check(tax.RemainingIncome==0 && JsonSerializer.Serialize(late.Capture())==snap,"an unsupported Tax Office forecasts zero income without changing state");
  var replacement=Board(7);Put(replacement,a,"startup",7,7);var replacementState=JsonSerializer.Serialize(replacement.Capture());
  Check(replacement.PreviewOutlook(a,"bank",7,7,true).RemainingIncome==1 && JsonSerializer.Serialize(replacement.Capture())==replacementState,"replacement forecast subtracts the old building's future income and restores it");
  return passed;
 }
}
