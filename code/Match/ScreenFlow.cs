using System;
using System.Collections.Generic;

namespace CitySim.Match;

public enum GameScreen { Menu, Lobby, Match, End, Pause, Settings, ContractPick, Goals, Recovery }

/// <summary>Only the top screen receives input. Back pops an overlay without losing the match.</summary>
public sealed class ScreenFlow
{
	readonly List<GameScreen> stack = new() { GameScreen.Menu };
	public GameScreen BaseScreen => stack[0];
	public GameScreen Top => stack[stack.Count-1];
	public event Action Changed;
	public void Replace( GameScreen screen ) { stack.Clear(); stack.Add(screen); Changed?.Invoke(); }
	public void Push( GameScreen screen ) { if (Top == screen) return; stack.Add(screen); Changed?.Invoke(); }
	public void Pop() { if (stack.Count <= 1) return; stack.RemoveAt(stack.Count-1); Changed?.Invoke(); }
}
