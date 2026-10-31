using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public enum ContractKind { TagCount, Coins, RivalNeighbours, DistinctCivic, NearbyHomeShops }
public sealed record ContractDef(string Id,string Name,string Icon,string Description,ContractKind Kind,int Target,int Vp,Tag Tag = Tag.None,int NearbyHomes = 0);
public sealed record ContractView(Guid Owner,int DraftId,IReadOnlyList<string> Choices,string Selected,ObjectiveProgress Progress);
public sealed record ContractReveal(Guid Player,string ContractId,bool Complete,int Vp,string Detail);

public static class Contracts
{
	public static IReadOnlyList<ContractDef> All { get; } = Array.AsReadOnly(new[] {
		new ContractDef("suburbia","Suburbia","🏘️","Own 10 Home buildings.",ContractKind.TagCount,10,5,Tag.Home),
		new ContractDef("greenbelt","Greenbelt","🌳","Own 10 Nature buildings.",ContractKind.TagCount,10,5,Tag.Nature),
		new ContractDef("hoarder","Hoarder","🪙","Finish with 15 coins unspent.",ContractKind.Coins,15,5),
		new ContractDef("cultured","Cultured","🎭","Own 5 Culture buildings.",ContractKind.TagCount,5,6,Tag.Culture),
		new ContractDef("landlord","Landlord","🏠","Own 3 Home buildings beside rivals' Homes.",ContractKind.RivalNeighbours,3,6),
		new ContractDef("industrialist","Industrialist","⚙","Own 5 Industry-tagged buildings.",ContractKind.TagCount,5,7,Tag.Industry),
		new ContractDef("mayor","Mayor","🏛️","Own 4 different Civic-tagged building types.",ContractKind.DistinctCivic,4,7),
		new ContractDef("entrepreneur","Entrepreneur","🏪","Own 3 Shops, each with at least 2 Home buildings within 2 tiles.",ContractKind.NearbyHomeShops,3,6,NearbyHomes:2)
	});
	public static ContractDef Get(string id) => All.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException("Unknown contract: "+id);
	public static ObjectiveProgress Evaluate(MatchState state,Guid owner,ContractDef contract)
	{
		var own = state.Cells().Where(c => c.Owner == owner).ToArray();
		var current = contract.Kind switch
		{
			ContractKind.TagCount => own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(contract.Tag)),
			ContractKind.Coins => state.Players.GetValueOrDefault(owner)?.Coins ?? 0,
			ContractKind.RivalNeighbours => own.Count(c => BoardRules.IsHome(c) && state.Neighbours(c.X,c.Y).Any(n => n.Owner != owner && state.Players.ContainsKey(n.Owner) && BoardRules.IsHome(n))),
			ContractKind.DistinctCivic => own.Where(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Civic)).Select(c => c.CardId).Distinct().Count(),
			ContractKind.NearbyHomeShops => CountShops(state,own,contract.NearbyHomes), _ => 0
		};
		return new(current,contract.Target,$"{current} / {contract.Target} · {contract.Description}");
	}
	static int CountShops(MatchState state,IReadOnlyList<BoardCell> own,int minimum)
	{
		return own.Count(c => c.CardId == "shop" && state.Cells().Count(n => BoardRules.IsHome(n) && BoardRules.Distance(c,n) <= 2) >= minimum);
	}
}

/// <summary>Host-only choices/selection. Only owner views exist until final reveal.</summary>
internal sealed class ContractSession
{
	readonly MatchState state;
	readonly MatchRandom random;
	readonly Dictionary<Guid,IReadOnlyList<string>> choices = new();
	readonly Dictionary<Guid,string> selected = new();
	public bool AllChosen => !state.Settings.EnableContracts || state.Players.Keys.All(selected.ContainsKey);
	public ContractSession(MatchState state) { this.state = state; random = new MatchRandom(unchecked(state.Seed ^ 0x517CC1)); Deal(); }
	internal ContractsCheckpoint Capture() => new(random.State,choices.Select(p => new ContractCheckpoint(p.Key,p.Value.ToArray(),selected.GetValueOrDefault(p.Key))).ToArray());
	internal void Restore(ContractsCheckpoint data)
	{
		random.State = data.RandomState; choices.Clear(); selected.Clear();
		foreach (var p in data.Players) { choices[p.Player] = Array.AsReadOnly(p.Choices.ToArray()); if (p.Selected != null) selected[p.Player] = p.Selected; }
	}
	public void Deal()
	{
		if (!state.Settings.EnableContracts) return;
		foreach (var player in state.Players.Keys.OrderBy(id => id))
		{
			if (choices.ContainsKey(player)) continue;
			var deck = Contracts.All.Select(c => c.Id).ToList();
			for (var i = deck.Count-1; i > 0; i--) { var j = random.Next(i+1); (deck[i],deck[j]) = (deck[j],deck[i]); }
			choices[player] = deck.Take(state.Settings.ContractChoices).ToList().AsReadOnly();
		}
	}
	public ContractView View(Guid owner)
	{
		if (!choices.TryGetValue(owner,out var offered)) return null;
		var chosen = selected.GetValueOrDefault(owner);
		return new(owner,state.Seed,chosen != null ? Array.Empty<string>() : offered,chosen,chosen != null ? Contracts.Evaluate(state,owner,Contracts.Get(chosen)) : null);
	}
	public bool Choose(Guid owner,int draftId,string id)
	{
		if (state.Phase != MatchPhase.Lobby || draftId != state.Seed || selected.ContainsKey(owner) || !choices.TryGetValue(owner,out var offered) || !offered.Contains(id)) return false;
		selected[owner] = id; state.Events.Contract(owner); return true;
	}
	public IReadOnlyList<ContractReveal> Reveal()
	{
		if (state.Phase != MatchPhase.Payout || state.Round != state.Settings.Rounds) return Array.Empty<ContractReveal>();
		return selected.Select(pair => { var def = Contracts.Get(pair.Value); var progress = Contracts.Evaluate(state,pair.Key,def); return new ContractReveal(pair.Key,pair.Value,progress.Complete,progress.Complete ? def.Vp : 0,progress.Detail); }).ToList().AsReadOnly();
	}
}
