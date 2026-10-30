using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Match;

public static class HospitalChecks
{
 static readonly Guid A = new("11111111-1111-1111-1111-111111111111"), B = new("22222222-2222-2222-2222-222222222222");
 public static IReadOnlyList<string> Run()
 {
  var results = new List<string>();
  void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); results.Add("PASS " + name); }
  (MatchState,RoundMachine) Board()
  {
   var state=new MatchState(12,12,1729,new MatchSettings { EnableAuctions=false, EnableGoals=false, EnableContracts=false, DefaultStreets=0, DefaultCommons=0 });
   state.AddPlayer(A,"A"); state.AddPlayer(B,"B");
   for (var x=0;x<12;x++) state.SeedStreet(x,5);
   state.GiveCard(B,"grant"); // Keep Build open for midpoint emergency checks.
   var rounds=new RoundMachine(state); rounds.Start(0); rounds.Ready(A,0); return (state,rounds);
  }
  void Place(MatchState state,Guid owner,string id,int x,int y)
  {
   if (!state.GiveCard(owner,id) || !state.TryPlace(owner,id,x,y).Success) throw new InvalidOperationException("Hospital test placement: "+id);
  }
  foreach (var invalid in new[] { -0.01, 1.01, double.NaN, double.PositiveInfinity })
  {
   var rejected=false; try { _=new MatchState(2,2,0,new MatchSettings { HospitalEmergencyFraction=invalid }); } catch (ArgumentException) { rejected=true; }
   Check(rejected,"invalid hospital schedule fraction rejected");
  }
  var (none,nr)=Board(); HospitalEmergencyResult empty=null; var count=0;
  none.Events.HospitalEmergency += e => { empty=e; count++; };
  nr.Tick(20); nr.Tick(20); Check(count==1 && empty.House==null && !empty.Answered,"no homes attempts once without a draw");
  var (state,r)=Board(); Place(state,A,"house",5,4); Place(state,B,"hospital",2,4); Place(state,A,"hospital",8,4);
  HospitalEmergencyResult notice=null; state.Events.HospitalEmergency += e=>notice=e;
  var before=state.Players[B].Vp; r.Tick(19); Check(notice==null,"emergency waits until midpoint"); r.Tick(20);
  Check(notice.Answered && notice.House.X==5 && notice.Hospital.X==2 && state.Players[B].Vp==before+notice.Award && notice.Award==CitySim.Cards.CardLibrary.Get("hospital").Effects.Single(e=>e.Rule=="hospital-emergency").Amount,"equal-distance hospital tie chooses oldest and awards its owner");
  r.Tick(40); r.Tick(40); Check(state.Players[B].Vp==before+1,"deadline cannot repeat emergency");
  var (far,fr)=Board(); Place(far,A,"cottage",1,1); Place(far,B,"hospital",9,4);
  HospitalEmergencyResult unanswered=null; far.Events.HospitalEmergency += e=>unanswered=e; fr.Ready(A,1); fr.Ready(B,1);
  Check(unanswered.House.CardId=="cottage" && !unanswered.Answered && unanswered.Hospital==null,"early ready attempts unanswered residential emergency");
  var (diag,dr)=Board(); Place(diag,A,"cottage",1,1); Place(diag,B,"hospital",5,4);
  HospitalEmergencyResult diagonal=null; diag.Events.HospitalEmergency += e=>diagonal=e; dr.Tick(20);
  Check(diagonal.Answered && BoardRules.Distance(diagonal.House,diagonal.Hospital)==4,"range uses Chebyshev distance including diagonals");
  var (source,sr)=Board(); Place(source,A,"house",2,4); Place(source,A,"house",4,4); Place(source,B,"hospital",6,4);
  var beforeCheckpoint=source.Capture(); var roundCheckpoint=sr.Capture();
  var restored=MatchState.Restore(beforeCheckpoint); var rr=RoundMachine.Restore(restored,roundCheckpoint);
  HospitalEmergencyResult first=null,second=null; source.Events.HospitalEmergency += e=>first=e; restored.Events.HospitalEmergency += e=>second=e;
  sr.Tick(20); rr.Tick(20);
  Check(first==second && sr.Capture().RandomState==rr.Capture().RandomState,"checkpoint preserves next emergency draw and RNG state");
  var post=MatchState.Restore(source.Capture()); var postRound=RoundMachine.Restore(post,sr.Capture()); var repeats=0; post.Events.HospitalEmergency += _=>repeats++; postRound.Tick(30); postRound.Tick(40);
  Check(repeats==0,"checkpoint after attempt does not replay emergency");
  var json=System.Text.Json.JsonSerializer.Serialize(first); Check(!json.Contains("Hand") && !json.Contains("Seed") && first.Round==1,"public result exposes board cells and round without hands or seed");
  return results.AsReadOnly();
 }
}
