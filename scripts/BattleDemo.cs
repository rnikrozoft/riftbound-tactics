using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum BattlePresentationMode { TurnBased, Brawl }

public partial class BattleDemo : Node
{
    [Signal] public delegate void TurnStartedEventHandler(string team, BattleUnit attacker, BattleUnit target);
    [Signal] public delegate void ImpactEventHandler(BattleUnit attacker, BattleUnit target);
    [Signal] public delegate void TurnFinishedEventHandler(string team, BattleUnit attacker);
    [Signal] public delegate void BattleFinishedEventHandler(string winner);
    [Export] public float StartDelay { get; set; } = .8f;
    [Export] public BattlePresentationMode PresentationMode {get;set;}=BattlePresentationMode.TurnBased;
    [Export] public float BrawlWalkSpeed {get;set;}=75;
    [Export] public bool NetworkEnabled { get; set; }
    [Export] public bool AutoStart { get; set; } = true;
    [Export] public bool CardShopEnabled { get; set; }
    [Export] public float TurnDelay { get; set; } = .45f;
    [Export] public float DashDuration { get; set; } = .24f;
    [Export] public float ReturnDuration { get; set; } = .32f;
    [Export] public float HitstopDuration { get; set; } = .075f;
    [Export] public float ShakeDuration { get; set; } = .2f;
    [Export] public float ShakeStrength { get; set; } = 2.5f;
    [Export] public float ArenaFloatAmplitude { get; set; } = 0f;
    [Export] public float ArenaFloatPeriod { get; set; } = 6f;
    [Export(PropertyHint.Range, "1,1000,1")] public int DamageMin { get; set; } = 25;
    [Export(PropertyHint.Range, "1,1000,1")] public int DamageMax { get; set; } = 40;

    private readonly List<BattleUnit> _teamA = new(6), _teamB = new(6);
    private readonly List<AnimatedSprite2D> _sprites = new(12);
    private float[] _speeds = new float[12];
    private int _frozenCount;
    private readonly RandomNumberGenerator _rng = new(), _shakeRng = new();
    private BattleDisplay _field = null!;
    private Control _result = null!;
    private Label _resultText = null!;
    private CardShop _shop = null!;
    private SceneTree _tree = null!;
    private float _shakeLeft, _floatTime;
    public long ServerVisualTimeMs { get; set; }
    private bool _exiting;
    private FinishingFocus _focus = null!;
    private BrawlCombatPlayback _brawl=null!;
    public bool BrawlActive=>_brawl!=null&&_brawl.Active;
    public void BeginCombatPresentation(OnlinePlan plan){EndCombatPresentation(true);if(PresentationMode==BattlePresentationMode.Brawl){_focus.Reset();_brawl.WalkSpeed=BrawlWalkSpeed;_brawl.Start(_shop,this,plan);}}
    public void EndCombatPresentation(bool restoreFormation=false){if(GodotObject.IsInstanceValid(_brawl))_brawl.Stop(restoreFormation);}
    internal void NotifyBrawlImpact(BattleUnit source,BattleUnit target){_shakeLeft=ShakeDuration;EmitSignal(SignalName.Impact,source,target);}
    internal void NotifyBrawlProgress(OnlineCombatHit hit,float progress)=>CombatHitProgress?.Invoke(hit,Math.Clamp(progress,0,1));
    private static readonly NodePath PositionPath = new("position");
    public int TurnCount { get; private set; }
    public bool SpeedEnabled {get;set;}
    public bool MatchEntered {get;set;}
    public event Action<OnlineCombatHit,float>? CombatHitProgress;

    public override void _Ready()
    {
        _rng.Randomize(); _shakeRng.Randomize();
        _tree = GetTree();
        _field = GetParent<BattleDisplay>();
        _result = GetParent().GetNode<Control>("UI/SafeArea/Content/BattleResult");
        _resultText = _result.GetNode<Label>("Text");
        _shop = GetParent().GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        _focus = new FinishingFocus { Name = "FinishingFocus" }; AddChild(_focus);
        _brawl=new BrawlCombatPlayback {Name="BrawlCombatPlayback"};AddChild(_brawl);
        CollectTeams();
        if (AutoStart && !NetworkEnabled) Callable.From(StartBattle).CallDeferred();
    }

    private void CollectTeams()
    {
        _teamA.Clear(); _teamB.Clear(); _sprites.Clear();
        foreach (var child in GetParent().GetChildren())
            if (child is BattleUnit unit && !unit.IsQueuedForDeletion() && !unit.IsDead)
            {
                (unit.IsAlly ? _teamA : _teamB).Add(unit);
                _sprites.Add(unit.Sprite);
            }
    }

    public override void _Process(double delta)
    {
        if (NetworkEnabled && ServerVisualTimeMs > 0)
            _floatTime = (float)((ServerVisualTimeMs / 1000.0) % Mathf.Max(.1f,ArenaFloatPeriod));
        else _floatTime += (float)delta;
        var offset = new Vector2(0, Mathf.Sin(_floatTime * Mathf.Tau / Mathf.Max(.1f, ArenaFloatPeriod)) * ArenaFloatAmplitude);
        if (_shakeLeft > 0)
        {
            _shakeLeft = Mathf.Max(0, _shakeLeft - (float)delta);
            float strength = ShakeStrength * _shakeLeft / Mathf.Max(.001f, ShakeDuration);
            if (NetworkEnabled && ServerVisualTimeMs > 0)
            {
                // Absolute server time keeps the shake pattern consistent across different frame rates.
                float phase = (float)(ServerVisualTimeMs % 10000) * .08f;
                offset += new Vector2(Mathf.Sin(phase),Mathf.Sin(phase * 1.7f)) * strength;
            }
            else offset += new Vector2(_shakeRng.RandfRange(-strength, strength), _shakeRng.RandfRange(-strength, strength));
        }
        _field.ArenaOffset = -offset * _field.Scale;
    }

    private long _serverEventDeadline;
    private void CheckAlive()
    {
        if (_exiting || !IsInsideTree() || (_serverEventDeadline>0 && ServerVisualTimeMs>=_serverEventDeadline)) throw new OperationCanceledException();
    }
    private async Task Delay(double seconds, bool ignoreTimeScale = false)
    {
        CheckAlive();
        double duration=ignoreTimeScale?seconds/Engine.TimeScale:seconds;
        await ToSignal(_tree.CreateTimer(Math.Max(.001, duration), false, false, ignoreTimeScale), SceneTreeTimer.SignalName.Timeout);
        CheckAlive();
    }
    private async Task Frame()
    {
        await ToSignal(_tree, SceneTree.SignalName.ProcessFrame);
        CheckAlive();
    }

    private async void StartBattle()
    {
        try
        {
            await Delay(StartDelay);
            if (!CardShopEnabled) { await RunCombat(); return; }
            for (int round = 1; !_exiting; round++)
            {
                _result.Hide();
                await _shop.PrepareTurn(round);
                CheckAlive();
                CollectTeams();
                await RunCombat();
                if (_shop.GameOver) break;
                await Delay(2.0);
                CheckAlive();
                _shop.ResetRoundUnits();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { GD.PushError(exception.ToString()); }
    }

    private async Task RunCombat()
    {
        await RunEffectsCombat();
    }
    private async Task RunEffectsCombat()
    {
        await _field.WaitForPhaseTransition();CheckAlive();
        CollectTeams();var units=new List<OnlineCombatUnit>();_shop.NetworkUnits.Clear();int token=0;
        foreach(var unit in _teamA){unit.ServerId=$"A:offline:{++token}";_shop.NetworkUnits[unit.ServerId]=unit;units.Add(new OnlineCombatUnit {Id=unit.ServerId,Team="A",Token=token,Kind=unit.CardKind,Stars=unit.Stars,Slot=unit.GridSlot>=0?unit.GridSlot:(token-1)%6});}
        token=0;foreach(var unit in _teamB){unit.ServerId=$"B:offline:{++token}";_shop.NetworkUnits[unit.ServerId]=unit;units.Add(new OnlineCombatUnit {Id=unit.ServerId,Team="B",Token=token,Kind=unit.CardKind,Stars=unit.Stars,Slot=unit.GridSlot>=0?unit.GridSlot:(token-1)%6});}
        var plan=new LocalCombat((int)_rng.Randi()).Simulate(units);_shop.SetReplayPlan(plan);
        BeginCombatPresentation(plan);SpeedEnabled=true;
        try {foreach(var combat in plan.Events){CheckAlive();var attacker=_shop.NetworkUnits[combat.Attacker];var target=_shop.NetworkUnits.TryGetValue(combat.Target,out var existing)?existing:attacker;await PlayServerEvent(attacker,target,combat);if(PresentationMode==BattlePresentationMode.TurnBased)await Delay(TurnDelay);}}
        finally {EndCombatPresentation();SpeedEnabled=false;}
        string winner=plan.Winner;CollectTeams();_resultText.Text=winner=="DRAW"?"DRAW":$"PLAYER {winner} WINS";_result.Show();
        if(CardShopEnabled){int stars=0;foreach(var survivor in winner=="A"?_teamA:_teamB)if(!survivor.IsSummoned)stars+=survivor.Stars;_shop.FinishBattle(winner,stars);if(_shop.GameOver)ShowMatchResult(winner);}
        EmitSignal(SignalName.BattleFinished,winner);
    }
    private async Task RunLegacyCombat()
    {
        bool aTurn = true;
        while (_teamA.Count > 0 && _teamB.Count > 0)
        {
            var attackers = aTurn ? _teamA : _teamB;
            var targets = aTurn ? _teamB : _teamA;
            var attacker = attackers[_rng.RandiRange(0, attackers.Count - 1)];
            var target = targets[_rng.RandiRange(0, targets.Count - 1)];
            string team = aTurn ? "A" : "B";
            TurnCount++;
            EmitSignal(SignalName.TurnStarted, team, attacker, target);
            await TakeTurn(attacker, target);
            EmitSignal(SignalName.TurnFinished, team, attacker);
            aTurn = !aTurn;
            if (_teamA.Count == 0 || _teamB.Count == 0) break;
            await Delay(TurnDelay * 10.0 / attacker.Speed);
        }
        string winner = _teamA.Count > 0 ? "A" : _teamB.Count > 0 ? "B" : "DRAW";
        _resultText.Text = winner == "DRAW" ? "DRAW" : $"PLAYER {winner} WINS";
        _result.Show();
        if (CardShopEnabled) {
            int stars = 0; foreach (var survivor in winner == "A" ? _teamA : _teamB) stars += survivor.Stars;
            _shop.FinishBattle(winner, stars);
            if (_shop.GameOver) ShowMatchResult(winner);
        }
        EmitSignal(SignalName.BattleFinished, winner);
    }

    private async Task TakeTurn(BattleUnit attacker, BattleUnit target, OnlineCombatEvent? serverEvent = null)
    {
        float speed = target.Sprite.SpeedScale;
        try { await TakeTurnPresentation(attacker,target,serverEvent); }
        finally {
            RestoreSprites();
            _focus.Reset();
            if (GodotObject.IsInstanceValid(target)) target.Sprite.SpeedScale = speed;
        }
    }

    private async Task TakeTurnPresentation(BattleUnit attacker, BattleUnit target, OnlineCombatEvent? serverEvent)
    {
        int damage = serverEvent == null ? _rng.RandiRange(Math.Min(DamageMin,DamageMax),Math.Max(DamageMin,DamageMax)) * attacker.Stars : serverEvent.Damage;
        bool finishing = !target.IsDead && (serverEvent != null ? serverEvent.TargetHp <= 0 : damage >= target.Health);
        if (finishing) {
            var focus = _focus.Begin(attacker);
            await ToSignal(focus,Tween.SignalName.Finished); CheckAlive();
            while (_focus.ChargePlaying) await Frame();
            var zoomOut = _focus.ZoomOut();
            await ToSignal(zoomOut,Tween.SignalName.Finished); CheckAlive();
        }
        var sprite = attacker.Sprite; var targetSprite = target.Sprite;
        var home = attacker.Position; var targetHome = target.Position;
        bool facing = sprite.FlipH, targetFacing = targetSprite.FlipH;
        var direction = (targetHome - home).Normalized();
        var attackPosition = targetHome - direction * 22;
        sprite.FlipH = targetHome.X < home.X;
        sprite.Play(BattleAnimations.Walk);
        var dash = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        dash.TweenProperty(attacker, PositionPath, attackPosition, DashDuration);
        await ToSignal(dash, Tween.SignalName.Finished); CheckAlive();
        sprite.Play(BattleAnimations.Attack); sprite.SetFrameAndProgress(0, 0);
        while (sprite.Frame < 2 && sprite.IsPlaying())
        { await ToSignal(sprite, AnimatedSprite2D.SignalName.FrameChanged); CheckAlive(); }
        targetSprite.FlipH = attackPosition.X < targetHome.X;
        if (serverEvent != null) target.ApplyServerHealth(serverEvent.TargetHp,serverEvent.Damage);
        else target.TakeDamage(damage);
        CharacterImpactEffects.Play(attacker,target,"attack01","damage",damage);
        bool lethal = target.IsDead;
        if (lethal) { _teamA.Remove(target); _teamB.Remove(target); }
        targetSprite.Play(lethal ? BattleAnimations.Die : BattleAnimations.Hit);
        if (lethal && finishing) _focus.Impact(target);
        _shakeLeft = ShakeDuration;
        FreezeSprites();
        EmitSignal(SignalName.Impact, attacker, target);
        await Delay(HitstopDuration, true);
        RestoreSprites();
        if (!lethal)
        {
            var recoil = CreateTween();
            recoil.TweenProperty(target, PositionPath, targetHome + direction * 3, .08);
            recoil.TweenProperty(target, PositionPath, targetHome, .16);
            await ToSignal(recoil, Tween.SignalName.Finished); CheckAlive();
        }
        while (sprite.IsPlaying()) await Frame();
        sprite.Play(BattleAnimations.Walk);
        var retreat = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        retreat.TweenProperty(attacker, PositionPath, home, ReturnDuration);
        await ToSignal(retreat, Tween.SignalName.Finished); CheckAlive();
        attacker.Position = home; target.Position = targetHome;
        sprite.FlipH = facing; targetSprite.FlipH = targetFacing;
        sprite.Play(BattleAnimations.Idle);
        if (lethal) { while (targetSprite.IsPlaying()) await Frame(); }
        else targetSprite.Play(BattleAnimations.Idle);
    }
    public async Task PlayServerEvent(BattleUnit attacker, BattleUnit target, OnlineCombatEvent serverEvent, long deadlineMs = 0)
    {
        await _field.WaitForPhaseTransition();CheckAlive();
        CollectTeams();
        TurnCount++;
        EmitSignal(SignalName.TurnStarted,attacker.IsAlly ? "A" : "B",attacker,target);
        var home=attacker.Position;var targetHome=target.Position;
        var replayPlan=_shop.ReplayPlan;
        _serverEventDeadline=deadlineMs;
        try { if(BrawlActive)await _brawl.PlayEvent(attacker,target,serverEvent,deadlineMs);else if(serverEvent.Hits.Length>0)await PlayEffects(attacker,target,serverEvent);else await TakeTurn(attacker,target,serverEvent); }
        catch(OperationCanceledException) when (!_exiting && deadlineMs>0 && ServerVisualTimeMs>=deadlineMs)
        {
            RestoreSprites();_shakeLeft=0;
            if(GodotObject.IsInstanceValid(attacker)&&!attacker.IsDead){attacker.Position=home;attacker.Sprite.Play(BattleAnimations.Idle);}
            if(GodotObject.IsInstanceValid(target)&&!target.IsDead){target.Position=targetHome;target.Sprite.Play(BattleAnimations.Idle);}
        }
        finally { _serverEventDeadline=0;if(ReferenceEquals(replayPlan,_shop.ReplayPlan))_shop.ApplyCombatEvent(serverEvent); }
        EmitSignal(SignalName.TurnFinished,attacker.IsAlly ? "A" : "B",attacker);
    }
    private async Task PlayEffects(BattleUnit attacker,BattleUnit target,OnlineCombatEvent combat)
    {
        var sprite=attacker.Sprite;var home=attacker.Position;bool facing=sprite.FlipH;
        bool dash=combat.Mode is "melee" or "execute";
        var replayPlan=_shop.ReplayPlan;
        string originalAnimation=combat.Animation.Length>0?combat.Animation:"attack01";
        StringName animation=new(CombatVisualSettings.AnimationFor(attacker.CardKind,originalAnimation,sprite.SpriteFrames));
        int originalCount=sprite.SpriteFrames.HasAnimation(originalAnimation)?sprite.SpriteFrames.GetFrameCount(originalAnimation):sprite.SpriteFrames.GetFrameCount(BattleAnimations.Attack);
        if(!sprite.SpriteFrames.HasAnimation(animation))animation=BattleAnimations.Attack;
        try {
            if(combat.Dead&&target!=attacker){var focus=_focus.Begin(attacker);await ToSignal(focus,Tween.SignalName.Finished);CheckAlive();while(_focus.ChargePlaying)await Frame();var zoom=_focus.ZoomOut();await ToSignal(zoom,Tween.SignalName.Finished);CheckAlive();}
            if(dash){sprite.FlipH=target.Position.X<home.X;sprite.Play(BattleAnimations.Walk);var move=CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);move.TweenProperty(attacker,PositionPath,target.Position-(target.Position-home).Normalized()*22,DashDuration);await ToSignal(move,Tween.SignalName.Finished);CheckAlive();}
            sprite.Play(animation);sprite.SetFrameAndProgress(0,0);
            if(combat.Mode=="stun"){
                var shake=CreateTween();shake.TweenProperty(attacker,PositionPath,home+new Vector2(2,0),.06);shake.TweenProperty(attacker,PositionPath,home-new Vector2(2,0),.06);shake.TweenProperty(attacker,PositionPath,home,.06);
            }
            int previousFrame=0;
            foreach(var hit in combat.Hits){
                if(!ReferenceEquals(replayPlan,_shop.ReplayPlan))throw new OperationCanceledException();
                int displayCount=sprite.SpriteFrames.GetFrameCount(animation);
                int frame=animation.ToString()==originalAnimation?Mathf.Clamp(hit.Frame,0,displayCount-1):CombatVisualSettings.RemapFrame(hit.Frame,originalCount,displayCount);
                while(sprite.Animation==animation&&sprite.IsPlaying()&&sprite.Frame<frame){
                    float progress=(sprite.Frame+sprite.FrameProgress-previousFrame)/Mathf.Max(1,frame-previousFrame);
                    CombatHitProgress?.Invoke(hit,progress);await Frame();
                }
                CheckAlive();_shop.ApplyCombatHit(hit);
                if(_shop.NetworkUnits.TryGetValue(hit.Target,out var effectTarget)){
                    var source=_shop.NetworkUnits.TryGetValue(hit.Source,out var effectSource)?effectSource:attacker;
                    CharacterImpactEffects.Play(source,effectTarget,combat.Animation,hit.Kind,hit.Damage);
                }
                previousFrame=frame;
                if(hit.Damage>0){
                    _shakeLeft=ShakeDuration;
                    if(_focus.Active&&hit.Target==combat.Target&&_shop.NetworkUnits.TryGetValue(hit.Target,out var hitTarget)&&hitTarget.IsDead)_focus.Impact(hitTarget);
                    FreezeSprites();EmitSignal(SignalName.Impact,attacker,target);await Delay(HitstopDuration,true);RestoreSprites();
                }
            }
            while(sprite.IsPlaying()&&sprite.Animation==animation)await Frame();
            if(dash&&!attacker.IsDead){sprite.Play(BattleAnimations.Walk);var back=CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);back.TweenProperty(attacker,PositionPath,home,ReturnDuration);await ToSignal(back,Tween.SignalName.Finished);CheckAlive();}
        }finally {
            RestoreSprites();_focus.Reset();
            if(GodotObject.IsInstanceValid(attacker)&&!attacker.IsDead){attacker.Position=home;sprite.FlipH=facing;sprite.Play(BattleAnimations.Idle);}
            foreach(var unit in _shop.NetworkUnits.Values)if(!unit.IsDead)unit.Sprite.Play(BattleAnimations.Idle);
        }
    }
    public void ShowServerResult(string winner)
    {
        _resultText.Text = winner=="DRAW" ? "ROUND DRAW" : winner==_shop.LocalTeam ? "ROUND WON" : "ROUND LOST";
        _result.Show(); EmitSignal(SignalName.BattleFinished,winner);
    }
    public void ShowResultMessage(string message){_resultText.Text=message;_result.Show();}
    public void ShowMatchResult(string winner) { _resultText.Text = $"PLAYER {winner} WINS MATCH"; _result.Show(); }
    public void ShowLeagueResult(bool won, int place) { _resultText.Text = won ? "YOU WIN MATCH" : $"MATCH OVER / PLACEMENT #{place}"; _result.Show(); }
    public void HideResult() => _result.Hide();

    private void FreezeSprites()
    {
        if(_speeds.Length<_sprites.Count)System.Array.Resize(ref _speeds,_sprites.Count);
        _frozenCount = _sprites.Count;
        for (int i = 0; i < _frozenCount; i++)
        { _speeds[i] = _sprites[i].SpeedScale; _sprites[i].SpeedScale = 0; }
    }
    private void RestoreSprites()
    {
        for (int i = 0; i < _frozenCount; i++)
            if (GodotObject.IsInstanceValid(_sprites[i])) _sprites[i].SpeedScale = _speeds[i];
        _frozenCount = 0;
    }
    public override void _ExitTree()
    {
        _exiting = true;
        EndCombatPresentation();
        RestoreSprites();
        if (GodotObject.IsInstanceValid(_focus)) _focus.Reset();
        if (GodotObject.IsInstanceValid(_field)) _field.ArenaOffset = Vector2.Zero;
    }
}
