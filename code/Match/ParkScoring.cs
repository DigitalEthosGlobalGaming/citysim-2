using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record RuleAward( Guid Player, int Vp, int Coins, string Reason, BoardCell Source = null );
public sealed record ParkScoreResult( BoardCell Added, int Size, int ScoringSize, IReadOnlyList<BoardCell> Cells, IReadOnlyList<RuleAward> Awards );

public static class ParkScoring
{
	public static int SizeOf(BoardCell cell)
	{
		var card = CardLibrary.Get(cell.CardId);
		var growth = card.Effects.FirstOrDefault(e => e.Hook == EffectHook.Growth && e.Rule == "forest");
		return growth != null && cell.Age >= growth.Cap ? growth.Amount : card.ParkSize;
	}
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
		var size = cells.Sum(SizeOf); var scoring = Math.Min(size,state.Settings.ParkScoringCap);
		// The resulting region already contains each joined component once. Replace the
		// added tile's size with its old size to obtain the pre-addition total.
		var previous = size - SizeOf(added) + (before == null ? 0 : SizeOf(before));
		var divisor = state.Settings.ParkPlacerDivisor;
		int Score(int n) => (Math.Min(n,state.Settings.ParkScoringCap)+divisor-1)/divisor;
		var points = Math.Max(0,Score(size)-Score(previous));
		var awards = new List<RuleAward>();
		if (points > 0 && state.Players.ContainsKey(added.Owner))
			awards.Add(new(added.Owner,points,0,"Park addition",added));
		return new(added,size,scoring,cells,awards.AsReadOnly());
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
