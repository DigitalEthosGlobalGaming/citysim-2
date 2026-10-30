using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record PlacementInteraction(BoardCell Cell, bool Positive, bool Negative, string Detail);

public sealed partial class MatchState
{
 // Compare the placement with and without each nearby building. All board changes are
 // temporary and the same scoring functions used by the match supply the outcomes.
 public IReadOnlyList<PlacementInteraction> PreviewInteractions(Guid owner,string cardId,int x,int y,bool replacement=false)
 {
  if (!InBounds(x,y) || !CardLibrary.TryGet(cardId,out var card) || card.Type == CardType.Event) return Array.Empty<PlacementInteraction>();
  var old=board[x,y]; var added=new BoardCell(x,y,cardId,owner,NextPlacementOrder,Round);
  var candidates=Cells().Where(c => (c.X!=x || c.Y!=y) && BoardRules.Distance(c,added)<=3).ToArray();
  var result=new List<PlacementInteraction>();
  Dictionary<(Guid,string),int> Outcome(bool place)
  {
   board[x,y]=place ? added : old;
   var values=new Dictionary<(Guid,string),int>();
   void Add(Guid player,string kind,int amount) { var key=(player,kind); values[key]=values.GetValueOrDefault(key)+amount; }
   foreach(var entry in Payout.Calculate(this).Entries) { Add(entry.Player,"coins per payout",entry.Coins); Add(entry.Player,"VP per payout",entry.Vp); }
   foreach(var line in FinalScoring.CardEffects(this)) Add(line.Player,"VP at game end",line.Vp);
   if(place)
   {
    board[x,y]=null;
    foreach(var award in PlacementScoring.Calculate(this,added).Awards) { Add(award.Player,"VP on placement",award.Vp); Add(award.Player,"coins on placement",award.Coins); }
    board[x,y]=added;
   }
   return values;
  }
  try
  {
   var before=Outcome(false); var after=Outcome(true);
   foreach(var candidate in candidates)
   {
    board[candidate.X,candidate.Y]=null;
    var withoutBefore=Outcome(false); var withoutAfter=Outcome(true);
    board[candidate.X,candidate.Y]=candidate;
    var keys=before.Keys.Concat(after.Keys).Concat(withoutBefore.Keys).Concat(withoutAfter.Keys).Distinct();
    var changes=keys.Select(k => (k.Item1,k.Item2,Amount:after.GetValueOrDefault(k)-before.GetValueOrDefault(k)-withoutAfter.GetValueOrDefault(k)+withoutBefore.GetValueOrDefault(k))).Where(v=>v.Amount!=0).ToArray();
    if(changes.Length==0) continue;
    var details=changes.Select(v => $"{(v.Amount>0 ? "+" : "")}{v.Amount} {v.Item2} ({(v.Item1==owner ? "you" : Players.TryGetValue(v.Item1,out var p) ? p.Name : "other")})");
    result.Add(new(candidate,changes.Any(v=>v.Amount>0),changes.Any(v=>v.Amount<0),CardLibrary.Get(candidate.CardId).Name+": "+string.Join(", ",details)));
   }
  }
  finally { board[x,y]=old; foreach(var candidate in candidates) board[candidate.X,candidate.Y]=candidate; }
  return result.AsReadOnly();
 }
}
