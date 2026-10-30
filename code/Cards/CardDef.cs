using System;
using System.Collections.Generic;

namespace CitySim.Cards;

[Flags]
public enum Tag { None = 0, Home = 1, Trade = 2, Nature = 4, Industry = 8, Culture = 16, Civic = 32 }
public enum Rarity { Common, Uncommon, Rare, Legendary }
public enum CardType { Road, Residential, Commerce, Industry, Green, Growth, Landmark, Civic, Event }
public enum EffectHook { Place, Payout, Growth, EndGame, OwnerPlacedCard, Reserve, RoundStart }

/// <summary>Rule identifiers and their parameters; interpreted by the rule classes, never scene objects.</summary>
public sealed record CardEffect( EffectHook Hook, string Rule, int Amount = 0, int Range = 0, int Cap = 0, int Divisor = 1 );

public sealed record CardDef(
	string Id, string Name, CardType Type, Tag Tags, Rarity Rarity, bool NeedsRoad,
	string Icon, string Rules, int Weight, IReadOnlyList<CardEffect> Effects,
	bool EffectTray = false, int OwnershipLimit = 0, int ParkSize = 0 );
