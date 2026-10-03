using Godot;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;

public partial class OnlineBattle : Node
{
    [Export] public string Host { get; set; } = "127.0.0.1";
    [Export] public string LeaderboardId { get; set; } = NakamaConnection.MmrLeaderboardId;
    private PlayerProfile _profileA = null!, _profileB = null!;
    private int _profileRound = -1;
    [Export] public int Port { get; set; } = 7350;
    public NakamaConnection? Connection { get; private set; }
    public OnlineState? State { get; private set; }
    public bool Connected { get; private set; }
    public string UserId => Connection?.Session?.UserId ?? "";
    public string LocalTeam => _shop.LocalTeam;
    private readonly ConcurrentQueue<OnlineState> _states = new();
    private readonly ConcurrentQueue<OnlineError> _errors = new();
    private CardShop _shop = null!;
    private BattleDemo _battle = null!;
    private Label _status = null!, _identity = null!;
    private HBoxContainer _roomControls = null!;
    private LineEdit _code = null!;
    private TextureButton _create = null!, _join = null!;
    private SceneTree _tree = null!;
    private long _sequence, _pendingSequence, _lastRevision = -1;
    private int _planRound, _replayGeneration;
    private bool _exiting, _connecting;
    private DeckDefinition? _selectedDeck;
    private bool _launchPending, _launchCreate, _matchmaking;
    private Label _roster = null!;
    private string _launchCode = "";
    private volatile bool _transportLost;
    private string _message = "";
    private string _displayedStatus = "";

    public override void _Ready()
    {
        _battle = GetParent().GetNode<BattleDemo>("BattleDemo");
        if (!_battle.NetworkEnabled || !_battle.AutoStart) { SetProcess(false); return; }
        _tree = GetTree(); _shop = GetParent().GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        _shop.Online = this; _shop.ZoneA.Hide(); _shop.ZoneB.Hide();
        _profileA = GetParent().GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileA");
        _profileB = GetParent().GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileB");
        _selectedDeck = BattleLaunch.Deck?.Clone();
        _launchPending = BattleLaunch.Pending; _launchCreate = BattleLaunch.Create; _launchCode = BattleLaunch.Code;
        _matchmaking=BattleLaunch.Matchmaking;BattleLaunch.Matchmaking=false;
        BattleLaunch.Pending = false;
        BuildUi();
        Callable.From(ConnectDebugUser).CallDeferred();
    }
    private TextureButton Button(string text, Action action, int width)
    {
        var button = new TextureButton {
            TextureNormal = TravelBookUi.Texture("FrameSelect01a"), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
            CustomMinimumSize = new(width,30), TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        TravelBookUi.StyleButton(button);
        var label = new Label { Text = text, Modulate = Colors.Black, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,MouseFilter = Control.MouseFilterEnum.Ignore };
        button.AddChild(label); label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.Pressed += action; return button;
    }
    private void BuildUi()
    {
        var panel = new VBoxContainer { Name = "RoomUI", Position = new(12,12),Size = new(330,120),MouseFilter = Control.MouseFilterEnum.Ignore };
        GetParent().GetNode<Control>("UI/SafeArea/Content").AddChild(panel);
        _identity = new Label { Text = "Connecting / new debug user...",MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(_identity);
        _roomControls = new HBoxContainer(); panel.AddChild(_roomControls);
        var inputFrame = new NinePatchRect {
            Texture = TravelBookUi.Texture("Popup01a"), PatchMarginLeft = 4,PatchMarginRight = 4,PatchMarginTop = 4,PatchMarginBottom = 4,
            CustomMinimumSize = new(112,30),MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _roomControls.AddChild(inputFrame);
        var paper = new TextureRect { Texture = TravelBookUi.Texture("BookPageRight01a"),ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter = Control.MouseFilterEnum.Ignore };
        inputFrame.AddChild(paper);paper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        paper.OffsetLeft = paper.OffsetTop = 4;paper.OffsetRight = paper.OffsetBottom = -4;
        _code = new LineEdit { PlaceholderText = "Room code",MaxLength = 6,MouseFilter = Control.MouseFilterEnum.Stop };
        _code.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());_code.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
        _code.AddThemeColorOverride("font_color",Colors.Black);_code.AddThemeColorOverride("font_placeholder_color",new Color(.3f,.3f,.3f));
        inputFrame.AddChild(_code);_code.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _code.OffsetLeft = 6;_code.OffsetRight = -6;
        _code.TextSubmitted += code => JoinRoom();
        _create = Button("CREATE",() => CreateRoom(),90);_join = Button("JOIN",() => JoinRoom(),80);
        _roomControls.AddChild(_create);_roomControls.AddChild(_join);
        _status = new Label { Text = "Starting connection...",AutowrapMode = TextServer.AutowrapMode.WordSmart,CustomMinimumSize = new(330,48),MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(_status);
        _roster=new Label {MouseFilter=Control.MouseFilterEnum.Ignore};_roster.AddThemeFontSizeOverride("font_size",12);panel.AddChild(_roster);
        if (_matchmaking) _roomControls.Hide();
        _create.Disabled = _join.Disabled = true;
        panel.AddChild(Button("LOBBY",()=>GetTree().ChangeSceneToFile("res://scenes/lobby.tscn"),90));
    }
    private async void ConnectDebugUser()
    {
        if (_connecting || _exiting) return;
        _connecting = true;
        try
        {
            string host = OS.GetEnvironment("RIFTBOUND_HOST");
            if (string.IsNullOrWhiteSpace(host)) host = Host;
            Connection = new(host,Port,deviceId:_selectedDeck!=null?GameAccount.DeviceId:null);
            Connection.StateReceived += state => _states.Enqueue(state);
            Connection.ErrorReceived += error => _errors.Enqueue(error);
            Connection.Disconnected += () => {
                _transportLost = true;
                _errors.Enqueue(new() { Message = "Connection closed. Return to Lobby and search again." });
            };
            await Connection.Connect();
            if (_exiting) { await Connection.Close(); return; }
            Connected = true;
            _identity.Text = "DEBUG USER " + UserId[..8];
            _create.Disabled = _join.Disabled = false;
            _message = "Create a room or enter a 6-digit code.";
            if (_launchPending) { _launchPending=false; _code.Text=_launchCode; if (_matchmaking) FindMatch(); else EnterRoom(_launchCreate); }
            else if (_matchmaking && State==null) FindMatch();
        }
        catch (Exception exception)
        {
            if (_exiting) return;
            _message = "Backend unavailable: " + exception.Message;
            _create.Disabled = false;
            _create.GetChild<Label>(0).Text = "RETRY";
            if (_matchmaking) ShowMatchmakingRetry();
        }
        finally { _connecting = false; }
    }
    public void CreateRoom() { if (_matchmaking && Connected) {FindMatch();return;} if (!Connected) { ConnectDebugUser(); return; } EnterRoom(true); }
    public void JoinRoom() { if (Connected) EnterRoom(false); }
    public void JoinRoom(string code) { _code.Text = code; JoinRoom(); }
    private async void EnterRoom(bool create)
    {
        if (_exiting || Connection == null || _create.Disabled) return;
        _create.Disabled = _join.Disabled = true;
        try
        {
            var room = await Connection.EnterRoom(create,_code.Text.Trim(),_selectedDeck);
            if (_exiting) return;
            _code.Text = room.Code;_roomControls.Hide();
            _message = "Waiting for second player...";
        }
        catch (Exception exception)
        { if (!_exiting) { _message = exception.Message;_create.Disabled = _join.Disabled = false; } }
    }
    private void ShowMatchmakingRetry()
    {
        _roomControls.Show();_code.GetParent<Control>().Hide();_join.Hide();
        _create.GetChild<Label>(0).Text="RETRY";
    }
    private async void FindMatch()
    {
        if (Connection==null || _selectedDeck==null || _exiting) return;
        _create.Disabled=_join.Disabled=true;_roomControls.Hide();
        _message="SEARCHING / Nearby ranks / Bots fill empty seats";
        try
        {
            await Connection.FindMatch(_selectedDeck,elapsed=>_errors.Enqueue(new() {Message=$"SEARCHING {elapsed}s / Up to 20s / 6 players"}));
            if (!_exiting) _message=State==null?"Preparing a 6-player match...":"";
        }
        catch (OperationCanceledException) { }
        catch(Exception e) {if (!_exiting) {_message="Matchmaking failed: "+e.Message;_create.Disabled=false;ShowMatchmakingRetry();}}
    }
    public async void SendAction(string type, int token = 0, int slot = 0)
    {
        if (_exiting || _transportLost || !Connected || Connection == null || State == null || _shop.NetworkPending) return;
        var action = new OnlineAction { Type = type,Token = token,Slot = slot,Round = State.Round,Sequence = ++_sequence };
        _pendingSequence = action.Sequence;_shop.NetworkPending = true; _shop.RefreshNetworkControls();
        try { await Connection.Send(action); }
        catch (Exception exception) { _errors.Enqueue(new() { Message = exception.Message,Sequence = action.Sequence }); }
    }
    public override void _Process(double delta)
    {
        if (Connection != null && Connected) _battle.ServerVisualTimeMs = Connection.ServerNowMs;
        while (_errors.TryDequeue(out var error))
        {
            _message = error.Message;
            if (error.Sequence == 0 || error.Sequence >= _pendingSequence)
            {
                _shop.NetworkPending = false;
                if (State != null) _shop.ApplyNetworkState(State,UserId);
            }
        }
        while (_states.TryDequeue(out var state))
        {
            if (state.AckSequence >= _pendingSequence) _shop.NetworkPending = false;
            if (state.Revision < _lastRevision) continue;
            bool changed = state.Revision != _lastRevision;
            State = state;_lastRevision = state.Revision;
            if (!changed) continue;
            _message = ""; _roomControls.Hide();
            _shop.ApplyNetworkState(state,UserId);
            PositionProfiles(_shop.LocalTeam);
            _profileA.Visible=state.Players.Any(p=>p?.Team=="A");_profileB.Visible=state.Players.Any(p=>p?.Team=="B");
            foreach (var player in state.Players)
                if (player != null) { var profile = player.Team == "A" ? _profileA : _profileB; profile.SetCoins(player.Coins); profile.SetHealth(player.Hp); }
            if (state.Phase == "game_over") ShowFinalResult(state);
            bool refreshProfiles = _profileRound != state.Round || state.Phase == "game_over";
            _profileRound = state.Round;
            _profileA.SetPlayer(Array.Find(state.Players, p => p?.Team == "A")?.UserId ?? "", Connection, LeaderboardId, refreshProfiles);
            _profileB.SetPlayer(Array.Find(state.Players, p => p?.Team == "B")?.UserId ?? "", Connection, LeaderboardId, refreshProfiles);
            if (state.Roster.Length>0)
            {
                foreach(var player in state.Players)
                    if(player!=null) {var ranked=state.Roster.FirstOrDefault(p=>p.UserId==player.UserId);if(ranked!=null&&!ranked.Bot)(player.Team=="A"?_profileA:_profileB).SetMmr(ranked.Rating);}

                _roster.Text=string.Join("\n",state.Roster.Select((p,i)=>$"{(p.UserId==UserId ? "YOU" : p.Bot ? "BOT" : "PLAYER")} {i+1} / HP {p.Hp}/30{(p.Place>0 ? $" / #{p.Place}" : "")}"));
                if (state.Phase=="eliminated") {_replayGeneration++;_battle.HideResult();}
            }
            _identity.Text = $"ROOM {state.Code} / PLAYER {_shop.LocalTeam} / {UserId[..8]}";
            if (state.Phase == "preparation")
            { _battle.HideResult();if (_planRound != state.Round) _replayGeneration++; }
            if (state.Phase == "battle" && state.Battle != null && _planRound != state.Round)
            {
                _planRound = state.Round;
                PlayReplay(state.Battle,++_replayGeneration);
            }
        }
        if (_transportLost) { _shop.UpdateCountdown(0, false); Connected = false; _shop.LockNetworkInteraction(); _replayGeneration++; _transportLost = false; }
        string status = _message;
        if (State != null && string.IsNullOrEmpty(status))
        {
            int remaining = State.Phase == "preparation" ? (int)Math.Max(0,(State.DeadlineMs - Connection!.ServerNowMs + 999)/1000) : 0;
            _shop.UpdateCountdown(remaining, State.Phase == "preparation" && Connected);
            string a = State.Players[0]?.Ready == true ? "READY" : "PREPARING";
            string b = State.Players.Length > 1 && State.Players[1]?.Ready == true ? "READY" : "PREPARING";
            status = State.Phase switch {
                "waiting" => "Waiting for second player...",
                "preparation" => State.Roster.Length>0 ? $"PREPARATION {remaining:00}s / {State.Roster.Count(p=>p.Hp>0&&p.Ready)}/{State.Roster.Count(p=>p.Hp>0)} READY" : $"PREPARATION {remaining:00}s / A {a} / B {b}",
                "battle" => State.Roster.Length>0
                    ? (Connection!.ServerNowMs >= (State.Battle?.EndMs ?? long.MaxValue)
                        ? $"WAITING FOR OTHER BATTLES / SHOP LOCKED / {Math.Max(0,(State.DeadlineMs-Connection.ServerNowMs+999)/1000)}s MAX"
                        : $"BATTLE / {Math.Max(0,(State.DeadlineMs-Connection.ServerNowMs+999)/1000)}s MAX")
                    : "BATTLE / SERVER REPLAY",
                "finished" => "PREPARING NEXT ROUND...",
                "eliminated" => "ELIMINATED / Left match / Return to Lobby",
                "game_over" => State.Roster.Length>0 ? (State.WinnerId==UserId ? "MATCH OVER / YOU WIN" : "MATCH OVER / Final standings") : $"MATCH OVER / PLAYER {State.Winner} WINS",
                _ => State.Phase
            };
            if (State.Roster.Length>0 && State.Phase is "eliminated" or "game_over")
                status += State.MmrPending ? " / MMR updating" : $" / MMR {State.MmrDelta:+0;-0;0}";
            foreach (var player in State.Players)
                if (player != null && !player.Connected) status += $" / {player.Team} DISCONNECTED";
        }
        if (status != _displayedStatus) { _displayedStatus = status;_status.Text = status; }
    }
    private void ShowFinalResult(OnlineState state)
    {
        if (state.Roster.Length==0) _battle.ShowMatchResult(state.Winner);
        else _battle.ShowLeagueResult(state.WinnerId==UserId,state.Roster.FirstOrDefault(p=>p.UserId==UserId)?.Place??0);
    }
    private void PositionProfiles(string localTeam)
    {
        // Keep each profile tied to its team; swap the responsive screen positions for player B.
        foreach (var profile in new[] { _profileA, _profileB })
        {
            bool local = profile.Team == localTeam;
            profile.AnchorLeft = profile.AnchorRight = local ? 0 : 1;
            profile.OffsetLeft = local ? 12 : -284;
            profile.OffsetRight = local ? 284 : -12;
        }
    }
    private async Task WaitUntil(long time, int generation)
    {
        while (Connection!.ServerNowMs < time)
        {
            if (_exiting || generation != _replayGeneration) throw new OperationCanceledException();
            await ToSignal(_tree,SceneTree.SignalName.ProcessFrame);
        }
        if (_exiting || generation != _replayGeneration) throw new OperationCanceledException();
    }
    private async void PlayReplay(OnlinePlan plan, int generation)
    {
        try
        {
            foreach (var combat in plan.Events)
            {
                if (Connection!.ServerNowMs >= plan.EndMs || combat.AtMs >= plan.EndMs) break;
                await WaitUntil(combat.AtMs,generation);
                if (Connection!.ServerNowMs >= plan.EndMs) break;
                if (!_shop.NetworkUnits.TryGetValue(combat.Attacker,out var attacker) || !_shop.NetworkUnits.TryGetValue(combat.Target,out var target))
                    throw new InvalidOperationException("Replay unit is missing.");
                if (Connection!.ServerNowMs >= combat.AtMs + 2400)
                {
                    target.ApplyServerHealth(combat.TargetHp);
                    if (combat.Dead)
                    {
                        target.Sprite.Play(BattleAnimations.Die);
                        target.Sprite.SetFrameAndProgress(target.Sprite.SpriteFrames.GetFrameCount(BattleAnimations.Die)-1,0);
                        target.Sprite.Stop();
                    }
                    continue;
                }
                await _battle.PlayServerEvent(attacker,target,combat,plan.EndMs);
            }
            await WaitUntil(plan.EndMs,generation);
            // Resolve skipped animation events without changing the computed outcome.
            foreach (var combat in plan.Events)
                if (_shop.NetworkUnits.TryGetValue(combat.Target,out var finalTarget)) {
                    finalTarget.ApplyServerHealth(combat.TargetHp);
                    if (combat.Dead) { finalTarget.Sprite.Play(BattleAnimations.Die);finalTarget.Sprite.SetFrameAndProgress(finalTarget.Sprite.SpriteFrames.GetFrameCount(BattleAnimations.Die)-1,0);finalTarget.Sprite.Stop(); }
                }
            if (State?.Phase == "game_over") ShowFinalResult(State);
            else _battle.ShowServerResult(plan.Winner);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (!_exiting) { _message = "Replay error: " + exception.Message;GD.PushError(exception.ToString()); } }
    }
    public override void _ExitTree()
    {
        _exiting = true;_replayGeneration++;
        if (Connection != null) _ = Connection.Close();
    }
}



