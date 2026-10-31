using System;
using CitySim.Cards;

namespace CitySim.Match;

public enum InteractionKind { None, Residential, House, Shop, Market, Cafe, Farm, Park, Lake, Building, Road, Home, Trade, Nature, Industry, Culture, Civic, Tagged, Card }
public sealed record InteractionTarget(string Label, InteractionKind Kind, bool OwnOnly = false, string CardId = null);

/// <summary>Matches the named category, not a guarantee of range, ownership, or an active effect.</summary>
public static class InteractionMatcher
{
    public static bool Matches(InteractionTarget target, BoardCell cell, Guid viewer)
    {
        if (target == null || cell == null || target.OwnOnly && cell.Owner != viewer) return false;
        var card = CardLibrary.Get(cell.CardId);
        return target.Kind switch {
            InteractionKind.Card => cell.CardId == target.CardId,
            InteractionKind.Residential => Match.BoardRules.IsHome(cell),
            InteractionKind.House => cell.CardId == "house",
            InteractionKind.Shop => cell.CardId == "shop",
            InteractionKind.Market => cell.CardId == "market",
            InteractionKind.Cafe => cell.CardId == "cafe",
            InteractionKind.Farm => Match.BoardRules.IsFarm(cell),
            InteractionKind.Park => Match.BoardRules.IsPark(cell),
            InteractionKind.Lake => cell.CardId == "lake",
            InteractionKind.Building => card.Type is not (CardType.Road or CardType.Event),
            InteractionKind.Road => card.Type == CardType.Road,
            InteractionKind.Home => card.Tags.HasFlag(Tag.Home),
            InteractionKind.Trade => card.Tags.HasFlag(Tag.Trade),
            InteractionKind.Nature => card.Tags.HasFlag(Tag.Nature),
            InteractionKind.Industry => card.Tags.HasFlag(Tag.Industry),
            InteractionKind.Culture => card.Tags.HasFlag(Tag.Culture),
            InteractionKind.Civic => card.Tags.HasFlag(Tag.Civic),
            InteractionKind.Tagged => card.Tags != Tag.None,
            _ => false
        };
    }
}
