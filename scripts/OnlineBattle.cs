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
    private GameModal? _defeatModal;
    private BloodScreenEffect _blood=null!;
    private TextureButton _defeatLobby=null!;
    private double _defeatRemaining;
    private DeckDefinition? _selectedDeck;
    private bool _launchPending, _launchCreate, _matchmaking;
    private Label _roster = null!;
    private PanelContainer _rosterPanel=null!;
    private Label _roundTitle=null!;
    private string _launchCode = "";
    private GameModal _surrenderModal=null!;
    private TextureButton _matchLobby=null!, _back=null!;
    private volatile bool _transportLost;
    private string _message = "";
    private string _displayedStatus = "";
    private readonly System.Collections.Generic.Dictionary<string,string> _rosterNames=new();
    private bool _fetchingRosterNames;

    public override void _Ready()
    {
        _battle = GetParent().GetNode<BattleDemo>("BattleDemo");
        if (!_battle.NetworkEnabled || !_battle.AutoStart) { SetProcess(false); return; }
        _tree = GetTree(); _shop = GetParent().GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        _shop.Online = this; _shop.ZoneA.Hide(); _shop.ZoneB.Hide();
        _blood=new BloodScreenEffect();AddChild(_blood);
        _shop.CombatHitApplied+=_blood.Apply;_battle.CombatHitProgress+=_blood.Preview;
        _blood.LastUnitDied+=ShowDefeat;
        _profileA = GetParent().GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileA");
        _profileB = GetParent().GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileB");
        _profileB.Hide();
        _selectedDeck = BattleLaunch.Deck?.Clone();
        _launchPending = BattleLaunch.Pending; _launchCreate = BattleLaunch.Create; _launchCode = BattleLaunch.Code;
        _matchmaking=BattleLaunch.Matchmaking;BattleLaunch.Matchmaking=false;
        BattleLaunch.Pending = false;
        MatchReplayStore.Clear();
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
        var label = new Label { Text = text, Modulate = Colors.White, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,MouseFilter = Control.MouseFilterEnum.Ignore };
        GameUi.Label(label,13);button.AddChild(label); label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.Pressed += action; return button;
    }
    private void BuildUi()
    {
        var panel = new VBoxContainer { Name = "RoomUI",MouseFilter = Control.MouseFilterEnum.Ignore };
        var frame=new PanelContainer {Name="ArenaStatus",Position=new(16,76),CustomMinimumSize=new(230,0)};_rosterPanel=frame;
        frame.AddThemeStyleboxOverride("panel",GameUi.Box());
        GetParent().GetNode<Control>("UI/SafeArea/Content").AddChild(frame);frame.AddChild(panel);
        panel.AddThemeConstantOverride("separation",9);
        var content=GetParent().GetNode<Control>("UI/SafeArea/Content");
        var back=Button("SURRENDER",ShowSurrender,100);back.Name="BackButton";back.Position=new(16,16);content.AddChild(back);
        _back=back;
        _matchLobby=Button("BACK TO LOBBY",ReturnToLobby,180);_matchLobby.Name="MatchLobbyButton";
        content.AddChild(_matchLobby);_matchLobby.AnchorLeft=_matchLobby.AnchorRight=_matchLobby.AnchorTop=_matchLobby.AnchorBottom=.5f;
        _matchLobby.OffsetLeft=-90;_matchLobby.OffsetRight=90;_matchLobby.OffsetTop=68;_matchLobby.OffsetBottom=112;
        GameUi.Primary(_matchLobby);_matchLobby.Hide();
        _roundTitle=new Label {Name="RoundTitle",Text="Searching",AnchorLeft=.5f,AnchorRight=.5f,OffsetLeft=-220,OffsetRight=220,OffsetTop=8,OffsetBottom=44,HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};GameUi.Label(_roundTitle,24);content.AddChild(_roundTitle);
        _identity = new Label { Text = "CONNECTING TO THE ARENA",MouseFilter = Control.MouseFilterEnum.Ignore };
        GameUi.Label(_identity,13);_identity.AddThemeColorOverride("font_color",GameUi.Gold);panel.AddChild(_identity);_identity.Hide();
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
        _status = new Label { Text = "Starting connection...",AutowrapMode = TextServer.AutowrapMode.WordSmart,CustomMinimumSize = new(250,48),MouseFilter = Control.MouseFilterEnum.Ignore };
        GameUi.Label(_status,13);panel.AddChild(_status);_status.Hide();
        _roster=new Label {MouseFilter=Control.MouseFilterEnum.Ignore};GameUi.Label(_roster,13,true);_roster.AddThemeConstantOverride("line_spacing",8);panel.AddChild(_roster);
        if (_matchmaking) _roomControls.Hide();
        _create.Disabled = _join.Disabled = true;
        if (_matchmaking) frame.Hide();
    }
    public void ShowSurrender()
    {
        if(_exiting)return;
        if(State?.Phase=="game_over") {ReturnToLobby();return;}
        _shop.CloseDetails();
        if(_surrenderModal==null){
            var layer=new CanvasLayer {Layer=30};AddChild(layer);_surrenderModal=new GameModal();layer.AddChild(_surrenderModal);
            _surrenderModal.Body.AddChild(DeckMenuUi.Text("SURRENDER?",26));
            var message=DeckMenuUi.Text("Leave this match and return to the lobby?",15);message.AutowrapMode=TextServer.AutowrapMode.WordSmart;_surrenderModal.Body.AddChild(message);
            var buttons=new HBoxContainer();_surrenderModal.Body.AddChild(buttons);
            buttons.AddChild(DeckMenuUi.Button("CANCEL",CancelSurrender,156,42));
            buttons.AddChild(DeckMenuUi.Button("SURRENDER",ReturnToLobby,156,42));
            _surrenderModal.Cancel=CancelSurrender;
        }
        _surrenderModal.Show();
    }
    private void CancelSurrender()
    {
        _surrenderModal.Hide();
        // A queued cancel from the old dialog must not strand a completed match.
        if(State?.Phase=="game_over" && _defeatModal==null)ReturnToLobby();
    }
    public async void ReturnToLobby()
    {
        if(_exiting)return;_exiting=true;_replayGeneration++;Connected=false;_shop.LockNetworkInteraction();SetProcess(false);
        MatchReplayStore.Leave();
        if(Connection!=null)await Connection.Close();
        if(IsInsideTree())_tree.ChangeSceneToFile("res://scenes/lobby.tscn");
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
            _identity.Text = "COMMANDER CONNECTED";
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
            _message = "Waiting for your opponent...";
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
        if(_exiting)return;
        if(_presentationPlaying)_presentationTimeMs+=delta*1000;
        if (Connection != null && Connected) _battle.ServerVisualTimeMs = Connection.ServerNowMs;
        if(_defeatModal!=null) {
            while(_states.TryDequeue(out var finalState)){State=finalState;MatchReplayStore.Observe(finalState,UserId);}
            _defeatRemaining-=delta/Engine.TimeScale;
            _defeatLobby.GetChild<Label>(0).Text=$"BACK TO LOBBY ({Math.Clamp((int)Math.Ceiling(_defeatRemaining),0,15)}s)";
            if(_defeatRemaining<=0)ReturnToLobby();
            return;
        }
        while (_errors.TryDequeue(out var error))
        {
            bool locked=error.Message.Contains("preparation is locked",StringComparison.OrdinalIgnoreCase);
            _message = locked?"":error.Message;
            if (error.Sequence == 0 || error.Sequence >= _pendingSequence)
            {
                _shop.NetworkPending = false;
                if (State != null) _shop.ApplyNetworkState(State,UserId);
            }
            if(locked)_battle.ShowResultMessage("PREPARATION IS LOCKED");
        }
        while (_states.TryDequeue(out var state))
        {
            if (state.AckSequence >= _pendingSequence) _shop.NetworkPending = false;
            if (state.Revision < _lastRevision) continue;
            bool changed = state.Revision != _lastRevision;
            State = state;_lastRevision = state.Revision;
            MatchReplayStore.RecordPreparation(state,UserId);
            MatchReplayStore.Observe(state,UserId);
            if(state.Roster.Any(p=>p.UserId==UserId && p.Hp<=0) || state.Players.Any(p=>p!=null && p.UserId==UserId && p.Hp<=0)){ShowDefeat();return;}
            if (!changed) continue;
            _message = ""; _roomControls.Hide();
            _shop.ApplyNetworkState(state,UserId);
            PositionProfiles(_shop.LocalTeam);
            _profileA.Visible=state.Players.Any(p=>p?.Team=="A" && !string.IsNullOrWhiteSpace(p.UserId));_profileB.Visible=state.Players.Any(p=>p?.Team=="B" && !string.IsNullOrWhiteSpace(p.UserId));
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

                UpdateRosterNames(state);
                FetchRosterNames();
                if (state.Phase=="eliminated") {_replayGeneration++;_battle.HideResult();}
            }
            _identity.Text = $"ARENA  /  ROUND {state.Round:00}";
            if (state.Phase == "preparation")
            { _blood.Clear();_battle.HideResult();if (_planRound != state.Round) _replayGeneration++; }
            if (state.Phase == "battle" && state.Battle != null && _planRound != state.Round)
            {
                MatchReplayStore.Record(state,UserId);
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
                "waiting" => "Waiting for your opponent...",
                "preparation" => State.Roster.Length>0 ? $"PREPARATION {remaining:00}s / {State.Roster.Count(p=>p.Hp>0&&p.Ready)}/{State.Roster.Count(p=>p.Hp>0)} READY" : $"PREPARATION {remaining:00}s / A {a} / B {b}",
                "battle" => State.Roster.Length>0
                    ? (Connection!.ServerNowMs >= (State.Battle?.EndMs ?? long.MaxValue)
                        ? $"OTHER BATTLES IN PROGRESS / {Math.Max(0,(State.DeadlineMs-Connection.ServerNowMs+999)/1000)}s MAX"
                        : $"BATTLE / {Math.Max(0,(State.DeadlineMs-Connection.ServerNowMs+999)/1000)}s MAX")
                    : "BATTLE IN PROGRESS",
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
        bool playing=State!=null && State.Round>0 && State.Phase!="waiting";
        _roundTitle.Text=playing?$"Round {State!.Round}":"Searching";
        _roundTitle.Visible=!playing || State!.Phase=="preparation";
        _rosterPanel.Visible=playing && State!.Roster.Length>0 || (!playing && !_matchmaking);
        _status.Visible=!string.IsNullOrEmpty(_message) && !_message.StartsWith("SEARCHING") && !_message.Contains("Searching");
        if (_status.Visible) _rosterPanel.Show();
        if (status != _displayedStatus) { _displayedStatus = status;_status.Text = status; }
    }
    private void ShowFinalResult(OnlineState state)
    {
        bool won=state.Roster.Length>0 ? state.WinnerId==UserId : state.Winner==LocalTeam;
        if(!won){ShowDefeat();return;}
        if(_surrenderModal!=null)_surrenderModal.Hide();
        _matchLobby.Show();
        _back.GetChild<Label>(0).Text="LOBBY";
        if (state.Roster.Length==0) _battle.ShowMatchResult(state.Winner);
        else _battle.ShowLeagueResult(state.WinnerId==UserId,state.Roster.FirstOrDefault(p=>p.UserId==UserId)?.Place??0);
    }
    private void ShowDefeat()
    {
        if(_defeatModal!=null || _exiting)return;
        if(_blood.Armed&&!_blood.Eliminated)return;
        _replayGeneration++;
        _surrenderModal?.Hide();_shop.CloseDetails();_shop.LockNetworkInteraction();
        _battle.HideResult();_matchLobby.Hide();_roundTitle.Hide();
        _defeatRemaining=20;
        var layer=new CanvasLayer {Layer=31};AddChild(layer);
        _defeatModal=new GameModal {Name="DefeatModal"};layer.AddChild(_defeatModal);
        _blood.Complete(!_shop.NetworkUnits.Values.Any(unit=>unit.IsAlly&&!unit.IsDead));
        var effect=_blood.Image;effect.Reparent(_defeatModal);_defeatModal.MoveChild(effect,1);effect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var title=DeckMenuUi.Text("DEFEAT",32);title.HorizontalAlignment=HorizontalAlignment.Center;_defeatModal.Body.AddChild(title);
        var buttons=new HBoxContainer();buttons.AddThemeConstantOverride("separation",12);_defeatModal.Body.AddChild(buttons);
        _defeatLobby=DeckMenuUi.Button("BACK TO LOBBY (15s)",ReturnToLobby,220,44);_defeatLobby.Name="DefeatLobbyButton";buttons.AddChild(_defeatLobby);
        var replay=DeckMenuUi.Button("WATCH REPLAY",WatchReplay,180,44);replay.Name="WatchReplayButton";replay.Disabled=MatchReplayStore.Rounds.Count==0;buttons.AddChild(replay);
        bool replayAvailable=!replay.Disabled;_defeatLobby.Disabled=replay.Disabled=true;
        var fade=_defeatModal.FadeIn(5);
        fade.Finished+=()=>{if(!_exiting){_defeatLobby.Disabled=false;replay.Disabled=!replayAvailable;}};
    }
    private async void WatchReplay()
    {
        if(_exiting || MatchReplayStore.Rounds.Count==0)return;
        _exiting=true;_replayGeneration++;Connected=false;SetProcess(false);_shop.LockNetworkInteraction();
        if(Connection!=null)await Connection.Close();
        if(IsInsideTree())_tree.ChangeSceneToFile("res://scenes/match_replay.tscn");
    }
    private void UpdateRosterNames(OnlineState state)
    {
        _roster.Text=string.Join("\n",state.Roster.Select((p,i)=> {
            string name=_rosterNames.TryGetValue(p.UserId,out var known) && !string.IsNullOrWhiteSpace(known) ? known : p.UserId==UserId ? "YOU" : p.Bot ? "BOT" : "PLAYER";
            var letters=new System.Globalization.StringInfo(name);
            if(letters.LengthInTextElements>14)name=letters.SubstringByTextElements(0,13)+"…";
            return $"{i+1:00}   {name}   {p.Hp:00} HP{(p.Place>0 ? $" / #{p.Place}" : "")}";
        }));
    }
    private async void FetchRosterNames()
    {
        if(_fetchingRosterNames || State==null || Connection?.Session==null)return;
        var ids=State.Roster.Where(p=>!p.Bot && !string.IsNullOrWhiteSpace(p.UserId) && !_rosterNames.ContainsKey(p.UserId)).Select(p=>p.UserId).Distinct().ToArray();
        if(ids.Length==0)return;
        _fetchingRosterNames=true;
        try {
            var users=await Connection.Client.GetUsersAsync(Connection.Session,ids);
            if(_exiting || !IsInsideTree())return;
            foreach(var user in users.Users)_rosterNames[user.Id]=string.IsNullOrWhiteSpace(user.DisplayName)?user.Username:user.DisplayName;
            foreach(var id in ids)if(!_rosterNames.ContainsKey(id))_rosterNames[id]="";
            if(State!=null)UpdateRosterNames(State);
        } catch(Exception e) {if(!_exiting)GD.Print("Roster names unavailable: "+e.Message);}
        finally {_fetchingRosterNames=false;}
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
    private double _presentationTimeMs;
    private bool _presentationPlaying;
    private async Task WaitForPresentation(long time,int generation)
    {
        while(_presentationTimeMs<time&&Connection!.ServerNowMs<time){
            if(_exiting||generation!=_replayGeneration)throw new OperationCanceledException();
            await ToSignal(_tree,SceneTree.SignalName.ProcessFrame);
        }
        if(_exiting||generation!=_replayGeneration)throw new OperationCanceledException();
    }
    private async void PlayReplay(OnlinePlan plan, int generation)
    {
        try
        {
            _shop.SetReplayPlan(plan);
            int playerHp=State?.Players.FirstOrDefault(p=>p?.UserId==UserId)?.Hp??int.MaxValue;
            _blood.Prepare(plan,LocalTeam,playerHp<=plan.PlayerDamage);
            _presentationTimeMs=Math.Max(plan.StartMs,Connection!.ServerNowMs);_presentationPlaying=true;_battle.SpeedEnabled=true;
            foreach (var combat in plan.Events)
            {
                if (Connection!.ServerNowMs >= plan.EndMs || combat.AtMs >= plan.EndMs) break;
                await WaitForPresentation(combat.AtMs,generation);
                if (Connection!.ServerNowMs >= plan.EndMs) break;
                if (!_shop.NetworkUnits.TryGetValue(combat.Attacker,out var attacker))
                    throw new InvalidOperationException("Replay unit is missing.");
                var target=_shop.NetworkUnits.TryGetValue(combat.Target,out var existingTarget)?existingTarget:attacker;
                if (Connection!.ServerNowMs >= combat.AtMs + 2400)
                {
                    _shop.ApplyCombatEvent(combat);
                    continue;
                }
                await _battle.PlayServerEvent(attacker,target,combat,plan.EndMs);
            }
            if(_exiting||generation!=_replayGeneration)throw new OperationCanceledException();
            _presentationPlaying=false;_battle.SpeedEnabled=false;
            await WaitUntil(plan.EndMs,generation);
            // Resolve skipped animation events without changing the computed outcome.
            foreach (var combat in plan.Events)
                _shop.ApplyCombatEvent(combat);
            if (State?.Phase == "game_over") ShowFinalResult(State);
            else _battle.ShowServerResult(plan.Winner);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (!_exiting) { _message = "Replay error: " + exception.Message;GD.PushError(exception.ToString()); } }
        finally {if(generation==_replayGeneration){_presentationPlaying=false;_battle.SpeedEnabled=false;}}
    }
    public override void _ExitTree()
    {
        if(_blood!=null){_shop.CombatHitApplied-=_blood.Apply;_battle.CombatHitProgress-=_blood.Preview;_blood.LastUnitDied-=ShowDefeat;}
        _exiting = true;_replayGeneration++;
        if (Connection != null) _ = Connection.Close();
    }
}
