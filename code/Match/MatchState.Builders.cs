using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

/// <summary>The owner's persistent choice for the next Builders' Yard default.</summary>
public sealed record BuilderChoice(Guid Player, bool Common);

public sealed partial class MatchState
{
    readonly Dictionary<Guid,bool> builderChoices = new();

    /// <summary>True selects a random Common; false selects a Street, the default.</summary>
    public bool BuilderCommon(Guid player) => builderChoices.GetValueOrDefault(player);

    /// <summary>Choose the next round's extra card while this player owns a Builders' Yard.</summary>
    public bool ChooseBuilderDefault(Guid player, bool common)
    {
        if (!players.ContainsKey(player) || Phase == MatchPhase.EndGame || !Cells().Any(c => c.Owner == player && c.CardId == "builders-yard")) return false;
        if (BuilderCommon(player) == common) return true;
        if (common) builderChoices[player] = true; else builderChoices.Remove(player);
        Events.Tray(player);
        return true;
    }

    internal BuilderChoice[] BuilderChoiceSnapshot() => builderChoices.OrderBy(p => p.Key).Select(p => new BuilderChoice(p.Key,p.Value)).ToArray();

    internal void LoadBuilderChoices(IEnumerable<BuilderChoice> choices)
    {
        var entries = (choices ?? Array.Empty<BuilderChoice>()).ToArray();
        if (entries.Any(choice => choice == null || !players.ContainsKey(choice.Player)) ||
            entries.Select(choice => choice.Player).Distinct().Count() != entries.Length)
            throw new ArgumentException("Invalid Builder preference checkpoint.");
        builderChoices.Clear();
        foreach (var choice in entries) if (choice.Common) builderChoices[choice.Player] = true;
    }

    /// <summary>Deal each owned builder's card in placement order, after shared defaults.</summary>
    internal void BuilderDefaults(Random random)
    {
        foreach (var source in Cells().OrderBy(c => c.Order))
        foreach (var effect in CardLibrary.Get(source.CardId).Effects.Where(e => e.Hook == EffectHook.RoundStart && e.Rule is "builder-street" or "builder-choice"))
        for (var i = 0; i < effect.Amount; i++)
        {
            if (!players.TryGetValue(source.Owner,out var owner) || owner.Hand.Count >= Settings.HandLimit) break;
            var card = effect.Rule == "builder-choice" && BuilderCommon(source.Owner) ? CardDraw.Common(random) : "street";
            GiveCard(source.Owner,card);
        }
    }
}
