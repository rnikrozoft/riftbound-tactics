using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class CardShop : Control
{
    [Signal] public delegate void TurnConfirmedEventHandler();
    [Signal] public delegate void HandChangedEventHandler(int count);
    [Signal] public delegate void NextRoundRequestedEventHandler();
    public const int ShopLimit = 4, HandLimit = 10, FieldLimit = 6;
    public static readonly Vector2I[] EnemyCells = { new(54,14),new(54,18),new(54,22),new(58,14),new(58,18),new(58,22) };
    public static readonly Vector2I[] DeploymentCells = {
        new(44,14), new(44,18), new(44,22), new(48,14), new(48,18), new(48,22)
    };
    public List<CardData> Offers { get; } = new(ShopLimit);
    public List<CardData> Hand { get; } = new(HandLimit);
    public Dictionary<int, Deployment> Deployed { get; } = new(FieldLimit);
    public BattleUnit?[] Occupants { get; } = new BattleUnit?[FieldLimit];
    public Vector2[] SlotCenters { get; } = new Vector2[FieldLimit];
    public bool Drafting { get; private set; }
    public bool Finished { get; private set; }
    public int TurnNumber { get; private set; }
    public Node2D Field { get; private set; } = null!;
    public TileMapLayer Tiles { get; private set; } = null!;
    public Camera2D Camera { get; private set; } = null!;
    public FieldDrop? FieldZone { get; private set; }
    public HBoxContainer ShopRow { get; private set; } = null!;
    public HBoxContainer HandRow { get; private set; } = null!;
    public Control OpponentHandZone { get; private set; } = null!;
    public HBoxContainer OpponentHandRow { get; private set; } = null!;
    private Label _opponentHandLabel = null!;
    private readonly List<TextureRect> _opponentBacks = new(HandLimit);
    public ShopZone ZoneA { get; private set; } = null!;
    public HandZone ZoneB { get; private set; } = null!;
    public NinePatchRect Details { get; private set; } = null!;
    public Label DetailsText { get; private set; } = null!;
    public int LayoutRevision { get; private set; }

    private readonly List<CardData> _pool = new(6);
    private readonly List<BattleUnit> _units = new(12);
    private readonly List<ShopCard> _shopViews = new(4), _handViews = new(10);
    private readonly int[] _shuffle = new int[6];
    private readonly RandomNumberGenerator _rng = new();
    private Label _shopLabel = null!, _handLabel = null!, _notice = null!, _nextText = null!;
    private TextureButton _next = null!;
    private Material? _playerMaterial;
    private PackedScene _unitScene = null!;
    private Texture2D _sheet = null!;
    private AtlasTexture _buttonTexture = null!;
    private bool _deploymentMode;
    public bool NetworkMode { get; private set; }
    public bool NetworkPending { get; set; }
    public string LocalTeam { get; private set; } = "A";
    public OnlineBattle? Online { get; set; }
    public Dictionary<string,BattleUnit> NetworkUnits { get; } = new(12);
    private Material? _redMaterial;
    private PackedScene _orcScene = null!;
    private int _networkRound;
    private string _networkPhase = "";
    private readonly List<string> _removedNetworkUnits = new(12);
    private int _serial;
    private Vector2 _cameraPosition;
    private CardDrag? _activeDrag;
    private BattleUnit? _inspectedUnit;
    private TaskCompletionSource<bool>? _turnReady, _nextRound;

    public override void _Ready()
    {
        SetProcess(false);
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        _rng.Randomize();
        Field = GetParent().GetParent().GetParent().GetParent<Node2D>();
        Tiles = Field.GetNode<TileMapLayer>("TileMapLayer");
        Camera = Field.GetNode<Camera2D>("Camera2D");
        var battle = Field.GetNode<BattleDemo>("BattleDemo");
        _deploymentMode = battle.CardShopEnabled && battle.AutoStart;
        NetworkMode = battle.NetworkEnabled && battle.AutoStart;
        foreach (var child in Field.GetChildren())
            if (child is BattleUnit unit)
            {
                if (NetworkMode)
                {
                    if (unit.IsAlly) _playerMaterial = unit.Sprite.Material; else _redMaterial = unit.Sprite.Material;
                    unit.Hide(); unit.QueueFree();
                }
                else if (_deploymentMode && unit.IsAlly)
                { _playerMaterial = unit.Sprite.Material; unit.Hide(); unit.QueueFree(); }
                else _units.Add(unit);
            }
        for (int i = 0; i < FieldLimit; i++) SlotCenters[i] = Tiles.MapToLocal(DeploymentCells[i]);
        _unitScene = GD.Load<PackedScene>("res://scenes/Character.tscn");
        _orcScene = GD.Load<PackedScene>("res://scenes/Orc.tscn");
        _sheet = GD.Load<Texture2D>("res://assets/cards/pixelCardAssest_V01.png");
        _buttonTexture = Atlas(new(16,223,96,29));
        string[] names = { "Blue","Red","Silver","Green","Gold","Stone" };
        int[] origins = { 14,133,250,367,482,611 };
        for (int i = 0; i < 6; i++) _pool.Add(new(i, names[i], Atlas(new(origins[i],4,100,128))));
        BuildUi();
        Refresh();
        if (NetworkMode) { ZoneA.Hide(); ZoneB.Hide(); }
    }

    private AtlasTexture Atlas(Rect2 region) => new() { Atlas = _sheet, Region = region };
    private static Label TextLabel() => new() { MouseFilter = MouseFilterEnum.Ignore };
    private void BuildUi()
    {
        if (_deploymentMode)
        {
            FieldZone = new FieldDrop { Name = "FieldDrop", Shop = this, MouseFilter = MouseFilterEnum.Stop };
            AddChild(FieldZone);
            FieldZone.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        ZoneA = new ShopZone {
            Name = "ZoneA", Shop = this, AnchorLeft = .5f, AnchorRight = .5f,
            OffsetLeft = -185, OffsetRight = 185, OffsetBottom = 167, MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(ZoneA);
        _shopLabel = TextLabel(); _shopLabel.Size = new(370,24);
        _shopLabel.HorizontalAlignment = HorizontalAlignment.Center;
        ZoneA.AddChild(_shopLabel);
        ShopRow = new HBoxContainer { Position = new(20,28), Size = new(330,100), Alignment = BoxContainer.AlignmentMode.Center };
        ZoneA.AddChild(ShopRow);
        _next = new TextureButton {
            Position = new(20,137), Size = new(160,30), IgnoreTextureSize = true,
            TextureNormal = _buttonTexture, StretchMode = TextureButton.StretchModeEnum.Scale
        };
        _next.Pressed += ConfirmTurn; ZoneA.AddChild(_next);
        _nextText = TextLabel(); _nextText.Name = "Text"; _nextText.Text = "BATTLE"; _nextText.Modulate = Colors.Black;
        _nextText.HorizontalAlignment = HorizontalAlignment.Center;
        _nextText.VerticalAlignment = VerticalAlignment.Center;
        _next.AddChild(_nextText); _nextText.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _notice = TextLabel(); _notice.Position = new(190,140); ZoneA.AddChild(_notice);
        ZoneB = new HandZone {
            Name = "ZoneB", Shop = this, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1, OffsetTop = -125
        };
        AddChild(ZoneB);
        _handLabel = TextLabel(); _handLabel.AnchorRight = 1; _handLabel.OffsetBottom = 24;
        _handLabel.HorizontalAlignment = HorizontalAlignment.Center; ZoneB.AddChild(_handLabel);
        HandRow = new HBoxContainer {
            AnchorLeft = .5f, AnchorRight = .5f, OffsetLeft = -338, OffsetRight = 338,
            OffsetTop = 26, OffsetBottom = 109, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center
        };
        ZoneB.AddChild(HandRow);
        OpponentHandZone = new Control { Name = "OpponentHand", AnchorRight = 1, OffsetBottom = 115, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(OpponentHandZone);
        _opponentHandLabel = TextLabel(); _opponentHandLabel.AnchorRight = 1; _opponentHandLabel.OffsetBottom = 24;
        _opponentHandLabel.HorizontalAlignment = HorizontalAlignment.Center; OpponentHandZone.AddChild(_opponentHandLabel);
        OpponentHandRow = new HBoxContainer { AnchorLeft = .5f, AnchorRight = .5f, OffsetLeft = -338, OffsetRight = 338,
            OffsetTop = 26, OffsetBottom = 109, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        OpponentHandZone.AddChild(OpponentHandRow);
        var back = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://assets/cards/cardBacks.png"), Region = new Rect2(14,12,96,128) };
        for (int i = 0; i < HandLimit; i++) {
            var card = new TextureRect { Texture = back, CustomMinimumSize = new(62,83), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
            OpponentHandRow.AddChild(card); _opponentBacks.Add(card); card.Hide();
        }
        OpponentHandZone.Hide();
        Details = new NinePatchRect {
            Name = "Details", Texture = Atlas(new(22,137,86,71)), PatchMarginLeft = 8, PatchMarginRight = 8,
            PatchMarginTop = 8, PatchMarginBottom = 8, AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = -300
        };
        AddChild(Details);
        var paper = new TextureRect { Texture = Atlas(new(24,484,76,100)), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        Details.AddChild(paper); paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        paper.OffsetLeft = paper.OffsetTop = 6; paper.OffsetRight = paper.OffsetBottom = -6;
        DetailsText = TextLabel(); DetailsText.Position = new(22,24); DetailsText.Size = new(256,430);
        DetailsText.AutowrapMode = TextServer.AutowrapMode.WordSmart; DetailsText.Modulate = Colors.Black;
        Details.AddChild(DetailsText); Details.Hide();
    }

    public Task PrepareTurn(int number)
    {
        CloseDetails();
        Finished = false; TurnNumber = number; Drafting = true;
        _turnReady = new();
        ZoneA.Show(); ZoneB.Show(); _shopLabel.Show(); RollShop();
        return _turnReady.Task;
    }
    public Task WaitForNextRound() => _nextRound?.Task ?? Task.CompletedTask;

    public void RollShop()
    {
        Offers.Clear();
        for (int i = 0; i < 6; i++) _shuffle[i] = i;
        for (int i = 0; i < ShopLimit; i++)
        {
            int index = _rng.RandiRange(i, 5);
            (_shuffle[i], _shuffle[index]) = (_shuffle[index], _shuffle[i]);
            var card = _pool[_shuffle[i]];
            Offers.Add(card with { Token = ++_serial });
        }
        Refresh();
    }
    private static int FindToken(List<CardData> cards, int token)
    {
        for (int i = 0; i < cards.Count; i++) if (cards[i].Token == token) return i;
        return -1;
    }
    public bool AcceptsToken(int token) => Drafting && !NetworkPending && Hand.Count < HandLimit && FindToken(Offers,token) >= 0;
    public bool AcceptsHandToken(int token) => Drafting && !NetworkPending && FindToken(Hand,token) >= 0;
    public bool TakeCard(int token)
    {
        if (!AcceptsToken(token)) return false;
        if (NetworkMode) { Online?.SendAction("buy",token); return true; }
        int index = FindToken(Offers, token);
        Hand.Add(Offers[index]); Offers.RemoveAt(index);
        Refresh(); EmitSignal(SignalName.HandChanged, Hand.Count); return true;
    }
    public void ConfirmTurn()
    {
        if (NetworkMode) { if (!NetworkPending) Online?.SendAction(Finished ? "next" : "ready"); return; }
        if (Finished)
        {
            Finished = false; _nextRound?.TrySetResult(true);
            EmitSignal(SignalName.NextRoundRequested); return;
        }
        if (!Drafting) return;
        if (_deploymentMode && Deployed.Count == 0) { _notice.Text = "DEPLOY A UNIT FIRST"; return; }
        Drafting = false;
        foreach (var entry in Deployed.Values) entry.Unit.HasBattled = true;
        CloseDetails(); Refresh(); ZoneA.Hide(); ZoneB.Show();
        _turnReady?.TrySetResult(true); EmitSignal(SignalName.TurnConfirmed);
    }
    public void FinishBattle()
    {
        Drafting = false; Finished = true; _nextRound = new();
        Refresh(); ZoneA.Show(); ShopRow.Hide(); _shopLabel.Hide();
        _next.Disabled = false; _nextText.Text = "NEXT ROUND"; _notice.Text = "BATTLE FINISHED";
    }

    private void RefreshViews(List<CardData> cards, List<ShopCard> views, HBoxContainer row, bool fromShop)
    {
        // Reuse controls and atlas textures instead of rebuilding both rows after every move.
        for (int i = 0; i < cards.Count; i++)
        {
            if (i == views.Count)
            {
                var view = new ShopCard {
                    Shop = this, CustomMinimumSize = fromShop ? new(72,94) : new(64,83),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Stop
                };
                views.Add(view); row.AddChild(view);
            }
            views[i].Configure(cards[i], fromShop, Drafting); views[i].Show();
        }
        for (int i = cards.Count; i < views.Count; i++) views[i].Hide();
    }
    private void Refresh()
    {
        RefreshViews(Offers,_shopViews,ShopRow,true); RefreshViews(Hand,_handViews,HandRow,false);
        _shopLabel.Text = NetworkMode ? $"SHOP / PLAYER {LocalTeam}  {Offers.Count}/{ShopLimit}  |  TURN {TurnNumber}" : $"A / SHOP  {Offers.Count}/{ShopLimit}  |  TURN {TurnNumber}";
        _handLabel.Text = $"{(NetworkMode ? "HAND / PLAYER " + LocalTeam : "B / HAND")}  {Hand.Count}/{HandLimit}  |  {(Drafting ? "DRAG TO SHOP TO SELL" : "BATTLE / HAND LOCKED")}";
        _notice.Text = _deploymentMode ? $"FIELD {Deployed.Count}/{FieldLimit}" : (Hand.Count == HandLimit ? "HAND FULL" : "CHOOSE CARDS");
        _next.Disabled = !Drafting || (_deploymentMode && Deployed.Count == 0);
        _nextText.Text = "BATTLE"; ShopRow.Visible = Drafting;
        LayoutRevision++;
    }

    public BattleUnit? PickUnit(Vector2 position) => Drafting ? PickFrom(position, true) : null;
    public BattleUnit? PickDetailUnit(Vector2 position) => PickFrom(position,false);
    private BattleUnit? PickFrom(Vector2 position, bool alliesOnly)
    {
        BattleUnit? picked = null;
        foreach (var unit in _units)
        {
            if (unit.IsQueuedForDeletion() || (alliesOnly && !Deployed.ContainsKey(unit.CardToken))) continue;
            if (new Rect2(unit.Position + new Vector2(-16,-36), new Vector2(32,42)).HasPoint(position)
                && (picked == null || unit.Position.Y > picked.Position.Y)) picked = unit;
        }
        return picked;
    }
    private bool IsDeployed(BattleUnit unit) => GodotObject.IsInstanceValid(unit) && Deployed.ContainsKey(unit.CardToken);
    public bool CanReturnUnit(BattleUnit unit) => Drafting && !NetworkPending && Hand.Count < HandLimit && IsDeployed(unit) && !unit.HasBattled;
    private void RemoveUnit(BattleUnit unit)
    {
        Occupants[unit.GridSlot] = null;
        Deployed.Remove(unit.CardToken); _units.Remove(unit); unit.Hide(); unit.QueueFree();
    }
    public bool ReturnUnit(BattleUnit unit)
    {
        if (!CanReturnUnit(unit)) return false;
        if (NetworkMode) { Online?.SendAction("return",unit.CardToken); return true; }
        Hand.Add(Deployed[unit.CardToken].Card);
        if (_inspectedUnit == unit) CloseDetails();
        RemoveUnit(unit); Refresh(); EmitSignal(SignalName.HandChanged,Hand.Count); return true;
    }
    public int DeploymentSlot(Vector2 worldPosition)
    {
        var local = Tiles.ToLocal(worldPosition);
        for (int i = 0; i < FieldLimit; i++)
        {
            var delta = local - SlotCenters[i];
            if (Mathf.Abs(delta.X) / 24 + Mathf.Abs(delta.Y) / 12 <= 1) return i;
        }
        return -1;
    }
    public bool CanPlace(CardDrag data, Vector2 position)
    {
        if (!Drafting || NetworkPending || !_deploymentMode || data.Shop != this) return false;
        if (data.Unit != null) { if (!IsDeployed(data.Unit)) return false; }
        else if (data.FromShop || !AcceptsHandToken(data.Token) || Deployed.Count >= FieldLimit) return false;
        int slot = DeploymentSlot(position);
        return slot >= 0 && (Occupants[slot] == null || data.Unit != null);
    }
    public bool PlaceCardOrUnit(CardDrag data, Vector2 position)
    {
        if (!CanPlace(data, position)) return false;
        int slot = DeploymentSlot(position);
        if (NetworkMode) { Online?.SendAction(data.Unit != null ? "move" : "deploy",data.Unit?.CardToken ?? data.Token,slot); return true; }
        if (data.Unit != null)
        {
            var moving = data.Unit; int oldSlot = moving.GridSlot;
            var occupant = Occupants[slot];
            if (occupant != null && occupant != moving)
            {
                occupant.GridSlot = oldSlot; occupant.SetMeta("grid_cell",DeploymentCells[oldSlot]);
                occupant.Position = Tiles.ToGlobal(SlotCenters[oldSlot]); Occupants[oldSlot] = occupant;
            }
            else Occupants[oldSlot] = null;
            moving.GridSlot = slot; moving.SetMeta("grid_cell",DeploymentCells[slot]);
            moving.Position = Tiles.ToGlobal(SlotCenters[slot]); Occupants[slot] = moving;
            LayoutRevision++; return true;
        }
        int index = FindToken(Hand,data.Token); var card = Hand[index]; Hand.RemoveAt(index);
        var unit = _unitScene.Instantiate<BattleUnit>();
        unit.Name = $"Deployed_{card.Token}"; unit.Position = Tiles.ToGlobal(SlotCenters[slot]);
        unit.CardToken = card.Token; unit.GridSlot = slot;
        unit.SetMeta("team","Ally"); unit.SetMeta("grid_cell",DeploymentCells[slot]); unit.SetMeta("card_token",card.Token);
        unit.GetNode<AnimatedSprite2D>("AnimatedSprite2D").Material = _playerMaterial;
        Field.AddChild(unit);
        Occupants[slot] = unit; Deployed.Add(card.Token,new(card,unit)); _units.Add(unit);
        Refresh(); EmitSignal(SignalName.HandChanged,Hand.Count); return true;
    }
    public int AutoPlaceSlot(CardDrag data, Vector2 position)
    {
        int closest = -1; float distance = float.PositiveInfinity;
        for (int i = 0; i < FieldLimit; i++)
        {
            var center = Tiles.ToGlobal(SlotCenters[i]); float d = center.DistanceSquaredTo(position);
            if (CanPlace(data,center) && d < distance) { closest = i; distance = d; }
        }
        return closest;
    }
    public void ResetRoundUnits()
    {
        foreach (var unit in _units) { unit.ResetHealth(); unit.Sprite.Play(BattleAnimations.Idle); }
    }
    public bool SellCard(int token)
    {
        if (!AcceptsHandToken(token)) return false;
        if (NetworkMode) { Online?.SendAction("sell",token); return true; }
        Hand.RemoveAt(FindToken(Hand,token)); CloseDetails(); Refresh();
        EmitSignal(SignalName.HandChanged,Hand.Count); return true;
    }
    public bool CanSell(CardDrag? data) => Drafting && !NetworkPending && data != null && data.Shop == this &&
        (data.Unit != null ? IsDeployed(data.Unit) : !data.FromShop && AcceptsHandToken(data.Token));
    public void SellDrop(CardDrag data)
    {
        if (!CanSell(data)) return;
        if (NetworkMode) { Online?.SendAction("sell",data.Unit?.CardToken ?? data.Token); return; }
        if (data.Unit == null) { SellCard(data.Token); return; }
        CloseDetails(); RemoveUnit(data.Unit); Refresh();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin)
        { var data = CardDrag.Read(GetViewport().GuiGetDragData()); _activeDrag = data?.Shop == this ? data : null; }
        else if (what == NotificationDragEnd)
        {
            if (_activeDrag != null && !GetViewport().GuiIsDragSuccessful())
            {
                var data = _activeDrag; var position = GetGlobalMousePosition();
                Callable.From(() => FinishLooseDrop(data,position)).CallDeferred();
            }
            _activeDrag = null;
        }
    }
    public void FinishLooseDrop(CardDrag data, Vector2 uiPosition)
    {
        if (!Drafting || data.Unit != null || data.Shop != this) return;
        if (data.FromShop)
        { if (!ZoneA.GetGlobalRect().HasPoint(uiPosition)) TakeCard(data.Token); return; }
        if (ZoneB.GetGlobalRect().HasPoint(uiPosition)) return;
        if (ZoneA.GetGlobalRect().HasPoint(uiPosition)) { SellCard(data.Token); return; }
        var worldPosition = GetViewport().GetCanvasTransform().AffineInverse() * uiPosition;
        int slot = AutoPlaceSlot(data,worldPosition);
        if (slot >= 0) PlaceCardOrUnit(data,Tiles.ToGlobal(SlotCenters[slot]));
    }

    private void ShowDetails(string title, string team, int health = 100, int maximum = 100)
    {
        if (!Details.Visible)
        {
            _cameraPosition = Camera.Position; Camera.Position += new Vector2(150 / Camera.Zoom.X,0);
            Camera.ForceUpdateScroll();
            ZoneA.OffsetLeft = -335; ZoneA.OffsetRight = 35; ZoneB.OffsetRight = -300; OpponentHandZone.OffsetRight = -300;
        }
        DetailsText.Text = $"{title}\n{team}\n\nMOCK DETAILS\nHP  {health} / {maximum}\nAttack  30\nSpeed  10\n\nABILITY / Lorem Strike\nLorem ipsum dolor sit amet, consectetur adipiscing elit.\n\nPASSIVE / Lorem Guard\nSed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\n\nPlaceholder abilities and stats.";
        Details.Show();
    }
    private void Uninspect()
    {
        if (GodotObject.IsInstanceValid(_inspectedUnit)) _inspectedUnit!.HealthChanged -= OnInspectedHealthChanged;
        _inspectedUnit = null;
    }
    public void CloseDetails()
    {
        Uninspect();
        if (Details == null || !Details.Visible) return;
        Camera.Position = _cameraPosition; Camera.ForceUpdateScroll(); Details.Hide();
        ZoneA.OffsetLeft = -185; ZoneA.OffsetRight = 185; ZoneB.OffsetRight = 0; OpponentHandZone.OffsetRight = 0;
    }
    public void ShowCardDetails(int token)
    {
        Uninspect();
        int index = FindToken(Offers,token);
        if (index >= 0) { ShowDetails(Offers[index].Name,NetworkMode ? $"PLAYER {LocalTeam} / SHOP" : "SHOP CARD"); return; }
        index = FindToken(Hand,token);
        if (index >= 0) ShowDetails(Hand[index].Name,$"PLAYER {(NetworkMode ? LocalTeam : "A")} / HAND");
    }
    public void ShowUnitDetails(BattleUnit unit)
    {
        if (!GodotObject.IsInstanceValid(unit)) return;
        if (_inspectedUnit != unit)
        { Uninspect(); _inspectedUnit = unit; unit.HealthChanged += OnInspectedHealthChanged; }
        UpdateUnitDetails();
    }
    private void OnInspectedHealthChanged(int current, int maximum) => UpdateUnitDetails();
    private void UpdateUnitDetails()
    {
        var unit = _inspectedUnit!;
        string title = Deployed.TryGetValue(unit.CardToken,out var entry) ? entry.Card.Name : (NetworkMode ? _pool[unit.CardKind].Name : (unit.IsAlly ? "Soldier" : "Orc"));
        ShowDetails(title, $"{(NetworkMode ? "PLAYER " + unit.ServerId.Split(':')[0] : (unit.IsAlly ? "PLAYER A" : "PLAYER B"))} / FIELD / {(unit.IsDead ? "DEAD" : "ALIVE")}", unit.Health,unit.MaxHealth);
    }
    public void RefreshNetworkControls() => Refresh();
    public void LockNetworkInteraction() { Drafting = false; NetworkPending = true; Refresh(); }

    public void ApplyNetworkState(OnlineState state, string userId)
    {
        OnlinePlayer? self = null;
        foreach (var player in state.Players) if (player?.UserId == userId) self = player;
        if (self == null) return;
        bool newRound = state.Round != _networkRound;
        bool enteringBattle = state.Phase == "battle" && _networkPhase != "battle";
        LocalTeam = self.Team;
        int opponentCards = 0;
        foreach (var player in state.Players) if (player != null && player.UserId != userId) opponentCards = System.Math.Clamp(player.HandCount,0,HandLimit);
        for (int i = 0; i < _opponentBacks.Count; i++) _opponentBacks[i].Visible = i < opponentCards;
        _opponentHandLabel.Text = $"OPPONENT / HAND {opponentCards}/{HandLimit}";
        OpponentHandZone.Visible = state.Phase == "battle";
        var cells = DeploymentCells;
        for (int i = 0; i < FieldLimit; i++) SlotCenters[i] = Tiles.MapToLocal(cells[i]);
        TurnNumber = state.Round;
        Drafting = state.Phase == "preparation" && !self.Ready;
        Finished = state.Phase == "finished";
        Offers.Clear(); Hand.Clear();
        foreach (var card in self.Offers) Offers.Add(_pool[card.Kind] with { Token = card.Token });
        foreach (var card in self.Hand) Hand.Add(_pool[card.Kind] with { Token = card.Token });
        if (state.Phase is "preparation" or "waiting" || enteringBattle)
        {
            ReconcileUnits(state,newRound || enteringBattle);
            if (newRound || enteringBattle) CloseDetails();
        }
        _networkRound = state.Round; _networkPhase = state.Phase;
        Refresh();
        ZoneB.Visible = state.Phase != "waiting";
        ZoneA.Visible = state.Phase is "preparation" or "finished";
        if (state.Phase == "preparation")
        {
            ShopRow.Show(); _shopLabel.Show();
            _next.Disabled = self.Ready || NetworkPending;
            _nextText.Text = self.Ready ? "READY" : "BATTLE";
            _notice.Text = self.Ready ? "WAITING FOR OPPONENT" : $"FIELD {Deployed.Count}/{FieldLimit}";
        }
        else if (Finished)
        {
            ShopRow.Hide(); _shopLabel.Hide();
            _next.Disabled = NetworkPending || self.Ready;
            _nextText.Text = self.Ready ? "WAITING" : "NEXT ROUND";
            _notice.Text = "BATTLE FINISHED";
        }
        EmitSignal(SignalName.HandChanged,Hand.Count);
    }

    private void ReconcileUnits(OnlineState state, bool resetHealth)
    {
        _removedNetworkUnits.Clear();
        foreach (var id in NetworkUnits.Keys) _removedNetworkUnits.Add(id);
        Deployed.Clear();
        System.Array.Clear(Occupants);
        foreach (var player in state.Players)
        {
            if (player == null) continue;
            bool owned = player.Team == LocalTeam;
            // The opponent faces the board from the other end: reverse both columns and rows.
            foreach (var entry in player.Units)
            {
                string id = $"{player.Team}:{entry.Token}";
                _removedNetworkUnits.Remove(id);
                if (!NetworkUnits.TryGetValue(id,out var unit))
                {
                    unit = (owned ? _unitScene : _orcScene).Instantiate<BattleUnit>();
                    unit.Name = $"Online_{player.Team}_{entry.Token}";
                    unit.SetMeta("team",owned ? "Ally" : "Enemy");
                    unit.GetNode<AnimatedSprite2D>("AnimatedSprite2D").Material = owned ? _playerMaterial : _redMaterial;
                    Field.AddChild(unit);
                    _units.Add(unit); NetworkUnits.Add(id,unit);
                }
                unit.CardToken = entry.Token; unit.CardKind = entry.Kind; unit.GridSlot = entry.Slot;
                unit.HasBattled = entry.Veteran; unit.ServerId = id;
                var cell = owned ? DeploymentCells[entry.Slot] : EnemyCells[FieldLimit - 1 - entry.Slot];
                unit.SetMeta("grid_cell",cell);
                unit.Position = Tiles.ToGlobal(Tiles.MapToLocal(cell));
                unit.Sprite.FlipH = !owned;
                if (resetHealth) { unit.ResetHealth(); unit.Sprite.Play(BattleAnimations.Idle); }
                if (player.Team == LocalTeam)
                {
                    var card = _pool[entry.Kind] with { Token = entry.Token };
                    Deployed.Add(entry.Token,new(card,unit)); Occupants[entry.Slot] = unit;
                }
            }
        }
        foreach (string id in _removedNetworkUnits)
        {
            var unit = NetworkUnits[id];
            if (_inspectedUnit == unit) CloseDetails();
            _units.Remove(unit); unit.Hide(); unit.QueueFree(); NetworkUnits.Remove(id);
        }
    }
    public override void _ExitTree()
    {
        Uninspect();
        _turnReady?.TrySetCanceled(); _nextRound?.TrySetCanceled();
    }
}
