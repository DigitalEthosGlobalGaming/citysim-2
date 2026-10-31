using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record RuleAward( Guid Player, int Vp, int Coins, string Reason, BoardCell Source = null );
public sealed record ParkScoreResult( BoardCell Added, int Size, int ScoringSize, IReadOnlyList<BoardCell> Cells, IReadOnlyList<RuleAward> Awards );

public static class ParkScoring
{
	/// <summary>Every park card is one physical tile, including Fountain, Lake and mature Forest.</summary>
	public static int SizeOf(BoardCell cell) => BoardRules.IsPark(cell) ? 1:0;
	public static IReadOnlyList<BoardCell> Region(MatchState state,BoardCell origin)
	{
		var board = state.Cells().ToDictionary(c => (c.X,c.Y));
		board[(origin.X,origin.Y)] = origin;
		var visited = new HashSet<(int,int)>(); var queue = new Queue<BoardCell>(); var cells = new List<BoardCell>();
		queue.Enqueue(origin);
		while (queue.Count > 0)
		{
			var cell = queue.Dequeue();
			if (!visited.Add((cell.X,cell.Y)) || SizeOf(cell) <= 0) continue;
			cells.Add(cell);
			foreach (var d in new[] {(0,-1),(1,0),(0,1),(-1,0)})
				if (board.TryGetValue((cell.X+d.Item1,cell.Y+d.Item2),out var n) && !visited.Contains((n.X,n.Y))) queue.Enqueue(n);
		}
		return cells.OrderBy(c => c.Order).ToList().AsReadOnly();
	}
	public static ParkScoreResult OnTileAdded(MatchState state,BoardCell added,BoardCell before = null)
	{
		var cells = Region(state,added);
		var card=CardLibrary.Get(added.CardId);
		var awards = new List<RuleAward>();
		if (state.Players.ContainsKey(added.Owner))
		{
			if (before!=null)
			{
				var growth=card.Effects.FirstOrDefault(e=>e.Hook==EffectHook.Growth && e.Rule=="forest");
				if(growth!=null && before.Age<growth.Cap && added.Age>=growth.Cap)
					awards.Add(new(added.Owner,growth.Amount,0,"Forest growth",added));
			}
			else
			{
				var points=card.Effects.Where(e=>e.Hook==EffectHook.Place && e.Rule=="park-score").Sum(e=>e.Amount);
				if(points>0) awards.Add(new(added.Owner,points,0,"Park placement",added));
				if(state.Neighbours(added.X,added.Y).Any(c=>CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Nature)))
				{
					var bonus=card.Effects.Where(e=>e.Hook==EffectHook.Place && e.Rule=="beside-nature-vp").Sum(e=>e.Amount);
					if(bonus>0) awards.Add(new(added.Owner,bonus,0,"Beside Nature",added));
				}
			}
		}
		return new(added,cells.Count,cells.Count,cells,awards.AsReadOnly());
	}
	public static IReadOnlyList<IReadOnlyList<BoardCell>> Regions(MatchState state)
	{
		var seen = new HashSet<int>(); var regions = new List<IReadOnlyList<BoardCell>>();
		foreach (var cell in state.Cells().Where(c => SizeOf(c) > 0))
		{
			if (seen.Contains(cell.Order)) continue;
			var region = Region(state,cell); foreach (var c in region) seen.Add(c.Order); regions.Add(region);
		}
		return regions.AsReadOnly();
	}
}
