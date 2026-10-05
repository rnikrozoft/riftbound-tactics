using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class CardShop : Control
{
    [Signal] public delegate void TurnConfirmedEventHandler();
    [Signal] public delegate void HandChangedEventHandler(int count);
    [Signal] public delegate void NextRoundRequestedEventHandler();
    public bool GameOver { get; private set; }
    public int PlayerHpA { get; private set; } = 30;
    public int PlayerHpB { get; private set; } = 30;
    private ulong _preparationDeadline;
    private PreparationCountdown _countdown = null!, _centerCountdown=null!;
    private Control _modalRoot=null!;
    public const int ShopLimit = 5, HandLimit = 10, FieldLimit = 6;
    public static readonly Vector2I[] EnemyCells = { new(54,14),new(54,18),new(54,22),new(58,14),new(58,18),new(58,22) };
    public static readonly Vector2I[] DeploymentCells = {
        new(44,14), new(44,18), new(44,22), new(48,14), new(48,18), new(48,22)
    };
    public const int StartingCoins = 4, RerollCost = 2, MaxShopLevel = 6;
    public static int CardKinds=>CardCatalog.Count;
    public int Coins { get; private set; }
    public int ShopLevel { get; private set; } = 2;
    public int UpgradeCost { get; private set; } = 2;
    public int OfferLimit => ShopSlots(ShopLevel);
    private readonly int[] _remainingCopies = new int[CardKinds];
    public static int RoundIncome(int round) => round < 1 ? 0 : new[] {4,6,9,12,15,18,20}[System.Math.Min(round,7)-1];
    public static int ShopSlots(int level) => level == 2 ? 3 : level <= 4 ? 4 : 5;
    public static int UpgradeBaseCost(int level) => level switch { 2 => 2, 3 => 8, 4 => 10, 5 => 14, _ => 0 };
    private readonly HashSet<int> _lockedOfferTokens = new();
    public bool ShopLocked { get; private set; }
    private TextureButton _reroll = null!, _lock = null!, _upgrade = null!;
    private Label _upgradeLabel = null!;
    private Label _coinLabel = null!, _lockLabel = null!;
    public List<CardData> Offers { get; } = new(ShopLimit);
    public List<CardData> Hand { get; } = new(HandLimit);
    public Dictionary<int, Deployment> Deployed { get; } = new(FieldLimit);
    public BattleUnit?[] Occupants { get; } = new BattleUnit?[FieldLimit];
    public Vector2[] SlotCenters { get; } = new Vector2[FieldLimit];
    public bool Drafting { get; private set; }
    public bool ReadyForBattle { get; private set; }
    public bool Finished { get; private set; }
    public int TurnNumber { get; private set; }
    public Node2D Field { get; private set; } = null!;
    public TileMapLayer Tiles { get; private set; } = null!;
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

    private readonly List<CardData> _pool = new(CardKinds);
    private readonly List<BattleUnit> _units = new(12);
    private readonly List<ShopCard> _shopViews = new(6), _handViews = new(10);
    private readonly RandomNumberGenerator _rng = new();
    private Label _shopLabel = null!, _handLabel = null!, _notice = null!, _nextText = null!;
    private TextureButton _next = null!;
    private Control _battleActions=null!;
    private Material? _playerMaterial;
    private bool _deploymentMode;
    public bool NetworkMode { get; private set; }
    public bool NetworkPending { get; set; }
    public string LocalTeam { get; private set; } = "A";
    public OnlineBattle? Online { get; set; }
    public Dictionary<string,BattleUnit> NetworkUnits { get; } = new(12);
    private Material? _redMaterial;
    private int _networkRound;
    private string _networkPhase = "";
    private readonly List<string> _removedNetworkUnits = new(12);
    private int _serial;
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
        for (int kind=0;kind<CardKinds;kind++)
            _pool.Add(new(kind,CardCatalog.Name(kind),CardCatalog.Art(kind),CardCatalog.Cost(kind)));
        var selectedDeck = BattleLaunch.Deck ?? DeckDefinition.Starter();
        foreach(var card in selectedDeck.Cards) _remainingCopies[card.Kind]=card.Copies;
        BuildUi();
        Refresh();
        if (NetworkMode) { ZoneA.Hide(); ZoneB.Hide(); }
    }

    private static Label TextLabel() { var label=new Label {MouseFilter=MouseFilterEnum.Ignore};GameUi.Label(label,14);return label; }
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
            OffsetLeft = -260, OffsetRight = 260, OffsetTop=46, OffsetBottom = 189, MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(ZoneA);
        GameUi.Backdrop(ZoneA,true);

        _shopLabel = TextLabel(); _shopLabel.AnchorRight = 1; _shopLabel.OffsetBottom = 24;
        _shopLabel.HorizontalAlignment = HorizontalAlignment.Center;_shopLabel.AddThemeColorOverride("font_color",GameUi.Gold);
        ZoneA.AddChild(_shopLabel);
        ShopRow = new HBoxContainer { Position = new(20,28), Size = new(452,100), Alignment = BoxContainer.AlignmentMode.Center };
        ZoneA.AddChild(ShopRow);
        _upgrade = ShopAction("Upgrade", "IconArrow01a", new(480,12), UpgradeShop);
        _upgradeLabel = TextLabel(); _upgradeLabel.Position = new(-12,25); _upgradeLabel.Size = new(48,14);
        _upgradeLabel.AddThemeFontSizeOverride("font_size",10); _upgradeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _upgrade.AddChild(_upgradeLabel);
        _reroll = ShopAction("Reroll", "IconRestart01a", new(480,56), RerollShop);
        var rerollPrice = TextLabel(); rerollPrice.Position = new(-12,25); rerollPrice.Size = new(48,14);
        rerollPrice.Text = $"{RerollCost} coin"; rerollPrice.AddThemeFontSizeOverride("font_size",11);
        rerollPrice.HorizontalAlignment = HorizontalAlignment.Center; _reroll.AddChild(rerollPrice);
        _reroll.TooltipText = $"Reroll shop / {RerollCost} coin / unlock automatically";
        _lock = ShopAction("Lock", "IconPause01a", new(480,100), ToggleShopLock);
        _lockLabel = TextLabel(); _lockLabel.Position = new(468,125); _lockLabel.Size = new(48,14);
        _lockLabel.AddThemeFontSizeOverride("font_size",11); _lockLabel.HorizontalAlignment = HorizontalAlignment.Center;
        ZoneA.AddChild(_lockLabel);
        var coin = new TextureRect { Texture = TravelBookUi.Texture("IconCoin01a"), Position = new(20,2), Size = new(20,22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        ZoneA.AddChild(coin);
        _coinLabel = TextLabel(); _coinLabel.Position = new(46,0); _coinLabel.Size = new(90,26); ZoneA.AddChild(_coinLabel);
        _battleActions=new Control {Name="BattleActions",AnchorLeft=1,AnchorRight=1,AnchorTop=.5f,AnchorBottom=.5f,OffsetLeft=-164,OffsetRight=-16,OffsetTop=-52,OffsetBottom=52,MouseFilter=MouseFilterEnum.Ignore};AddChild(_battleActions);
        _next = new TextureButton {
            Name="BattleButton",Position = new(0,48), Size = new(148,44), IgnoreTextureSize = true,
            TextureNormal = TravelBookUi.Texture("FrameSelect01a"), StretchMode = TextureButton.StretchModeEnum.Scale
        };
        TravelBookUi.StyleButton(_next);
        _next.Pressed += ConfirmTurn; _battleActions.AddChild(_next);
        _nextText = TextLabel(); _nextText.Name = "Text"; _nextText.Text = "BATTLE"; _nextText.Modulate = Colors.White;
        _nextText.HorizontalAlignment = HorizontalAlignment.Center;
        _nextText.VerticalAlignment = VerticalAlignment.Center;
        _next.AddChild(_nextText); _nextText.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);GameUi.Primary(_next);
        _notice = TextLabel(); _notice.Position = new(190,140); ZoneA.AddChild(_notice);_notice.Hide();
        ZoneB = new HandZone {
            Name = "ZoneB", Shop = this, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1, OffsetTop = -125
        };
        AddChild(ZoneB);
        var handSurface=GameUi.Backdrop(ZoneB);handSurface.OffsetLeft=296;handSurface.OffsetRight=-296;handSurface.OffsetTop=2;handSurface.OffsetBottom=-8;

        _handLabel = TextLabel(); _handLabel.AnchorRight = 1; _handLabel.OffsetBottom = 24;
        _handLabel.HorizontalAlignment = HorizontalAlignment.Center; ZoneB.AddChild(_handLabel);_handLabel.Hide();
        HandRow = new HBoxContainer {
            AnchorLeft = .5f, AnchorRight = .5f, OffsetLeft = -338, OffsetRight = 338,
            OffsetTop = 26, OffsetBottom = 109, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center
        };
        ZoneB.AddChild(HandRow);
        OpponentHandZone = new Control { Name = "OpponentHand", AnchorRight = 1, OffsetBottom = 115, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(OpponentHandZone);
        _opponentHandLabel = TextLabel(); _opponentHandLabel.AnchorRight = 1; _opponentHandLabel.OffsetBottom = 24;
        _opponentHandLabel.HorizontalAlignment = HorizontalAlignment.Center; OpponentHandZone.AddChild(_opponentHandLabel); _opponentHandLabel.Hide();
        OpponentHandRow = new HBoxContainer { AnchorLeft = .5f, AnchorRight = .5f, OffsetLeft = -338, OffsetRight = 338,
            OffsetTop = 26, OffsetBottom = 109, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        OpponentHandZone.AddChild(OpponentHandRow);
        var back = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://assets/ui/cards/card_backs.png"), Region = new Rect2(14,12,96,128) };
        for (int i = 0; i < HandLimit; i++) {
            var card = new TextureRect { Texture = back, CustomMinimumSize = new(62,83), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
            OpponentHandRow.AddChild(card); _opponentBacks.Add(card); card.Hide();
        }
        OpponentHandZone.Hide();
        var modalLayer=new CanvasLayer {Name="DetailsLayer",Layer=20};AddChild(modalLayer);
        _modalRoot=new Control {Name="DetailsModal",MouseFilter=MouseFilterEnum.Stop};modalLayer.AddChild(_modalRoot);_modalRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade=new ColorRect {Color=new Color(0,0,0,.65f),MouseFilter=MouseFilterEnum.Stop};_modalRoot.AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shade.GuiInput+=input=>{if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:true})CloseDetails();};
        Details=new NinePatchRect {Name="Details",Texture=TravelBookUi.Texture("BookCover01a"),PatchMarginLeft=8,PatchMarginRight=8,PatchMarginTop=8,PatchMarginBottom=8,AnchorLeft=.5f,AnchorRight=.5f,AnchorTop=.5f,AnchorBottom=.5f,OffsetLeft=-220,OffsetRight=220,OffsetTop=-280,OffsetBottom=280,MouseFilter=MouseFilterEnum.Stop};
        _modalRoot.AddChild(Details);
        var paper=new TextureRect {Texture=TravelBookUi.Texture("BookPageRight01a"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore,SelfModulate=new Color(.12f,.17f,.23f)};
        Details.AddChild(paper);paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);paper.OffsetLeft=paper.OffsetTop=6;paper.OffsetRight=paper.OffsetBottom=-6;
        var close=DeckMenuUi.Button("CLOSE",CloseDetails,80,30);Details.AddChild(close);close.AnchorLeft=close.AnchorRight=1;close.OffsetLeft=-96;close.OffsetRight=-16;close.OffsetTop=12;close.OffsetBottom=42;
        var detailsScroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};Details.AddChild(detailsScroll);detailsScroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);detailsScroll.OffsetLeft=24;detailsScroll.OffsetTop=52;detailsScroll.OffsetRight=-24;detailsScroll.OffsetBottom=-24;
        DetailsText=TextLabel();DetailsText.SizeFlagsHorizontal=SizeFlags.ExpandFill;DetailsText.AutowrapMode=TextServer.AutowrapMode.WordSmart;detailsScroll.AddChild(DetailsText);
        void FitDetails(){var half=new Vector2(Mathf.Min(440,_modalRoot.Size.X-32),Mathf.Min(560,_modalRoot.Size.Y-32))/2;Details.OffsetLeft=-half.X;Details.OffsetRight=half.X;Details.OffsetTop=-half.Y;Details.OffsetBottom=half.Y;}
        _modalRoot.Resized+=FitDetails;FitDetails();Details.Hide();_modalRoot.Hide();
        _countdown = new PreparationCountdown { Name = "PreparationCountdown",Size=new(148,40) }; _battleActions.AddChild(_countdown);
        _centerCountdown=new PreparationCountdown {Name="CenterCountdown",CenterBurst=true};AddChild(_centerCountdown);
    }

    public Task PrepareTurn(int number)
    {
        if (GameOver) return Task.CompletedTask;
        _preparationDeadline = Time.GetTicksMsec() + 60000;
        CloseDetails();
        Finished = false; ReadyForBattle = false; TurnNumber = number; Drafting = true;
        _turnReady = new();
        Coins += RoundIncome(number);
        if (number > 1) UpgradeCost = Mathf.Max(0, UpgradeCost - 2);
        ZoneA.Show(); ZoneB.Show(); _shopLabel.Show();
        var replacedNames = new HashSet<string>();
        if (ShopLocked) {
            foreach(var card in Offers) if(!_lockedOfferTokens.Contains(card.Token)) replacedNames.Add(card.Name);
            Offers.RemoveAll(card=>!_lockedOfferTokens.Contains(card.Token));
            if(Offers.Count==0){ShopLocked=false;_lockedOfferTokens.Clear();}
        }
        if (!ShopLocked) RollShop(); else { FillShop(replacedNames); Refresh(); }
        return _turnReady.Task;
    }
    public void UpdateCountdown(int remaining, bool active) { _countdown.UpdateCountdown(remaining,active);_centerCountdown.UpdateCountdown(remaining,active); }
    public override void _Process(double delta)
    {
        if (NetworkMode || _countdown == null) return;
        int remaining = (int)System.Math.Max(0, ((long)_preparationDeadline - (long)Time.GetTicksMsec() + 999) / 1000);
        UpdateCountdown(remaining, Drafting && !GameOver);
        if (Drafting && remaining == 0) ConfirmTurn();
    }
    public Task WaitForNextRound() => _nextRound?.Task ?? Task.CompletedTask;

    private TextureButton ShopAction(string name, string icon, Vector2 position, System.Action action)
    {
        var button = new TextureButton {
            Name = name, Position = position, Size = new(24,24), IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            TextureNormal = TravelBookUi.Texture("IconCoin01a"),
            TextureHover = TravelBookUi.Texture("IconCoin01a"),
            TexturePressed = TravelBookUi.Texture("IconCoin01a")
        };
        var image = new TextureRect { Texture = TravelBookUi.Texture(icon), Position = new(6,6), Size = new(12,12), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        if (name == "Upgrade") { image.PivotOffset = new(6,6); image.Rotation = -Mathf.Pi / 2; }
        TravelBookUi.StyleButton(button);button.AddChild(image); button.Pressed += action; ZoneA.AddChild(button); return button;
    }
    public void RerollShop()
    {
        if (!Drafting || NetworkPending || Coins < RerollCost) return;
        if (NetworkMode) { Online?.SendAction("reroll"); return; }
        Coins -= RerollCost; ShopLocked = false; _lockedOfferTokens.Clear(); CloseDetails(); RollShop();
    }
    public void ToggleShopLock()
    {
        if (!Drafting || NetworkPending) return;
        if (NetworkMode) { Online?.SendAction("lock"); return; }
        ShopLocked = !ShopLocked; _lockedOfferTokens.Clear();
        if(ShopLocked)foreach(var card in Offers)_lockedOfferTokens.Add(card.Token);
        Refresh();
    }
    private bool Affordable(int token)
    {
        int index = FindToken(Offers, token);
        return index >= 0 && Coins >= Offers[index].Price;
    }
    public void UpgradeShop()
    {
        if (!Drafting || NetworkPending || ShopLevel >= MaxShopLevel || Coins < UpgradeCost) return;
        if (NetworkMode) { Online?.SendAction("upgrade"); return; }
        Coins -= UpgradeCost;
        ShopLevel++; UpgradeCost = UpgradeBaseCost(ShopLevel);
        Refresh();
    }
    public void RollShop()
    {
        Offers.Clear(); FillShop(); Refresh();
    }
    private void FillShop(HashSet<string>? excludedNames=null)
    {
        var candidates = new List<int>();
        for (int i=0; i<_pool.Count; i++)
        {
            if (_remainingCopies[i] <= 0 || !CharacterData.Get(i).Enabled || _pool[i].Price > ShopLevel) continue;
            bool present = false; foreach (var card in Offers) if (card.Name == _pool[i].Name) { present = true; break; }
            if (!present) candidates.Add(i);
        }
        if(excludedNames!=null){var preferred=candidates.FindAll(kind=>!excludedNames.Contains(_pool[kind].Name));if(preferred.Count>=OfferLimit-Offers.Count)candidates=preferred;}
        while (Offers.Count < OfferLimit && candidates.Count > 0)
        {
            int index = _rng.RandiRange(0,candidates.Count-1); int kind = candidates[index]; candidates.RemoveAt(index);
            Offers.Add(_pool[kind] with { Token = ++_serial });
        }
    }
    private static int FindToken(List<CardData> cards, int token)
    {
        for (int i = 0; i < cards.Count; i++) if (cards[i].Token == token) return i;
        return -1;
    }
    private CardData? OwnedCopy(int token)
    {
        int index = FindToken(Offers,token); if(index < 0) return null;
        string name = Offers[index].Name;
        foreach(var entry in Deployed.Values) if(entry.Card.Name == name) return entry.Card;
        return Hand.Find(card => card.Name == name);
    }
    public bool AcceptsToken(int token)
    {
        var owned = OwnedCopy(token);
        return Drafting && !NetworkPending && Affordable(token) && (owned != null ? owned.Stars < 4 && (Hand.Exists(c=>c.Token==owned.Token) || Hand.Count < HandLimit) : Hand.Count < HandLimit);
    }
    public bool AcceptsHandToken(int token) => Drafting && !NetworkPending && FindToken(Hand,token) >= 0;
    public bool TakeCard(int token)
    {
        if (!AcceptsToken(token)) return false;
        if (NetworkMode) { Online?.SendAction("buy",token); return true; }
        int index = FindToken(Offers, token);
        var purchase = Offers[index];
        int kind = _pool.FindIndex(c => c.Name == purchase.Name);
        if (kind < 0 || _remainingCopies[kind] <= 0) return false;
        _remainingCopies[kind]--;
        Coins -= purchase.Price;
        int handIndex = Hand.FindIndex(c=>c.Name==purchase.Name);
        Deployment? deployed = null; foreach(var entry in Deployed.Values) if(entry.Card.Name==purchase.Name) { deployed=entry; break; }
        if(deployed != null) {
            var upgraded = deployed.Card with { Stars = deployed.Card.Stars+1, Paid = deployed.Card.Investment+purchase.Price, Veteran = deployed.Unit.HasBattled };
            if(_inspectedUnit == deployed.Unit) CloseDetails();
            Hand.Add(upgraded); RemoveUnit(deployed.Unit); handIndex = Hand.Count-1;
        } else if(handIndex >= 0) {
            var original = Hand[handIndex]; Hand[handIndex] = original with { Stars=original.Stars+1, Paid=original.Investment+purchase.Price };
        } else Hand.Add(purchase with { Paid=purchase.Price });
        Offers.RemoveAt(index);
        Refresh(); if(handIndex >= 0) PlayHandUpgrade(Hand[handIndex].Token); if(_inspectedUnit != null) UpdateUnitDetails(); EmitSignal(SignalName.HandChanged, Hand.Count); return true;
    }
    public void ConfirmTurn()
    {
        if (GameOver) return;
        if (NetworkMode) { if (!NetworkPending && (Finished || !ReadyForBattle)) Online?.SendAction(Finished ? "next" : "ready"); return; }
        if (Finished)
        {
            Finished = false; _nextRound?.TrySetResult(true);
            EmitSignal(SignalName.NextRoundRequested); return;
        }
        if (!Drafting) return;
        if (_deploymentMode && Deployed.Count == 0 && Time.GetTicksMsec() < _preparationDeadline) { _notice.Text = "DEPLOY A UNIT FIRST"; return; }
        Drafting = false;
        foreach (var entry in Deployed.Values) entry.Unit.HasBattled = true;
        CloseDetails(); Refresh(); ZoneA.Hide(); ZoneB.Show();
        _turnReady?.TrySetResult(true); EmitSignal(SignalName.TurnConfirmed);
    }
    public void FinishBattle(string winner, int survivorStars = 0)
    {
        if (Finished || GameOver) return;
        int damage = winner == "DRAW" ? 0 : (winner == "A" ? ShopLevel : 2) + Mathf.Max(0, survivorStars);
        if (winner == "A") PlayerHpB = Mathf.Max(0, PlayerHpB - damage);
        if (winner == "B") PlayerHpA = Mathf.Max(0, PlayerHpA - damage);
        GameOver = PlayerHpA == 0 || PlayerHpB == 0;
        GetParent().GetNode<PlayerProfile>("ProfileA").SetHealth(PlayerHpA);
        GetParent().GetNode<PlayerProfile>("ProfileB").SetHealth(PlayerHpB);
        Drafting = false; Finished = true; _nextRound = new();
        Refresh(); ZoneA.Hide(); ShopRow.Hide(); _shopLabel.Hide();
        _next.Disabled = GameOver; _nextText.Text = GameOver ? "GAME OVER" : "NEXT ROUND"; _notice.Text = GameOver ? $"PLAYER {winner} WINS MATCH" : $"BATTLE FINISHED / {damage} HP DAMAGE";
        UpdateCountdown(0, false);
    }

    private void PlayHandUpgrade(int token)
    {
        int index = FindToken(Hand,token); if(index >= 0 && index < _handViews.Count) UpgradeEffect.Play(_handViews[index], _handViews[index].Size * .5f);
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
        _shopLabel.Text = $"LEVEL {ShopLevel}";
        _handLabel.Text = $"{("YOUR HAND")}  {Hand.Count}/{HandLimit}  |  {(Drafting ? "DRAG TO DEPLOY OR SELL" : "BATTLE IN PROGRESS")}";
        _notice.Text = _deploymentMode ? $"FIELD {Deployed.Count}/{FieldLimit}" : (Hand.Count == HandLimit ? "HAND FULL" : "CHOOSE CARDS");
        _next.Disabled = !Drafting || ReadyForBattle || NetworkPending || (_deploymentMode && Deployed.Count == 0);
        _nextText.Text = ReadyForBattle ? "READY" : "BATTLE"; ShopRow.Visible = Drafting;
        _coinLabel.Text = $"{Coins} GOLD";
        if (!NetworkMode) GetParent().GetNode<PlayerProfile>("ProfileA").SetCoins(Coins);
        _reroll.Visible = _lock.Visible = _lockLabel.Visible = _upgrade.Visible = Drafting;
        _upgrade.Disabled = !Drafting || NetworkPending || ShopLevel >= MaxShopLevel || Coins < UpgradeCost;
        _upgradeLabel.Text = ShopLevel >= MaxShopLevel ? "MAX" : $"UP {UpgradeCost}";
        _upgrade.TooltipText = ShopLevel >= MaxShopLevel ? "Shop Lv.6 / maximum level" : $"Upgrade Lv.{ShopLevel} to Lv.{ShopLevel+1} / {UpgradeCost} coin / next-round discount 2 / unlock cards up to {ShopLevel+1} coin";
        _reroll.Disabled = !Drafting || NetworkPending || Coins < RerollCost;
        _lock.Disabled = !Drafting || NetworkPending;
        _lockLabel.Text = ShopLocked ? "LOCKED" : "LOCK";
        _lock.TooltipText = ShopLocked ? "Shop locked / keep remaining offers next round / click to unlock" : "Lock shop / keep remaining offers next round / free";
        _battleActions.Visible=Drafting || (Finished && !GameOver);
        if(Finished) {
            _battleActions.AnchorLeft=_battleActions.AnchorRight=.5f;
            _battleActions.OffsetLeft=-74;_battleActions.OffsetRight=74;
            _battleActions.OffsetTop=68;_battleActions.OffsetBottom=112;
            _next.Position=Vector2.Zero;
        } else {
            _battleActions.AnchorLeft=_battleActions.AnchorRight=1;
            _battleActions.OffsetLeft=-164;_battleActions.OffsetRight=-16;
            _battleActions.OffsetTop=-52;_battleActions.OffsetBottom=52;
            _next.Position=new Vector2(0,48);
        }
        RefreshUpgradeHints();
        LayoutRevision++;
    }

    private void RefreshUpgradeHints()
    {
        var matchingTokens=new HashSet<int>();
        for(int i=0;i<_shopViews.Count;i++) {
            var owned=i<Offers.Count && Drafting ? OwnedCopy(Offers[i].Token) : null;
            bool upgrade=owned!=null && owned.Stars<4;
            UpgradeHint.Set(_shopViews[i],upgrade);
            if(upgrade)matchingTokens.Add(owned!.Token);
        }
        for(int i=0;i<_handViews.Count;i++)
            UpgradeHint.Set(_handViews[i],Drafting && i<Hand.Count && matchingTokens.Contains(Hand[i].Token));
        foreach(var unit in _units)
            if(GodotObject.IsInstanceValid(unit) && !unit.IsQueuedForDeletion())
                UpgradeHint.Set(unit,Drafting && unit.IsAlly && Deployed.ContainsKey(unit.CardToken) && matchingTokens.Contains(unit.CardToken));
    }

    public BattleUnit? PickUnit(Vector2 position) => Drafting ? PickFrom(position, true) : null;
    public BattleUnit? PickDetailUnit(Vector2 position) => PickFrom(position,false);
    private BattleUnit? PickFrom(Vector2 position, bool alliesOnly)
    {
        position = Field.ToLocal(position);
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
    public bool CanReturnUnit(BattleUnit unit) => Drafting && !ReadyForBattle && !NetworkPending && Hand.Count < HandLimit && IsDeployed(unit) && !unit.HasBattled;
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
                occupant.Position = Field.ToLocal(Tiles.ToGlobal(SlotCenters[oldSlot])); Occupants[oldSlot] = occupant;
            }
            else Occupants[oldSlot] = null;
            moving.GridSlot = slot; moving.SetMeta("grid_cell",DeploymentCells[slot]);
            moving.Position = Field.ToLocal(Tiles.ToGlobal(SlotCenters[slot])); Occupants[slot] = moving;
            _battleActions.Visible=Drafting || (Finished && !GameOver);
        LayoutRevision++; return true;
        }
        int index = FindToken(Hand,data.Token); var card = Hand[index]; Hand.RemoveAt(index);
        var definition=CharacterData.Get(_pool.FindIndex(c=>c.Name==card.Name));
        var unit = GD.Load<PackedScene>(definition.ScenePath).Instantiate<BattleUnit>();
        unit.Name = $"Deployed_{card.Token}"; unit.Position = Field.ToLocal(Tiles.ToGlobal(SlotCenters[slot]));
        unit.HasBattled = card.Veteran; unit.CardKind = _pool.FindIndex(c=>c.Name==card.Name); unit.ConfigureStars(card.Stars); unit.CardToken = card.Token; unit.GridSlot = slot;
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
        int index = FindToken(Hand,token); Coins += Hand[index].Investment / 2;
        Hand.RemoveAt(index); CloseDetails(); Refresh();
        EmitSignal(SignalName.HandChanged,Hand.Count); return true;
    }
    public bool CanSell(CardDrag? data) => Drafting && !NetworkPending && data != null && data.Shop == this &&
        (data.Unit != null ? IsDeployed(data.Unit) : !data.FromShop && AcceptsHandToken(data.Token));
    public void SellDrop(CardDrag data)
    {
        if (!CanSell(data)) return;
        if (NetworkMode) { Online?.SendAction("sell",data.Unit?.CardToken ?? data.Token); return; }
        if (data.Unit == null) { SellCard(data.Token); return; }
        Coins += Deployed[data.Unit.CardToken].Card.Investment / 2;
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

    private void ShowDetails(string title,string team,int health=100,int maximum=100,int stars=1,int kind=0)
    {
        var c=CharacterData.Get(kind);var stats=CharacterData.Stats(kind,stars);
        DetailsText.Text=$"{title}\n{team}\n\n{stars} STARS\nHP  {health} / {maximum}\nAttack  {stats.Attack}\nDamage  {stats.DamageMin}–{stats.DamageMax}\nSpeed  {stats.Speed}\n\n{c.Description}\n\nABILITY / {c.AbilityName}\n{c.AbilityDescription}\n\nPASSIVE / {c.PassiveName}\n{c.PassiveDescription}";
        _modalRoot.Show();Details.Show();
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
        Details.Hide();_modalRoot.Hide();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(Details.Visible && input is InputEventKey {Keycode:Key.Escape,Pressed:true}){CloseDetails();GetViewport().SetInputAsHandled();}
    }
    public void ShowCardDetails(int token)
    {
        Uninspect();
        int index = FindToken(Offers,token);
        if (index >= 0) { {int kind=_pool.FindIndex(c=>c.Name==Offers[index].Name);var stats=CharacterData.Stats(kind,1);ShowDetails(Offers[index].Name,NetworkMode ? $"PLAYER {LocalTeam} / SHOP" : "SHOP CARD",stats.Hp,stats.Hp,1,kind);} return; }
        index = FindToken(Hand,token);
        if (index >= 0) {int kind=_pool.FindIndex(c=>c.Name==Hand[index].Name);var stats=CharacterData.Stats(kind,Hand[index].Stars);ShowDetails(Hand[index].Name,$"PLAYER {(NetworkMode ? LocalTeam : "A")} / HAND",stats.Hp,stats.Hp,Hand[index].Stars,kind);}
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
        ShowDetails(title, $"{(NetworkMode ? "PLAYER " + unit.ServerId.Split(':')[0] : (unit.IsAlly ? "PLAYER A" : "PLAYER B"))} / FIELD / {(unit.IsDead ? "DEAD" : "ALIVE")}", unit.Health,unit.MaxHealth,unit.Stars,unit.CardKind);
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
        LocalTeam = self.Team; Coins = self.Coins; ShopLocked = self.ShopLocked; ShopLevel = self.ShopLevel; UpgradeCost = self.UpgradeCost;
        int opponentCards = 0;
        foreach (var player in state.Players) if (player != null && player.UserId != userId) opponentCards = System.Math.Clamp(player.HandCount,0,HandLimit);
        for (int i = 0; i < _opponentBacks.Count; i++) _opponentBacks[i].Visible = i < opponentCards;
        _opponentHandLabel.Text = $"OPPONENT / HAND {opponentCards}/{HandLimit}";
        OpponentHandZone.Visible = state.Phase == "battle";
        var cells = DeploymentCells;
        for (int i = 0; i < FieldLimit; i++) SlotCenters[i] = Tiles.MapToLocal(cells[i]);
        TurnNumber = state.Round;
        Drafting = state.Phase == "preparation";
        ReadyForBattle = Drafting && self.Ready;
        Finished = state.Phase is "finished" or "game_over";
        GameOver = state.Phase == "game_over";
        var priorHandStars = new Dictionary<int,int>(); foreach(var card in Hand) priorHandStars[card.Token] = card.Stars; foreach(var entry in Deployed.Values) priorHandStars[entry.Card.Token] = entry.Card.Stars;
        Offers.Clear(); Hand.Clear();
        foreach (var card in self.Offers) Offers.Add(_pool[card.Kind] with { Token = card.Token, Price = card.Price, Stars = Mathf.Max(1,card.Stars), Paid = card.Paid, Veteran = card.Veteran });
        foreach (var card in self.Hand) Hand.Add(_pool[card.Kind] with { Token = card.Token, Price = card.Price, Stars = Mathf.Max(1,card.Stars), Paid = card.Paid, Veteran = card.Veteran });
        if (state.Phase is "preparation" or "waiting" || enteringBattle)
        {
            ReconcileUnits(state,newRound || enteringBattle);
            if (newRound || enteringBattle) CloseDetails();
        }
        _networkRound = state.Round; _networkPhase = state.Phase;
        Refresh();
        foreach(var card in Hand) if(priorHandStars.TryGetValue(card.Token,out int oldStars) && card.Stars > oldStars) PlayHandUpgrade(card.Token);
        if(_inspectedUnit != null) UpdateUnitDetails();
        ZoneB.Visible = state.Phase != "waiting";
        ZoneA.Visible = state.Phase == "preparation";
        if (state.Phase == "preparation")
        {
            ShopRow.Show(); _shopLabel.Show();
            _next.Disabled = self.Ready || NetworkPending;
            _nextText.Text = self.Ready ? "READY" : "BATTLE";
            _notice.Text = $"FIELD {Deployed.Count}/{FieldLimit}" + (self.Ready ? (state.Roster.Length>0 ? " / WAITING FOR PLAYERS" : " / WAITING FOR OPPONENT") : "");
        }
        else if (Finished)
        {
            ShopRow.Hide(); _shopLabel.Hide();
            _next.Disabled = GameOver || NetworkPending || self.Ready;
            _nextText.Text = GameOver ? "GAME OVER" : self.Ready ? "WAITING" : "NEXT ROUND";
            _notice.Text = GameOver ? $"PLAYER {state.Winner} WINS MATCH" : $"BATTLE FINISHED / LOST {self.LastDamage} HP";
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
                bool existed = NetworkUnits.TryGetValue(id,out var unit);
                if (!existed)
                {
                    var definition=CharacterData.Get(entry.Kind);
                    unit = GD.Load<PackedScene>(definition.ScenePath).Instantiate<BattleUnit>();
                    unit.CardKind=entry.Kind;unit.ConfigureStars(entry.Stars);
                    unit.Name = $"Online_{player.Team}_{entry.Token}";
                    unit.SetMeta("team",owned ? "Ally" : "Enemy");
                    unit.GetNode<AnimatedSprite2D>("AnimatedSprite2D").Material = owned ? _playerMaterial : _redMaterial;
                    Field.AddChild(unit);
                    _units.Add(unit); NetworkUnits.Add(id,unit);
                }
                unit!.CardKind=entry.Kind;unit.ConfigureStars(entry.Stars,existed && state.Phase == "preparation"); unit.CardToken = entry.Token; unit.GridSlot = entry.Slot;
                unit.HasBattled = entry.Veteran; unit.ServerId = id;
                var cell = owned ? DeploymentCells[entry.Slot] : EnemyCells[FieldLimit - 1 - entry.Slot];
                unit.SetMeta("grid_cell",cell);
                unit.Position = Field.ToLocal(Tiles.ToGlobal(Tiles.MapToLocal(cell)));
                unit.Sprite.FlipH = !owned;
                if (resetHealth) { unit.ResetHealth(); unit.Sprite.Play(BattleAnimations.Idle); }
                if (player.Team == LocalTeam)
                {
                    var card = _pool[entry.Kind] with { Token = entry.Token, Stars = Mathf.Max(1,entry.Stars), Paid = entry.PurchasePrice, Veteran = entry.Veteran };
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
