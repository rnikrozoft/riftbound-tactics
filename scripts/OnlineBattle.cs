using Godot;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public partial class OnlineBattle : Node
{
    [Export] public string Host { get; set; } = "127.0.0.1";
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
    private Texture2D _sheet = null!;
    private SceneTree _tree = null!;
    private long _sequence, _pendingSequence, _lastRevision = -1;
    private int _planRound, _replayGeneration;
    private bool _exiting, _connecting;
    private volatile bool _transportLost;
    private string _message = "";
    private string _displayedStatus = "";

    public override void _Ready()
    {
        _battle = GetParent().GetNode<BattleDemo>("BattleDemo");
        if (!_battle.NetworkEnabled || !_battle.AutoStart) { SetProcess(false); return; }
        _tree = GetTree(); _shop = GetParent().GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        _shop.Online = this; _shop.ZoneA.Hide(); _shop.ZoneB.Hide();
        _sheet = GD.Load<Texture2D>("res://assets/cards/pixelCardAssest_V01.png");
        BuildUi();
        Callable.From(ConnectDebugUser).CallDeferred();
    }
    private AtlasTexture Atlas(Rect2 rect) => new() { Atlas = _sheet,Region = rect };
    private TextureButton Button(string text, Action action, int width)
    {
        var button = new TextureButton {
            TextureNormal = Atlas(new(16,223,96,29)), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
            CustomMinimumSize = new(width,30), TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        var label = new Label { Text = text, Modulate = Colors.Black, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,MouseFilter = Control.MouseFilterEnum.Ignore };
        button.AddChild(label); label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.Pressed += action; return button;
    }
    private void BuildUi()
    {
        var panel = new VBoxContainer { Name = "RoomUI", Position = new(0,0),Size = new(330,120),MouseFilter = Control.MouseFilterEnum.Ignore };
        GetParent().GetNode<Control>("UI/SafeArea/Content").AddChild(panel);
        _identity = new Label { Text = "Connecting / new debug user...",MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(_identity);
        _roomControls = new HBoxContainer(); panel.AddChild(_roomControls);
        var inputFrame = new NinePatchRect {
            Texture = Atlas(new(22,137,86,71)), PatchMarginLeft = 8,PatchMarginRight = 8,PatchMarginTop = 8,PatchMarginBottom = 8,
            CustomMinimumSize = new(112,30),MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _roomControls.AddChild(inputFrame);
        var paper = new TextureRect { Texture = Atlas(new(24,484,76,100)),ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter = Control.MouseFilterEnum.Ignore };
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
        panel.AddChild(_status); _create.Disabled = _join.Disabled = true;
    }
    private async void ConnectDebugUser()
    {
        if (_connecting || _exiting) return;
        _connecting = true;
        try
        {
            string host = OS.GetEnvironment("RIFTBOUND_HOST");
            if (string.IsNullOrWhiteSpace(host)) host = Host;
            Connection = new(host,Port);
            Connection.StateReceived += state => _states.Enqueue(state);
            Connection.ErrorReceived += error => _errors.Enqueue(error);
            Connection.Disconnected += () => {
                _transportLost = true;
                _errors.Enqueue(new() { Message = "Connection closed. Restart the game to create a new debug user." });
            };
            await Connection.Connect();
            if (_exiting) { await Connection.Close(); return; }
            Connected = true;
            _identity.Text = "DEBUG USER " + UserId[..8];
            _create.Disabled = _join.Disabled = false;
            _message = "Create a room or enter a 6-digit code.";
        }
        catch (Exception exception)
        {
            if (_exiting) return;
            _message = "Backend unavailable: " + exception.Message;
            _create.Disabled = false;
            _create.GetChild<Label>(0).Text = "RETRY";
        }
        finally { _connecting = false; }
    }
    public void CreateRoom() { if (!Connected) { ConnectDebugUser(); return; } EnterRoom(true); }
    public void JoinRoom() { if (Connected) EnterRoom(false); }
    public void JoinRoom(string code) { _code.Text = code; JoinRoom(); }
    private async void EnterRoom(bool create)
    {
        if (_exiting || Connection == null || _create.Disabled) return;
        _create.Disabled = _join.Disabled = true;
        try
        {
            var room = await Connection.EnterRoom(create,_code.Text.Trim());
            if (_exiting) return;
            _code.Text = room.Code;_roomControls.Hide();
            _message = "Waiting for second player...";
        }
        catch (Exception exception)
        { if (!_exiting) { _message = exception.Message;_create.Disabled = _join.Disabled = false; } }
    }
    public async void SendAction(string type, int token = 0, int slot = 0)
    {
        if (_exiting || _transportLost || !Connected || Connection == null || State == null || _shop.NetworkPending) return;
        var action = new OnlineAction { Type = type,Token = token,Slot = slot,Round = State.Round,Sequence = ++_sequence };
        _pendingSequence = action.Sequence;_shop.NetworkPending = true;
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
            _identity.Text = $"ROOM {state.Code} / PLAYER {_shop.LocalTeam} / {UserId[..8]}";
            if (state.Phase == "preparation")
            { _battle.HideResult();if (_planRound != state.Round) _replayGeneration++; }
            if (state.Phase == "battle" && state.Battle != null && _planRound != state.Round)
            {
                _planRound = state.Round;
                PlayReplay(state.Battle,++_replayGeneration);
            }
        }
        if (_transportLost) { Connected = false; _shop.LockNetworkInteraction(); _replayGeneration++; _transportLost = false; }
        string status = _message;
        if (State != null && string.IsNullOrEmpty(status))
        {
            int remaining = State.Phase == "preparation" ? (int)Math.Max(0,(State.DeadlineMs - Connection!.ServerNowMs + 999)/1000) : 0;
            string a = State.Players[0]?.Ready == true ? "READY" : "PREPARING";
            string b = State.Players.Length > 1 && State.Players[1]?.Ready == true ? "READY" : "PREPARING";
            status = State.Phase switch {
                "waiting" => "Waiting for second player...",
                "preparation" => $"PREPARATION {remaining:00}s / A {a} / B {b}",
                "battle" => "BATTLE / SERVER REPLAY",
                "finished" => "BATTLE FINISHED / NEXT ROUND",
                _ => State.Phase
            };
            foreach (var player in State.Players)
                if (player != null && !player.Connected) status += $" / {player.Team} DISCONNECTED";
        }
        if (status != _displayedStatus) { _displayedStatus = status;_status.Text = status; }
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
                await WaitUntil(combat.AtMs,generation);
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
                await _battle.PlayServerEvent(attacker,target,combat);
            }
            await WaitUntil(plan.EndMs,generation);
            _battle.ShowServerResult(plan.Winner);
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



