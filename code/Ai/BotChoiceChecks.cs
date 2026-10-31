using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CitySim.Cards;
using CitySim.Match;

namespace CitySim.Ai;

public static class BotChoiceChecks
{
 public static IReadOnlyList<string> Run()
 {
  var results=new List<string>();void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);results.Add("PASS "+message);}
  var ratings=new[]{10d,9.5,9,2,double.NaN,double.NegativeInfinity};
  foreach(var difficulty in Enum.GetValues<BotDifficulty>())
  {
   var p=BotChoice.Probabilities(ratings,difficulty);
   Check(Math.Abs(p.Sum()-1)<1e-10 && Math.Abs(p[0]-BotChoice.BestChoiceChance(difficulty))<1e-10,
    difficulty+" gives the best rated option its configured probability");
   Check(p[1]>p[2] && p[2]>0 && p.Skip(3).All(v=>v==0),difficulty+" weights reasonable alternatives by rating and excludes bad/nonfinite options");
   var rng=new Random(711);var counts=new int[ratings.Length];
   for(var i=0;i<20000;i++) counts[BotChoice.PickIndex(ratings,difficulty,rng)]++;
   Check(Math.Abs(counts[0]/20000d-p[0])<0.015 && counts[1]>counts[2] && counts.Skip(3).All(n=>n==0),
    difficulty+" observed choice frequency matches its probability over 20000 draws");
   var ties=BotChoice.Probabilities(new[]{4d,4,3.5},difficulty);
   Check(Math.Abs(ties[0]-ties[1])<1e-10 && Math.Abs(ties[0]+ties[1]-BotChoice.BestChoiceChance(difficulty))<1e-10,
    difficulty+" shares the best-choice budget fairly between equal ratings");
   Check(BotChoice.Probabilities(new[]{10d,0},difficulty).SequenceEqual(new[]{1d,0}),difficulty+" always takes the sole clearly superior choice");
  }
  Check(BotChoice.PickIndex(Array.Empty<double>(),BotDifficulty.Hard,new(1))==-1 &&
   BotChoice.PickIndex(new[]{double.NaN,double.PositiveInfinity},BotDifficulty.Normal,new(1))==-1,"empty and entirely nonfinite rating sets have no selectable option");
  var first=new Random(45);var second=new Random(45);
  Check(Enumerable.Range(0,200).All(_=>BotChoice.PickIndex(ratings,BotDifficulty.Normal,first)==BotChoice.PickIndex(ratings,BotDifficulty.Normal,second)),"weighted choices replay exactly with the same seed");
  var a=new Guid("a1111111-0000-0000-0000-000000000001");var b=new Guid("b1111111-0000-0000-0000-000000000002");
  MatchState Board(){var s=new MatchState(9,9,1,new MatchSettings{EnableContracts=false,EnableGoals=false,DefaultCommons=0});s.AddPlayer(a,"A");s.AddPlayer(b,"B");s.SetPhase(MatchPhase.Build,8,40);return s;}
  var evaluation=new BotEvaluation(a,BotDifficulty.Hard,BotStyle.Balanced,null);
  var nature=Board();nature.UpdateCell(new(5,4,"farm",b,1,1));nature.GiveCard(a,"fountain");
  var near=BotEvaluation.PublicClone(nature,a);var far=BotEvaluation.PublicClone(nature,a);near.TryPlace(a,"fountain",5,5);far.TryPlace(a,"fountain",8,8);
  Check(Math.Abs(evaluation.Score(near)-evaluation.Score(far)-1)<1e-8,"AI rating values exactly one extra VP beside a rival Nature card");
  var roads=Board();roads.UpdateCell(new(4,4,"street",Guid.Empty,1,0));roads.GiveCard(a,"house");
  var roadside=BotEvaluation.PublicClone(roads,a);var remote=BotEvaluation.PublicClone(roads,a);roadside.TryPlace(a,"house",4,3);remote.TryPlace(a,"house",8,8);
  Check(Math.Abs(evaluation.Score(roadside)-evaluation.Score(remote)-0.8)<1e-8,"AI rating includes the one-time road bonus and its effect on catch-up income");
  var forest=Board();forest.SetPhase(MatchPhase.Build,7,40);forest.UpdateCell(new(2,2,"sapling",a,1,7));
  var mature=BotEvaluation.PublicClone(forest,a);mature.UpdateCell(mature.Get(2,2) with {Age=2});
  Check(Math.Abs(evaluation.Score(forest)-evaluation.Score(mature)-2)<1e-8,"AI forecasts two fixed Forest growth VP without weighted park size");
  var moves=Board();moves.UpdateCell(new(4,4,"street",Guid.Empty,1,0));moves.GiveCard(a,"house");moves.GiveCard(a,"fountain");
  var search=new BotSearch(BotEvaluation.PublicClone(moves,a),a,evaluation,BotDifficulty.Hard);
  for(var step=0;step<5000 && !search.Complete;step++) search.Step();
  var blocked=search.Best.Move;moves.UpdateCell(new(blocked.X,blocked.Y,"park",b,101,8));
  Check(search.Complete && Enumerable.Range(0,200).All(seed=>search.Choose(new(seed),m=>m.Valid(moves,a))?.Move.Valid(moves,a)==true),"randomized placement selection filters occupied sites before drawing");
  foreach(var difficulty in Enum.GetValues<BotDifficulty>())
  {
   var equal=Board();equal.GiveCard(a,"park");equal.GiveCard(a,"wild-garden");
   var job=new BotSearch(BotEvaluation.PublicClone(equal,a),a,new(a,difficulty,BotStyle.Balanced,null),difficulty);
   for(var step=0;step<5000 && !job.Complete;step++) job.Step();
   var rng=new Random(731);var picked=Enumerable.Range(0,2000).Select(_=>job.Choose(rng)).ToArray();
   Check(job.Complete && picked.All(p=>p!=null) && Math.Abs(picked.Count(p=>p.Move.Card=="park")/2000d-0.5)<0.06,
    difficulty+" selects equally rated cards fairly even when their numbers of shortlisted sites differ");
   Check(picked.Select(p=>(p.Move.X,p.Move.Y)).Distinct().Count()>1,difficulty+" varies equally rated placement sites");
  }
  var variety=new HashSet<string>();
  foreach(var seed in Enumerable.Range(0,40))
  {
   var s=new MatchState(5,5,1,new MatchSettings{EnableContracts=false,EnableGoals=false,DefaultCommons=0});s.AddPlayer(a,"A");s.AddPlayer(b,"B");s.SeedStreet(2,2);
   var rounds=new RoundMachine(s);rounds.Start(0);var saved=rounds.Capture();rounds=RoundMachine.Restore(s,saved with {Auction=saved.Auction with {Lots=new[]{new AuctionLot(0,"park",1),new AuctionLot(1,"wild-garden",1),new AuctionLot(2,"house",1)}}});
   var bot=new Bot(a,s,rounds,BotDifficulty.Normal,seed);
   for(var tick=0;tick<600 && !rounds.GetAuctionView(a).Ready;tick++) bot.Update(tick/60d);
   Check(rounds.GetAuctionView(a).Ready && bot.RejectedActions==0,"weighted market fixture finishes legally for decision seed "+seed);
   variety.Add(s.Players[a].Hand.SingleOrDefault() ?? "pass");
  }
  Check(variety.Count>=2,"Normal bots choose different rated market cards under different decision seeds");
  return results;
 }
}
