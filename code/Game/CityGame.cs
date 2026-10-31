using CitySim.Cards;
using CitySim.Match;
using CitySim.Feel;
using CitySim.Net;

namespace CitySim;

/// <summary>Wires the pure match to the scene. Multiplayer transport is introduced in M5.</summary>
public sealed class CityGame : Component
{
	static CityGame instance;
	public static CityGame Instance
	{
		get
		{
			if (!instance.IsValid()) instance = Game.ActiveScene?.GetAllComponents<CityGame>().FirstOrDefault();
			return instance;
		}
	}

	public const int MaxVehicles = 100;
	public static readonly Guid LocalPlayerId = new("11111111-0000-0000-0000-000000000001");
	public static readonly Guid StubPlayerId = new("22222222-0000-0000-0000-000000000002");
	static readonly Guid[] BotPlayerIds = Enumerable.Range(0,Ai.SoloSetup.MaximumOpponents).Select(Ai.SoloSetup.PlayerId).ToArray();
	public static Guid Me => Networking.IsActive ? Instance?.Session?.PlayerId ?? Connection.Local.Id : LocalPlayerId;
	public CityNetwork Session { get; private set; }
	public bool Multiplayer => Session?.IsConnected==true;
	public GameMode Mode { get; private set; } = GameMode.Match;
	public Ai.SoloSetupData GameSetup { get; private set; } = Ai.SoloSetup.Create(new Random().Next());
	public IReadOnlyList<Ai.OpponentSetup> ActiveOpponents { get; private set; } = Array.Empty<Ai.OpponentSetup>();
	public int SoloRivals => GameSetup.OpponentCount;
	public Ai.BotDifficulty SoloDifficulty => GameSetup.Opponents[0].Difficulty;
	public void OpenGameSetup() { if (!Multiplayer && Screens.Top==GameScreen.Menu) Screens.Push(GameScreen.GameSetup); }
	public void SetOpponentCount(int count) { if (Screens.Top!=GameScreen.GameSetup) return; GameSetup=Ai.SoloSetup.Resize(GameSetup,count); SaveGameSetup(); }
	public void EditOpponent(int seat,string name,Ai.BotDifficulty difficulty) { if (Screens.Top!=GameScreen.GameSetup) return; GameSetup=Ai.SoloSetup.Edit(GameSetup,seat,name,difficulty); SaveGameSetup(); }
	public void RandomizeOpponentNames() { if (Screens.Top!=GameScreen.GameSetup) return; GameSetup=Ai.SoloSetup.Randomize(GameSetup,new Random().Next()); SaveGameSetup(); }
	void SaveGameSetup() { SoloSetupPreferences.Save(GameSetup); NotifyUi(); }
	public void ConfigureSolo(int rivals,Ai.BotDifficulty difficulty)
	{
		if (Multiplayer || Screens.Top is not (GameScreen.Menu or GameScreen.GameSetup)) return;
		GameSetup=Ai.SoloSetup.Resize(GameSetup,rivals);
		foreach(var opponent in GameSetup.Opponents) GameSetup=Ai.SoloSetup.Edit(GameSetup,opponent.Seat,opponent.Name,difficulty);
		SaveGameSetup();
	}
	public GameState State => Screens.BaseScreen switch { GameScreen.Menu => GameState.Menu, GameScreen.End => GameState.End, _ => GameState.Playing };
	public bool EndInspect { get; private set; }
	public void SetEndInspect(bool inspect) { EndInspect=inspect && State==GameState.End; NotifyUi(); }
	public ScreenFlow Screens { get; } = new();
	public MatchState Match { get; private set; }
	public RoundMachine Rounds { get; private set; }
	public CityGrid Grid { get; private set; }
	public CityPlayer Player { get; private set; }
	public Feedback Feedback { get; private set; }
	public TrafficController Traffic { get; private set; }
	public LightingPreview Lighting { get; private set; }
	public MatchPlayer LocalPlayer => Match?.Players.GetValueOrDefault(Me);
	public int SelectedSlot { get; private set; } = -1;
	public int RenovationEventSlot { get; private set; } = -1;
	public int RenovationReplacementSlot { get; private set; } = -1;
	long renovationHandVersion;
	public bool RenovationArmed => RenovationEventSlot >= 0;
	public CardDef RenovationReplacement => LocalPlayer != null && RenovationReplacementSlot >= 0 && RenovationReplacementSlot < LocalPlayer.Hand.Count ? CardLibrary.Get(LocalPlayer.Hand[RenovationReplacementSlot]) : null;
	public string SelectedCardId => LocalPlayer != null && SelectedSlot >= 0 && SelectedSlot < LocalPlayer.Hand.Count ? LocalPlayer.Hand[SelectedSlot] : null;
	public CardDef SelectedCard => SelectedCardId is { } id ? CardLibrary.Get(id) : null;
	public bool SelectedImmediateEvent => SelectedCardId is "grant" or "ribbon-cutting" or "rush-hour";
	public Piece Held => RenovationArmed ? RenovationReplacement is { } replacement ? new Piece { Type = BoardView.PlaceholderType(replacement) } : default : SelectedCardId == "land-claim" ? new Piece { Type = TileType.Road } : SelectedCard is { } card && card.Type != CardType.Event ? new Piece { Type = BoardView.PlaceholderType(card) } : default;
	public Tag HighlightedTag { get; private set; }
	object interactionHighlightSource;
	InteractionTarget[] lastInteractionTargets;
	Tag lastReferenceTag;
	string lastInteractionLabel;
	bool interactionPinned;
	public bool HasInteractionHighlight => interactionHighlightSource != null;
	public IReadOnlyList<BoardCell> HighlightedCells => board?.HighlightedCells ?? Array.Empty<BoardCell>();
	public string HighlightLabel { get; private set; }
	public string HighlightIcon => interactionHighlightSource != null && lastInteractionTargets?.Any(t => t.Kind == InteractionKind.Road) == true ? "🛣️" : ReferenceTag != Tag.None ? TagChip.Icon(ReferenceTag) : HighlightedTag != Tag.None ? TagChip.Icon(HighlightedTag) : HighlightLabel?.Contains(' ') == true && !HighlightLabel.StartsWith("INTERACTS") ? HighlightLabel.Split(' ')[0] : "✦";
	public string HighlightStatus => !HasInteractionHighlight ? "" : HighlightedCells.Count == 0 ? $"{HighlightLabel} · None in city" : $"{HighlightLabel} · {HighlightedCells.Count} in city";
	public Tag ReferenceTag { get; private set; }
	public Guid HighlightOwner { get; private set; } = LocalPlayerId;
	public bool BuildReady => Multiplayer ? Session.Snapshot?.Public.Players.FirstOrDefault(p=>p.Id==Me)?.BuildReady==true : Rounds?.IsBuildReady(Me)==true;
	public bool BoardInput => State == GameState.Playing && Screens.Top == GameScreen.Match && Match?.Phase == MatchPhase.Build && LocalPlayer!=null && !BuildReady;
	public AuctionView OwnAuction => Multiplayer && !Session.IsHost ? Session.Snapshot?.Own?.Auction ?? Session.SpectatorAuction : Rounds?.GetAuctionView(Me);
	public ContractView OwnContract
	{
		get
		{
			if (!Multiplayer) return Rounds?.GetContractView(Me);
			if (!Session.IsHost) return Session.Snapshot?.Own?.Contract;
			var view=Rounds?.GetContractView(Me);
			return view==null ? null : view with {DraftId=Session.Snapshot?.MatchId.GetHashCode() ?? 0};
		}
	}
	public AuctionResult LastAuction => Multiplayer && !Session.IsHost ? Session.Snapshot?.Public.LastAuction : Rounds?.LastAuction;
	public float LastBidInputAt { get; private set; } = -1;
	public double Clock { get; private set; }
	public event Action UiChanged;
	public event Action<int,MatchPhase,double> AiFrameMeasured;
	public event Action MatchChanged;
	BoardView board;
	readonly List<Ai.Bot> bots = new();
	BalanceAnalytics analytics;
	int nextBot;

	protected override void OnAwake() { instance = this; }
	protected override void OnStart()
	{
		Mouse.Visibility = MouseVisibility.Visible;
		var city = new GameObject(true,"City") { Parent = GameObject,Flags=GameObjectFlags.NotNetworked };
		Grid = city.Components.Create<CityGrid>();
		board = city.Components.Create<BoardView>();
		var ground = new GameObject(true,"Ground") { Parent = GameObject,Flags=GameObjectFlags.NotNetworked };
		ground.WorldPosition = new Vector3(0,0,-12f); ground.WorldScale = 60f;
		var renderer = ground.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load("models/dev/plane.vmdl");
		renderer.MaterialOverride = Material.Load("materials/board/board_flat.vmat");
		renderer.Tint = new Color(0.165f,0.45f,0.467f);
		Player = Scene.GetAllComponents<CityPlayer>().FirstOrDefault();
		if (!Player.IsValid())
		{
			var camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault(c => c.IsMainCamera);
			var cameraObject = camera?.GameObject ?? new GameObject(true,"Camera");
			camera ??= cameraObject.Components.Create<CameraComponent>();
			camera.IsMainCamera = true; camera.ZNear = 2; camera.ZFar = 20000; camera.FieldOfView = 60;
			camera.BackgroundColor = new Color(0.55f,0.72f,0.9f);
			Player = cameraObject.Components.GetOrCreate<CityPlayer>();
		}
		Player.GameObject.Flags |= GameObjectFlags.NotNetworked;
		Feedback = Components.GetOrCreate<Feedback>();
		Feedback.Bind(this);
		Traffic = Components.GetOrCreate<TrafficController>();
		Traffic.Bind(this);
		PlayerPreferences.Load(); ApplyPreferences(); GameSetup=SoloSetupPreferences.Load();
		Lighting = new LightingPreview(Scene);
		if (!Scene.GetAllComponents<Hud>().Any())
		{
			var ui = new GameObject(true,"HUD") { Parent = GameObject,Flags=GameObjectFlags.NotNetworked };
			ui.Components.Create<ScreenPanel>();
			ui.Components.Create<Hud>();
		}
		Screens.Changed += NotifyUi;
		Grid.Build(12,12);
		Player.FocusOn(Grid);
		Session=Components.GetOrCreate<CityNetwork>(); Session.Bind(this);
		NotifyUi();
	}
	public void NetworkChanged() => NotifyUi();
	public void NetworkDenied(string reason) => Feedback?.Notice(reason);
	public void SetNetworkClock(double time) => Clock=time;
	public void ShowNetworkLobby()
	{
		EndInspect=false;
		if (Screens.BaseScreen!=GameScreen.Lobby)
		{
			DetachMatch(); Match=null; SelectedSlot=-1; ClearVehicles(); Grid.Build(12,12); Player.FocusOn(Grid);
			Screens.Replace(GameScreen.Lobby); MatchChanged?.Invoke();
		}
		NotifyUi();
	}
	public void AcceptNetworkState(MatchState state,RoundMachine rounds,RecipientSnapshot snapshot)
	{
		if (state==null) return;
		if (state.Phase!=MatchPhase.EndGame) EndInspect=false;
		Clock=snapshot.Clock; Mode=GameMode.Match;
		if (Match!=state)
		{
			DetachMatch(); Match=state; Rounds=rounds; SelectedSlot=-1;
			board.Bind(Match,Grid); Match.Events.HandChanged+=OnHand; Match.Events.PhaseChanged+=OnPhase; Match.Events.PlayerReadyChanged+=OnReady; Match.Events.ContractChanged+=OnContract;
			Feedback.Attach(Match); MatchChanged?.Invoke(); Player.FocusOn(Grid);
		}
		Rounds=rounds;
		if (RenovationArmed && (state.Phase != MatchPhase.Build || LocalPlayer == null || LocalPlayer.HandVersion != renovationHandVersion)) ClearRenovation();
		if (state.Phase==MatchPhase.EndGame) { if (Screens.BaseScreen!=GameScreen.End) Screens.Replace(GameScreen.End); }
		else if (state.Phase==MatchPhase.Lobby && snapshot.Own!=null && state.Settings.EnableContracts)
		{
			if (Screens.Top!=GameScreen.ContractPick) { Screens.Replace(GameScreen.Match); Screens.Push(GameScreen.ContractPick); }
		}
		else if (Screens.BaseScreen!=GameScreen.Match || Screens.Top==GameScreen.ContractPick) Screens.Replace(GameScreen.Match);
		if (LocalPlayer!=null && (SelectedSlot<0 || SelectedSlot>=LocalPlayer.Hand.Count)) SelectedSlot=LocalPlayer.Hand.Count>0 ? 0 : -1;
		NotifyUi();
	}
	public void NetworkLeft() { RequestMenu(); }

	public void RequestStart( GameMode mode, MatchSettings settings = null )
	{
		if (Multiplayer) return;
		if (!Grid.IsValid()) return;
		Input.Clear("ConfirmCard"); Input.Clear("Attack1");
		DetachMatch();
		ClearVehicles();
		EndInspect=false;
		Mode = mode; Clock = 0; SelectedSlot = -1;
		settings ??= new MatchSettings();
		ActiveOpponents=mode==GameMode.Match ? Ai.SoloSetup.StartRoster(GameSetup,new Random().Next()) : Array.Empty<Ai.OpponentSetup>();
		var rivalCount=ActiveOpponents.Count;
		var side = Math.Clamp((int)Math.Ceiling(Math.Sqrt(settings.TilesPerPlayer*(1+rivalCount))),settings.MinimumBoardSide,settings.MaximumBoardSide);
		Match = new MatchState(side,side,Game.Random.Int(1,1_000_000),settings);
		Match.AddPlayer(Me,"You");
		for(var i=0;i<rivalCount;i++) Match.AddPlayer(BotPlayerIds[i],ActiveOpponents[i].Name);
		var (row, col) = StartingRoads.Choose(Match.Width, Match.Height, Match.Seed);
		for (var x = 0; x < Match.Width; x++) Match.SeedStreet(x,row);
		for (var y = 0; y < Match.Height; y++) Match.SeedStreet(col,y);
		board.Bind(Match,Grid);
		Match.Events.HandChanged += OnHand;
		Match.Events.PhaseChanged += OnPhase;
		Match.Events.PlayerReadyChanged += OnReady;
		Match.Events.ContractChanged += OnContract;
		Feedback.Attach(Match);
		Screens.Replace(GameScreen.Match);
		MatchChanged?.Invoke();
		if (mode == GameMode.Sandbox)
		{
			analytics=LocalBalanceAnalytics.Start(Match,null,Guid.NewGuid(),"sandbox");
			Match.SetPhase(MatchPhase.Build,1,double.MaxValue);
			foreach (var id in new[] { "street", "house", "shop", "farm", "park" }) Match.GiveCard(Me,id);
		}
		else
		{
			Rounds = new RoundMachine(Match);
			var configurations=ActiveOpponents.ToDictionary(p=>BotPlayerIds[p.Seat],p=>new BotAnalyticsConfiguration(p.Difficulty.ToString(),Ai.BotStyles.ForSeat(Match.Seed,p.Seat).ToString()));
			analytics=LocalBalanceAnalytics.Start(Match,Rounds,Guid.NewGuid(),"solo",BotPlayerIds.Take(rivalCount),string.Join(",",ActiveOpponents.Select(p=>p.Difficulty.ToString())),configurations);
			for(var i=0;i<rivalCount;i++)
			{
				var bot=new Ai.Bot(BotPlayerIds[i],Match,Rounds,ActiveOpponents[i].Difficulty,unchecked(Match.Seed+i*7919),
					finishBuildingQuickly: () => LocalPlayer is { } human && (human.Hand.Count == 0 || BuildReady),style: Ai.BotStyles.ForSeat(Match.Seed,i));
				bots.Add(bot); bot.ChooseContract();
			}
			if (settings.EnableContracts) Screens.Push(GameScreen.ContractPick);
			else Rounds.Start(Clock);
		}
		SelectSlot(0);
		Player.FocusOn(Grid);
		NotifyUi();
	}

	public void RequestMenu()
	{
		EndInspect=false;
		if (Multiplayer) { Session.Leave(); return; }
		DetachMatch(); Match = null; Rounds = null; SelectedSlot = -1;
		ClearVehicles(); Screens.Replace(GameScreen.Menu);
		Grid.Build(12,12); Player.FocusOn(Grid);
		MatchChanged?.Invoke(); NotifyUi();
	}
	public void TogglePause()
	{
		if (Screens.Top is GameScreen.Settings or GameScreen.GameSetup) { Screens.Pop(); return; }
		if (Screens.Top==GameScreen.Recovery) { RequestMenu(); return; }
		if (Screens.Top==GameScreen.Lobby) { RequestMenu(); return; }
		if (Screens.Top == GameScreen.ContractPick) { RequestMenu(); return; }
		if (Screens.Top == GameScreen.End) { RequestMenu(); return; }
		if (Screens.Top is GameScreen.Pause or GameScreen.Goals) Screens.Pop();
		else if (Screens.Top == GameScreen.Match) Screens.Push(GameScreen.Pause);
	}
	public void OpenSettings() { if (Screens.Top is GameScreen.Menu or GameScreen.Pause) Screens.Push(GameScreen.Settings); }
	public void SetPreferences(PlayerPreferences.Data data) { PlayerPreferences.Set(data); ApplyPreferences(); NotifyUi(); }
	void ApplyPreferences() { if (Feedback.IsValid()) { Feedback.ReduceShake=PlayerPreferences.Current.ReduceShake; Feedback.ReduceFlashing=PlayerPreferences.Current.ReduceFlashing; } }
	public void DismissHints() { SetPreferences(PlayerPreferences.Current with {HintsDismissed=true}); }
	public void ToggleGoals() { if (Screens.Top == GameScreen.Goals) Screens.Pop(); else if (Screens.Top == GameScreen.Match) Screens.Push(GameScreen.Goals); }
	public bool KeepContract(int draftId,string id)
	{
		if (Multiplayer) return Screens.Top==GameScreen.ContractPick && Session.Send(MatchCommandKind.KeepContract,card:id,auctionId:draftId);
		if (Screens.Top != GameScreen.ContractPick || Rounds?.ChooseContract(Me,draftId,id) != true) return false;
		if (Rounds.ContractsChosen) { Screens.Pop(); Rounds.Start(Clock); SelectSlot(0); }
		NotifyUi(); return true;
	}
	void ClearRenovation() { RenovationEventSlot = -1; RenovationReplacementSlot = -1; }
	public void SelectSlot( int slot )
	{
		if (LocalPlayer == null) return;
		var previousSlot = SelectedSlot;
		if (RenovationArmed)
		{
			if (slot == RenovationEventSlot) { ClearRenovation(); SelectedSlot = slot; }
			else if (slot >= 0 && slot < LocalPlayer.Hand.Count && CardLibrary.Get(LocalPlayer.Hand[slot]).Type is not (CardType.Event or CardType.Road)) { RenovationReplacementSlot = slot; SelectedSlot = slot; }
			if (SelectedSlot != previousSlot && SelectedSlot >= 0) TabletopAudio.Card();
			NotifyUi(); return;
		}
		SelectedSlot = slot >= 0 && slot < LocalPlayer.Hand.Count ? slot : -1;
		if (SelectedSlot != previousSlot && SelectedSlot >= 0) TabletopAudio.Card();
		if (BoardInput && SelectedCardId == "renovation") { RenovationEventSlot = SelectedSlot; renovationHandVersion = LocalPlayer.HandVersion; }
		NotifyUi();
	}
	public void Deselect() { ClearRenovation(); SelectedSlot = -1; NotifyUi(); }

	public void DiscardSelected() => DiscardSlot(SelectedSlot);
	public event Action<int,string,long> DiscardRequested;
	public void DiscardSlot(int slot)
	{
		if (!BoardInput || RenovationArmed || Mode == GameMode.Sandbox || slot != SelectedSlot || slot < 0 || slot >= LocalPlayer.Hand.Count) return;
		var cardId = LocalPlayer.Hand[slot];
		DiscardRequested?.Invoke(slot,cardId,LocalPlayer.HandVersion);
		if (Multiplayer) Session.Send(MatchCommandKind.Discard,slot,cardId); else Match.Discard(Me,slot);
	}
	public void ChooseBuilderDefault(bool common)
	{
		if (Match == null || LocalPlayer == null) return;
		if (Multiplayer) Session.Send(MatchCommandKind.BuilderDefault,amount:common ? 1 : 0);
		else if (Match.ChooseBuilderDefault(Me,common)) NotifyUi();
	}
	public void PlaySelectedEvent()
	{
		if (BoardInput) { if (Multiplayer) Session.Send(MatchCommandKind.PlayEvent,SelectedSlot,SelectedCardId); else Match.PlayEvent(Me,SelectedSlot); }
	}
	public void RequestPlace( int x, int y )
	{
		if (!BoardInput || SelectedCardId is not { } id) return;
		if (RenovationArmed)
		{
			if (RenovationReplacementSlot < 0) return;
			if (Multiplayer) Session.Send(MatchCommandKind.Renovate,RenovationEventSlot,"renovation",x,y,replacementSlot:RenovationReplacementSlot);
			else Match.TryRenovate(Me,RenovationEventSlot,RenovationReplacementSlot,x,y);
			return;
		}
		if (Multiplayer) { Session.Send(id=="land-claim" ? MatchCommandKind.Claim : MatchCommandKind.Place,SelectedSlot,id,x,y); return; }
		var result = id == "land-claim" ? Match.TryClaim(Me,SelectedSlot,x,y) : Match.TryPlace(Me,id,x,y);
		if (result.Success && Mode == GameMode.Sandbox) { Match.GiveCard(Me,id); SelectSlot(LocalPlayer.Hand.Count-1); }
	}
	public void PreviewPlacementInteractions(IReadOnlyList<PlacementInteraction> interactions) => board?.PreviewInteractions(interactions);
	object playerHighlightSource;
	public void HighlightPlayer(object source,Guid owner) { playerHighlightSource = source; board?.HighlightPlayer(owner); }
	public void ClearPlayerHighlight(object source) { if (!ReferenceEquals(playerHighlightSource,source)) return; playerHighlightSource = null; board?.HighlightPlayer(null); }
	public void HighlightTag(Tag tag,Guid owner)
	{
		interactionPinned = false; interactionHighlightSource = null; ReferenceTag = Tag.None; HighlightLabel = null; HighlightedTag = tag; HighlightOwner = owner; board?.Highlight(tag,owner);
	}
	public void HighlightInteractions(object source,IReadOnlyList<InteractionTarget> targets,string label = null)
	{
		if (source == null || targets == null || !targets.Any(t => t.Kind != InteractionKind.None)) return;
		interactionPinned = false; interactionHighlightSource = source; ReferenceTag = Tag.None; HighlightedTag = Tag.None;
		lastInteractionTargets = targets.ToArray(); lastReferenceTag = Tag.None; HighlightLabel = lastInteractionLabel = label ?? "Matches";
		board?.HighlightInteractions(lastInteractionTargets,Me);
	}
	public void HighlightReferenceTag(object source,Tag tag)
	{
		if (source == null || tag == Tag.None) return;
		var kind = tag switch { Tag.Home => InteractionKind.Home, Tag.Trade => InteractionKind.Trade, Tag.Nature => InteractionKind.Nature, Tag.Industry => InteractionKind.Industry, Tag.Culture => InteractionKind.Culture, Tag.Civic => InteractionKind.Civic, _ => InteractionKind.None };
		if (kind == InteractionKind.None) return;
		interactionPinned = false; interactionHighlightSource = source; HighlightedTag = Tag.None; ReferenceTag = tag;
		lastInteractionTargets = new[] { new InteractionTarget(tag.ToString(),kind) }; lastReferenceTag = tag;
		HighlightLabel = lastInteractionLabel = $"{TagChip.Icon(tag)} {tag}";
		board?.HighlightInteractions(lastInteractionTargets,Me);
	}
	public void PinLastInteractionHighlight()
	{
		if (lastInteractionTargets == null) return;
		interactionPinned = true; interactionHighlightSource = this; HighlightedTag = Tag.None;
		ReferenceTag = lastReferenceTag; HighlightLabel = lastInteractionLabel;
		board?.HighlightInteractions(lastInteractionTargets,Me);
	}
	public void UnpinInteractionHighlight(bool clearLast = false)
	{
		interactionPinned = false; interactionHighlightSource = null; ReferenceTag = Tag.None; HighlightLabel = null;
		board?.Highlight(Tag.None,Me);
		if (clearLast) { lastInteractionTargets = null; lastReferenceTag = Tag.None; lastInteractionLabel = null; }
	}
	public void ClearInteractionHighlight(object source)
	{
		if (interactionPinned || !ReferenceEquals(interactionHighlightSource,source)) return;
		interactionHighlightSource = null; ReferenceTag = Tag.None; HighlightLabel = null; board?.Highlight(Tag.None,Me);
	}
	public void Ready()
	{
		if (Multiplayer && Screens.Top==GameScreen.Lobby) { Session.ToggleReady(); return; }
		if (Screens.Top != GameScreen.Match || Mode == GameMode.Sandbox) return;
		if (Multiplayer) { Session.Send(MatchCommandKind.Ready,auctionId:OwnAuction?.Id ?? 0); return; }
		Rounds?.Ready(Me,Clock);
	}
	public bool HurryAuction(int auctionId)
	{
		if (Screens.Top != GameScreen.Match || Mode == GameMode.Sandbox) return false;
		if (Multiplayer) return Session.Send(MatchCommandKind.HurryAuction,auctionId:auctionId);
		return Rounds?.HurryAuction(Me,auctionId,Clock) == true;
	}
	public bool BuyMarketCard(int marketId,int lot)
	{
		if (Screens.Top != GameScreen.Match || LastBidInputAt == Time.Now) return false;
		var accepted = Multiplayer ? Session.Send(MatchCommandKind.BuyMarketCard,lot,auctionId:marketId) : Rounds?.BuyMarketCard(Me,marketId,lot,Clock) == true;
		if (accepted) LastBidInputAt = Time.Now;
		return accepted;
	}
	void OnHand( Guid player )
	{
		if (player != Me) return;
		ClearRenovation();
		if (LocalPlayer.Hand.Count == 0) SelectedSlot = -1;
		else if (SelectedSlot >= LocalPlayer.Hand.Count) SelectedSlot = LocalPlayer.Hand.Count-1;
		else if (SelectedSlot < 0) SelectedSlot = 0;
		NotifyUi();
	}
	void OnPhase( PhaseChange phase )
	{
		ClearRenovation();
		if (phase.Phase == MatchPhase.EndGame) Screens.Replace(GameScreen.End);
		NotifyUi();
	}
	void OnReady(Guid owner) => NotifyUi();
	void OnContract(Guid owner) => NotifyUi();
	void NotifyUi() => UiChanged?.Invoke();
	void DetachMatch()
	{
		analytics?.Dispose(); analytics=null;
		if (Match != null) { Match.Events.HandChanged -= OnHand; Match.Events.PhaseChanged -= OnPhase; Match.Events.PlayerReadyChanged -= OnReady; Match.Events.ContractChanged -= OnContract; }
		ClearRenovation(); interactionPinned = false; lastInteractionTargets = null; lastReferenceTag = Tag.None; lastInteractionLabel = null; HighlightLabel = null; interactionHighlightSource = null; ReferenceTag = Tag.None; Feedback?.Attach(null); board?.Unbind(); Rounds = null; bots.Clear(); nextBot=0;
	}
	static void ClearVehicles() { foreach (var v in Vehicle.All.ToList()) if (v.IsValid()) v.GameObject.Destroy(); }
	protected override void OnUpdate()
	{
		if (Multiplayer) return;
		if (Match == null || State != GameState.Playing || Screens.Top is not (GameScreen.Match or GameScreen.Goals)) return;
		Clock += Time.Delta;
		Rounds?.Tick(Clock);
		var observer=AiFrameMeasured;
		var round=Match.Round; var phase=Match.Phase;
		var start=System.Diagnostics.Stopwatch.GetTimestamp();
		Ai.BotFrameScheduler.Run(bots.Count,ref nextBot,Match.Settings.AiFrameBudgetMilliseconds,
			i => bots[i].Update(Clock), () => System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		if (observer != null) observer(round,phase,System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
	}
	protected override void OnDestroy()
	{
		Lighting?.Dispose();
		DetachMatch(); Screens.Changed -= NotifyUi; ClearVehicles();
		if (instance == this) instance = null;
	}
}


