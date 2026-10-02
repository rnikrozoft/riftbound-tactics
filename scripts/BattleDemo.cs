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
    private Camera2D _camera = null!;
    private Control _result = null!;
    private Label _resultText = null!;
    private CardShop _shop = null!;
    private SceneTree _tree = null!;
    private Vector2 _cameraOffset;
    private float _shakeLeft, _floatTime;
    private bool _exiting;
    private static readonly NodePath PositionPath = new("position");
    public int TurnCount { get; private set; }

    public override void _Ready()
    {
        _rng.Randomize(); _shakeRng.Randomize();
        _tree = GetTree();
        _camera = GetParent().GetNode<Camera2D>("Camera2D");
        _cameraOffset = _camera.Offset;
        _result = GetParent().GetNode<Control>("UI/SafeArea/Content/BattleResult");
        _resultText = _result.GetNode<Label>("Text");
        _shop = GetParent().GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        CollectTeams();
        if (AutoStart) Callable.From(StartBattle).CallDeferred();
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
        _floatTime += (float)delta;
        var offset = _cameraOffset + new Vector2(0, Mathf.Sin(_floatTime * Mathf.Tau / Mathf.Max(.1f, ArenaFloatPeriod)) * ArenaFloatAmplitude);
        if (_shakeLeft > 0)
        {
            _shakeLeft = Mathf.Max(0, _shakeLeft - (float)delta);
            float strength = ShakeStrength * _shakeLeft / Mathf.Max(.001f, ShakeDuration);
            offset += new Vector2(_shakeRng.RandfRange(-strength, strength), _shakeRng.RandfRange(-strength, strength));
        }
        _camera.Offset = offset;
    }

    private void CheckAlive()
    {
        if (_exiting || !IsInsideTree()) throw new OperationCanceledException();
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
                await _shop.WaitForNextRound();
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
            await Delay(TurnDelay);
        }
        string winner = _teamA.Count > 0 ? "A" : "B";
        _resultText.Text = $"PLAYER {winner} WINS";
        _result.Show();
        if (CardShopEnabled) _shop.FinishBattle();
        EmitSignal(SignalName.BattleFinished, winner);
    }

    private async Task TakeTurn(BattleUnit attacker, BattleUnit target)
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
        target.TakeDamage(_rng.RandiRange(Math.Min(DamageMin, DamageMax), Math.Max(DamageMin, DamageMax)));
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
        if (GodotObject.IsInstanceValid(_camera)) _camera.Offset = _cameraOffset;
    }
}


