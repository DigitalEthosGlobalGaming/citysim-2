namespace CitySim.Punchboard;

/// <summary>Card rarity. Always draw the marker shape as well as the colour.</summary>
public enum PbRarity { Common, Uncommon, Rare, Legendary }

/// <summary>Building tags. Each has an icon so it never relies on colour alone.</summary>
public enum PbTag { Home, Trade, Nature, Industry, Culture, Civic }

/// <summary>Printed ink colours for small accents.</summary>
public enum PbTone { Accent, Red, Teal, Good, Bad, Ink }

public enum PbButtonKind { Primary, Secondary, Tertiary, Ghost, Danger }

public enum PbSize { Small, Normal, Big }

public enum PbPanelKind { Standard, Raised, Tooltip }

/// <summary>Shared helpers for the Punchboard components. Contains no game logic.</summary>
public static class Pb
{
	/// <summary>Lower-case class name for an enum value, e.g. PbRarity.Legendary becomes "legendary".</summary>
	public static string Css<T>( this T value ) where T : struct, Enum => value.ToString().ToLowerInvariant();

	/// <summary>The marker shape for a rarity: circle, diamond, triangle, star.</summary>
	public static string Glyph( PbRarity rarity ) => rarity switch
	{
		PbRarity.Common => "●",
		PbRarity.Uncommon => "◆",
		PbRarity.Rare => "▲",
		_ => "★",
	};

	public static string Label( PbRarity rarity ) => rarity.ToString();

	/// <summary>The default icon glyph for a tag. Replace with textures if you have them.</summary>
	public static string Icon( PbTag tag ) => tag switch
	{
		PbTag.Home => "🏠",
		PbTag.Trade => "🪙",
		PbTag.Nature => "🌿",
		PbTag.Industry => "⚙",
		PbTag.Culture => "🎭",
		_ => "🏛",
	};

	/// <summary>Back-out easing (overshoots, then settles). t is clamped to 0..1.</summary>
	public static float BackOut( float t )
	{
		t = Math.Clamp( t, 0f, 1f ) - 1f;
		return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
	}

	/// <summary>Ease-out cubic.</summary>
	public static float EaseOut( float t ) => 1f - MathF.Pow( 1f - Math.Clamp( t, 0f, 1f ), 3f );

	/// <summary>Frame-rate independent smoothing factor for exponential easing.</summary>
	public static float Smooth( float rate, float dt ) => 1f - MathF.Exp( -rate * dt );

	/// <summary>Invariant-culture number formatting for style strings.</summary>
	public static string F( float value ) => FormattableString.Invariant( $"{value:0.###}" );

	/// <summary>The card stock colour, for mixing art tints.</summary>
	public static readonly Color CardStock = new( 1f, 0.98f, 0.94f );

	/// <summary>A colour as a CSS rgba() string, for inline styles.</summary>
	public static string Rgba( Color c ) => FormattableString.Invariant( $"rgba({(int)(c.r * 255)},{(int)(c.g * 255)},{(int)(c.b * 255)},{c.a:0.##})" );

	/// <summary>True if the string looks like a texture path rather than a glyph.</summary>
	public static bool IsImagePath( string icon ) => !string.IsNullOrEmpty( icon ) && (icon.Contains( '/' ) || icon.Contains( '.' ));
}
