using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public enum GoalKind { ConnectedHomes, ShopLine, ParkOwner, ConnectedFarms, TagCount, TagCollector, MiniMall, Coins }
public sealed record GoalDef(string Id,string Name,string Icon,string Description,GoalKind Kind,int Target,Tag Tag = Tag.None,int Groups = 1);
public sealed record ObjectiveProgress(int Current,int Target,string Detail) { public bool Complete => Current >= Target; }
public sealed record GoalAward(Guid Player,int Vp);
public sealed record GoalClaim(string GoalId,int Round,IReadOnlyList<GoalAward> Awards);
public sealed record PublicGoal(string GoalId,GoalClaim Claim = null);
public sealed record GoalsResult(int Round,IReadOnlyList<GoalClaim> Claims);

public static class GoalLibrary
{
	public static IReadOnlyList<GoalDef> All { get; } = Array.AsReadOnly(new[] {
		new GoalDef("neighbourhood","Neighbourhood","🏘️","Own 4 connected Home buildings.",GoalKind.ConnectedHomes,4),
		new GoalDef("high-street","High Street","🏪","Own 3 consecutive Shops in one row or column.",GoalKind.ShopLine,3),
		new GoalDef("green-thumb","Green Thumb","🌿","Own 4 physical tiles in a single park.",GoalKind.ParkOwner,4),
		new GoalDef("farm-belt","Farm Belt","🌾","Own 3 connected Farms.",GoalKind.ConnectedFarms,3),
		new GoalDef("civic-pride","Civic Pride","🏛️","Own 3 Civic-tagged buildings.",GoalKind.TagCount,3,Tag.Civic),
		new GoalDef("arts-district","Arts District","🎭","Own 3 Culture-tagged buildings.",GoalKind.TagCount,3,Tag.Culture),
		new GoalDef("tag-collector","Tag Collector","🎨","Own 4 buildings in each of 3 different tags.",GoalKind.TagCollector,4,Groups:3),
		new GoalDef("mini-mall","Mini Mall","🛍️","Own a Shop, Cafe and park tile beside the same road.",GoalKind.MiniMall,3),
		new GoalDef("rainy-day","Rainy Day","🪙","Hold 12 coins when Build ends.",GoalKind.Coins,12)
	});
	public static GoalDef Get(string id) => All.FirstOrDefault(g => g.Id == id) ?? throw new ArgumentException("Unknown goal: "+id);
}

public static class GoalRules
{
	public static int Connected(MatchState state,Guid owner,Func<BoardCell,bool> accepts)
	{
		var cells = state.Cells().Where(c => c.Owner == owner && accepts(c)).ToDictionary(c => c.Order);
		var unseen = cells.Keys.ToHashSet(); var best = 0;
		while (unseen.Count > 0)
		{
			var queue = new Queue<BoardCell>(); queue.Enqueue(cells[unseen.First()]); var count = 0;
			while (queue.Count > 0) { var c = queue.Dequeue(); if (!unseen.Remove(c.Order)) continue; count++; foreach (var n in state.Neighbours(c.X,c.Y)) if (unseen.Contains(n.Order)) queue.Enqueue(n); }
			best = Math.Max(best,count);
		}
		return best;
	}
	public static ObjectiveProgress Evaluate(MatchState state,Guid owner,GoalDef goal)
	{
		var own = state.Cells().Where(c => c.Owner == owner).ToArray(); var current = 0; var target = goal.Target;
		switch (goal.Kind)
		{
			case GoalKind.ConnectedHomes: current = Connected(state,owner,BoardRules.IsHome); break;
			case GoalKind.ConnectedFarms: current = Connected(state,owner,BoardRules.IsFarm); break;
			case GoalKind.ParkOwner: current = ParkScoring.Regions(state).Select(r => r.Count(c => c.Owner == owner)).DefaultIfEmpty(0).Max(); break;
			case GoalKind.TagCount: current = own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(goal.Tag)); break;
			case GoalKind.Coins: current = state.Players.GetValueOrDefault(owner)?.Coins ?? 0; break;
			case GoalKind.TagCollector:
				current = new[] {Tag.Home,Tag.Trade,Tag.Nature,Tag.Industry,Tag.Culture,Tag.Civic}.Count(t => own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(t)) >= goal.Target);
				target = goal.Groups; break;
			case GoalKind.ShopLine:
				foreach (var c in own.Where(c => c.CardId == "shop")) foreach (var d in new[] {(1,0),(0,1)})
				{ var length = 0; var x = c.X; var y = c.Y; while (state.Get(x,y) is { } n && n.Owner == owner && n.CardId == "shop") { length++; x += d.Item1; y += d.Item2; } current = Math.Max(current,length); }
				break;
			case GoalKind.MiniMall:
				foreach (var road in state.Cells().Where(c => CardLibrary.Get(c.CardId).Type == CardType.Road))
				{
					var neighbours = state.Neighbours(road.X,road.Y).Where(c => c.Owner == owner).ToArray();
					var count = (neighbours.Any(c => c.CardId == "shop") ? 1 : 0)+(neighbours.Any(c => c.CardId == "cafe") ? 1 : 0)+(neighbours.Any(BoardRules.IsPark) ? 1 : 0);
					current = Math.Max(current,count);
				}
				break;
		}
		return new(current,target,$"{current} / {target}");
	}
}

/// <summary>Public goal state and a deterministic shuffled draw/discard pile. No private player data.</summary>
public sealed class Goals
{
	readonly MatchState state;
	readonly MatchRandom random;
	readonly List<PublicGoal> active = new();
	readonly List<GoalClaim> history = new();
	readonly List<string> deck = new();
	readonly List<string> discard = new();
	bool initialized;
	int checkedRound;
	public IReadOnlyList<PublicGoal> Active => active.AsReadOnly();
	public IReadOnlyList<GoalClaim> History => history.AsReadOnly();
	public Goals(MatchState state) { this.state = state; random = new MatchRandom(unchecked(state.Seed ^ 0x3B77F01)); }
	internal GoalsCheckpoint Capture() => new(initialized,checkedRound,random.State,active.ToArray(),history.ToArray(),deck.ToArray(),discard.ToArray());
	internal void Restore(GoalsCheckpoint data)
	{
		initialized = data.Initialized; checkedRound = data.CheckedRound; random.State = data.RandomState;
		active.Clear(); active.AddRange(data.Active); history.Clear(); history.AddRange(data.History);
		deck.Clear(); deck.AddRange(data.Deck); discard.Clear(); discard.AddRange(data.Discard);
	}
	internal void ApplyPublic(PublicGoal[] current,GoalClaim[] previous)
	{
		active.Clear(); active.AddRange(current); history.Clear(); history.AddRange(previous);
		// A replica has no draw pile and cannot authoritatively settle goals.
		initialized = false;
	}
	void Shuffle(List<string> cards) { for (var i = cards.Count-1; i > 0; i--) { var j = random.Next(i+1); (cards[i],cards[j]) = (cards[j],cards[i]); } }
	string Draw()
	{
		if (deck.Count == 0) { deck.AddRange(discard.Where(id => active.All(g => g == null || g.GoalId != id))); foreach (var id in deck) discard.Remove(id); Shuffle(deck); }
		var index = deck.FindLastIndex(id => active.All(g => g == null || g.GoalId != id));
		if (index < 0) throw new InvalidOperationException("Goal deck cannot supply a distinct active goal.");
		var card = deck[index]; deck.RemoveAt(index); return card;
	}
	public void Initialize(IReadOnlyList<string> initial = null)
	{
		if (initialized || !state.Settings.EnableGoals) return;
		if (initial != null && (initial.Count != state.Settings.PublicGoalCount || initial.Distinct().Count() != initial.Count)) throw new ArgumentException("Initial goals must be distinct and fill every slot.");
		if (initial != null) foreach (var id in initial) GoalLibrary.Get(id);
		initialized = true; deck.AddRange(GoalLibrary.All.Select(g => g.Id).Where(id => initial == null || !initial.Contains(id))); Shuffle(deck);
		for (var i = 0; i < state.Settings.PublicGoalCount; i++) active.Add(new(initial != null ? initial[i] : Draw()));
		state.Events.GoalsChanged();
	}
	internal void StartRound()
	{
		if (!initialized) return;
		var changed = false;
		for (var i = 0; i < active.Count; i++) if (active[i].Claim != null) { discard.Add(active[i].GoalId); active[i] = null; changed = true; }
		for (var i = 0; i < active.Count; i++) if (active[i] == null) active[i] = new(Draw());
		if (changed) state.Events.GoalsChanged();
	}
	public GoalsResult CheckAtBuildEnd()
	{
		if (!initialized || state.Phase != MatchPhase.Build || state.Round <= checkedRound) return new(state.Round,Array.Empty<GoalClaim>());
		checkedRound = state.Round; var claims = new List<GoalClaim>();
		foreach (var goal in active.Where(g => g.Claim == null))
		{
			var winners = state.Players.Keys.Where(id => GoalRules.Evaluate(state,id,GoalLibrary.Get(goal.GoalId)).Complete).ToArray();
			if (winners.Length == 0) continue;
			var vp = winners.Length > 1 ? state.Settings.TiedGoalVp : state.Settings.GoalVp;
			claims.Add(new(goal.GoalId,state.Round,winners.Select(id => new GoalAward(id,vp)).ToList().AsReadOnly()));
		}
		foreach (var claim in claims)
		{
			var slot = active.FindIndex(g => g.GoalId == claim.GoalId); active[slot] = active[slot] with { Claim = claim }; history.Add(claim);
			foreach (var award in claim.Awards) state.ChangeVp(award.Player,award.Vp);
		}
		var result = new GoalsResult(state.Round,claims.AsReadOnly());
		if (claims.Count > 0) { state.Events.GoalsChanged(); state.Events.GoalClaims(result); }
		return result;
	}
}
