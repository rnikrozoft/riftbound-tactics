using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleDemo : Node
{
    [Signal] public delegate void TurnStartedEventHandler(string team, BattleUnit attacker, BattleUnit target);
    [Signal] public delegate void ImpactEventHandler(BattleUnit attacker, BattleUnit target);
    [Signal] public delegate void TurnFinishedEventHandler(string team, BattleUnit attacker);
    [Signal] public delegate void BattleFinishedEventHandler(string winner);
    [Export] public float StartDelay { get; set; } = .8f;
    [Export] public bool NetworkEnabled { get; set; }
    [Export] public bool AutoStart { get; set; } = true;
    [Export] public bool CardShopEnabled { get; set; }
    [Export] public float TurnDelay { get; set; } = .45f;
    [Export] public float DashDuration { get; set; } = .24f;
    [Export] public float ReturnDuration { get; set; } = .32f;
    [Export] public float HitstopDuration { get; set; } = .075f;
    [Export] public float ShakeDuration { get; set; } = .2f;
    [Export] public float ShakeStrength { get; set; } = 2.5f;
    [Export] public float ArenaFloatAmplitude { get; set; } = 1.5f;
    [Export] public float ArenaFloatPeriod { get; set; } = 6f;
    [Export(PropertyHint.Range, "1,1000,1")] public int DamageMin { get; set; } = 25;
    [Export(PropertyHint.Range, "1,1000,1")] public int DamageMax { get; set; } = 40;

    private readonly List<BattleUnit> _teamA = new(6), _teamB = new(6);
    private readonly List<AnimatedSprite2D> _sprites = new(12);
    private readonly float[] _speeds = new float[12];
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
    private static readonly NodePath PositionPath = new("position");
    public int TurnCount { get; private set; }

    public override void _Ready()
    {
        _rng.Randomize(); _shakeRng.Randomize();
        _tree = GetTree();
        _field = GetParent<BattleDisplay>();
        _result = GetParent().GetNode<Control>("UI/SafeArea/Content/BattleResult");
        _resultText = _result.GetNode<Label>("Text");
        _shop = GetParent().GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        CollectTeams();
        if (AutoStart && !NetworkEnabled) Callable.From(StartBattle).CallDeferred();
    }

    private void CollectTeams()
    {
        _teamA.Clear(); _teamB.Clear(); _sprites.Clear();
        foreach (var child in GetParent().GetChildren())
            if (child is BattleUnit unit && !unit.IsQueuedForDeletion())
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
        await ToSignal(_tree.CreateTimer(Math.Max(.001, seconds), true, false, ignoreTimeScale), SceneTreeTimer.SignalName.Timeout);
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
        if (CardShopEnabled) _result.Hide(); else _result.Show();
        if (CardShopEnabled) {
            int stars = 0; foreach (var survivor in winner == "A" ? _teamA : _teamB) stars += survivor.Stars;
            _shop.FinishBattle(winner, stars);
            if (_shop.GameOver) ShowMatchResult(winner);
        }
        EmitSignal(SignalName.BattleFinished, winner);
    }

    private async Task TakeTurn(BattleUnit attacker, BattleUnit target, OnlineCombatEvent? serverEvent = null)
    {
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
        if (serverEvent != null) target.ApplyServerHealth(serverEvent.TargetHp);
        else target.TakeDamage(_rng.RandiRange(Math.Min(DamageMin, DamageMax), Math.Max(DamageMin, DamageMax)) * attacker.Stars);
        bool lethal = target.IsDead;
        if (lethal) { _teamA.Remove(target); _teamB.Remove(target); }
        targetSprite.Play(lethal ? BattleAnimations.Die : BattleAnimations.Hit);
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
        CollectTeams();
        TurnCount++;
        EmitSignal(SignalName.TurnStarted,attacker.IsAlly ? "A" : "B",attacker,target);
        var home=attacker.Position;var targetHome=target.Position;
        _serverEventDeadline=deadlineMs;
        try { await TakeTurn(attacker,target,serverEvent); }
        catch(OperationCanceledException) when (!_exiting && deadlineMs>0 && ServerVisualTimeMs>=deadlineMs)
        {
            RestoreSprites();_shakeLeft=0;
            if(GodotObject.IsInstanceValid(attacker)){attacker.Position=home;attacker.Sprite.Stop();}
            if(GodotObject.IsInstanceValid(target)){target.Position=targetHome;target.Sprite.Stop();}
        }
        finally { _serverEventDeadline=0; }
        EmitSignal(SignalName.TurnFinished,attacker.IsAlly ? "A" : "B",attacker);
    }
    public void ShowServerResult(string winner)
    {
        _resultText.Text = winner == "DRAW" ? "DRAW" : $"PLAYER {winner} WINS";
        _result.Hide(); EmitSignal(SignalName.BattleFinished,winner);
    }
    public void ShowMatchResult(string winner) { _resultText.Text = $"PLAYER {winner} WINS MATCH"; _result.Show(); }
    public void ShowLeagueResult(bool won, int place) { _resultText.Text = won ? "YOU WIN MATCH" : $"MATCH OVER / PLACEMENT #{place}"; _result.Show(); }
    public void HideResult() => _result.Hide();

    private void FreezeSprites()
    {
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
        RestoreSprites();
        if (GodotObject.IsInstanceValid(_field)) _field.ArenaOffset = Vector2.Zero;
    }
}
