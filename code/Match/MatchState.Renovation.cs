using System;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed partial class MatchState
{
 /// <summary>Checks a replacement against the board with the old building excluded, without emitting events.</summary>
 public string RenovationError(Guid owner,int eventSlot,int replacementSlot,int x,int y)
 {
  if (Phase!=MatchPhase.Build) return "Wait for the Build phase.";
  if (!players.TryGetValue(owner,out var player)) return "Unknown player.";
  if (eventSlot<0 || eventSlot>=player.Hand.Count || player.Hand[eventSlot]!="renovation") return "Select Renovation first.";
  if (replacementSlot<0 || replacementSlot>=player.Hand.Count || replacementSlot==eventSlot) return "Select a different building card.";
  var replacement=player.Hand[replacementSlot];
  if (!CardLibrary.TryGet(replacement,out var card) || card.Type is CardType.Road or CardType.Event) return "Choose a building card.";
  if (!InBounds(x,y)) return "Outside the board.";
  var old=board[x,y];
  if (old==null || old.Owner!=owner || CardLibrary.Get(old.CardId).Type is CardType.Road or CardType.Event) return "Choose one of your buildings.";
  if (old.CardId==replacement) return "Choose a different building type.";
  board[x,y]=null;
  try { return PlacementError(owner,replacement,x,y); }
  finally { board[x,y]=old; }
 }

 /// <summary>Preview is pure: the target is restored even if scoring throws.</summary>
 public int? PreviewRenovation(Guid owner,int eventSlot,int replacementSlot,int x,int y)
 {
  if (RenovationError(owner,eventSlot,replacementSlot,x,y)!=null) return null;
  var old=board[x,y]; board[x,y]=null;
  try { return PlacementScoring.Preview(this,players[owner].Hand[replacementSlot],x,y,owner); }
  finally { board[x,y]=old; }
 }

 /// <summary>Replaces an owned building; prior VP is retained and both selected cards are consumed.</summary>
 public PlacementResult TryRenovate(Guid owner,int eventSlot,int replacementSlot,int x,int y)
 {
  var error=RenovationError(owner,eventSlot,replacementSlot,x,y);
  if (error!=null) return new(false,error);
  var player=players[owner]; var old=board[x,y]; var id=player.Hand[replacementSlot];
  var cell=new BoardCell(x,y,id,owner,NextPlacementOrder,Round); PlacementEffects effects;
  board[x,y]=null;
  try { effects=PlacementScoring.Calculate(this,cell); }
  finally { board[x,y]=old; }
  player.MutableHand.RemoveAt(Math.Max(eventSlot,replacementSlot)); player.MutableHand.RemoveAt(Math.Min(eventSlot,replacementSlot));
  order++; ChangeCoins(owner,-Settings.BuildCost(CardLibrary.Get(id))); CommitPlacement(cell,effects,old);
  Events.Played(new(owner,"renovation"));
  return new(true,"",cell);
 }
}
