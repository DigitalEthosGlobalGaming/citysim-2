using System;

namespace CitySim.Match;

/// <summary>Explicit, portable draw state. A host checkpoint resumes the next draw, not a new shuffle.</summary>
internal sealed class MatchRandom : Random
{
	uint state;
	public uint State { get => state; set => state = value != 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); }
	public MatchRandom(int seed) { state = unchecked((uint)seed) ^ 0x9E3779B9u; if (state == 0) state = 1; }
	uint Draw() { var x = state; x ^= x << 13; x ^= x >> 17; x ^= x << 5; return state = x; }
	protected override double Sample() => Draw()/4294967296d;
	public override double NextDouble() => Sample();
	public override int Next() => Next(int.MaxValue);
	public override int Next(int maxValue)
	{
		if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
		if (maxValue == 0) return 0;
		var limit = 4294967296UL-4294967296UL%(uint)maxValue;
		uint value; do { value = Draw(); } while (value >= limit);
		return (int)(value%(uint)maxValue);
	}
	public override int Next(int minValue,int maxValue)
	{
		if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
		var range = (long)maxValue-minValue;
		return range <= int.MaxValue ? minValue+Next((int)range) : (int)(minValue+(long)(Sample()*range));
	}
	public override void NextBytes(byte[] buffer) { foreach (ref var b in buffer.AsSpan()) b = (byte)Draw(); }
}
