using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class RoadScoring
{
	/// <summary>Longest simple chain of owned roads, not the size of a branching network. Neutral roads do not count.</summary>
	public static int Longest(MatchState state,Guid owner)
	{
		var roads = state.Cells().Where(c => c.Owner == owner && c.PlacedRound > 0 && CardLibrary.Get(c.CardId).Type == CardType.Road).ToDictionary(c => c.Order);
		var edges = roads.Values.ToDictionary(c => c.Order,c => state.Neighbours(c.X,c.Y).Where(n => roads.ContainsKey(n.Order)).Select(n => n.Order).ToArray());
		int Weight(int id) => roads[id].CardId == "avenue" ? 2 : 1;
		var unseen = roads.Keys.ToHashSet(); var best = 0;
		while (unseen.Count > 0)
		{
			var component = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(unseen.First());
			while (queue.Count > 0) { var node = queue.Dequeue(); if (!component.Add(node)) continue; unseen.Remove(node); foreach (var next in edges[node]) queue.Enqueue(next); }
			var total = component.Sum(Weight);
			if (total <= best) continue;
			if (component.All(n => edges[n].Length <= 2)) { best = total; continue; }
			if (component.Sum(n => edges[n].Length)/2 == component.Count-1)
			{
				int Tree(int node,int parent)
				{
					var legs = edges[node].Where(n => n != parent).Select(n => Tree(n,node)).OrderByDescending(n => n).Take(2).ToArray();
					best = Math.Max(best,Weight(node)+legs.Sum()); return Weight(node)+(legs.FirstOrDefault());
				}
				Tree(component.First(),-1); continue;
			}
			// Cyclic branching networks need an exact search. Reachability pruning avoids visiting dead branches.
			var used = new HashSet<int>();
			void Search(int node,int length)
			{
				used.Add(node); length += Weight(node); best = Math.Max(best,length);
				if (best < total)
				{
					var reachable = new HashSet<int>(); var frontier = new Queue<int>(); frontier.Enqueue(node);
					while (frontier.Count > 0) { var n = frontier.Dequeue(); if (!reachable.Add(n)) continue; foreach (var next in edges[n]) if (!used.Contains(next)) frontier.Enqueue(next); }
					var upper = length+reachable.Where(n => !used.Contains(n)).Sum(Weight);
					if (upper > best) foreach (var next in edges[node].Where(n => !used.Contains(n)).OrderBy(n => edges[n].Length)) { Search(next,length); if (best == total) break; }
				}
				used.Remove(node);
			}
			foreach (var start in component.OrderBy(n => edges[n].Length)) { Search(start,0); if (best == total) break; }
		}
		return best;
	}
}
